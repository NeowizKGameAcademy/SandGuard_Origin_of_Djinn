using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    public enum PrefabRole { Enemy, Player, Core, Facility }
    [Serializable] public sealed class PrefabEntry
    {
        public string key;
        public PrefabRole role;
        public GameObject prefab;
        [Min(0)] public float coreDamage = 10;
    }
    [CreateAssetMenu(menuName = "Desert Tower/Integration/Prefab Catalog")]
    public sealed class PrefabCatalog : ScriptableObject
    {
        public List<PrefabEntry> entries = new List<PrefabEntry>();
        public PrefabEntry Find(string key) => entries.Find(e => e != null && e.key == key);
        public List<string> ValidateCatalog()
        {
            var errors = new List<string>(); var keys = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key) || !keys.Add(e.key)) { errors.Add("프리팹 키 누락/중복"); continue; }
                if (!e.prefab) { errors.Add(e.key + ": 프리팹 누락"); continue; }
                if (e.role == PrefabRole.Enemy && !e.prefab.GetComponent<ActorBridge>()) errors.Add(e.key + ": 루트에 ActorBridge 필요");
                if (e.coreDamage < 0 || float.IsNaN(e.coreDamage) || float.IsInfinity(e.coreDamage)) errors.Add(e.key + ": 코어 피해 오류");
            }
            return errors;
        }
    }
}
