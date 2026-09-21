using System;
using System.Collections.Generic;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>관통 빔 한 발의 결과. 테스트·연출이 읽는다.</summary>
    public readonly struct PlayerBeamShot
    {
        public readonly Vector3 Origin, Direction, End;
        public readonly float Length;
        public readonly int EnemiesHit;
        /// <summary>벽·시설에 막혔거나 적을 맞혔다. 허공으로 사거리 끝까지 갔으면 false.</summary>
        public readonly bool Landed;
        public PlayerBeamShot(Vector3 origin, Vector3 direction, Vector3 end, int enemiesHit, bool landed)
        { Origin = origin; Direction = direction; End = end; Length = Vector3.Distance(origin, end); EnemiesHit = enemiesHit; Landed = landed; }
    }

    // Release after the spell visual has applied its final wrist rotation in LateUpdate.
    [DefaultExecutionOrder(200)]
    public sealed class PlayerBasicAttack : MonoBehaviour
    {
        public PlayerInputReader input;
        [Tooltip("ILifeState 및 ICombatTarget을 제공하는 컴포넌트")]
        public MonoBehaviour lifeSource;
        public PlayerAimer aimer;
        public PlayerVisuals visuals;
        [Tooltip("발사하면 잠시 카메라 정면을 보게 한다. 비워도 된다")]
        public PlayerMotor motor;
        public PlayerProjectile projectilePrefab;
        [Tooltip("모델의 발사 위치가 벽을 넘어갔는지 확인하는 몸통 기준점")]
        public Transform shotOrigin;
        [Min(0.02f)] public float attackInterval = 0.3f;
        [Min(0f)] public float damage = 10f;
        [Min(0), Tooltip("일반 공격 한 발의 마나 비용. 0이면 소모하지 않는다")]
        public int manaCost = 5;
        public string factionId = "Ally";
        [Tooltip("스탯 수정자. 비우면 같은 오브젝트에서 찾고, 없으면 위 기본값을 그대로 쓴다")]
        public PlayerStats stats;
        [Tooltip("일반 공격 비용과 타격 시 마나 회복에 쓰는 IManaWallet. 비우면 같은 오브젝트에서 찾는다")]
        public MonoBehaviour manaSource;
        [Header("스킬 — 공통")]
        [Tooltip("빔·폭발·족쇄가 훑는 레이어")] public LayerMask skillMask = ~0;
        [Min(0.1f), Tooltip("빔·폭발 연출 인스턴스를 지우기까지의 시간")] public float skillVfxLifetime = 3f;
        [Tooltip("폭발 때 카메라 킥을 줄 리그. 비우면 자식에서 찾는다")] public PlayerCameraRig cameraRig;
        [Header("스킬 — 관통탄 (PierceBeam 스탯이 켜면 볼트 대신 이 빔)")]
        [Tooltip("VFX_Pierce_Beam. 총구에서 +Z가 발사 방향, 자식 VfxBeam의 길이를 실제 도달 거리로 맞춘다")] public GameObject beamPrefab;
        [Tooltip("빔이 꿰뚫은 적마다 재생 (VFX_ManaBolt_Impact)")] public GameObject beamHitPrefab;
        [Min(0.5f), Tooltip("관통 빔 사거리(m). 맵 크기(신전 외벽 76m, 스폰 90m)를 덮도록 100")] public float beamRange = 100f;
        [Min(0.01f), Tooltip("빔 판정 굵기(반지름)")] public float beamRadius = 0.2f;
        [Header("스킬 — 모래 폭발 (SandBurst 스탯 = Q 해금. 시전은 PlayerSkillCaster, 폭발 관통탄도 이 수치를 쓴다)")]
        [Tooltip("VFX_Sand_Burst. 착탄점(바닥)에 놓는다")] public GameObject burstPrefab;
        [Min(0.1f)] public float burstRadius = 3.75f;
        [Min(0.1f), Tooltip("폭발 연출 프리팹이 만들어진 기준 반경(m). 실제 반경이 이보다 크거나 작으면 그 비율로 연출을 키우거나 줄여 판정과 맞춘다")] public float burstVfxRadius = 2.5f;
        [Min(0f), Tooltip("폭발 피해 = 볼트 피해 × 이 값. 직접 맞은 적은 볼트 피해와 폭발 피해를 둘 다 받는다")] public float burstDamageRatio = 1f;
        [Min(0f), Tooltip("폭발마다 카메라 감쇠 흔들림 진폭(m)")] public float burstCameraKick = 0.08f;
        [Min(0.01f)] public float burstCameraKickDuration = 0.18f;
        [Min(0f), Tooltip("폭발 반경 안 적을 띄우는 수직 속도(m/s). 0이면 띄우지 않는다. 낙하·착지·복귀는 적의 EnemyFall이 맡는다")] public float burstLaunchUp = 5.5f;
        [Min(0f), Tooltip("띄울 때 폭발 중심에서 바깥으로 날리는 수평 속도(m/s)")] public float burstLaunchOut = 5f;
        [Range(0f, 1f), Tooltip("관통탄이 꿰뚫은 자리에서 터지는 폭발의 띄우기·밀어내기 배율. 연출과 같이 절반(0.5). Q 폭발은 항상 1배")] public float piercedBurstLaunchScale = 0.5f;
        [Header("스킬 — 관통탄 충전 (PlayerPierceCharge가 charge 0~1로 TrySkillPierce를 부른다)")]
        [Min(1f), Tooltip("만충 시 빔 피해 배수")] public float chargedDamageMultiplier = 3.5f;
        [Min(1f), Tooltip("만충 시 빔 굵기(판정·연출) 배수")] public float chargedRadiusMultiplier = 5f;
        [Min(0f), Tooltip("만충 시 사거리에 더하는 거리(m)")] public float chargedRangeBonus = 8f;
        [Min(0f), Tooltip("만충 시 꿰뚫은 적을 진행 방향으로 미는 속도(m/s). 충전량에 비례")] public float chargedKnockback = 6f;
        [Min(0.01f), Tooltip("VFX_Pierce_Beam의 기본 반경. 충전 배수를 곱해 연출 굵기로 쓴다")] public float beamVfxRadius = 0.14f;
        [Min(0f), Tooltip("VFX_Pierce_Beam의 기본 밝기(HDR). 만충이면 1.25배. 대낮 신전에서 블룸으로 하얗게 날아가지 않는 값")] public float beamVfxIntensity = 1.1f;
        [Range(0f, 1f), Tooltip("이 충전량 이상이면 빔을 따라 달리는 링(자식 Rings)을 켠다")] public float beamRingsFromCharge = 0.5f;
        [Tooltip("관통탄의 시작점(몸 가운데 앞, 두 팔을 뻗은 자리). 비우면 총구(FirePoint)에서 나간다. 충전 연출도 여기에 붙는다")] public Transform pierceOrigin;
        [Min(0.05f), Tooltip("관통 빔 연출이 유지되는 시간(초). 발사 동작이 팔을 뻗고 있는 시간과 맞춘다(PlayerPierceAnimationBuilder.FireHoldSeconds)")] public float pierceBeamDuration = 1f;
        [Header("스킬 — 모래 족쇄 (SandShackle 스탯, 패시브: 모든 모래 폭발 지점에서 속박)")]
        [Min(0.1f)] public float shackleRadius = 2f;
        [Min(0.05f)] public float shackleDuration = 1.5f;
        public bool CombatEnabled { get; set; } = true;
        public float CooldownRemaining { get; private set; }
        /// <summary>수정자를 적용한 최종 피해·간격. 위 필드는 기본값이다.</summary>
        public float Damage => stats != null ? stats.Evaluate(PlayerStat.AttackDamage, damage) : damage;
        public float AttackInterval => stats != null ? stats.Evaluate(PlayerStat.AttackInterval, attackInterval) : attackInterval;
        /// <summary>명중마다 회복하는 마나. 기본 0이며 스킬(⑩ 마나 순환)이 ManaPerHit 수정자로 올린다.</summary>
        public int ManaPerHit => stats != null ? stats.EvaluateCount(PlayerStat.ManaPerHit, 0) : 0;
        // 공격 마법 스킬. 켜짐은 개수형 스탯(0보다 크면), 수치는 인스펙터 기본값에 수정자를 얹은 값.
        public float BoltScale => Stat(PlayerStat.BoltScale, 1f);
        public Func<bool> SkillTreePierceAllowed;
        // In tree mode piercing is an equipped active skill, not a free basic-attack upgrade.
        public bool PierceBeam => SkillTreePierceAllowed==null && Flag(PlayerStat.PierceBeam);
        /// <summary>스킬트리 모드의 폭발 관통탄. 꿰뚫은 적마다 폭발한다. charge 0 = 탭, 1 = 만충(피해·굵기·사거리·밀어내기 최대).</summary>
        public bool TrySkillPierce(float charge = 0f)
        {
            if(SkillTreePierceAllowed!=null && !SkillTreePierceAllowed() || !CanFire)return false;
            Aim(out var muzzle,out var direction,out var origin);
            if (pierceOrigin != null)
            {
                // 두 손으로 쏘는 빔: 오른손 총구가 아니라 몸 가운데 앞에서, 그 점에서 조준점을 향해.
                muzzle = pierceOrigin.position;
                Vector3 aim = aimer != null ? aimer.GetAimPoint() : muzzle + transform.forward * 10f;
                direction = (aim - muzzle).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            }
            FireBeam(muzzle,direction,origin,Mathf.Clamp01(charge), explodeOnPierce: true);
            // 양팔 내지르기 동작이 있으면 그걸로, 없으면 기존 손바닥 시전 동작으로.
            if (motor != null) motor.FaceCamera();
            if (visuals == null || !visuals.PlayPierceFire()) PlayCastVisual();
            return true;
        }
        /// <summary>마지막 빔의 충전량(0~1).</summary>
        public float LastBeamCharge { get; private set; }
        public float BeamRange => Stat(PlayerStat.BeamRange, beamRange);
        public bool SandBurst => Flag(PlayerStat.SandBurst);
        public float BurstRadius => Stat(PlayerStat.BurstRadius, burstRadius);
        public float BurstDamage => Damage * Stat(PlayerStat.BurstDamageRatio, burstDamageRatio);
        public bool BurstPerPierce => Flag(PlayerStat.BurstPerPierce);
        public bool SandShackle => Flag(PlayerStat.SandShackle);
        public float ShackleRadius => Stat(PlayerStat.ShackleRadius, shackleRadius);
        public float ShackleDuration => Stat(PlayerStat.ShackleDuration, shackleDuration);
        float Stat(PlayerStat stat, float baseValue) => stats != null ? stats.Evaluate(stat, baseValue) : baseValue;
        bool Flag(PlayerStat stat) => stats != null && stats.EvaluateCount(stat, 0) > 0;
        /// <summary>플레이어가 실제로 피해를 준 명중. 볼트·빔이 여러 적을 꿰뚫으면 적마다, 폭발도 맞힌 적마다 한 번씩 온다.</summary>
        public event Action<PlayerHitInfo> Hit;
        /// <summary>타격으로 실제 회복한 마나. 지갑이 가득 찼으면 발생하지 않는다.</summary>
        public event Action<int> ManaAbsorbed;
        /// <summary>관통 빔을 쏠 때마다.</summary>
        public event Action<PlayerBeamShot> BeamFired;
        /// <summary>모래 폭발이 터진 위치.</summary>
        public event Action<Vector3> Burst;
        /// <summary>모래 족쇄가 걸린 위치와 묶인 적 수.</summary>
        public event Action<Vector3, int> Shackled;
        /// <summary>모래 폭발이 띄운 위치와 적 수(실제로 떠오른 것만).</summary>
        public event Action<Vector3, int> Launched;
        public int LastLaunchCount { get; private set; }
        public int HitCount { get; private set; }
        public PlayerBeamShot LastBeam { get; private set; }
        IManaWallet Mana => (manaSource as IManaWallet) ?? (motor != null ? motor.manaSource as IManaWallet : null);
        string Faction => (lifeSource as ICombatTarget)?.FactionId ?? factionId;
        void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (manaSource == null) manaSource = GetComponent<IManaWallet>() as MonoBehaviour;
            if (cameraRig == null) cameraRig = GetComponentInChildren<PlayerCameraRig>(true);
        }

        void OnProjectileHit(PlayerHitInfo info)
        {
            if (this == null) return; // 볼트가 플레이어보다 오래 살 수 있다
            HitCount++;
            int gain = ManaPerHit;
            if (gain > 0)
            {
                int recovered = Mana?.Gain(gain) ?? 0;
                if (recovered > 0) ManaAbsorbed?.Invoke(recovered);
            }
            Hit?.Invoke(info);
        }
        bool pendingShot;
        bool HasAttackMana => manaCost <= 0 || (Mana != null && Mana.CurrentMana >= manaCost);
        PlayerSpellcasting Casting => visuals != null && visuals.Spellcasting != null && visuals.Spellcasting.isActiveAndEnabled
            ? visuals.Spellcasting : null;

        void OnEnable() { if (input != null) input.PrimaryActionPressed += RequestShot; }
        void OnDisable()
        {
            if (input != null) input.PrimaryActionPressed -= RequestShot;
            pendingShot = false;
            if (Casting != null) Casting.SetCasting(false);
        }

        /// <summary>부활 등으로 자원을 회복할 때 공격 쿨다운과 예약된 발사를 지운다.</summary>
        public void ResetCooldown() { CooldownRemaining = 0f; pendingShot = false; }

        void RequestShot()
        {
            if (Available() && HasAttackMana && CooldownRemaining <= 0f) pendingShot = true;
        }

        void Update()
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            if (!Available() || !HasAttackMana) pendingShot = false;
            bool wantsShot = Available() && HasAttackMana && (pendingShot || (input != null && input.PrimaryAttackHeld));
            if (Casting != null) Casting.SetCasting(wantsShot);
            if (wantsShot && motor != null) motor.FaceCamera();
        }

        void LateUpdate()
        {
            if (pendingShot || (input != null && input.PrimaryAttackHeld)) TryFire();
        }

        bool Available() => isActiveAndEnabled && CombatEnabled && Time.timeScale > 0f
            && (input == null || input.AcceptsInput)
            && (lifeSource == null || (lifeSource as ILifeState)?.State == global::LifeState.Alive)
            && projectilePrefab != null && aimer != null && visuals != null && visuals.FirePoint != null
            && !string.IsNullOrWhiteSpace(factionId);

        /// <summary>Returns true only when a bolt is released. A casting visual queues the shot during its short windup.</summary>
        public bool TryFire()
        {
            if (!Available() || CooldownRemaining > 0f) return false;
            if (!HasAttackMana)
            {
                pendingShot = false;
                if (Casting != null) Casting.SetCasting(false);
                return false;
            }
            if (Casting != null)
            {
                pendingShot = true;
                Casting.SetCasting(true);
                if (!Casting.ReadyToFire) return false;
            }
            Aim(out Vector3 muzzle, out Vector3 direction, out Vector3 origin);
            if (manaCost > 0 && !Mana.TrySpend(manaCost))
            {
                pendingShot = false;
                if (Casting != null) Casting.SetCasting(false);
                return false;
            }
            if (PierceBeam) FireBeam(muzzle, direction, origin);
            else FireBolt(muzzle, direction, origin, null);
            CooldownRemaining = AttackInterval;
            pendingShot = false;
            if (motor != null) motor.FaceCamera();
            visuals.PlayFire();
            return true;
        }

        /// <summary>지금 발사할 수 있는 상태인지(생존·전투 가능·조준 부품·커서). 쿨다운은 보지 않는다.</summary>
        public bool CanFire => Available();

        /// <summary>총구·발사 방향·몸통 기준점. 발사 방향은 총구에서 화면 중앙 조준점으로.</summary>
        public void Aim(out Vector3 muzzle, out Vector3 direction, out Vector3 origin)
        {
            muzzle = visuals != null && visuals.FirePoint != null ? visuals.FirePoint.position : transform.position + Vector3.up;
            Vector3 aim = aimer != null ? aimer.GetAimPoint() : muzzle + transform.forward * 10f;
            direction = (aim - muzzle).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            origin = shotOrigin != null ? shotOrigin.position : transform.position + Vector3.up;
        }

        /// <summary>지정 지점 스킬(소용돌이·폭풍)의 시전 연출. 카메라 정면을 보고 시전 동작·총구 이펙트를 낸다.</summary>
        public void PlayCastVisual()
        {
            if (motor != null) motor.FaceCamera();
            if (visuals != null) visuals.PlayFire();
        }

        /// <summary>스킬(Q 모래 폭발)이 쏘는 볼트. 기본 공격 쿨다운과 별개로 나가며, 착탄점을 onImpact로 알린다. 발사 연출(시전 동작·총구)은 기본 공격과 같다.</summary>
        public PlayerProjectile FireSkillBolt(Action<Vector3> onImpact)
        {
            if (!Available()) return null;
            Aim(out Vector3 muzzle, out Vector3 direction, out Vector3 origin);
            var projectile = FireBolt(muzzle, direction, origin, onImpact);
            if (motor != null) motor.FaceCamera();
            visuals.PlayFire();
            return projectile;
        }

        PlayerProjectile FireBolt(Vector3 muzzle, Vector3 direction, Vector3 origin, Action<Vector3> onImpact)
        {
            PlayerProjectile projectile = PrefabPool.Spawn(projectilePrefab, muzzle, Quaternion.LookRotation(direction));
            if (stats != null) projectile.speed = stats.Evaluate(PlayerStat.ProjectileSpeed, projectile.speed);
            float scale = BoltScale;
            if (projectile.visualRoot != null && Mathf.Abs(scale - 1f) > 0.001f) projectile.visualRoot.localScale *= scale;
            projectile.Hit += OnProjectileHit;
            if (onImpact != null) projectile.Impacted += onImpact;
            projectile.Launch(transform, Faction, Damage, direction, origin);
            return projectile;
        }

        /// <summary>
        /// 관통탄: 몸통→총구, 총구→사거리 순으로 훑어 적대 대상은 전부 꿰뚫고(피해 각각) 그 외 콜라이더(벽·아군 시설)에서 멈춘다.
        /// 죽은 개체와 트리거는 통과한다. 폭발 관통탄이면 꿰뚫은 적마다 모래 폭발이 터진다.
        /// </summary>
        void FireBeam(Vector3 muzzle, Vector3 direction, Vector3 origin, float charge = 0f, bool explodeOnPierce = false)
        {
            string faction = Faction;
            float amount = Damage * Mathf.Lerp(1f, chargedDamageMultiplier, charge);
            float radius = beamRadius * Mathf.Lerp(1f, chargedRadiusMultiplier, charge);
            float range = BeamRange + chargedRangeBonus * charge;
            float knock = chargedKnockback * charge;
            var seen = new HashSet<IDamageable>();
            var enemyPoints = new List<Vector3>();
            var pushed = new List<IDisplaceable>();
            Vector3 end = muzzle + direction * range;
            bool landed = false;
            // 몸통에서 총구까지 먼저: 총구가 벽 너머로 들어갔으면 빔은 거기서 끝난다.
            Vector3 toMuzzle = muzzle - origin;
            bool blockedBeforeMuzzle = toMuzzle.sqrMagnitude > 0.0001f
                && Trace(origin, toMuzzle.normalized, toMuzzle.magnitude, radius, faction, amount, seen, enemyPoints, pushed, out end);
            if (blockedBeforeMuzzle) landed = true;
            else landed = Trace(muzzle, direction, range, radius, faction, amount, seen, enemyPoints, pushed, out end);
            if (enemyPoints.Count > 0) landed = true;
            Vector3 visualStart = blockedBeforeMuzzle ? end : muzzle;
            LastBeam = new PlayerBeamShot(visualStart, direction, end, enemyPoints.Count, landed);
            LastBeamCharge = charge;
            if (knock > 0f) { Vector3 push = direction; push.y = 0f; push = push.sqrMagnitude > 0.0001f ? push.normalized * knock : Vector3.zero; foreach (var target in pushed) target.Knockback(push); }
            if (beamPrefab != null)
            {
                var beam = PrefabPool.Spawn(beamPrefab, visualStart, Quaternion.LookRotation(direction));
                var driver = beam.GetComponentInChildren<VfxBeam>();
                if (driver != null)
                {
                    // 풀에서 다시 빌려도 배율이 쌓이지 않도록 기본 반경에서 매번 계산한다.
                    driver.Radius = beamVfxRadius * Mathf.Lerp(1f, chargedRadiusMultiplier, charge);
                    driver.Intensity = beamVfxIntensity * Mathf.Lerp(1f, 1.25f, charge);
                    if (charge > 0f || pierceOrigin != null) driver.Duration = pierceBeamDuration; // 스킬 관통탄은 팔을 뻗고 있는 동안 빔이 남는다
                    driver.SetLength(Mathf.Max(0.05f, LastBeam.Length));
                }
                var rings = beam.transform.Find("Rings");
                var ringSystem = rings != null ? rings.GetComponent<ParticleSystem>() : null;
                if (ringSystem != null)
                {
                    if (charge >= beamRingsFromCharge && charge > 0f)
                    {
                        var main = ringSystem.main;
                        float speed = Mathf.Max(1f, main.startSpeed.constant);
                        main.startLifetime = Mathf.Max(0.05f, LastBeam.Length / speed); // 링이 빔 끝에서 사라진다
                        ringSystem.Clear(true); ringSystem.Play(true);
                    }
                    else ringSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                PrefabPool.Release(beam, skillVfxLifetime);
            }
            if (beamHitPrefab != null)
                foreach (var point in enemyPoints)
                    PrefabPool.Release(PrefabPool.Spawn(beamHitPrefab, point, Quaternion.identity), skillVfxLifetime);
            BeamFired?.Invoke(LastBeam);
            if (explodeOnPierce || BurstPerPierce) foreach (var point in enemyPoints) DetonateExplosion(point, 0.5f, piercedBurstLaunchScale);
        }

        /// <summary>한 구간을 훑는다. 막히면 true와 막힌 점, 아니면 false와 구간 끝점을 돌려준다. 꿰뚫은 적은 seen·enemyPoints에 쌓인다.</summary>
        bool Trace(Vector3 from, Vector3 direction, float distance, float radius, string faction, float amount, HashSet<IDamageable> seen, List<Vector3> enemyPoints, List<IDisplaceable> pushed, out Vector3 end)
        {
            var hits = Physics.SphereCastAll(from, radius, direction, distance, skillMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                Vector3 point = hit.distance <= 0f ? hit.collider.ClosestPoint(from) : hit.point;
                IDamageable receiver = hit.collider.GetComponentInParent<IDamageable>();
                ICombatTarget target = hit.collider.GetComponentInParent<ICombatTarget>();
                if (target != null)
                {
                    if (!target.IsTargetable) continue; // 죽은 개체는 통과
                    if (target.FactionId == faction) { end = point; return true; } // 아군 벽·시설은 막는다
                    receiver = target.DamageReceiver;
                }
                if (receiver == null) { end = point; return true; } // 지형·벽
                if (!seen.Add(receiver)) continue; // 콜라이더가 여럿인 적은 한 번만
                var displaceable = hit.collider.GetComponentInParent<IDisplaceable>();
                if (displaceable != null && !pushed.Contains(displaceable)) pushed.Add(displaceable);
                var result = receiver.TakeDamage(new DamageInfo(amount, faction, causeId: "player.pierce", hitPosition: point, hitDirection: direction));
                if (result.Status == DamageStatus.InvalidRequest) Debug.LogWarning("관통 빔의 피해 요청이 거부되었습니다.", this);
                if (result.WasApplied && result.AppliedDamage > 0f)
                    OnProjectileHit(new PlayerHitInfo(target, receiver, result.AppliedDamage, result.WasKilled, point, direction, "player.pierce"));
                enemyPoints.Add(point);
            }
            end = from + direction * distance;
            return false;
        }

        /// <summary>실제 피해를 준 명중을 기록한다(마나 순환·Hit 이벤트). 지역 스킬(폭풍)도 여기로 보고한다.</summary>
        public void ReportHit(PlayerHitInfo info) => OnProjectileHit(info);

        /// <summary>
        /// 모래 폭발. Q 스킬 볼트의 착탄점과 폭발 관통탄이 꿰뚫은 점마다 불린다. 반경 안 적대 대상에 볼트 피해 × 비율, 연출·카메라 킥,
        /// 그리고 모래 족쇄(패시브)가 켜져 있으면 같은 자리에서 속박까지 건다.
        /// </summary>
        public void Detonate(Vector3 center)
            => DetonateExplosion(center, 1f);

        void DetonateExplosion(Vector3 center, float visualScale, float launchScale = 1f)
        {
            if (this == null) return; // 볼트가 플레이어보다 오래 살 수 있다
            string faction = Faction;
            float amount = BurstDamage, radius = BurstRadius;
            var seen = new HashSet<IDamageable>();
            var launched = new HashSet<IDisplaceable>();
            int launchCount = 0;
            foreach (var collider in Physics.OverlapSphere(center, radius, skillMask, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(transform)) continue;
                ICombatTarget target = collider.GetComponentInParent<ICombatTarget>();
                if (target != null && (!target.IsTargetable || target.FactionId == faction)) continue;
                if (burstLaunchUp * launchScale > 0f)
                {
                    var displaceable = collider.GetComponentInParent<IDisplaceable>();
                    if (displaceable != null && launched.Add(displaceable))
                    {
                        Vector3 outward = ((Component)displaceable).transform.position - center; outward.y = 0f;
                        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;
                        if (displaceable.Launch((Vector3.up * burstLaunchUp + outward * burstLaunchOut) * launchScale)) launchCount++;
                    }
                }
                IDamageable receiver = target != null ? target.DamageReceiver : collider.GetComponentInParent<IDamageable>();
                if (receiver == null || !seen.Add(receiver)) continue;
                Vector3 point = PlayerSandZone.ClosestPointSafe(collider, center);
                Vector3 direction = point - center; direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
                var result = receiver.TakeDamage(new DamageInfo(amount, faction, causeId: "player.burst", hitPosition: point, hitDirection: direction));
                if (result.WasApplied && result.AppliedDamage > 0f)
                    OnProjectileHit(new PlayerHitInfo(target, receiver, result.AppliedDamage, result.WasKilled, point, direction, "player.burst"));
            }
            if (burstPrefab != null)
            {
                var vfx = PrefabPool.Spawn(burstPrefab, center, Quaternion.identity);
                // 풀이 대여할 때 원본 크기로 되돌리므로 배율이 재사용마다 겹쳐 쌓이지 않는다.
                vfx.transform.localScale *= radius / burstVfxRadius * visualScale; // 관통탄은 모래 폭발의 절반 크기로 연출한다
                PrefabPool.Release(vfx, skillVfxLifetime);
            }
            if (cameraRig != null && burstCameraKick > 0f) cameraRig.Kick(burstCameraKick, burstCameraKickDuration);
            LastLaunchCount = launchCount;
            Burst?.Invoke(center);
            if (launchCount > 0) Launched?.Invoke(center, launchCount);
            if (SandShackle) Shackle(center);
        }

        void Shackle(Vector3 center)
        {
            string faction = Faction;
            float duration = ShackleDuration;
            var seen = new HashSet<IRestrainable>();
            int restrained = 0;
            foreach (var collider in Physics.OverlapSphere(center, ShackleRadius, skillMask, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(transform)) continue;
                IRestrainable restrainable = collider.GetComponentInParent<IRestrainable>();
                if (restrainable == null || !seen.Add(restrainable)) continue;
                ICombatTarget target = collider.GetComponentInParent<ICombatTarget>();
                if (target != null && (!target.IsTargetable || target.FactionId == faction)) continue;
                restrainable.Restrain(duration); restrained++;
            }
            Shackled?.Invoke(center, restrained);
        }
    }
}
