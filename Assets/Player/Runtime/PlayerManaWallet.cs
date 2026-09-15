using System;
using UnityEngine;

namespace SandGuard.Player
{
    public sealed class PlayerManaWallet : MonoBehaviour, IManaWallet
    {
        [SerializeField, Min(0)] int maxMana = 100;
        [SerializeField, Min(0)] int startingMana = 100;
        public int CurrentMana { get; private set; }
        [Tooltip("스탯 수정자. MaxMana 수정자(레벨 성장)를 인스펙터 최대 마나에 얹는다. 비우면 같은 오브젝트에서 찾는다")]
        public PlayerStats stats;
        /// <summary>인스펙터 기본값에 <see cref="PlayerStat.MaxMana"/> 수정자를 얹은 최대 마나.</summary>
        public int MaxMana => stats != null ? stats.EvaluateCount(PlayerStat.MaxMana, maxMana) : maxMana;
        int reserved, generation, knownMaxMana;
        public event Action<ManaChangedInfo> Changed;
        void Awake() { if (stats == null) stats = GetComponent<PlayerStats>(); knownMaxMana = MaxMana; ResetWallet(); }
        void OnEnable() { if (stats != null) { stats.Changed += OnStatChanged; OnStatChanged(PlayerStat.MaxMana); } }
        void OnDisable() { if (stats != null) stats.Changed -= OnStatChanged; }
        void OnValidate() { maxMana = Mathf.Max(0, maxMana); startingMana = Mathf.Clamp(startingMana, 0, maxMana); }
        void OnDestroy() { generation++; reserved = 0; }

        public void ResetWallet()
        {
            int previous = CurrentMana;
            generation++; reserved = 0;
            CurrentMana = Mathf.Clamp(startingMana, 0, MaxMana);
            Notify(previous);
        }
        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > CurrentMana - reserved) return false;
            int previous = CurrentMana; CurrentMana -= amount; Notify(previous); return true;
        }
        public bool TryReserve(int amount, out IManaReservation reservation)
        {
            reservation = null;
            if (amount < 0 || amount > CurrentMana - reserved) return false;
            reserved += amount;
            reservation = new Reservation(this, amount, generation);
            return true;
        }
        public int Gain(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int previous = CurrentMana;
            int applied = Mathf.Min(amount, MaxMana - CurrentMana);
            CurrentMana += applied; Notify(previous); return applied;
        }
        void Notify(int previous)
        { if (previous != CurrentMana) Changed?.Invoke(new ManaChangedInfo(previous, CurrentMana, MaxMana)); }
        // 최대 마나가 오르면 현재 마나는 그대로 두고(회복은 레벨업 설정이 맡는다), 내려가면 새 최대치로 자른다. 막대가 다시 그려지도록 최대치만 바뀌어도 알린다.
        void OnStatChanged(PlayerStat stat)
        {
            if (stat != PlayerStat.MaxMana || knownMaxMana == MaxMana) return;
            knownMaxMana = MaxMana;
            int previous = CurrentMana;
            CurrentMana = Mathf.Min(CurrentMana, MaxMana);
            Changed?.Invoke(new ManaChangedInfo(previous, CurrentMana, MaxMana));
        }

        sealed class Reservation : IManaReservation
        {
            readonly PlayerManaWallet wallet;
            readonly int amount, generation;
            bool finished;
            public Reservation(PlayerManaWallet wallet, int amount, int generation)
            { this.wallet = wallet; this.amount = amount; this.generation = generation; }
            public bool TryCommit()
            {
                if (finished || wallet == null || generation != wallet.generation) return false;
                finished = true;
                int previous = wallet.CurrentMana;
                wallet.reserved -= amount; wallet.CurrentMana -= amount;
                wallet.Notify(previous); return true;
            }
            public void Dispose()
            {
                if (finished) return;
                finished = true;
                if (wallet != null && generation == wallet.generation) wallet.reserved -= amount;
            }
        }
    }
}
