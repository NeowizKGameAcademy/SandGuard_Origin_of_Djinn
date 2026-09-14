using DesertTower.Levels;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>레벨 슬롯 위에 놓인 받침대. 플레이어가 다가오면 건설 메뉴가 뜨고, 시설은 이 아래에 생성된다.</summary>
    /// <remarks>
    /// 받침대와 시설은 같은 좌표계로 만들어졌으므로 둘 다 이 트랜스폼(스케일 포함) 아래 원점에 둔다.
    /// 타워 담당자의 받침(Tower Base)은 사거리 원이 본체의 DetectRange를 찾는다. 본체가 없을 때 켜 두면 오류가 나므로
    /// <see cref="onlyWhenOccupied"/>에 넣어 두면 시설이 있을 때만 켠다(처음 켜질 때 본체를 찾는다).
    /// </remarks>
    public sealed class FacilityAnchor : MonoBehaviour
    {
        [Tooltip("비우면 부모에서 찾는다")]
        public LevelBuildSlot slot;
        [Min(0.5f)] public float interactionRadius = 3.5f;
        [Tooltip("메뉴가 뜨는 높이(월드 단위). 받침대 위쪽")]
        public float menuHeight = 1.2f;
        [Tooltip("시설이 있을 때만 켜는 오브젝트(받침의 사거리 원 등)")]
        public GameObject[] onlyWhenOccupied = new GameObject[0];

        FacilityInstance occupant;
        public FacilityInstance Occupant
        {
            get => occupant;
            internal set { occupant = value; RefreshOccupiedVisuals(); }
        }
        public bool IsOccupied => Occupant != null;
        public string SlotId => slot != null ? slot.id : null;

        void Awake()
        {
            if (slot == null) slot = GetComponentInParent<LevelBuildSlot>();
            RefreshOccupiedVisuals();
        }
        void Start() { FindFirstObjectByType<FacilityBuildService>()?.Register(this); }

        void RefreshOccupiedVisuals()
        {
            foreach (var go in onlyWhenOccupied) if (go != null) go.SetActive(occupant != null);
        }
    }
}
