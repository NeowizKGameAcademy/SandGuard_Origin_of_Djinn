using System;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>방패를 든 적의 정면 방어. 몸 정면 frontHalfAngle 안에서 날아온 공격은 피해에 frontMultiplier를 곱한다(0이면 막는다).</summary>
    /// <remarks>
    /// 방향은 <see cref="DamageInfo.HitDirection"/>(공격이 날아가는 방향)으로 판단한다. 방향이 없는 피해(낙사 등)는 막지 않는다.
    /// 폭발·폭풍은 폭발 중심에서 적으로 향하는 방향이므로, 중심이 적 정면에 있으면 막힌다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyShield : MonoBehaviour, IDamageModifier
    {
        [Range(0f, 180f), Tooltip("정면으로 보는 반각(도). 60이면 앞쪽 120도")]
        public float frontHalfAngle = 60f;
        [Range(0f, 1f), Tooltip("정면 피해 배율. 0이면 완전히 막고, 0.5면 절반만 받는다")]
        public float frontMultiplier = 0f;
        [Min(1f), Tooltip("방패가 흡수할 수 있는 피해량")]
        public float maxHealth = 30f;
        [Tooltip("떨어뜨릴 방패 외형. 비어 있으면 TowerShield_Placement를 찾는다")]
        public Transform shieldVisual;
        [Min(0.1f)] public float debrisLifetime = 6f;
        [Tooltip("정면으로 받았을 때의 연출(VFX_Shield_Front_Guard). +Z가 공격해 온 쪽을 향한다")]
        public GameObject guardVfx;
        [Min(0.1f)] public float guardVfxLifetime = 1f;

        public int GuardCount { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsBroken => CurrentHealth <= 0f;
        GameObject debris;

        void Awake() => ResetForReuse();

        public void ResetForReuse()
        {
            CurrentHealth = Mathf.Max(1f, maxHealth);
            GuardCount = 0;
            if (shieldVisual != null) shieldVisual.gameObject.SetActive(true);
            ClearDebris();
        }

        void OnDisable() => ClearDebris();

        void ClearDebris()
        {
            // 수명이 다해 이미 풀로 돌아간 파편은 그대로 둔다. 두 번 돌려보내면 다음에 빌려 간 쪽의 것을 거둔다.
            if (debris != null && (!debris.TryGetComponent(out PooledInstance pooled) || !pooled.IsIdle))
                PrefabPool.Release(debris);
            debris = null;
        }

        void DropShield(Vector3 direction)
        {
            if (shieldVisual == null)
                foreach (var child in GetComponentsInChildren<Transform>(true))
                    if (child.name == "TowerShield_Placement") { shieldVisual = child; break; }
            if (shieldVisual == null) return;

            // Keep the attached original for pool reuse; its world-space copy becomes debris.
            // 풀이 방패 외형을 원본 삼아 파편을 만들고 돌려 쓴다. 같은 적이 다시 나와도 같은 파편을 꺼내 쓴다.
            debris = PrefabPool.Spawn(shieldVisual.gameObject, shieldVisual.position, shieldVisual.rotation);
            debris.name = "BrokenEnemyShield";
            debris.transform.localScale = shieldVisual.lossyScale;
            shieldVisual.gameObject.SetActive(false);
            foreach (var child in debris.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2; // Ignore Raycast
            foreach (var collider in debris.GetComponentsInChildren<Collider>()) collider.enabled = false;
            // 재사용한 파편에는 지난번 상자·물체가 그대로 붙어 있다. 새로 붙이지 않고 값만 다시 잡는다.
            if (!debris.TryGetComponent(out BoxCollider box)) box = debris.AddComponent<BoxCollider>();
            box.enabled = true;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var renderer in debris.GetComponentsInChildren<Renderer>())
            {
                Bounds local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = debris.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            box.center = bounds.center;
            box.size = first ? new Vector3(.6f, 1f, .1f) : Vector3.Max(bounds.size, Vector3.one * .02f);
            if (!debris.TryGetComponent(out Rigidbody body)) body = debris.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.useGravity = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = direction.normalized * 1.5f + Vector3.up * .5f;
            body.angularVelocity = transform.right * 3f;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(box, collider);
            PrefabPool.Release(debris, Mathf.Max(.1f, debrisLifetime));
        }
        /// <summary>모델이 EnemyScaleBuilder로 커진 배율. 사람 크기 기준으로 만든 연출을 몸에 맞출 때 쓴다.</summary>
        float BodyScale { get { var visuals = GetComponent<EnemyVisuals>(); return visuals ? visuals.BodyScale : 1f; } }
        /// <summary>정면 공격을 방패로 받았다(막았거나 줄였다).</summary>
        public event Action<DamageInfo> Guarded;

        /// <summary>공격이 날아가는 방향이 이 적의 정면에서 온 것인지. 높낮이는 무시한다.</summary>
        public bool IsFrontal(Vector3 hitDirection)
        {
            Vector3 incoming = -hitDirection; incoming.y = 0f;
            Vector3 forward = transform.forward; forward.y = 0f;
            if (incoming.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(forward, incoming) <= frontHalfAngle;
        }

        public float ModifyIncoming(DamageInfo damage, float amount)
        {
            if (IsBroken || amount <= 0f || !damage.HitDirection.HasValue || !IsFrontal(damage.HitDirection.Value)) return amount;
            float absorbed = Mathf.Min(CurrentHealth, amount * (1f - Mathf.Clamp01(frontMultiplier)));
            if (absorbed <= 0f) return amount;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - absorbed);
            if (IsBroken) DropShield(damage.HitDirection.Value);
            GuardCount++;
            if (guardVfx != null)
            {
                Vector3 point = damage.HitPosition ?? transform.position + Vector3.up;
                Vector3 outward = -damage.HitDirection.Value;
                var effect = PrefabPool.Spawn(guardVfx, point, Quaternion.LookRotation(outward.normalized));
                // 연출은 사람 크기 방패 기준으로 만들어졌다. 모델 스케일만큼 키워 손에 든 방패와 맞춘다. 풀 재사용이라 매번 설정한다.
                effect.transform.localScale = guardVfx.transform.localScale * BodyScale;
                PrefabPool.Release(effect, guardVfxLifetime);
            }
            Guarded?.Invoke(damage);
            return amount - absorbed;
        }
    }
}
