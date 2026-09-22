using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    // [통합 추가 2026-09-21] IFacilityKind를 더했습니다. 기존 필드·메서드는 그대로입니다.
    //   왜: 적은 콜라이더에서 GetComponentInParent<ICombatTarget>()으로 대상을 찾는데, Hit Box 콜라이더가
    //       먼저 잡히면 TowerHitBox가 돌아옵니다. 여기에 종류 ID가 없으면 우두머리의 철거 폭탄처럼
    //       "tower.cobra만 노린다" 같은 조건이 본체를 못 알아보고 그냥 지나칩니다.
    public class TowerHitBox : MonoBehaviour, ICombatTarget, IFacilityKind
    {
        [Header("Tower Body")]
        [SerializeField] private TowerHealth towerHealth;

        [Header("Hitbox")]
        [SerializeField] private Collider hitCollider;

        private readonly List<TowerHealth> candidates = new List<TowerHealth>();
        // 빈 베이스에도 TowerHitBox가 남아 있을 수 있다. 그 경우 Guid.Empty를 적 탐색 후보에
        // 넘기면 TargetCandidate 생성자가 예외를 던지므로, 타겟 불가 상태라도 고유 ID는 유지한다.
        private readonly Guid unboundEntityId = Guid.NewGuid();

        // 코브라 프리팹에서는 Hit Box와 TowerHealth가 형제다. 비활성/사망 중에도
        // 인스펙터에서 연결한 참조는 유지하고, 타게팅 가능 여부만 IsTargetable에서 판단한다.
        private ICombatTarget Target => towerHealth != null ? towerHealth : null;

        Guid ICombatTarget.EntityId => Target != null ? Target.EntityId : unboundEntityId;
        string ICombatTarget.FactionId => Target != null ? Target.FactionId : "Ally";
        CombatTargetKind ICombatTarget.Kind => CombatTargetKind.Tower;

        // [통합 추가 2026-09-21] 종류 ID는 본체가 정한다("tower.cobra" 등). 본체가 없으면 종류를 밝히지 않는다.
        public string DefinitionId => towerHealth != null ? towerHealth.DefinitionId : string.Empty;

        Vector3 ICombatTarget.HitPosition => hitCollider != null ? hitCollider.bounds.center : transform.position;

        bool ICombatTarget.IsTargetable => isActiveAndEnabled
            && hitCollider != null
            && hitCollider.enabled
            && !hitCollider.isTrigger
            && hitCollider.gameObject.activeInHierarchy
            && Target != null
            && Target.IsTargetable;

        IDamageable ICombatTarget.DamageReceiver => Target != null ? Target.DamageReceiver : null;
        ILifeState ICombatTarget.LifeState => Target != null ? Target.LifeState : null;

        private void Awake()
        {
            if (hitCollider == null)
                TryGetComponent(out hitCollider);
        }

        private void OnEnable()
        {
            RefreshTower();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshTower();
        }

        private void LateUpdate()
        {
            if (towerHealth == null)
                RefreshTower();
        }

        private bool IsAvailable(TowerHealth health)
        {
            return health != null
                && ((ICombatTarget)health).IsTargetable;
        }

        private void RefreshTower()
        {
            // 프리팹에 연결한 형제 본체를 자동 탐색이 지우지 않게 한다.
            if (towerHealth != null)
                return;

            towerHealth = null;
            GetComponentsInChildren<TowerHealth>(false, candidates);

            foreach (var candidate in candidates)
            {
                if (!IsAvailable(candidate))
                    continue;

                towerHealth = candidate;

                Transform towerRoot = candidate.transform;

                while (towerRoot.parent != transform)
                    towerRoot = towerRoot.parent;

                towerRoot.localPosition = Vector3.zero;

                break;
            }

            candidates.Clear();
        }

        public void Bind(TowerHealth health)
        {
            towerHealth = health;
        }
    }
}
