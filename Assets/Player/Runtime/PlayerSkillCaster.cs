using System;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 공격 계열 액티브 스킬 Q / E / R. Q 모래 폭발은 볼트를 쏴서 착탄점에서 터뜨리고(<see cref="PlayerBasicAttack.Detonate"/>),
    /// E 모래 소용돌이와 R 사막 폭풍은 조준 지점(십자선이 가리키는 곳, 최대 maxCastDistance, 바닥에 붙임)에 지역을 만든다.
    /// 해금은 스탯 플래그(SandBurst·SandVortex·SandStorm), 마나·쿨다운·수치는 여기 인스펙터 기본값이며 반경·지속 등은 스탯 수정자로 올릴 수 있다.
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
        [Min(0.1f)] public float vortexRadius = 4f;
        [Min(0.05f)] public float vortexDuration = 1.5f;
        [Min(0f)] public float vortexPullSpeed = 3f;
        [Min(0f), Tooltip("끝날 때 모인 적을 묶는 시간")] public float vortexEndShackle = 0.5f;
        [Tooltip("VFX_Sand_Vortex. 반경 vortexVfxRadius 기준으로 만들어져 실제 반경 비율로 스케일한다")] public GameObject vortexPrefab;
        [Min(0.1f)] public float vortexVfxRadius = 1f;
        [Header("R 사막 폭풍")]
        [Min(0)] public int stormManaCost = 40;
        [Min(0f)] public float stormCooldown = 15f;
        [Min(0.1f)] public float stormRadius = 5f;
        [Min(0.05f)] public float stormDuration = 5f;
        [Min(0.05f)] public float stormTickInterval = 0.5f;
        [Min(0f), Tooltip("틱 피해 = 볼트 피해 × 이 값")] public float stormDamageRatio = 0.25f;
        [Range(0f, 1f), Tooltip("둔화 비율. 0.6이면 속도 40%")] public float stormSlow = 0.6f;
        [Tooltip("VFX_Sand_Storm. 반경 stormVfxRadius 기준")] public GameObject stormPrefab;
        [Min(0.1f)] public float stormVfxRadius = 1f;
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
        public float StormDuration => Stat(PlayerStat.StormDuration, stormDuration);
        public float StormTickDamage => (attack != null ? attack.Damage : 0f) * Stat(PlayerStat.StormDamageRatio, stormDamageRatio);
        public float StormSlow => Mathf.Clamp01(Stat(PlayerStat.StormSlow, stormSlow));
        public float CooldownRemaining(int slot) => slot >= 0 && slot < 3 ? cooldowns[slot] : 0f;
        public bool Unlocked(int slot) => slot == Burst ? BurstUnlocked : slot == Vortex ? VortexUnlocked : slot == Storm && StormUnlocked;
        public int ManaCost(int slot) => slot == Burst ? burstManaCost : slot == Vortex ? vortexManaCost : stormManaCost;
        public float Cooldown(int slot) => slot == Burst ? burstCooldown : slot == Vortex ? vortexCooldown : stormCooldown;
        /// <summary>(슬롯, 시전 지점) 시전 성공마다.</summary>
        public event Action<int, Vector3> Cast;
        /// <summary>인스펙터 연결용(소리 등). <see cref="Cast"/>와 같은 순간에 부른다.</summary>
        public UnityEngine.Events.UnityEvent onCast = new UnityEngine.Events.UnityEvent();
        public PlayerSandVortex LastVortex { get; private set; }
        public PlayerSandStorm LastStorm { get; private set; }
        public Vector3 LastCastPoint { get; private set; }
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

        /// <summary>부활 등으로 자원을 회복할 때 쿨다운을 지운다.</summary>
        public void ResetCooldowns() => Array.Clear(cooldowns, 0, cooldowns.Length);

        public ActionResult TryCast(int slot)
        {
            if (slot < Burst || slot > Storm) return ActionResult.Fail(ActionFailure.InvalidRequest);
            if (!Unlocked(slot)) return ActionResult.Fail(ActionFailure.Locked);
            if (attack == null || !attack.CanFire)
                return ActionResult.Fail((attack != null ? attack.lifeSource as ILifeState : null)?.State is LifeState state && state != LifeState.Alive
                    ? ActionFailure.NotAlive : ActionFailure.InvalidRequest);
            if (cooldowns[slot] > 0f) return ActionResult.Fail(ActionFailure.Cooldown);
            var mana = Mana;
            if (mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            int cost = ManaCost(slot);
            if (mana.CurrentMana < cost || !mana.TrySpend(cost)) return ActionResult.Fail(ActionFailure.InsufficientMana);
            Vector3 point;
            if (slot == Burst)
            {
                var bolt = attack.FireSkillBolt(attack.Detonate);
                if (bolt == null) { mana.Gain(cost); return ActionResult.Fail(ActionFailure.InvalidRequest); }
                point = bolt.transform.position;
            }
            else
            {
                point = CastPoint();
                if (slot == Vortex) LastVortex = SpawnVortex(point); else LastStorm = SpawnStorm(point);
                attack.PlayCastVisual();
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
            zone.pullSpeed = vortexPullSpeed; zone.endShackle = vortexEndShackle;
            return zone;
        }

        PlayerSandStorm SpawnStorm(Vector3 point)
        {
            var zone = Zone<PlayerSandStorm>("SandStorm", point, StormRadius, StormDuration, stormPrefab, stormVfxRadius);
            zone.tickInterval = stormTickInterval; zone.tickDamage = StormTickDamage; zone.slowFactor = StormSlow;
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
            }
            return zone;
        }
    }
}
