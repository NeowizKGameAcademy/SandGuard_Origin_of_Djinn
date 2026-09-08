using System;
using UnityEngine;

namespace SandGuard.Player
{
    public sealed class PlayerManaWallet : MonoBehaviour, IManaWallet
    {
        [SerializeField, Min(0)] int maxMana = 100;
        [SerializeField, Min(0)] int startingMana = 100;
        public int CurrentMana { get; private set; }
        public int MaxMana => maxMana;
        int reserved, generation;
        public event Action<ManaChangedInfo> Changed;
        void Awake() => ResetWallet();
        void OnValidate() { maxMana = Mathf.Max(0, maxMana); startingMana = Mathf.Clamp(startingMana, 0, maxMana); }
        void OnDestroy() { generation++; reserved = 0; }

        public void ResetWallet()
        {
            int previous = CurrentMana;
            generation++; reserved = 0;
            CurrentMana = Mathf.Clamp(startingMana, 0, maxMana);
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
