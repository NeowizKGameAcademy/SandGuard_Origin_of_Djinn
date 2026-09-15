using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 레벨 성장 데이터. 최대 레벨 = steps 개수 + 1.
    /// steps[i]는 Lv.(i+1)에서 Lv.(i+2)로 오르는 데 필요한 경험치와 그 레벨에 도달할 때 주는 스킬 포인트다.
    /// </summary>
    [CreateAssetMenu(menuName = "SandGuard/Player Progression Table", fileName = "PlayerProgressionTable")]
    public sealed class PlayerProgressionTable : ScriptableObject
    {
        [Serializable]
        public sealed class Step
        {
            [Min(1)] public int experience = 100;
            [Min(0)] public int skillPoints = 1;
        }

        [Serializable]
        public sealed class StatGrowth
        {
            public PlayerStat stat;
            public StatModifierKind kind = StatModifierKind.Flat;
            [Tooltip("Lv.1 대비 한 레벨마다 쌓이는 값. Lv.N의 수정자 = 이 값 × (N-1)")]
            public float valuePerLevel;
        }

        [Min(0), Tooltip("Lv.1에서 가진 스킬 포인트")]
        public int startingSkillPoints = 1;
        public List<Step> steps = new List<Step>();

        [Header("레벨업 보상")]
        [Range(0f, 1f), Tooltip("레벨이 오를 때마다 최대 체력의 이 비율만큼 회복. 0이면 회복 없음")]
        public float healRatioOnLevelUp;
        [Range(0f, 1f), Tooltip("레벨이 오를 때마다 최대 마나의 이 비율만큼 회복. 0이면 회복 없음")]
        public float manaRatioOnLevelUp;
        [Tooltip("레벨마다 쌓이는 기본 능력치")]
        public List<StatGrowth> statGrowth = new List<StatGrowth>();

        public int MaxLevel => steps.Count + 1;

        /// <summary>Lv.<paramref name="level"/>에서 다음 레벨까지 필요한 경험치. 최대 레벨 이상이면 0.</summary>
        public int ExperienceToNext(int level) => level >= 1 && level < MaxLevel && steps[level - 1] != null ? Mathf.Max(1, steps[level - 1].experience) : 0;

        /// <summary>Lv.<paramref name="level"/>에 도달할 때 주는 스킬 포인트.</summary>
        public int SkillPointsOnReach(int level) => level >= 2 && level <= MaxLevel && steps[level - 2] != null ? Mathf.Max(0, steps[level - 2].skillPoints) : 0;

        /// <summary>Lv.10, 누적 포인트 25(시작 1 + 레벨업 24), 레벨마다 최대 체력 +10·최대 마나 +5·마나탄 피해 +3%. 회복 0. 임시 수치.</summary>
        public void ApplyDefaults()
        {
            int[] experience = { 50, 80, 120, 170, 230, 300, 380, 470, 570 };
            int[] points = { 2, 2, 2, 3, 3, 3, 3, 3, 3 };
            startingSkillPoints = 1;
            steps = new List<Step>();
            for (int i = 0; i < experience.Length; i++) steps.Add(new Step { experience = experience[i], skillPoints = points[i] });
            healRatioOnLevelUp = 0f;
            manaRatioOnLevelUp = 0f;
            statGrowth = new List<StatGrowth>
            {
                new StatGrowth { stat = PlayerStat.MaxHealth, kind = StatModifierKind.Flat, valuePerLevel = 10f },
                new StatGrowth { stat = PlayerStat.MaxMana, kind = StatModifierKind.Flat, valuePerLevel = 5f },
                new StatGrowth { stat = PlayerStat.AttackDamage, kind = StatModifierKind.PercentAdd, valuePerLevel = 0.03f },
            };
        }

        void Reset() => ApplyDefaults();
    }
}
