using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>
    /// 보스 표식. 살아 있는 동안 <see cref="Active"/>에 들어가 HUD 보스 체력바의 대상이 되고, 처치·제거·비활성화되면 빠진다.
    /// 풀에서 재사용되면 OnEnable에서 다시 등록된다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyBossInfo : MonoBehaviour
    {
        public string displayName = "BOSS";
        public Sprite icon;

        static readonly List<EnemyBossInfo> active = new List<EnemyBossInfo>();
        /// <summary>지금 살아 있는 보스들. 먼저 등장한 순서.</summary>
        public static IReadOnlyList<EnemyBossInfo> Active => active;
        public static event Action ActiveChanged;

        public EnemyHealth Health { get; private set; }

        void Awake() => Health = GetComponent<EnemyHealth>();
        void OnEnable()
        {
            Health.Died += OnGone; Health.Despawned += OnGone;
            Register();
        }
        /// <summary>풀 재사용: 활성화된 뒤에 체력이 되살아나므로 그때 다시 등록한다 (EnemyPool.Revive).</summary>
        public void ResetForReuse() => Register();
        void Register()
        {
            if (Health.IsAlive && isActiveAndEnabled && !active.Contains(this)) { active.Add(this); ActiveChanged?.Invoke(); }
        }
        void OnDisable()
        {
            Health.Died -= OnGone; Health.Despawned -= OnGone;
            Remove();
        }
        void OnGone(DeathInfo _) => Remove();
        void OnGone(Guid _) => Remove();
        void Remove() { if (active.Remove(this)) ActiveChanged?.Invoke(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { active.Clear(); ActiveChanged = null; }
    }
}
