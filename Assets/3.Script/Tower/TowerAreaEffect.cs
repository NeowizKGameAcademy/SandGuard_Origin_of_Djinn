using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public abstract class TowerAreaEffect : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TowerStatus status;
        [SerializeField] private TargetDetector detector;
        [SerializeField] private Collider area;

        [Header("Detection")]
        [SerializeField] private LayerMask targetMask = ~0;

        private Collider[] buffer = new Collider[64];
        private readonly HashSet<Guid> handled = new HashSet<Guid>();
        private float tickTimer;
        private ICombatTarget owner;

        protected TowerStatus Status => status;
        protected Vector3 Origin => detector != null ? detector.transform.position : transform.position;
        protected ICombatTarget Owner => owner is Component component && component != null ? owner : null;
        protected string Faction => Owner != null ? Owner.FactionId : "Ally";
        protected abstract float TickInterval { get; }
        protected abstract bool HasConfig { get; }

        protected virtual void Awake()
        {
            if (status == null)
                status = GetComponentInParent<TowerStatus>();

            if (detector == null)
                detector = GetComponentInParent<TargetDetector>();

            if (area == null)
                TryGetComponent(out area);

            owner = GetComponentInParent<ICombatTarget>();
        }

        protected virtual void OnEnable()
        {
            tickTimer = 0f;
        }

        protected virtual void OnDisable()
        {
            tickTimer = 0f;
            handled.Clear();
            Array.Clear(buffer, 0, buffer.Length);
        }

        private void Update()
        {
            if (status == null || !HasConfig || area == null || !area.enabled || !area.gameObject.activeInHierarchy || detector == null || !detector.isActiveAndEnabled)
                return;

            if (Owner != null && !Owner.IsTargetable)
                return;

            tickTimer -= Time.deltaTime;

            if (tickTimer > 0f)
                return;

            float interval = TickInterval;

            if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0f)
                return;

            tickTimer = interval;
            int count;

            while (true)
            {
                count = OverlapArea();

                if (count < buffer.Length)
                    break;

                Array.Resize(ref buffer, buffer.Length * 2);
            }

            handled.Clear();

            for (int i = 0; i < count; i++)
            {
                if (!isActiveAndEnabled || !area.enabled || !detector.isActiveAndEnabled || (Owner != null && !Owner.IsTargetable))
                    break;

                Collider other = buffer[i];

                if (!detector.Contains(other))
                    continue;

                var target = other.GetComponentInParent<ICombatTarget>();

                if (!detector.IsValidTarget(target) || !handled.Add(target.EntityId))
                    continue;

                ApplyEffect(target, other, interval);
            }

            Array.Clear(buffer, 0, count);
        }

        protected abstract void ApplyEffect(ICombatTarget target, Collider other, float interval);

        private int OverlapArea()
        {
            Transform areaTransform = area.transform;
            Vector3 scale = areaTransform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            switch (area)
            {
                case BoxCollider box:
                    return Physics.OverlapBoxNonAlloc(areaTransform.TransformPoint(box.center), Vector3.Scale(box.size * 0.5f, scale), buffer, areaTransform.rotation, targetMask, QueryTriggerInteraction.Ignore);

                case SphereCollider sphere:
                    float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    return Physics.OverlapSphereNonAlloc(areaTransform.TransformPoint(sphere.center), radius, buffer, targetMask, QueryTriggerInteraction.Ignore);

                default:
                    return Physics.OverlapBoxNonAlloc(area.bounds.center, area.bounds.extents,buffer, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);
            }
        }
    }
}
