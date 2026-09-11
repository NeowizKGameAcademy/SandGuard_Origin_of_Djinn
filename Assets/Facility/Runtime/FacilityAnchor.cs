using DesertTower.Levels;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>레벨 슬롯 위에 놓인 받침대. 플레이어가 다가오면 건설 메뉴가 뜨고, 시설은 이 아래에 생성된다.</summary>
    /// <remarks>받침대와 시설은 같은 좌표계로 만들어졌으므로 둘 다 이 트랜스폼(스케일 포함) 아래 원점에 둔다.</remarks>
    public sealed class FacilityAnchor : MonoBehaviour
    {
        [Tooltip("비우면 부모에서 찾는다")]
        public LevelBuildSlot slot;
        [Min(0.5f)] public float interactionRadius = 3.5f;
        [Tooltip("메뉴가 뜨는 높이(월드 단위). 받침대 위쪽")]
        public float menuHeight = 1.2f;
        public FacilityInstance Occupant { get; internal set; }
        public bool IsOccupied => Occupant != null;
        public string SlotId => slot != null ? slot.id : null;

        void Awake() { if (slot == null) slot = GetComponentInParent<LevelBuildSlot>(); }
        void Start() { FindFirstObjectByType<FacilityBuildService>()?.Register(this); }
    }
}
