using System;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>지어진 시설 하나. 체력은 같은 오브젝트의 <see cref="FacilityHealth"/>가 맡는다. 수리·강화·철거는 아직 없다.</summary>
    public sealed class FacilityInstance : MonoBehaviour
    {
        public string definitionId;
        public string slotId;
        public float maxHealth = 150f;
        public int maxUpgradeLevel = 3;
        public Guid EntityId { get; private set; }
        public FacilityHealth Health { get; private set; }

        internal void Initialize(Guid entityId, FacilityDefinition definition, string buildSlotId)
        {
            EntityId = entityId; definitionId = definition.id; slotId = buildSlotId;
            maxHealth = definition.maxHealth; maxUpgradeLevel = definition.maxUpgradeLevel;
            // 공격 대상 ID를 시설 ID와 같게 맞춰 점유 기록·표시 정보·전투가 한 ID로 이어지게 한다.
            Health = GetComponent<FacilityHealth>();
            if (Health != null) Health.Configure(entityId, definition.id, definition.maxHealth);
        }

        public FacilityViewData ViewData => new FacilityViewData(EntityId, definitionId,
            Health != null ? Health.CurrentHealth : maxHealth, maxHealth, 0, maxUpgradeLevel, PlacementKind.DesignatedSlot, slotId);
    }
}
