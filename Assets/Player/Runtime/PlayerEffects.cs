using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>효과가 플레이어에 손을 대는 통로. 필요한 컴포넌트를 한 번 모아 둔다.</summary>
    public sealed class PlayerEffectContext
    {
        public readonly GameObject Root;
        public readonly PlayerStats Stats;
        public readonly PlayerMotor Motor;
        public readonly PlayerBasicAttack Attack;
        public readonly PlayerHealth Health;
        public readonly IManaWallet Mana;

        public PlayerEffectContext(GameObject root)
        {
            Root = root;
            Stats = root.GetComponent<PlayerStats>();
            Motor = root.GetComponent<PlayerMotor>();
            Attack = root.GetComponent<PlayerBasicAttack>();
            Health = root.GetComponent<PlayerHealth>();
            Mana = root.GetComponent<IManaWallet>();
        }
    }

    /// <summary>
    /// 붙였다 뗄 수 있는 플레이어 효과. 스킬 노드·버프는 모두 이 계약을 따른다.
    /// Remove 뒤에는 Apply 전과 같은 상태여야 하며, 그 대칭은 <see cref="PlayerEffect"/> 베이스가 대신 지켜 준다.
    /// </summary>
    public interface IPlayerEffect
    {
        void Apply(PlayerEffectContext context);
        void Remove(PlayerEffectContext context);
    }

    /// <summary>
    /// 되돌리기를 자동으로 기록하는 효과 베이스. <see cref="AddStat"/>로 넣은 수정자와 <see cref="Subscribe"/>로 건 구독은
    /// Remove 때 역순으로 풀린다. 파생 효과는 OnApply에서 그 둘만 쓰면 떼는 코드를 따로 쓰지 않아도 된다.
    /// </summary>
    public abstract class PlayerEffect : IPlayerEffect
    {
        readonly List<Action> undo = new List<Action>();
        public bool IsApplied { get; private set; }
        public virtual string DisplayName => GetType().Name;

        public void Apply(PlayerEffectContext context)
        {
            if (IsApplied) return;
            IsApplied = true;
            OnApply(context);
        }

        public void Remove(PlayerEffectContext context)
        {
            if (!IsApplied) return;
            for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
            undo.Clear();
            OnRemove(context);
            IsApplied = false;
        }

        protected abstract void OnApply(PlayerEffectContext context);
        /// <summary>자동 되돌리기로 부족한 정리. 대부분 비워 둔다.</summary>
        protected virtual void OnRemove(PlayerEffectContext context) { }

        /// <summary>이 효과를 출처로 스탯 수정자를 넣는다. Remove 때 지워진다.</summary>
        protected StatModifierHandle AddStat(PlayerEffectContext context, PlayerStat stat, StatModifierKind kind, float value, float duration = 0f, string label = null)
        {
            if (context.Stats == null) throw new InvalidOperationException(DisplayName + ": 플레이어에 PlayerStats가 없습니다.");
            var handle = context.Stats.Add(stat, kind, value, this, duration, label ?? DisplayName);
            var stats = context.Stats;
            undo.Add(() => { if (stats != null) stats.Remove(handle); });
            return handle;
        }

        /// <summary>이벤트 구독처럼 짝이 있는 동작을 등록한다. subscribe는 지금 실행되고 unsubscribe는 Remove 때 실행된다.</summary>
        protected void Subscribe(Action subscribe, Action unsubscribe)
        {
            subscribe();
            undo.Add(unsubscribe);
        }

        /// <summary>되돌릴 동작만 등록한다 (컴포넌트 값 변경 등).</summary>
        protected void OnUndo(Action undoAction) => undo.Add(undoAction);
    }

    /// <summary>플레이어에 붙은 효과 목록. 스킬 해금·해제, 리스펙, 버프가 여기를 통해 붙였다 뗀다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerEffects : MonoBehaviour
    {
        readonly List<IPlayerEffect> applied = new List<IPlayerEffect>();
        PlayerEffectContext context;
        public PlayerEffectContext Context => context ?? (context = new PlayerEffectContext(gameObject));
        public IReadOnlyList<IPlayerEffect> Applied => applied;
        /// <summary>(효과, 붙었는지) 알림. UI·저장이 구독한다.</summary>
        public event Action<IPlayerEffect, bool> Changed;

        public bool IsApplied(IPlayerEffect effect) => applied.Contains(effect);

        /// <summary>이미 붙어 있으면 false. 같은 효과를 두 번 세지 않는다.</summary>
        public bool Apply(IPlayerEffect effect)
        {
            if (effect == null || applied.Contains(effect)) return false;
            effect.Apply(Context);
            applied.Add(effect);
            Changed?.Invoke(effect, true);
            return true;
        }

        public bool Remove(IPlayerEffect effect)
        {
            if (effect == null || !applied.Remove(effect)) return false;
            effect.Remove(Context);
            Changed?.Invoke(effect, false);
            return true;
        }

        /// <summary>전부 뗀다 (리스펙, 씬 정리). 붙인 역순으로 푼다.</summary>
        public void RemoveAll()
        {
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                var effect = applied[i];
                applied.RemoveAt(i);
                effect.Remove(Context);
                Changed?.Invoke(effect, false);
            }
        }

        void OnDestroy() { if (applied.Count > 0) RemoveAll(); }
    }
}
