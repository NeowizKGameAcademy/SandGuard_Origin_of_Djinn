using System;
using DesertTower.Levels;
using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    public enum PrefabRole { Enemy, Player, Core, Facility }
    [Serializable] public sealed class PrefabEntry
    {
        public string key;
        [Tooltip("웨이브 편집기에서 사람이 보는 이름. 비우면 key를 쓴다.")]
        public string displayName;
        public PrefabRole role;
        public GameObject prefab;
    }
    [CreateAssetMenu(menuName = "Desert Tower/Integration/Prefab Catalog")]
    public sealed class PrefabCatalog : LevelElementCatalog
    {
        public List<PrefabEntry> entries = new List<PrefabEntry>();

        // 웨이브에 넣을 수 있는 건 적뿐이다. 플레이어·코어·시설은 목록에 내지 않는다.
        public override void CollectKeys(List<string> keys)
        {
            keys.Clear();
            foreach (var e in entries)
                if (e != null && e.role == PrefabRole.Enemy && !string.IsNullOrWhiteSpace(e.key)) keys.Add(e.key);
        }
        public override string DisplayNameFor(string key)
        {
            var entry = Find(key);
            return entry == null || string.IsNullOrWhiteSpace(entry.displayName) ? key : entry.displayName;
        }
        public PrefabEntry Find(string key) => entries.Find(e => e != null && e.key == key);
        public List<string> ValidateCatalog()
        {
            var errors = new List<string>(); var keys = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key) || !keys.Add(e.key)) { errors.Add("프리팹 키 누락/중복"); continue; }
                if (!e.prefab) { errors.Add(e.key + ": 프리팹 누락"); continue; }
                var bridge = e.prefab.GetComponent<ActorBridge>();
                if (e.role == PrefabRole.Enemy && !bridge) { errors.Add(e.key + ": 루트에 ActorBridge 필요"); continue; }
                if (bridge && (bridge.coreDamage < 0 || float.IsNaN(bridge.coreDamage) || float.IsInfinity(bridge.coreDamage)))
                    errors.Add(e.key + ": 코어 피해 오류");
            }
            return errors;
        }
    }
}
