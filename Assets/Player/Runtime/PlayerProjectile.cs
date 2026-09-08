using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>루트가 이동/명중을 담당한다. VisualRoot 자식만 교체하면 외형을 바꿀 수 있다.</summary>
    public sealed class PlayerProjectile : MonoBehaviour
    {
        [Min(0.1f)] public float speed = 28f;
        [Min(0.001f)] public float radius = 0.08f;
        [Min(0.01f)] public float lifetime = 4f;
        public LayerMask hitMask = ~0;
        [Tooltip("자체 종료하는 효과 또는 아래 수명으로 정리할 효과")]
        public GameObject impactPrefab;
        [Min(0.1f)] public float impactLifetime = 2f;
        Transform owner;
        string faction;
        float damage, age;
        Vector3 direction;
        bool launched, consumed;

        public void Launch(Transform source, string sourceFaction, float amount, Vector3 heading, Vector3 sourceOrigin)
        {
            owner = source; faction = sourceFaction; damage = amount; direction = heading.normalized;
            launched = true;
            // 총구가 벽 내부/반대편으로 들어가도 몸통에서 총구까지 먼저 검사한다.
            Sweep(sourceOrigin, transform.position);
        }

        void Update()
        {
            if (!launched || consumed || Time.deltaTime <= 0f) return;
            age += Time.deltaTime;
            if (age >= lifetime) { Consume(transform.position, false); return; }
            Vector3 next = transform.position + direction * (speed * Time.deltaTime);
            if (!Sweep(transform.position, next)) transform.position = next;
        }

        bool Ignore(Collider collider) => collider.transform.IsChildOf(transform)
            || (owner != null && collider.transform.IsChildOf(owner));

        bool Sweep(Vector3 from, Vector3 to)
        {
            foreach (Collider collider in Physics.OverlapSphere(from, radius, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (Ignore(collider)) continue;
                ApplyHit(collider, collider.ClosestPoint(from)); return true;
            }
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return false;
            RaycastHit? nearest = null;
            foreach (var hit in Physics.SphereCastAll(from, radius, delta.normalized, delta.magnitude, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (Ignore(hit.collider)) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
            }
            if (!nearest.HasValue) return false;
            ApplyHit(nearest.Value.collider, nearest.Value.point); return true;
        }

        void ApplyHit(Collider collider, Vector3 point)
        {
            if (consumed) return;
            consumed = true; // 알림에서 재진입해도 두 번 피해를 주지 않는다.
            IDamageable receiver = collider.GetComponentInParent<IDamageable>();
            ICombatTarget target = collider.GetComponentInParent<ICombatTarget>();
            if (target != null) receiver = target.IsTargetable && target.FactionId != faction ? target.DamageReceiver : null;
            if (receiver != null)
            {
                var result = receiver.TakeDamage(new DamageInfo(damage, faction,
                    causeId: "player.basic", hitPosition: point, hitDirection: direction));
                if (result.Status == DamageStatus.InvalidRequest) Debug.LogWarning("기본 투사체의 피해 요청이 거부되었습니다.", this);
            }
            Finish(point, true);
        }
        void Consume(Vector3 point, bool impact) { if (consumed) return; consumed = true; Finish(point, impact); }
        void Finish(Vector3 point, bool impact)
        {
            if (impact && impactPrefab != null) Destroy(Instantiate(impactPrefab, point, Quaternion.identity), impactLifetime);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
