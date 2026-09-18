using System;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 공격 계열 액티브 스킬 Q / E / R. Q 모래 폭발은 볼트를 쏴서 착탄점에서 터뜨리고(<see cref="PlayerBasicAttack.Detonate"/>, 반경 안 적을 띄운다),
    /// E 모래 소용돌이는 조준 지점(십자선이 가리키는 곳, 최대 maxCastDistance, 바닥에 붙임)에 지역을 만들어 끌어모았다가 쳐올리고,
    /// R 사막 폭풍은 코어(없으면 시전자)에서 바깥으로 퍼지는 충격 링이다(<see cref="PlayerSandStorm"/>).
    /// 해금은 스탯 플래그(SandBurst·SandVortex·SandStorm), 마나·쿨다운·수치는 여기 인스펙터 기본값이며 반경 등은 스탯 수정자로 올릴 수 있다.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class PlayerSkillCaster : MonoBehaviour
    {
        public PlayerInputReader input;
        public PlayerBasicAttack attack;
        public PlayerAimer aimer;
        public PlayerStats stats;
        public PlayerMotor motor;
        [Tooltip("IManaWallet. 비우면 같은 오브젝트에서 찾는다")] public MonoBehaviour manaSource;
        [Header("공통")]
        [Tooltip("지역 스킬이 훑는 레이어와 바닥 판정")] public LayerMask skillMask = ~0;
        [Min(1f), Tooltip("지정 지점의 최대 거리(m). 조준점이 더 멀면 그 방향 이 거리에 놓는다")] public float maxCastDistance = 20f;
        [Min(0.1f), Tooltip("지정 지점을 바닥에 붙일 때 위아래로 찾는 높이")] public float groundSnapHeight = 3f;
        [Header("Q 모래 폭발")]
        [Min(0)] public int burstManaCost = 15;
        [Min(0f)] public float burstCooldown = 4f;
        [Header("E 모래 소용돌이")]
        [Min(0)] public int vortexManaCost = 25;
        [Min(0f)] public float vortexCooldown = 8f;
        [Min(0.1f)] public float vortexRadius = 3f;
        [Min(0.05f)] public float vortexDuration = 2.5f;
        [Min(0f)] public float vortexPullSpeed = 4f;
        [Min(0f), Tooltip("끝날 때 모인 적을 위로 쳐올리는 속도(m/s). 0이면 안 띄운다")] public float vortexEndLaunch = 4.5f;
        [Min(0f), Tooltip("끝날 때 모인 적을 묶는 시간(예전 방식). 0이면 묶지 않는다")] public float vortexEndShackle = 0f;
        [Tooltip("VFX_Sand_Vortex. 반경 vortexVfxRadius 기준으로 만들어져 실제 반경 비율로 스케일한다")] public GameObject vortexPrefab;
        [Min(0.1f)] public float vortexVfxRadius = 1f;
        [Header("R 사막 폭풍 — 코어에서 바깥으로 퍼지는 충격 링")]
        [Min(0)] public int stormManaCost = 60;
        [Min(0f)] public float stormCooldown = 45f;
        [Min(1f), Tooltip("링이 닿는 최대 반경(m). 신전 외벽 76m와 스폰 지점 90m를 덮는다")] public float stormRadius = 90f;
        [Min(0.1f), Tooltip("링이 퍼지는 속도(m/s)")] public float stormExpandSpeed = 14f;
        [Min(0.1f), Tooltip("링 두께(m)")] public float stormRingThickness = 4f;
        [Min(0f), Tooltip("링 피해 = 볼트 피해 × 이 값(한 번)")] public float stormDamageRatio = 1.5f;
        [Min(0f), Tooltip("링이 바깥으로 미는 속도(m/s)")] public float stormKnockback = 7f;
        [Range(0f, 1f), Tooltip("둔화 비율. 0.5면 속도 50%")] public float stormSlow = 0.5f;
        [Min(0f)] public float stormSlowDuration = 3f;
        [Tooltip("VFX_Sand_Storm. 자식 VfxStormFront가 링 반경을 따라간다(스케일하지 않음)")] public GameObject stormPrefab;
        [Tooltip("폭풍 시작점. 비우면 씬에서 코어(ICombatTarget Kind=Core)를 찾고, 없으면 시전자 위치")] public Transform stormOrigin;
        readonly float[] cooldowns = new float[3];
        public const int Burst = 0, Vortex = 1, Storm = 2;
        public Func<int,bool> SkillTreeAllowed;
        public Action<int> SkillTreeInput;
        public bool BurstUnlocked => SkillTreeAllowed!=null?SkillTreeAllowed(Burst):attack != null && attack.SandBurst;
        public bool VortexUnlocked => SkillTreeAllowed!=null?SkillTreeAllowed(Vortex):Flag(PlayerStat.SandVortex);
        public bool StormUnlocked => SkillTreeAllowed!=null?SkillTreeAllowed(Storm):Flag(PlayerStat.SandStorm);
        public float VortexRadius => Stat(PlayerStat.VortexRadius, vortexRadius);
        public float VortexDuration => Stat(PlayerStat.VortexDuration, vortexDuration);
        public float StormRadius => Stat(PlayerStat.StormRadius, stormRadius);
        /// <summary>링이 최대 반경을 지나 완전히 빠져나갈 때까지.</summary>
        public float StormDuration => (StormRadius + stormRingThickness) / Mathf.Max(0.1f, stormExpandSpeed);
        public float StormDamage => (attack != null ? attack.Damage : 0f) * Stat(PlayerStat.StormDamageRatio, stormDamageRatio);
        public float StormSlow => Mathf.Clamp01(Stat(PlayerStat.StormSlow, stormSlow));
        public float CooldownRemaining(int slot) => slot >= 0 && slot < 3 ? cooldowns[slot] : 0f;
        public bool Unlocked(int slot) => slot == Burst ? BurstUnlocked : slot == Vortex ? VortexUnlocked : slot == Storm && StormUnlocked;
        public int ManaCost(int slot) => slot == Burst ? burstManaCost : slot == Vortex ? vortexManaCost : stormManaCost;
        public float Cooldown(int slot) => slot == Burst ? burstCooldown : slot == Vortex ? vortexCooldown : stormCooldown;
        /// <summary>(슬롯, 시전 지점) 시전 성공마다. 폭풍은 시작점(코어).</summary>
        public event Action<int, Vector3> Cast;
        /// <summary>인스펙터 연결용(소리 등). <see cref="Cast"/>와 같은 순간에 부른다.</summary>
        public UnityEngine.Events.UnityEvent onCast = new UnityEngine.Events.UnityEvent();
        /// <summary>인스펙터 연결용: 사막 폭풍 시전(바깥 폭풍 연출·코어 플래시 등).</summary>
        public UnityEngine.Events.UnityEvent onStormCast = new UnityEngine.Events.UnityEvent();
        public PlayerSandVortex LastVortex { get; private set; }
        public PlayerSandStorm LastStorm { get; private set; }
        public Vector3 LastCastPoint { get; private set; }
        Transform cachedCore;
        float Stat(PlayerStat stat, float baseValue) => stats != null ? stats.Evaluate(stat, baseValue) : baseValue;
        bool Flag(PlayerStat stat) => stats != null && stats.EvaluateCount(stat, 0) > 0;
        IManaWallet Mana => (manaSource as IManaWallet) ?? (motor != null ? motor.manaSource as IManaWallet : null);

        void Awake()
        {
            if (attack == null) attack = GetComponent<PlayerBasicAttack>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (aimer == null) aimer = GetComponent<PlayerAimer>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (manaSource == null) manaSource = GetComponent<IManaWallet>() as MonoBehaviour;
        }
        void OnEnable() { if (input != null) input.SkillPressed += OnSkill; }
        void OnDisable() { if (input != null) input.SkillPressed -= OnSkill; }
        void Update() { for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Mathf.Max(0f, cooldowns[i] - Time.deltaTime); }
        void OnSkill(int slot) { if(SkillTreeInput!=null)SkillTreeInput(slot);else TryCast(slot); }

        /// <summary>플레이어가 누른 스킬이 쿨다운·마나 부족으로 나가지 못했을 때(거절음 등). 잠김·사망 같은 상태 거부는 알리지 않는다.</summary>
        public UnityEngine.Events.UnityEvent onCastFailed = new UnityEngine.Events.UnityEvent();

        ActionResult CastFailed(ActionFailure reason) { onCastFailed.Invoke(); return ActionResult.Fail(reason); }

        /// <summary>부활 등으로 자원을 회복할 때 쿨다운을 지운다.</summary>
        public void ResetCooldowns() => Array.Clear(cooldowns, 0, cooldowns.Length);

        public ActionResult TryCast(int slot)
        {
            if (slot < Burst || slot > Storm) return ActionResult.Fail(ActionFailure.InvalidRequest);
            if (!Unlocked(slot)) return ActionResult.Fail(ActionFailure.Locked);
            if (attack == null || !attack.CanFire)
                return ActionResult.Fail((attack != null ? attack.lifeSource as ILifeState : null)?.State is LifeState state && state != LifeState.Alive
                    ? ActionFailure.NotAlive : ActionFailure.InvalidRequest);
            if (cooldowns[slot] > 0f) return CastFailed(ActionFailure.Cooldown);
            var mana = Mana;
            if (mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            int cost = ManaCost(slot);
            if (mana.CurrentMana < cost || !mana.TrySpend(cost)) return CastFailed(ActionFailure.InsufficientMana);
            Vector3 point;
            if (slot == Burst)
            {
                var bolt = attack.FireSkillBolt(attack.Detonate);
                if (bolt == null) { mana.Gain(cost); return ActionResult.Fail(ActionFailure.InvalidRequest); }
                point = bolt.transform.position;
            }
            else if (slot == Vortex)
            {
                point = CastPoint();
                LastVortex = SpawnVortex(point);
                attack.PlayCastVisual();
            }
            else
            {
                point = StormOriginPoint();
                LastStorm = SpawnStorm(point);
                attack.PlayCastVisual();
                onStormCast.Invoke();
            }
            cooldowns[slot] = Cooldown(slot);
            LastCastPoint = point;
            Cast?.Invoke(slot, point);
            onCast.Invoke();
            return ActionResult.Success();
        }

        /// <summary>조준점을 최대 거리로 자르고 바닥에 붙인다. 벽을 조준했으면 벽 앞 바닥에 놓인다(벽 위가 아니라).</summary>
        public Vector3 CastPoint()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 point = aimer != null ? aimer.GetAimPoint() : origin + transform.forward * maxCastDistance;
            Vector3 offset = point - origin;
            if (offset.magnitude > maxCastDistance) point = origin + offset.normalized * maxCastDistance;
            Vector3 back = offset; back.y = 0f;
            if (back.sqrMagnitude > 0.01f) point -= back.normalized * 0.3f; // 조준한 면에서 시전자 쪽으로 살짝 물러나 그 앞 바닥을 찾는다
            return SnapToGround(point);
        }

        /// <summary>폭풍 시작점: stormOrigin → 씬의 코어 → 시전자. 바닥에 붙인다.</summary>
        public Vector3 StormOriginPoint()
        {
            var origin = stormOrigin != null ? stormOrigin : Core;
            return SnapToGround(origin != null ? origin.position : transform.position);
        }

        /// <summary>
        /// 씬의 코어. 전투 개체(ICombatTarget Kind=Core)를 먼저 찾고, 없으면 Level 통합의 CoreReceiver(이 어셈블리가 참조하지 않아 타입 이름으로 찾는다)를 쓴다.
        /// 없으면 null. 한 번 찾으면 기억한다.
        /// </summary>
        public Transform Core
        {
            get
            {
                if (cachedCore != null) return cachedCore;
                MonoBehaviour receiver = null;
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (behaviour is ICombatTarget target && target.Kind == CombatTargetKind.Core) { cachedCore = behaviour.transform; return cachedCore; }
                    if (receiver == null && behaviour.GetType().Name == "CoreReceiver") receiver = behaviour;
                }
                if (receiver != null) cachedCore = receiver.transform;
                return cachedCore;
            }
        }

        Vector3 SnapToGround(Vector3 point)
        {
            RaycastHit? ground = null;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.down, groundSnapHeight + 0.5f, skillMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (!ground.HasValue || hit.distance < ground.Value.distance) ground = hit;
            }
            return ground.HasValue ? ground.Value.point : point;
        }

        PlayerSandVortex SpawnVortex(Vector3 point)
        {
            var zone = Zone<PlayerSandVortex>("SandVortex", point, VortexRadius, VortexDuration, vortexPrefab, vortexVfxRadius);
            zone.pullSpeed = vortexPullSpeed; zone.endLaunch = vortexEndLaunch; zone.endShackle = vortexEndShackle;
            return zone;
        }

        PlayerSandStorm SpawnStorm(Vector3 point)
        {
            float radius = StormRadius;
            var zone = Zone<PlayerSandStorm>("SandStorm", point, radius, StormDuration, stormPrefab, radius); // 연출은 스케일하지 않는다(VfxStormFront가 반경을 받는다)
            zone.expandSpeed = stormExpandSpeed; zone.ringThickness = stormRingThickness;
            zone.damage = StormDamage; zone.knockback = stormKnockback; zone.slowFactor = StormSlow; zone.slowDuration = stormSlowDuration;
            zone.hitSink = attack.ReportHit;
            return zone;
        }

        T Zone<T>(string name, Vector3 point, float radius, float duration, GameObject prefab, float prefabRadius) where T : PlayerSandZone
        {
            var go = new GameObject(name);
            go.transform.position = point;
            var zone = go.AddComponent<T>();
            zone.owner = transform; zone.faction = (attack.lifeSource as ICombatTarget)?.FactionId ?? attack.factionId;
            zone.radius = radius; zone.duration = duration; zone.mask = skillMask;
            if (prefab != null)
            {
                var vfx = Instantiate(prefab, go.transform);
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localScale = Vector3.one * (radius / Mathf.Max(0.01f, prefabRadius));
                // Play On Awake는 이펙트 계층이 공유하므로 끝날 때 터질 "OnEnd*" 자식은 여기서 멈춰 두고 PlayerSandZone.End가 재생한다.
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                    if (ps.gameObject.name.StartsWith("OnEnd", StringComparison.Ordinal)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return zone;
        }
    }
}
