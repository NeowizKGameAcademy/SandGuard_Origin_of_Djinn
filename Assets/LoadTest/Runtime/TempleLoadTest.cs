using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SandGuard.Enemy;
using SandGuard.Waves;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace SandGuard.LoadTest
{
    // Benchmark-only component. Never attached to production scenes or prefabs.
    public sealed class TempleLoadTest : MonoBehaviour
    {
        public GameObject[] enemyPrefabs;
        public int[] counts = { 0, 25, 50, 100, 150, 200 };
        public float warmupSeconds = 10, measureSeconds = 60;
        public int repeats = 1;
        public bool heavyMix;
        public bool soak;
        public int soakCount = 100;
        public int seed = 61723;
        public bool autoRun = true;
        public enum Scenario { Travel, Crowd, CombatProxy, Churn }
        public enum View { Overview, Close }
        sealed class Unit
        {
            public GameObject go;
            public EnemyMotor motor;
            public EnemyBrain brain;
            public EnemyHealth health;
            public Transform goal;
            public int route;
            public Vector3 previous;
        }
        readonly List<Unit> units = new List<Unit>();
        readonly List<Unit> returned = new List<Unit>();
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<GameObject> temporary = new List<GameObject>();
        readonly FrameTiming[] timings = new FrameTiming[1];
        EnemyPool pool;
        Camera testCamera;
        Vector3 focus;
        Scenario scenario;
        string output, status = "Preparing", csv;
        float nextHit, nextChurn;
        int recycleCursor, replacements, deaths;
        double distanceTravelled;
        int oldVSync, oldTarget;
        int renderCount;
        void OnEnable() { RenderPipelineManager.endCameraRendering += Rendered; Camera.onPostRender += RenderedBuiltin; }
        void OnDisable() { RenderPipelineManager.endCameraRendering -= Rendered; Camera.onPostRender -= RenderedBuiltin; }
        void Rendered(ScriptableRenderContext context, Camera camera) { if (camera == testCamera) renderCount++; }
        void RenderedBuiltin(Camera camera) { if (camera == testCamera) renderCount++; }

        IEnumerator Start()
        {
            if (!autoRun) yield break;
            oldVSync = QualitySettings.vSyncCount; oldTarget = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Application.runInBackground = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            string[] args = Environment.GetCommandLineArgs();
            string Option(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            if (Option("--counts") is string list) counts = list.Split(',').Select(int.Parse).ToArray();
            if (Option("--seconds") is string seconds) measureSeconds = float.Parse(seconds, CultureInfo.InvariantCulture);
            if (Option("--repeats") is string reps) repeats = int.Parse(reps);
            heavyMix |= args.Contains("--heavy");
            soak |= args.Contains("--soak");
            if (Option("--soak-count") is string number) soakCount = int.Parse(number);
            bool smoke = args.Contains("--smoke");
            string onlyScenario = Option("--scenario"), onlyView = Option("--view");
            if (smoke) { counts = new[] { 0, 5 }; warmupSeconds = 1; measureSeconds = 2; repeats = 1; soak = false; }
            output = Option("--output") ?? Path.Combine(Application.persistentDataPath, "TempleLoadTest", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            csv = Path.Combine(output, "results.csv");
            File.WriteAllText(csv, "scenario,view,mix,count,repeat,seconds,frames,avg_fps,p50_ms,p95_ms,p99_ms,max_ms,cpu_timing_mean_ms,gpu_timing_mean_ms,timing_samples,gc_collections,allocated_mb_start,allocated_mb_end,managed_mb_end,spawn_ms,replacements,deaths,alive_min,off_navmesh_max,travel_metres,rendered_frames,p95_60fps_pass\n");
            File.WriteAllText(Path.Combine(output, "environment.txt"),
                $"UTC: {DateTime.UtcNow:O}\nUnity: {Application.unityVersion}\nEditor: {Application.isEditor}\nOS: {SystemInfo.operatingSystem}\nCPU: {SystemInfo.processorType}\nGPU: {SystemInfo.graphicsDeviceName}\nRAM MB: {SystemInfo.systemMemorySize}\nQuality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}\nResolution requested: 1920x1080\nVSync: off\nSeed: {seed}\nMix: {(heavyMix ? "60% HammerBrute, 40% Chief" : "five types equally")}\nCombatProxy uses synthetic incoming hits and immortal targets; excludes actual tower targeting/projectiles.\nTiming values of 0 with timing_samples=0 mean unavailable.\nChurn deliberately varies alive count during death animations; it is not a steady-state capacity result.\n");
            yield return null;
            if (!Prepare()) { File.WriteAllText(Path.Combine(output, "FAILED.txt"), status); if (!Application.isEditor) Application.Quit(2); yield break; }
            File.AppendAllText(Path.Combine(output, "environment.txt"), $"Actual resolution: {Screen.width}x{Screen.height}\nConnected route samples: {points.Count}\n");
            foreach (Scenario mode in Enum.GetValues(typeof(Scenario)))
                foreach (View view in Enum.GetValues(typeof(View)))
                    if ((onlyScenario == null || mode.ToString().Equals(onlyScenario, StringComparison.OrdinalIgnoreCase)) &&
                        (onlyView == null || view.ToString().Equals(onlyView, StringComparison.OrdinalIgnoreCase)))
                    foreach (int count in counts)
                        for (int repeat = 1; repeat <= repeats; repeat++)
                            yield return RunCase(mode, view, Mathf.Clamp(count, 0, 2000), repeat, smoke && mode == Scenario.Churn && count > 0 ? 10f : Mathf.Max(.1f, measureSeconds));
            if (soak) yield return RunCase(Scenario.Travel, View.Close, Mathf.Clamp(soakCount, 1, 2000), 1, 600);
            CleanupCase();
            status = "Complete: " + output;
            File.WriteAllText(Path.Combine(output, "COMPLETE.txt"), status);
            Debug.Log("TEMPLE_LOAD_TEST_COMPLETE " + output);
            if (!Application.isEditor) Application.Quit();
        }

        bool Prepare()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length != 5 || enemyPrefabs.Any(p => p == null))
            { status = "Five enemy prefabs are required."; return false; }
            var nav = NavMesh.CalculateTriangulation();
            if (nav.indices.Length == 0) { status = "No baked NavMesh in test scene."; return false; }
            // Choose a connected component by testing routes to one sampled point.
            var candidates = new List<Vector3>();
            int step = Mathf.Max(1, nav.indices.Length / 3 / 300);
            for (int t = 0; t < nav.indices.Length / 3; t += step)
            {
                Vector3 center = (nav.vertices[nav.indices[t * 3]] + nav.vertices[nav.indices[t * 3 + 1]] + nav.vertices[nav.indices[t * 3 + 2]]) / 3f;
                if (NavMesh.SamplePosition(center, out var hit, .5f, NavMesh.AllAreas)) candidates.Add(hit.position);
            }
            var path = new NavMeshPath();
            // Select the largest of several candidate components, not an arbitrary isolated first triangle.
            for (int a = 0; a < candidates.Count; a += Mathf.Max(1, candidates.Count / 8))
            {
                var connected = candidates.Where(p => NavMesh.CalculatePath(candidates[a], p, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete).ToList();
                if (connected.Count > points.Count) { points.Clear(); points.AddRange(connected); }
            }
            if (points.Count < 4) { status = "Insufficient connected NavMesh points."; return false; }
            focus = points[points.Count / 2];
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            testCamera = new GameObject("Load Test Camera").AddComponent<Camera>();
            testCamera.nearClipPlane = .1f; testCamera.farClipPlane = 1000;
            pool = new GameObject("Load Test Enemy Pool").AddComponent<EnemyPool>();
            return true;
        }

        IEnumerator RunCase(Scenario mode, View view, int count, int repeat, float duration)
        {
            CleanupCase();
            yield return null;
            // Start each measurement from a cold pool; Churn measures reuse within the case.
            scenario = mode;
            UnityEngine.Random.InitState(seed);
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            Vector3 center = view == View.Overview ? bounds.center : focus + Vector3.up;
            float distance = view == View.Overview ? Mathf.Max(20, bounds.size.magnitude * .8f) : 20f;
            testCamera.transform.position = center + new Vector3(.6f, .8f, -.7f).normalized * distance;
            testCamera.transform.LookAt(center);
            if (mode == Scenario.CombatProxy)
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "Benchmark combat proxy (not a tower)";
                target.transform.position = focus + Vector3.up;
                target.transform.localScale = new Vector3(2, 2, 2);
                var health = target.AddComponent<EnemyTestTarget>();
                health.kind = CombatTargetKind.Tower; health.maxHealth = 1e9f;
                temporary.Add(target);
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++) AddUnit(i);
            watch.Stop();
            float spawnMs = (float)watch.Elapsed.TotalMilliseconds;
            nextHit = Time.realtimeSinceStartup; nextChurn = Time.realtimeSinceStartup + 5;
            float until = Time.realtimeSinceStartup + warmupSeconds;
            while (Time.realtimeSinceStartup < until) { Drive(); yield return null; }
            replacements = deaths = 0; distanceTravelled = 0;
            foreach (var unit in units) unit.previous = unit.go.transform.position;
            int renderStart = renderCount;
            var frames = new List<float>(Mathf.CeilToInt(duration * 1000));
            double cpu = 0, gpu = 0; int timingSamples = 0, aliveMin = count, offMax = 0;
            int gcStart = GC.CollectionCount(0);
            long memoryStart = Profiler.GetTotalAllocatedMemoryLong();
            float start = Time.realtimeSinceStartup, previous = start, nextStatus = start;
            while (Time.realtimeSinceStartup - start < duration)
            {
                if (Time.realtimeSinceStartup >= nextStatus)
                {
                    status = $"{mode} / {view} / {count} enemies / repeat {repeat} / {Time.realtimeSinceStartup - start:F0}s of {duration:F0}s";
                    nextStatus = Time.realtimeSinceStartup + .5f;
                }
                Drive();
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                float now = Time.realtimeSinceStartup;
                frames.Add((now - previous) * 1000f); previous = now;
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                { cpu += timings[0].cpuFrameTime; gpu += timings[0].gpuFrameTime; timingSamples++; }
                int alive = 0, off = 0;
                foreach (var unit in units)
                {
                    if (!unit.health.IsAlive || !unit.go.activeSelf) continue;
                    alive++; if (!unit.motor.IsOnNavMesh) off++;
                    distanceTravelled += Vector3.Distance(unit.previous, unit.go.transform.position);
                    unit.previous = unit.go.transform.position;
                }
                aliveMin = Math.Min(aliveMin, alive); offMax = Math.Max(offMax, off);
            }
            float elapsed = Time.realtimeSinceStartup - start;
            string raw = $"{mode}-{view}-{count}-{repeat}-{(duration >= 600 ? "soak" : "sweep")}.csv";
            File.WriteAllLines(Path.Combine(output, raw), new[] { "frame_ms" }.Concat(frames.Select(f => f.ToString("F4", CultureInfo.InvariantCulture))));
            frames.Sort();
            float Percent(float p) => frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count * p) - 1, 0, frames.Count - 1)];
            int rendered = renderCount - renderStart;
            bool pass = Percent(.95f) <= 1000f / 60f && offMax == 0 && aliveMin == count && mode != Scenario.Churn && rendered >= frames.Count * .9f;
            var values = new object[] { mode, view, heavyMix ? "heavy" : "balanced", count, repeat, elapsed, frames.Count, frames.Count / elapsed,
                Percent(.5f), Percent(.95f), Percent(.99f), frames[frames.Count - 1], timingSamples > 0 ? cpu / timingSamples : 0,
                timingSamples > 0 ? gpu / timingSamples : 0, timingSamples, GC.CollectionCount(0) - gcStart, memoryStart / 1048576d,
                Profiler.GetTotalAllocatedMemoryLong() / 1048576d, GC.GetTotalMemory(false) / 1048576d, spawnMs, replacements, deaths,
                aliveMin, offMax, distanceTravelled, rendered, pass };
            File.AppendAllText(csv, string.Join(",", values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))) + "\n");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, Path.ChangeExtension(raw, ".png")));
            yield return null;
        }

        void AddUnit(int index)
        {
            int prefab = heavyMix ? (index % 5 < 3 ? 3 : 4) : index % 5;
            Vector3 position = points[index * 37 % points.Count];
            if (scenario != Scenario.Travel)
            {
                var near = points.OrderBy(p => (p - focus).sqrMagnitude).Take(Math.Min(30, points.Count)).ToArray();
                position = near[index % near.Length];
            }
            var go = pool.Rent(enemyPrefabs[prefab], position, Quaternion.identity);
            var goal = new GameObject("Benchmark route target"); temporary.Add(goal);
            var unit = new Unit { go = go, motor = go.GetComponent<EnemyMotor>(), brain = go.GetComponent<EnemyBrain>(), health = go.GetComponent<EnemyHealth>(), goal = goal.transform, route = (index * 37 + points.Count / 2) % points.Count, previous = position };
            unit.brain.objective = goal.transform; unit.brain.despawnOnArrival = false;
            goal.transform.position = scenario == Scenario.Travel ? points[unit.route] : focus;
            units.Add(unit);
        }

        void Drive()
        {
            // Snapshot returned units before renting; the pool may return another unit's former instance.
            returned.Clear();
            foreach (var unit in units) if (!unit.go.activeSelf) returned.Add(unit);
            foreach (var unit in returned)
            {
                    int index = units.IndexOf(unit);
                    int prefab = heavyMix ? (index % 5 < 3 ? 3 : 4) : index % 5;
                    Vector3 pos = points[index * 37 % points.Count];
                    unit.go = pool.Rent(enemyPrefabs[prefab], pos, Quaternion.identity);
                    unit.motor = unit.go.GetComponent<EnemyMotor>(); unit.brain = unit.go.GetComponent<EnemyBrain>(); unit.health = unit.go.GetComponent<EnemyHealth>();
                    unit.brain.objective = unit.goal; unit.brain.despawnOnArrival = false; unit.previous = pos; replacements++;
            }
            foreach (var unit in units)
            {
                if (scenario == Scenario.Travel && Vector3.Distance(unit.go.transform.position, unit.goal.position) < 3f)
                { unit.route = (unit.route + Math.Max(1, points.Count / 3)) % points.Count; unit.goal.position = points[unit.route]; }
            }
            if (scenario == Scenario.CombatProxy && Time.realtimeSinceStartup >= nextHit)
            {
                nextHit = Time.realtimeSinceStartup + .25f;
                // Four synthetic impacts per second per 10 enemies. Keep a fixed live population.
                for (int i = 0; i < units.Count; i += 10)
                {
                    var unit = units[(i + recycleCursor) % units.Count];
                    if (unit.health.CurrentHealth < 10) unit.health.ResetForReuse();
                    unit.health.TakeDamage(new DamageInfo(1, "Ally", hitPosition: unit.health.HitPosition, hitDirection: Vector3.forward));
                }
                recycleCursor++;
            }
            if (scenario == Scenario.Churn && Time.realtimeSinceStartup >= nextChurn)
            {
                nextChurn = Time.realtimeSinceStartup + 5;
                foreach (var unit in units)
                    if (unit.health.IsAlive) { unit.health.TakeDamage(new DamageInfo(1e6f, "Ally")); deaths++; }
            }
        }

        void CleanupCase()
        {
            foreach (var unit in units) if (unit.go != null) Destroy(unit.go);
            units.Clear();
            foreach (var go in temporary) if (go != null) Destroy(go);
            temporary.Clear();
            if (pool != null) pool.Clear();
        }
        void OnDestroy() { QualitySettings.vSyncCount = oldVSync; Application.targetFrameRate = oldTarget; }
        void OnGUI() { GUI.Label(new Rect(16, 16, 1200, 32), status); }
    }
}
