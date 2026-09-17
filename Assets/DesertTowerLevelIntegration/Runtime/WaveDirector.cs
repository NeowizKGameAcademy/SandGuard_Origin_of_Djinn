using System;
using System.Collections.Generic;
using DesertTower.Levels;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace DesertTower.LevelIntegration
{
    public enum RunState { Idle, Preparing, Running, Won, Lost, Error, Stopped }

    public sealed class WaveDirector : MonoBehaviour, IWaveStateReader, IGameStateReader
    {
        public RouteGraph graph;
        public PrefabCatalog catalog;
        [Tooltip("ILevelCoreReceiver 구현 컴포넌트")]
        public MonoBehaviour coreReceiver;
        [Tooltip("적 생성을 맡는 풀 팩토리. 비우면 같은 오브젝트에서 찾고, 그래도 없으면 Instantiate/Destroy를 쓴다")]
        public EnemyPoolActorFactory actorFactory;
        public bool startAutomatically;
        public int randomSeed = 1234;
        [Min(1)] public int maxSpawnsPerFrame = 32;
        public UnityEvent onStateChanged = new UnityEvent();
        public UnityEvent onWaveCleared = new UnityEvent();
        public RunState State { get; private set; }
        public int WaveIndex { get; private set; } = -1;
        public int AliveCount => actors.Count;
        public int TotalSpawned { get; private set; }
        public int Killed { get; private set; }
        public int Absorbed { get; private set; }
        public float RunElapsedSeconds { get; private set; }
        public float PreparationRemaining { get; private set; }
        public string LastError { get; private set; }

        // IWaveStateReader / IGameStateReader — HUD(GameHUDPresenter)와 건설 단계 판정
        // (FacilityBuildService, PlacementPhaseRule)이 이 계약만 보고 동작한다.
        public event Action Changed;
        /// <summary>Idle·Stopped·Error는 전투가 도는 상태가 아니다. Error를 Defeat로 내면 설정 오류가
        /// 패배 연출로 보이므로 준비 단계로 낸다. 오류 자체는 LastError와 콘솔에 남는다.</summary>
        public GamePhase Phase => State == RunState.Running ? GamePhase.Combat
            : State == RunState.Won ? GamePhase.Victory
            : State == RunState.Lost ? GamePhase.Defeat
            : GamePhase.Preparation;
        public bool IsPaused => Time.timeScale <= 0f;
        public int TotalWaves => graph && graph.level && graph.level.waves ? graph.level.waves.waves.Count : 0;
        public int WaveNumber => TotalWaves == 0 ? 0 : Mathf.Clamp(WaveIndex + 1, 1, TotalWaves);
        public int AliveEnemyCount => actors.Count;
        /// <summary>아직 내보내지 않은 이번 웨이브의 적 수. 준비 단계에는 일정이 아직 없으므로 웨이브 총량을 낸다.</summary>
        public int PendingEnemyCount
        {
            get
            {
                if (State == RunState.Preparing) return WaveTotal(WaveIndex);
                if (State != RunState.Running) return 0;
                int pending = 0;
                foreach (var schedule in schedules) pending += Mathf.Max(0, schedule.group.count - schedule.emitted);
                return pending;
            }
        }
        public float? PreparationSecondsRemaining =>
            State == RunState.Preparing ? (float?)Mathf.Max(0f, PreparationRemaining) : 0f;
        public IReadOnlyList<string> NextRouteIds => nextRoutes;

        Wave WaveAt(int index)
        {
            var set = graph && graph.level ? graph.level.waves : null;
            return set && index >= 0 && index < set.waves.Count ? set.waves[index] : null;
        }
        int WaveTotal(int index)
        {
            var wave = WaveAt(index);
            if (wave == null) return 0;
            int total = 0;
            foreach (var group in wave.groups) if (group != null) total += Mathf.Max(0, group.count);
            return total;
        }
        void RebuildNextRoutes(Wave wave)
        {
            nextRoutes.Clear();
            if (wave == null) return;
            foreach (var group in wave.groups)
            {
                if (group == null) continue;
                // 경로 가이드가 없으면 스폰 ID가 진입로 식별자 역할을 한다.
                string id = string.IsNullOrWhiteSpace(group.routeId) ? group.spawnId : group.routeId;
                if (!string.IsNullOrWhiteSpace(id) && !nextRoutes.Contains(id)) nextRoutes.Add(id);
            }
        }
        void Notify() { Changed?.Invoke(); }

        /// <summary>진행 중인 적마다 다음 노드까지의 수평 거리·높이차를 도착 반경·허용치와 나란히 보여 준다. 멈춘 적을 찾을 때 쓴다.</summary>
        public string DescribeActors()
        {
            var text = new System.Text.StringBuilder();
            foreach (var w in actors)
            {
                if (!w.actor) continue;
                if (!w.next) { text.Append(w.actor.name).Append(" (경로 가이드 이동)\n"); continue; }
                Vector3 d = w.actor.FeetPosition - w.next.transform.position;
                text.Append(w.actor.name).Append(" → ").Append(w.next.label)
                    .Append("  수평 ").Append(new Vector2(d.x, d.z).magnitude.ToString("0.00")).Append(" / 반경 ").Append(w.next.arrivalRadius.ToString("0.##"))
                    .Append("  높이차 ").Append(d.y.ToString("0.00")).Append(" / 허용 ").Append(w.next.heightTolerance.ToString("0.##")).Append('\n');
            }
            return text.ToString();
        }

        sealed class Schedule { public SpawnGroup group; public int emitted; public float next; }
        sealed class Walker
        {
            public ActorBridge actor;
            public RouteNode next, goal;
            public Queue<Vector3> legacy;
            public float damage;
            public bool arrived;
        }
        enum SpawnOutcome { Spawned, Deferred, Failed }
        readonly List<Schedule> schedules = new List<Schedule>();
        readonly List<Walker> actors = new List<Walker>();
        readonly List<string> nextRoutes = new List<string>();
        System.Random random;
        float elapsed;
        ILevelCoreReceiver Core => coreReceiver as ILevelCoreReceiver;

        void Awake()
        {
            if (actorFactory == null) actorFactory = GetComponent<EnemyPoolActorFactory>();
        }

        void Start() { if (startAutomatically) Begin(); }
        void OnDisable() { StopRun(); }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            if (!graph || !graph.level || !graph.level.waves) { errors.Add("Graph / LevelRoot / WaveSet 필요"); return errors; }
            errors.AddRange(graph.ValidateGraph());
            if (!catalog) { errors.Add("PrefabCatalog 필요"); return errors; }
            errors.AddRange(catalog.ValidateCatalog());
            // 도착 반경이 가장 큰 적의 몸통 반경보다 작으면, 둘만 몰려도 회피가 서로를 원 밖으로 밀어내
            // 밀려난 적은 영영 도착 판정을 받지 못한다.
            float widest = 0f; string widestKey = null;
            foreach (var e in catalog.entries)
            {
                if (e == null || e.role != PrefabRole.Enemy || !e.prefab) continue;
                var agent = e.prefab.GetComponent<NavMeshAgent>();
                if (!agent) continue;
                var scale = agent.transform.lossyScale;
                float radius = agent.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                if (radius > widest) { widest = radius; widestKey = e.key; }
            }
            if (widest > 0f)
                foreach (var node in graph.Nodes)
                    if (node && node.arrivalRadius < widest)
                        errors.Add(node.label + ": 도착 반경 " + node.arrivalRadius.ToString("0.##") + "이 가장 큰 적(" + widestKey
                            + ", 반경 " + widest.ToString("0.##") + ")보다 작습니다. 몰리면 서로 밀어내 도착하지 못합니다.");
            if (Core == null) errors.Add("ILevelCoreReceiver 연결 필요");
            if (graph.level.waves.waves.Count == 0) errors.Add("웨이브가 없습니다.");
            foreach (var wave in graph.level.waves.waves)
            {
                if (wave == null || !FiniteNonnegative(wave.preparationSeconds)) { errors.Add("준비 시간 오류"); continue; }
                foreach (var group in wave.groups)
                {
                    if (group == null || group.count <= 0 || !FiniteNonnegative(group.delay) || !FiniteNonnegative(group.interval) || group.interval <= 0)
                    { errors.Add("출현 수량/시간 오류"); continue; }
                    if (!graph.level.TryResolveSpawnGroup(group, out var binding, out var error)) { errors.Add(error); continue; }
                    var entry = catalog.Find(group.ResolvedKey);
                    if (entry == null || entry.role != PrefabRole.Enemy) errors.Add("웨이브의 gameKey가 적 프리팹에 연결되지 않았습니다.");
                    if (!binding.SuggestedRoute)
                    {
                        var start = graph.Find(binding.Spawn); var goal = graph.Find(binding.Target);
                        if (!start || !goal || !graph.CanReach(start, goal)) errors.Add("입구에서 코어까지 활성 노드 경로가 없습니다.");
                    }
                    var receiver = coreReceiver ? coreReceiver.transform : null;
                    if (receiver && Vector3.Distance(receiver.position, binding.Target.transform.position) > 1f)
                        errors.Add("코어 마커와 수신 컴포넌트 위치를 일치시키세요.");
                }
            }
            return errors;
        }
        static bool FiniteNonnegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        public void Begin()
        {
            if (State == RunState.Preparing || State == RunState.Running) return;
            var errors = ValidateSetup();
            if (errors.Count > 0) { Fail(string.Join("\n", errors)); return; }
            if (Core.IsDefeated) { Fail("코어를 초기화하거나 씬을 다시 시작하세요."); return; }
            Cleanup(); random = new System.Random(randomSeed);
            TotalSpawned = Killed = Absorbed = 0; RunElapsedSeconds = 0; WaveIndex = -1; LastError = null;
            NextWave();
        }
        /// <summary>남은 준비 시간을 건너뛰고 바로 전투로 들어간다. 시작 전이면 시작부터 한다.
        /// 다음 Update에서 일정이 세워지므로 한 프레임 뒤에 Running이 된다.</summary>
        public void SkipPreparation()
        {
            if (State != RunState.Preparing && State != RunState.Running) Begin();
            if (State != RunState.Preparing) return;
            PreparationRemaining = 0; Notify();
        }
        void NextWave()
        {
            WaveIndex++;
            if (WaveIndex >= graph.level.waves.waves.Count) { nextRoutes.Clear(); SetState(RunState.Won); return; }
            PreparationRemaining = graph.level.waves.waves[WaveIndex].preparationSeconds;
            // 표시용 값은 상태를 알리기 전에 갖춘다. 구독자가 SetState 안에서 바로 읽는다.
            RebuildNextRoutes(graph.level.waves.waves[WaveIndex]);
            SetState(RunState.Preparing);
        }
        void Update()
        {
            if (State != RunState.Preparing && State != RunState.Running) return;
            if (Time.timeScale <= 0) return;
            RunElapsedSeconds += Time.deltaTime;
            if (Core == null || !coreReceiver) { Fail("코어 연결이 사라졌습니다."); return; }
            if (Core.IsDefeated) { FinishLoss(); return; }
            if (State == RunState.Preparing)
            {
                PrewarmStep();
                PreparationRemaining = Mathf.Max(0, PreparationRemaining - Time.deltaTime);
                if (PreparationRemaining > 0) return;
                schedules.Clear(); elapsed = 0;
                foreach (var group in graph.level.waves.waves[WaveIndex].groups)
                    schedules.Add(new Schedule { group = group, next = group.delay });
                SetState(RunState.Running);
            }
            int budget = Mathf.Max(1, maxSpawnsPerFrame);
            foreach (var schedule in schedules)
                while (budget > 0 && schedule.emitted < schedule.group.count && elapsed >= schedule.next)
                {
                    SpawnOutcome outcome = Spawn(schedule.group);
                    if (outcome == SpawnOutcome.Failed) return;
                    // 풀이 가득 찼다. 예정 시각을 그대로 두어 다음 프레임에 이어서 낸다.
                    if (outcome == SpawnOutcome.Deferred) break;
                    budget--; schedule.emitted++; schedule.next += schedule.group.interval;
                }
            elapsed += Time.deltaTime;
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var walker = actors[i];
                if (!walker.actor || !walker.actor.Alive) { if (walker.actor) walker.actor.Halt(); Killed++; actors.RemoveAt(i); Notify(); continue; }
                if (!Advance(walker)) return;
                if (walker.arrived)
                {
                    walker.actor.Halt();
                    if (!Core.TryAbsorb(walker.actor, walker.damage)) continue;
                    if (State != RunState.Running) return; // external core callback may stop/clean the run
                    Absorbed++; walker.actor.Remove(); actors.Remove(walker); Notify();
                    if (Core.IsDefeated) { FinishLoss(); return; }
                }
            }
            if (actors.Count == 0 && schedules.TrueForAll(s => s.emitted == s.group.count))
            { onWaveCleared.Invoke(); if (State == RunState.Running) NextWave(); }
        }
        /// <summary>준비 중에 다음 웨이브가 쓸 프리팹을 하나씩 미리 만든다. 프레임당 하나만 만들어
        /// 준비 시간 전체에 비용을 흩뜨린다. 팩토리가 이 기능을 지원하지 않으면 아무것도 하지 않는다.</summary>
        void PrewarmStep()
        {
            if (!actorFactory || !catalog) return;
            var wave = WaveAt(WaveIndex);
            if (wave == null) return;
            foreach (var group in wave.groups)
            {
                if (group == null) continue;
                var entry = catalog.Find(group.ResolvedKey);
                if (entry == null || !entry.prefab) continue;
                if (actorFactory.PrewarmStep(entry.prefab)) return;
            }
        }

        SpawnOutcome Spawn(SpawnGroup group)
        {
            if (!graph.level.TryResolveSpawnGroup(group, out var binding, out var error)) { Fail(error); return SpawnOutcome.Failed; }
            var entry = catalog.Find(group.ResolvedKey);
            if (entry == null || !entry.prefab) { Fail("적 프리팹이 사라졌습니다."); return SpawnOutcome.Failed; }
            // Exact marker position avoids NavMesh sampling onto an adjacent floor.
            Vector3 position = binding.Spawn.transform.position;
            Quaternion rotation = binding.Spawn.transform.rotation;
            GameObject instance = actorFactory
                ? actorFactory.Spawn(entry.prefab, position, rotation)
                : Instantiate(entry.prefab, position, rotation);
            if (instance == null) return SpawnOutcome.Deferred; // 풀의 동시 활성 상한
            var actor = instance.GetComponent<ActorBridge>();
            if (!actor || !actor.Prepare(out error)) { Despawn(instance); Fail(error ?? "ActorBridge 누락"); return SpawnOutcome.Failed; }
            var walker = new Walker { actor = actor, damage = actor.coreDamage };
            if (binding.SuggestedRoute) walker.legacy = new Queue<Vector3>(binding.SuggestedRoute.WorldPoints());
            else { walker.next = graph.Find(binding.Spawn); walker.goal = graph.Find(binding.Target); }
            actors.Add(walker); TotalSpawned++; Notify(); return SpawnOutcome.Spawned;
        }

        void Despawn(GameObject instance)
        {
            if (actorFactory) actorFactory.Despawn(instance); else Destroy(instance);
        }
        bool Advance(Walker w)
        {
            if (w.arrived) return true;
            if (w.legacy != null)
            {
                if (w.legacy.Count > 0 && Vector3.Distance(w.actor.FeetPosition, w.legacy.Peek()) <= .5f) w.legacy.Dequeue();
                if (w.legacy.Count == 0) w.arrived = true;
                else w.actor.Travel(w.legacy.Peek());
                return true;
            }
            if (!w.next || !w.goal) { Fail("이동 중 노드가 제거됐습니다."); return false; }
            if (w.next.Contains(w.actor.FeetPosition))
            {
                if (w.next == w.goal) { w.arrived = true; return true; }
                var next = graph.Choose(w.next, w.goal, random.NextDouble());
                if (!next) { Fail(w.next.label + ": 코어로 이어지는 활성 분기가 없습니다."); return false; }
                w.next = next;
            }
            w.actor.Travel(w.next.transform.position); return true;
        }
        void FinishLoss() { SetState(RunState.Lost); foreach (var w in actors) if (w.actor) w.actor.Halt(); }
        void Fail(string error) { LastError = error; SetState(RunState.Error); foreach (var w in actors) if (w.actor) w.actor.Halt(); Debug.LogError(error, this); }
        void SetState(RunState value) { State = value; onStateChanged.Invoke(); Notify(); }
        public void StopRun() { Cleanup(); if (State == RunState.Running || State == RunState.Preparing) SetState(RunState.Stopped); }
        void Cleanup() { foreach (var w in actors) if (w.actor) w.actor.Remove(); actors.Clear(); schedules.Clear(); }
    }
}
