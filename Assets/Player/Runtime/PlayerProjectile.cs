using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>루트가 이동/명중을 담당한다. VisualRoot 자식만 교체하면 외형을 바꿀 수 있다.</summary>
    /// <remarks>
    /// <see cref="PrefabPool"/>에서 대여·반납된다. 명중하면 그 자리에 멈춘 채 잔상만 마저 재생하고
    /// <see cref="visualTailLifetime"/> 뒤에 풀로 돌아간다. 그동안 다음 발사는 풀의 다른 개체를 쓴다.
    /// </remarks>
    public sealed class PlayerProjectile : MonoBehaviour, IPoolable
    {
        [Min(0.1f)] public float speed = 28f;
        [Min(0.001f)] public float radius = 0.08f;
        [Min(0.01f)] public float lifetime = 4f;
        public LayerMask hitMask = ~0;
        [Tooltip("자체 종료하는 효과 또는 아래 수명으로 정리할 효과")]
        public GameObject impactPrefab;
        [Min(0.1f)] public float impactLifetime = 2f;
        [Tooltip("명중 후 새 방출을 멈추고 남아 있는 월드 공간 잔상만 마저 재생한다")]
        public Transform visualRoot;
        [Min(0f)] public float visualTailLifetime = 0.6f;
        Transform owner;
        string faction;
        float damage, age;
        Vector3 direction;
        bool launched, consumed;
        float defaultSpeed;
        Vector3 defaultVisualScale;
        bool defaultsCaptured;
        /// <summary>아직 날아가는 중인지. 명중 후 잔상만 남은 개체는 false다.</summary>
        public bool IsLive => launched && !consumed;
        /// <summary>실제로 피해가 적용된 명중. Launch 전에 구독해야 총구 앞 즉시 명중도 받는다.</summary>
        public event System.Action<PlayerHitInfo> Hit;
        /// <summary>무언가에 닿아 끝났을 때의 착탄점(적·벽 모두). 수명이 다해 사라질 때는 오지 않는다. 광역 스킬(폭발·족쇄)이 여기서 터진다.</summary>
        public event System.Action<Vector3> Impacted;

        void Awake() => CaptureDefaults();

        void CaptureDefaults()
        {
            if (defaultsCaptured) return;
            defaultSpeed = speed;
            defaultVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            defaultsCaptured = true;
        }

        /// <summary>풀에서 나올 때: 지난 발사의 구독·수명·배율을 전부 지우고 잔상을 되살린다.</summary>
        void IPoolable.OnRent()
        {
            CaptureDefaults();
            Hit = null; Impacted = null;
            owner = null; faction = null; damage = 0f; age = 0f;
            launched = false; consumed = false;
            speed = defaultSpeed; // 스탯 배율이 재사용마다 겹쳐 쌓이지 않게 기본값에서 다시 시작한다
            if (visualRoot == null) return;
            visualRoot.localScale = defaultVisualScale;
            foreach (var mesh in visualRoot.GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = true;
            foreach (var light in visualRoot.GetComponentsInChildren<Light>(true)) light.enabled = true;
        }

        void IPoolable.OnReturn() { Hit = null; Impacted = null; }

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
                if (result.WasApplied && result.AppliedDamage > 0f)
                    Hit?.Invoke(new PlayerHitInfo(target, receiver, result.AppliedDamage, result.WasKilled, point, direction, "player.basic"));
            }
            Finish(point, true);
        }
        void Consume(Vector3 point, bool impact) { if (consumed) return; consumed = true; Finish(point, impact); }
        void Finish(Vector3 point, bool impact)
        {
            if (impact && impactPrefab != null)
                PrefabPool.Release(PrefabPool.Spawn(impactPrefab, point, Quaternion.identity), impactLifetime);
            if (impact) Impacted?.Invoke(point);
            // 명중 뒤에는 Update가 멈춰 제자리에 서 있으므로 잔상을 떼어내지 않아도 그 자리에서 마저 재생된다.
            if (visualRoot != null)
            {
                foreach (var system in visualRoot.GetComponentsInChildren<ParticleSystem>(true))
                    system.Stop(false, system.main.simulationSpace == ParticleSystemSimulationSpace.World
                        ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var mesh in visualRoot.GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
                foreach (var light in visualRoot.GetComponentsInChildren<Light>(true)) light.enabled = false;
            }
            // 잔상이 사라진 뒤 풀로 돌아간다. 그동안의 발사는 풀의 다른 개체가 맡는다.
            PrefabPool.Release(gameObject, Mathf.Max(0f, visualTailLifetime));
        }
    }
}
