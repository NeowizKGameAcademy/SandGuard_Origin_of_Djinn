using System;
using SandGuard.Skills;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Player
{
    /// <summary>
    /// 경험치·레벨·스킬 포인트. 적이 떨군 경험치 입자는 <see cref="IExperienceReceiver"/>로 여기에 들어온다.
    /// 레벨이 오르면 표의 포인트를 주고, 레벨마다 쌓이는 기본 능력치를 <see cref="PlayerStats"/>에 이 컴포넌트 출처로 다시 얹고,
    /// 설정된 비율만큼 체력·마나를 회복한 뒤 알린다. 사망·부활해도 유지되고 <see cref="ResetProgression"/>만 처음으로 돌린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerProgression : MonoBehaviour, IProgressionReader, IExperienceReceiver
    {
        public PlayerProgressionTable table;
        [Tooltip("비우면 같은 오브젝트에서 찾는다")] public PlayerHealth health;
        [Tooltip("비우면 같은 오브젝트에서 찾는다")] public PlayerManaWallet mana;
        [Tooltip("비우면 같은 오브젝트에서 찾는다")] public PlayerStats stats;
        [Tooltip("경험치 입자가 닿는 높이(발 기준)")] public float collectHeight = 1f;
        [Tooltip("레벨이 오를 때 한 번 (한 번에 여러 레벨이 올라도 한 번). 레벨업 VFX 연결용")]
        public UnityEvent onLevelUp = new UnityEvent();

        public int Level { get; private set; } = 1;
        public int MaxLevel => table != null ? table.MaxLevel : 1;
        public int ExperienceInLevel { get; private set; }
        public int ExperienceToNextLevel => table != null ? table.ExperienceToNext(Level) : 0;
        public bool IsMaxLevel => Level >= MaxLevel;
        /// <summary>지금까지 실제로 반영된 경험치 합.</summary>
        public int TotalExperience { get; private set; }
        /// <summary>아직 쓰지 않은 스킬 포인트. 스킬트리가 <see cref="TrySpendSkillPoints"/>로 쓴다.</summary>
        public int SkillPoints => PointWallet.Balance;
        public SkillPointWallet PointWallet { get; }=new SkillPointWallet();
        bool updatingProgression,changingExperience;
        void PointsChanged(){if(!updatingProgression)Changed?.Invoke();}

        public Vector3 CollectPosition => transform.position + Vector3.up * collectHeight;
        public bool CanCollect => isActiveAndEnabled && (health == null || health.State == LifeState.Alive);

        public event Action Changed;
        public event Action<int> LevelUp;

        void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
            if (mana == null) mana = GetComponent<PlayerManaWallet>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            ResetState();
            PointWallet.Changed+=PointsChanged;
        }
        void OnEnable() => ExperienceReceivers.Register(this);
        void OnDisable() => ExperienceReceivers.Unregister(this);

        /// <summary>Lv.1, 경험치 0, 시작 포인트로 되돌리고 레벨 능력치를 걷어낸다. 게임 재시작용.</summary>
        public void ResetProgression()
        {
            if(changingExperience || PointWallet.IsBusy)return;
            ResetState();
            Changed?.Invoke();
        }

        void ResetState()
        {
            Level = 1; ExperienceInLevel = 0; TotalExperience = 0;
            updatingProgression=true;
            try{PointWallet.Reset(table != null ? table.startingSkillPoints : 0);}finally{updatingProgression=false;}
            if (stats != null) stats.RemoveAll(this);
        }

        public int GainExperience(int amount)
        {
            if(changingExperience || PointWallet.IsBusy)return 0;
            changingExperience=true;
            try{return GainExperienceInternal(amount);}finally{changingExperience=false;}
        }
        int GainExperienceInternal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || IsMaxLevel) return 0;
            int startLevel = Level, applied = 0; long pointsAward=0;
            while (amount > 0 && !IsMaxLevel)
            {
                int take = Mathf.Min(amount, ExperienceToNextLevel - ExperienceInLevel);
                ExperienceInLevel += take; amount -= take; applied += take;
                if (ExperienceInLevel < ExperienceToNextLevel) break;
                Level++;
                ExperienceInLevel = 0; // 최대 레벨에서는 계약대로 0, 남은 경험치는 버린다
                pointsAward+=table.SkillPointsOnReach(Level);
            }
            TotalExperience += applied;
            if (Level != startLevel) ApplyLevelRewards(Level - startLevel);
            updatingProgression=true;
            try{if(pointsAward>0)PointWallet.TryChange((int)Math.Min(pointsAward,int.MaxValue-(long)SkillPoints));}
            finally{updatingProgression=false;}
            Changed?.Invoke();
            if (Level == startLevel) return applied;
            for (int level = startLevel + 1; level <= Level; level++) LevelUp?.Invoke(level);
            onLevelUp.Invoke();
            return applied;
        }

        public bool TrySpendSkillPoints(int amount)
        {
            if (amount <= 0 || amount > SkillPoints) return false;
            return PointWallet.TryChange(-amount);
        }

        void OnDestroy(){PointWallet.Changed-=PointsChanged;}

        void ApplyLevelRewards(int levelsGained)
        {
            if (stats != null)
            {
                stats.RemoveAll(this);
                foreach (var growth in table.statGrowth)
                    if (growth != null && growth.valuePerLevel != 0f)
                        stats.Add(growth.stat, growth.kind, growth.valuePerLevel * (Level - 1), this, label: "Lv." + Level);
            }
            // 최대치가 오른 뒤에 회복해야 새 최대치 기준 비율이 된다.
            if (health != null && table.healRatioOnLevelUp > 0f) health.Heal(health.MaxHealth * table.healRatioOnLevelUp * levelsGained);
            if (mana != null && table.manaRatioOnLevelUp > 0f) mana.Gain(Mathf.RoundToInt(mana.MaxMana * table.manaRatioOnLevelUp * levelsGained));
        }
    }
}
