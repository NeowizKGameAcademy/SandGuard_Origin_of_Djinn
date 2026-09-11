using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Waves
{
    /// <summary>레벨 요소의 gameKey를 실제 적 프리팹으로 푼다. 웨이브 에셋은 프리팹을 모르고 키만 든다.</summary>
    [CreateAssetMenu(menuName = "SandGuard/Enemy Catalog", fileName = "EnemyCatalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("LevelElementDefinition.gameKey와 같은 값")]
            public string gameKey;
            public string displayName;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();

        public bool TryGet(string gameKey, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(gameKey)) return false;
            foreach (var entry in entries)
                if (entry != null && entry.gameKey == gameKey && entry.prefab != null) { prefab = entry.prefab; return true; }
            return false;
        }
    }
}
