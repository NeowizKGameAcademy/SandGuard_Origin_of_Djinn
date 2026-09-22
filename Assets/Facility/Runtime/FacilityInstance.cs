using System;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>지어진 시설 하나. 체력은 시설 아래 어딘가의 체력 컴포넌트가 맡는다. 강화·철거는 아직 없다.</summary>
    /// <remarks>
    /// 체력은 구체 타입이 아니라 인터페이스로 잡는다. 시설 팀의 <see cref="FacilityHealth"/>와 타워 팀의
    /// Tower.TowerHealth가 서로 다른 어셈블리에 있고(asmdef는 Assembly-CSharp를 참조할 수 없다),
    /// 둘의 공용 어휘는 DesertTower.Gameplay.Abstractions의 인터페이스뿐이기 때문이다.
    /// 타워 팀 프리팹은 체력이 루트가 아니라 자식(Tower (Cobra) 등)에 있으므로 자식까지 찾는다.
    /// </remarks>
    public sealed class FacilityInstance : MonoBehaviour
    {
        public string definitionId;
        public string slotId;
        public float maxHealth = 150f;
        public int maxUpgradeLevel = 3;
        public Guid EntityId { get; private set; }
        /// <summary>현재·최대 체력. 체력 컴포넌트가 없으면 null이다.</summary>
        public IHealth Health { get; private set; }
        /// <summary>생존 상태와 파괴·제거 알림. 체력 컴포넌트가 ILifeState도 구현할 때만 채워진다.</summary>
        public ILifeState Life { get; private set; }
        /// <summary>수리 수단. 수리를 지원하지 않는 시설은 null이다.</summary>
        public IRepairable Repairable { get; private set; }

        void Awake() { if (Health == null) Bind(); }

        internal void Initialize(Guid entityId, FacilityDefinition definition, string buildSlotId)
        {
            EntityId = entityId; definitionId = definition.id; slotId = buildSlotId;
            maxHealth = definition.maxHealth; maxUpgradeLevel = definition.maxUpgradeLevel;
            Bind();
            // 공격 대상 ID를 시설 ID와 같게 맞춰 점유 기록·표시 정보·전투가 한 ID로 이어지게 한다.
            // 맞출 수 없는 남의 체력 컴포넌트(타워 팀)라면 반대로 그쪽 ID를 따라가 한 값으로 유지한다.
            if (Health is FacilityHealth own) own.Configure(entityId, definition.id, definition.maxHealth);
            else if (Health is ICombatTarget target && target.EntityId != Guid.Empty) EntityId = target.EntityId;
        }

        void Bind()
        {
            Health = GetComponentInChildren<IHealth>(true);
            Life = Health as ILifeState;
            Repairable = Health as IRepairable;
        }

        public FacilityViewData ViewData => new FacilityViewData(EntityId, definitionId,
            Health != null ? Health.CurrentHealth : maxHealth, Health != null ? Health.MaxHealth : maxHealth,
            0, maxUpgradeLevel, PlacementKind.DesignatedSlot, slotId);
    }
}
