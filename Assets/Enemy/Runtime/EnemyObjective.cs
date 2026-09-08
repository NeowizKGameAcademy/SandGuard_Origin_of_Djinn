using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>적이 향하는 목표(코어)에 붙인다. EnemyBrain의 objective가 비어 있으면 이것을 찾는다.</summary>
    public sealed class EnemyObjective : MonoBehaviour
    {
        static readonly List<EnemyObjective> active = new List<EnemyObjective>();
        /// <summary>씬에 켜져 있는 첫 목표. 없으면 null이다.</summary>
        public static EnemyObjective Current => active.Count > 0 ? active[0] : null;
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);
    }
}
