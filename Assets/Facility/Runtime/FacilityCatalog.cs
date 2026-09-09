using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>건설 메뉴 한 칸. 메뉴의 번호는 카탈로그 순서다.</summary>
    [Serializable]
    public sealed class FacilityDefinition
    {
        [Tooltip("PlacementRequest와 슬롯 허용 목록에 쓰는 시설 종류 ID")]
        public string id = "cobra";
        public string displayName = "화염 코브라";
        [Tooltip("슬롯 위에 생성되는 프리팹. 루트 스케일 1, 바닥 원점, 정면 +Z")]
        public GameObject prefab;
        public Sprite icon;
        [Min(0)] public int manaCost = 30;
        [Min(1f)] public float maxHealth = 150f;
        [Min(0)] public int maxUpgradeLevel = 3;
    }

    /// <summary>지을 수 있는 시설 목록. 씬마다 다른 목록을 쓰려면 에셋을 복제한다.</summary>
    [CreateAssetMenu(menuName = "SandGuard/Facility Catalog", fileName = "FacilityCatalog")]
    public sealed class FacilityCatalog : ScriptableObject
    {
        public List<FacilityDefinition> facilities = new List<FacilityDefinition>();

        public FacilityDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var definition in facilities) if (definition != null && definition.id == id) return definition;
            return null;
        }
    }
}
