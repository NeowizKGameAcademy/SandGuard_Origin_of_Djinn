using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using DesertTower.VFX;
using Object = UnityEngine.Object;

/// <summary>Level.unity 전용: 모래폭풍을 신전 벽에 바짝 붙은 두껍고 불투명한 벽으로 다시 만든다.
/// 웨이브 스폰(모서리 ±64m)이 폭풍 몸통 안에 들어가 적이 모래 속에서 걸어 나오는 것처럼 보인다.
/// 메시는 Environment/LevelStorm_*.asset 에만 쓰므로 아트 씬(Level_TempleCourtyard)은 바뀌지 않는다.</summary>
public static class LevelStormSeal
{
    const string Env = "Assets/TempleArt/ArchitectureV2/Environment";
    const string ScenePath = "Assets/1.Scene/Level.unity";
    const float Bottom = -25f;                    // 지형 기복(반경 120m까지 3m)을 묻어 두는 바닥
    // ── 반지름: 이 값 하나로 폭풍 전체가 움직인다 ──
    // 안쪽 면의 변 반폭(m). 신전 외벽이 |x|,|z| 76m 이므로 78 이상, 스폰 (±64,±64)=반지름 90.5m 가 모래 속에 남으려면 88 이하.
    const float InnerHalf = 100;
    // 안쪽 면은 둥근 정사각형: 변은 InnerHalf, 모서리는 반지름 InnerHalf+2 인 원으로 깎는다. 층마다 4m 씩 뒤로.
    static readonly float[] Side = { InnerHalf, InnerHalf + 4, InnerHalf + 8 }, Corner = { InnerHalf + 2, InnerHalf + 6, InnerHalf + 10 };
    // 높이는 예전 폭풍(최고 43m)과 같게 유지. Lean = 꼭대기에서 바깥으로 기우는 거리 → 고리의 두께.
    static readonly float[] Height = { 24, 30, 36 }, Lean = { 12, 16, 20 };
    const float CurtainSide = InnerHalf + 20, CurtainCorner = InnerHalf + 22, CurtainTop = 28, CurtainLean = 10, CrownTop = 30;
    // 플레이어 차단벽: 폭풍 안쪽 면(층 0)보다 3m 뒤, 층 1 앞. 안쪽 면과 같은 둥근 정사각형을 따라간다.
    const float BarrierSide = InnerHalf + 3, BarrierCorner = InnerHalf + 5, BarrierBottom = -20, BarrierTop = 220;
    const int BarrierLayer = 2; // Ignore Raycast: 플레이어(CharacterController)는 막고, NavMesh 굽기·조준 레이캐스트에서는 제외
    const float LightningSide = InnerHalf - 2, LightningCorner = InnerHalf, LightningHeight = 40;
    const float DustRadius = InnerHalf + 8, EmberRadius = InnerHalf + 2;

    [MenuItem("Tools/Desert Tower/Storm/Seal storm around temple (Level.unity)")]
    public static void Apply()
    {
        try
        {
            if (!Application.isBatchMode && Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("먼저 열린 씬을 저장하세요.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var storm = Object.FindObjectsByType<TempleSandstorm>(FindObjectsSortMode.None).Single(s => s.gameObject.scene == scene && s.name == "VFX_TempleSandstorm");
            storm.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); storm.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(storm.transform);

            var filters = storm.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Sand wall layer")).OrderBy(f => f.name).ToArray();
            if (filters.Length != 3) throw new Exception("Sand wall layer 1..3 을 찾지 못했습니다: " + filters.Length);
            for (int i = 0; i < 3; i++)
            {
                var mesh = Wall("Level storm wall " + i, Side[i], Corner[i], Bottom, Height[i] - Bottom, Lean[i], i * .8f, 2.5f);
                filters[i].sharedMesh = Save(mesh, Env + "/LevelStorm_Wall_" + i + ".asset");
                PrefabUtility.RecordPrefabInstancePropertyModifications(filters[i]);
                var r = filters[i].GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                var mat = r.sharedMaterial;
                if (!AssetDatabase.GetAssetPath(mat).Contains("LevelStorm_")) throw new Exception("Level 전용 머티리얼이 아닙니다: " + mat.name);
                mat.SetFloat("_Opacity", 1f);
                mat.SetFloat("_DensityFloor", .80f + .06f * i);
                mat.SetFloat("_WaveAmplitude", .25f);
                mat.SetColor("_DarkColor", new Color(.40f, .27f, .16f));
                mat.SetColor("_LightColor", new Color(.84f, .66f, .44f));
                mat.renderQueue = 3002 - i;            // 안쪽 층을 마지막에 그려 신전 쪽에서 봤을 때 앞에 오게
                EditorUtility.SetDirty(mat);
            }
            foreach (var ps in storm.GetComponentsInChildren<ParticleSystem>(true))
            {
                var sh = ps.shape;
                if (ps.name.Contains("dust")) { sh.radius = DustRadius; sh.radiusThickness = .3f; }
                else if (ps.name.Contains("embers")) { sh.radius = EmberRadius; sh.radiusThickness = .05f; }
                PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
            }
            EditorUtility.SetDirty(storm);

            var curtain = Find(scene, "Storm - opaque buried curtain");
            Reset(curtain);
            SetMesh(curtain, Wall("Level storm curtain", CurtainSide, CurtainCorner, Bottom, CurtainTop - Bottom, CurtainLean, .4f, 1.5f), Env + "/LevelStorm_Curtain.asset");
            var crown = Find(scene, "Storm - curtain crown");
            Reset(crown);
            SetMesh(crown, Wall("Level storm crown", CurtainSide + CurtainLean, CurtainCorner + CurtainLean, CurtainTop - 10, CrownTop - CurtainTop + 10, 14, 1.1f, 2f), Env + "/LevelStorm_CurtainCrown.asset");
            var crownMat = crown.GetComponent<Renderer>().sharedMaterial;
            crownMat.SetFloat("_DensityFloor", .9f); crownMat.SetFloat("_Opacity", 1f); EditorUtility.SetDirty(crownMat);

            BuildBarrier(Find(scene, "Storm - player barrier"));
            // 스폰이 차단벽 바깥(모래 속)에 있으므로 차단벽은 NavMesh 에 구워지면 안 된다. 적은 NavMeshAgent 라 콜라이더와 충돌하지 않는다.
            foreach (var surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
                if ((surface.layerMask & (1 << BarrierLayer)) != 0) { surface.layerMask &= ~(1 << BarrierLayer); EditorUtility.SetDirty(surface); }

            var bolts = Find(scene, "Storm - magic lightning").GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Bolts");
            var ring = Wall("Level storm lightning ring", LightningSide, LightningCorner, -5, LightningHeight + 5, 0, 0, 0);
            SetMesh(bolts.transform, ring, Env + "/LevelStorm_Lightning.asset");
            var boltMat = bolts.GetComponent<Renderer>().sharedMaterial;
            boltMat.SetFloat("_Perimeter", Perimeter(LightningSide, LightningCorner)); boltMat.SetFloat("_Height", LightningHeight + 5); EditorUtility.SetDirty(boltMat);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("LEVEL_STORM_SEALED");
            if (Application.isBatchMode) Capture();
        }
        catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    /// <summary>폭풍 안쪽 면을 따라가는 박스 콜라이더 고리. 플레이어(CharacterController)가 모래 속으로 들어가 바깥을 보는 것을 막는다.
    /// 씬 루트 오브젝트라 LevelRoot 자식만 굽는 NavMesh 에는 들어가지 않는다(적 경로 영향 없음). 기존 콜라이더·자식은 지우고 다시 만든다.</summary>
    static void BuildBarrier(Transform root)
    {
        Reset(root);
        foreach (var c in root.GetComponents<Collider>()) Object.DestroyImmediate(c);
        for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
        const int segments = 64;
        root.gameObject.layer = BarrierLayer;
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = Ring(i / (float)segments), b = Ring((i + 1) / (float)segments);
            var go = new GameObject("Barrier segment " + i) { layer = BarrierLayer };
            go.transform.SetParent(root, false);
            go.transform.position = (a + b) * .5f + Vector3.up * (BarrierBottom + BarrierTop) * .5f;
            go.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            go.AddComponent<BoxCollider>().size = new Vector3(2f, BarrierTop - BarrierBottom, Vector3.Distance(a, b) + 1f);
        }
        EditorUtility.SetDirty(root);
        static Vector3 Ring(float u) { float theta = u * Mathf.PI * 2, r = Inner(theta, BarrierSide, BarrierCorner); return new Vector3(Mathf.Cos(theta) * r, 0, Mathf.Sin(theta) * r); }
    }

    /// <summary>둥근 정사각형 안쪽 면: 변 반폭 side, 모서리는 반지름 corner 원으로 깎는다.</summary>
    static float Inner(float theta, float side, float corner)
    {
        float m = Mathf.Max(Mathf.Abs(Mathf.Cos(theta)), Mathf.Abs(Mathf.Sin(theta)));
        return Mathf.Min(side / m, corner);
    }

    static float Perimeter(float side, float corner)
    {
        const int n = 2048; float sum = 0; Vector2 prev = Vector2.zero;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n * Mathf.PI * 2, r = Inner(t, side, corner);
            var p = new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
            if (i > 0) sum += Vector2.Distance(prev, p); prev = p;
        }
        return sum;
    }

    /// <summary>바닥 yBottom 에서 height 만큼 솟으며 위로 갈수록 lean 만큼 바깥으로 기우는 벽면. uv = (둘레, 높이).</summary>
    static Mesh Wall(string name, float side, float corner, float yBottom, float height, float lean, float phase, float ripple)
    {
        const int rings = 256, rows = 24;
        var v = new Vector3[(rings + 1) * (rows + 1)]; var uv = new Vector2[v.Length]; var tri = new int[rings * rows * 6]; int k = 0;
        for (int a = 0; a <= rings; a++)
            for (int b = 0; b <= rows; b++)
            {
                float u = a / (float)rings, s = b / (float)rows, theta = u * Mathf.PI * 2;
                float bulge = Mathf.Sin(s * Mathf.PI) * ripple * (Mathf.Sin(theta * 5 + phase) + .6f * Mathf.Sin(theta * 9 + phase * 2));
                float r = Inner(theta, side, corner) + lean * s * s + bulge;
                float y = yBottom + s * height * (1 + .05f * Mathf.Sin(theta * 4 + phase));
                int index = a * (rows + 1) + b;
                v[index] = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r); uv[index] = new Vector2(u, s);
                if (a < rings && b < rows) { int q = index; tri[k++] = q; tri[k++] = q + rows + 1; tri[k++] = q + 1; tri[k++] = q + 1; tri[k++] = q + rows + 1; tri[k++] = q + rows + 2; }
            }
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, vertices = v, uv = uv, triangles = tri };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var bounds = mesh.bounds; bounds.Expand(12); mesh.bounds = bounds; return mesh;
    }

    static Mesh Save(Mesh mesh, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old)
        {
            // CopySerialized 는 같은 세션의 렌더 데이터를 갱신하지 않아 촬영이 옛 형상을 보여준다. 정점을 직접 옮긴다.
            old.Clear(); old.indexFormat = mesh.indexFormat; old.name = mesh.name;
            old.vertices = mesh.vertices; old.uv = mesh.uv; old.normals = mesh.normals; old.triangles = mesh.triangles; old.bounds = mesh.bounds;
            old.UploadMeshData(false); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(old); return old;
        }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    static void SetMesh(Transform t, Mesh mesh, string path)
    {
        var f = t.GetComponent<MeshFilter>(); f.sharedMesh = Save(mesh, path); EditorUtility.SetDirty(f);
    }

    static Transform Find(UnityEngine.SceneManagement.Scene scene, string name)
    {
        var t = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(x => x.name == name);
        if (!t) throw new Exception("씬 루트에서 찾지 못했습니다: " + name); return t;
    }

    static void Reset(Transform t) { t.SetPositionAndRotation(Vector3.zero, Quaternion.identity); t.localScale = Vector3.one; EditorUtility.SetDirty(t); }

    // ---------- 확인 촬영 (배치: -executeMethod LevelStormSeal.Capture, 환경변수 STORM_OUT 으로 출력 폴더) ----------
    static int ticks; static string outDir;
    [MenuItem("Tools/Desert Tower/Storm/Capture storm views (Level.unity)")]
    public static void Capture()
    {
        try
        {
            outDir = Environment.GetEnvironmentVariable("STORM_OUT"); if (string.IsNullOrEmpty(outDir)) outDir = "Docs/LevelArt/LevelEnvironment/storm-tight/after";
            Directory.CreateDirectory(outDir);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            ticks = 0; EditorApplication.update += Tick;
        }
        catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void Tick()
    {
        if (++ticks < 30) return; EditorApplication.update -= Tick;
        try
        {
            var log = new StringBuilder();
            foreach (var storm in Object.FindObjectsByType<TempleSandstorm>(FindObjectsSortMode.None)) storm.ApplyTime(16);
            foreach (var l in Object.FindObjectsByType<StormLightning>(FindObjectsSortMode.None)) l.Pose(3);
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) { ps.useAutoRandomSeed = false; ps.randomSeed = 47; ps.Simulate(10, false, true, true); }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("Sand wall") || r.name.StartsWith("Storm") || r.name == "Bolts"))
                log.AppendLine(r.name + " bounds min=" + r.bounds.min + " max=" + r.bounds.max);
            Shoot("overview", new Vector3(330, 300, -430), new Vector3(0, 30, 0), 34);
            Shoot("overview-high", new Vector3(120, 620, -520), new Vector3(0, 40, 0), 40);
            Shoot("ground-ne-corner", new Vector3(44, 4, 44), new Vector3(100, 40, 100), 70);
            Shoot("ground-ne-spawn", new Vector3(30, 5, 30), new Vector3(64, 8, 64), 60);
            Shoot("courtyard-east", new Vector3(-30, 6, 0), new Vector3(140, 60, 0), 65);
            Shoot("summit", new Vector3(0, 62, -6), new Vector3(90, 60, 90), 70);
            Shoot("top", new Vector3(0, 700, 0), Vector3.zero, 0, true, 260);
            File.WriteAllText(outDir + "/facts.txt", log.ToString());
            Debug.Log("STORM_CAPTURE_DONE"); if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void Shoot(string name, Vector3 pos, Vector3 target, float fov, bool ortho = false, float size = 0)
    {
        var go = new GameObject("storm capture"); var c = go.AddComponent<Camera>(); c.transform.position = pos; c.transform.LookAt(target);
        if (ortho) c.transform.rotation = Quaternion.Euler(90, 0, 0);
        c.orthographic = ortho; c.orthographicSize = size; c.fieldOfView = fov; c.nearClipPlane = .3f; c.farClipPlane = 2000; c.clearFlags = CameraClearFlags.Skybox; c.useOcclusionCulling = false;
        var d = c.GetUniversalAdditionalCameraData(); d.requiresDepthTexture = true; d.renderPostProcessing = false;
        int w = 1600, h = ortho ? 1600 : 900;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create(); c.targetTexture = rt; c.aspect = w / (float)h;
        c.Render(); c.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(outDir + "/" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = prev; c.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
