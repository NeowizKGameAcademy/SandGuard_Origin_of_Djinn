using System;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>지어진 시설 하나. 체력·수리·철거는 아직 없고 표시 정보만 낸다.</summary>
    public sealed class FacilityInstance : MonoBehaviour
    {
        public string definitionId;
        public string slotId;
        public float maxHealth = 150f;
        public int maxUpgradeLevel = 3;
        public Guid EntityId { get; private set; }

        internal void Initialize(Guid entityId, FacilityDefinition definition, string buildSlotId)
        {
            EntityId = entityId; definitionId = definition.id; slotId = buildSlotId;
            maxHealth = definition.maxHealth; maxUpgradeLevel = definition.maxUpgradeLevel;
        }

        public FacilityViewData ViewData => new FacilityViewData(EntityId, definitionId, maxHealth, maxHealth, 0, maxUpgradeLevel, PlacementKind.DesignatedSlot, slotId);
    }
}
