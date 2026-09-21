using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class TargetDetector : MonoBehaviour
    {
        [Header("Status")]
        [SerializeField] private TowerStatus status;

        [Header("Detection")]
        [Tooltip("TowerStatus가 없을 때 사용하는 탐지 반경")]
        [Min(0f)] [SerializeField] private float detectionRange = 25f;
        [SerializeField] private LayerMask targetMask = ~0;

        private Collider[] buffer = new Collider[64];
        private readonly List<ICombatTarget> targets = new List<ICombatTarget>();
        private readonly HashSet<Guid> detectedIds = new HashSet<Guid>();
        private ICombatTarget owner;

        public IReadOnlyList<ICombatTarget> Targets => targets;
        public int DetectCount => targets.Count;
        public bool IsDetecting => DetectCount > 0;
        public float Range
        {
            get
            {
                float range = status != null ? status.detectRange : detectionRange;

                return !float.IsNaN(range) && !float.IsInfinity(range) ? Mathf.Max(0f, range) : 0f;
            }
        }

        private void Awake()
        {
            status = GetComponentInParent<TowerStatus>();

            owner = GetComponentInParent<ICombatTarget>();
        }

        private void Update()
        {
            Scan();
        }

        private void OnDisable()
        {
            targets.Clear();
            detectedIds.Clear();
            Array.Clear(buffer, 0, buffer.Length);
        }

        public void Scan()
        {
            targets.Clear();
            detectedIds.Clear();

            if (!isActiveAndEnabled || Range <= 0f)
                return;

            int count;

            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(transform.position, Range, buffer,
                    targetMask, QueryTriggerInteraction.Ignore);

                if (count < buffer.Length)
                    break;

                Array.Resize(ref buffer, buffer.Length * 2);
            }

            for (int i = 0; i < count; i++)
            {
                var candidate = buffer[i].GetComponentInParent<ICombatTarget>();

                if (!IsValidTarget(candidate) || !detectedIds.Add(candidate.EntityId))
                    continue;

                targets.Add(candidate);
            }

            Array.Clear(buffer, 0, count);
        }

        public bool IsValidTarget(ICombatTarget candidate)
        {
            if (!(candidate is Component component) || component == null)
                return false;

            string faction = owner is Component ownerComponent && ownerComponent != null
                ? owner.FactionId : "Ally";

            return component.gameObject.activeInHierarchy
                && candidate.IsTargetable
                && candidate.FactionId != faction;
        }

        public bool Contains(ICombatTarget candidate)
        {
            return isActiveAndEnabled && IsValidTarget(candidate) && detectedIds.Contains(candidate.EntityId);
        }

        public bool Contains(Collider other)
        {
            return other != null && other.enabled && other.gameObject.activeInHierarchy
                && (other.ClosestPoint(transform.position) - transform.position).sqrMagnitude <= Range * Range;
        }

        public bool Contains(Vector3 position, float range)
        {
            return range >= 0f && (position - transform.position).sqrMagnitude <= range * range;
        }
    }
}
