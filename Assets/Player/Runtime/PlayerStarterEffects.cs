using System.Collections.Generic;
using SandGuard.Player.Effects;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 스킬트리가 붙기 전까지 테스트 씬의 기본 해금을 대신한다. 시작할 때 고른 효과를 플레이어에 붙이고, 사라질 때 뗀다.
    /// 실제 게임에서는 스킬트리 해금이 같은 효과를 붙이므로 이 컴포넌트는 테스트 씬에만 둔다.
    /// </summary>
    public sealed class PlayerStarterEffects : MonoBehaviour
    {
        [Tooltip("비우면 씬에서 찾는다")]
        public PlayerEffects target;
        public bool doubleJump = true;
        public bool airDash = true;
        public bool manaOnHit = false;
        public bool updraft = true;
        [Header("공격 마법 (기본 꺼짐)")]
        public bool condensedBolt = false;
        public bool pierceBeam = false;
        public bool sandBurst = false;
        public bool sandShackle = false;
        public bool explosivePierce = false;
        public bool sandVortex = false;
        public bool sandStorm = false;
        readonly List<IPlayerEffect> applied = new List<IPlayerEffect>();
        public IReadOnlyList<IPlayerEffect> Applied => applied;

        public bool SuppressedBySkillTree { get; private set; }
        public void SuppressForSkillTree()
        {
            SuppressedBySkillTree=true;
            if(target!=null)foreach(var effect in applied)target.Remove(effect);
            applied.Clear();
        }
        void Start()
        {
            if(SuppressedBySkillTree)return;
            if (target == null) target = FindFirstObjectByType<PlayerEffects>();
            if (target == null) { Debug.LogWarning("PlayerStarterEffects: 씬에 PlayerEffects가 없습니다.", this); return; }
            if (doubleJump) Add(new DoubleJumpEffect());
            if (airDash) Add(new AirDashEffect());
            if (manaOnHit) Add(new ManaOnHitEffect());
            if (updraft) Add(new UpdraftEffect());
            if (condensedBolt) Add(new CondensedBoltEffect());
            if (pierceBeam) Add(new PierceBeamEffect());
            if (sandBurst) Add(new SandBurstEffect());
            if (sandShackle) Add(new SandShackleEffect());
            if (explosivePierce) Add(new ExplosivePierceEffect());
            if (sandVortex) Add(new SandVortexEffect());
            if (sandStorm) Add(new SandStormEffect());
        }

        void Add(IPlayerEffect effect) { if (target.Apply(effect)) applied.Add(effect); }

        void OnDestroy()
        {
            if (target == null) return;
            foreach (var effect in applied) target.Remove(effect);
            applied.Clear();
        }
    }
}
