using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>수정자를 얹을 수 있는 플레이어 수치. 기본값은 각 컴포넌트(모터·공격·체력)가 갖고, 여기서는 배수·가산만 관리한다.</summary>
    public enum PlayerStat
    {
        MoveSpeed,        // m/s
        JumpHeight,       // m
        ExtraAirJumps,    // 개수 (반올림)
        DashDistance,     // m
        DashCooldown,     // 초
        DashManaCost,     // 개수 (반올림)
        AttackDamage,     // 볼트 피해
        AttackInterval,   // 초
        ProjectileSpeed,  // m/s
        DamageTaken,      // 받는 피해 배수. 기본 1
        ManaPerHit,       // 명중당 마나 회복(개수, 반올림). 기본 0
        AirDashes,        // 공중에서 쓸 수 있는 대시 횟수(개수, 반올림). 접지하면 다시 찬다. 기본 0
        // 공격 마법 스킬. 켜짐 여부는 개수형(0보다 크면 켜짐, 여러 노드가 같이 켜도 합산될 뿐 세기는 안 변한다), 수치는 PlayerBasicAttack 인스펙터 기본값에 얹는다.
        BoltScale,        // 마나탄 크기 배수. 기본 1 (⑬ 응축 마나탄)
        PierceBeam,       // 0보다 크면 볼트 대신 즉발 관통 빔 (관통탄)
        BeamRange,        // 관통 빔 사거리 m
        SandBurst,        // 0보다 크면 착탄점에서 폭발 (모래 폭발)
        BurstRadius,      // 폭발 반경 m
        BurstDamageRatio, // 폭발 피해 = 볼트 피해 × 이 값
        BurstPerPierce,   // 0보다 크면 빔이 꿰뚫은 적마다 폭발 (폭발 관통탄). 아니면 빔 끝에서 한 번
        SandShackle,      // 0보다 크면 폭발 지점 주변 적을 묶는다 (모래 족쇄, 패시브)
        ShackleRadius,    // 족쇄 반경 m
        ShackleDuration,  // 족쇄 지속 초
        SandVortex,       // 0보다 크면 E 모래 소용돌이 해금
        VortexRadius,     // 소용돌이 반경 m
        VortexDuration,   // 소용돌이 지속 초
        SandStorm,        // 0보다 크면 R 사막 폭풍 해금
        StormRadius,      // 폭풍 반경 m
        StormDuration,    // 폭풍 지속 초
        StormDamageRatio, // 폭풍 틱 피해 = 볼트 피해 × 이 값
        StormSlow         // 폭풍 안 둔화 비율(0.6 = 속도 40%)
    }

    public enum StatModifierKind
    {
        /// <summary>기본값에 더한다. 먼저 적용된다.</summary>
        Flat,
        /// <summary>백분율끼리 합친 뒤 곱한다. +0.25와 +0.25는 ×1.5.</summary>
        PercentAdd,
        /// <summary>각각 곱한다. ×0.7과 ×0.7은 ×0.49.</summary>
        Multiply
    }

    /// <summary><see cref="PlayerStats.Add"/>가 돌려주는 식별자. 나중에 그 수정자만 지울 때 쓴다.</summary>
    public readonly struct StatModifierHandle : IEquatable<StatModifierHandle>
    {
        internal readonly int Id;
        internal StatModifierHandle(int id) { Id = id; }
        public bool IsValid => Id != 0;
        public bool Equals(StatModifierHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is StatModifierHandle other && Equals(other);
        public override int GetHashCode() => Id;
    }

    /// <summary>UI·디버그용 수정자 설명.</summary>
    public readonly struct StatModifierInfo
    {
        public readonly PlayerStat Stat; public readonly StatModifierKind Kind; public readonly float Value;
        public readonly object Source; public readonly string Label; public readonly float RemainingSeconds; // 영구는 음수
        public StatModifierInfo(PlayerStat stat, StatModifierKind kind, float value, object source, string label, float remaining)
        { Stat = stat; Kind = kind; Value = value; Source = source; Label = label; RemainingSeconds = remaining; }
    }

    /// <summary>
    /// 스탯 수정자 층. 최종값 = (기본값 + 가산 합) × (1 + 백분율 합) × (배수 곱), 0 미만은 0.
    /// 스킬트리 패시브(대시 거리 +25%, 쿨타임 -20%, 피해 +30%, 받는 피해 -30% 등)는 여기에 출처를 붙여 넣고,
    /// 해금 취소나 버프 만료 때 핸들이나 출처로 지운다. duration을 주면 그 시간 뒤 스스로 사라진다.
    /// 값을 읽는 쪽(PlayerMotor·PlayerBasicAttack·PlayerHealth)은 매번 Evaluate를 부르므로 캐시가 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        sealed class Entry
        {
            public int Id; public PlayerStat Stat; public StatModifierKind Kind; public float Value;
            public object Source; public string Label; public float ExpiresAt; // 음수면 영구
            public bool Expired => ExpiresAt >= 0f && Time.time >= ExpiresAt;
        }
        readonly List<Entry> entries = new List<Entry>();
        int nextId = 1;
        /// <summary>어느 스탯의 수정자가 추가·제거·만료됐는지 알린다.</summary>
        public event Action<PlayerStat> Changed;
        /// <summary>변경될 때마다 1씩 오른다. UI가 다시 그릴지 판단할 때 쓴다.</summary>
        public int Version { get; private set; }
        public int Count => entries.Count;

        /// <param name="duration">초. 0 이하면 영구.</param>
        public StatModifierHandle Add(PlayerStat stat, StatModifierKind kind, float value, object source = null, float duration = 0f, string label = null)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            var entry = new Entry
            {
                Id = nextId++, Stat = stat, Kind = kind, Value = value, Source = source, Label = label,
                ExpiresAt = duration > 0f ? Time.time + duration : -1f
            };
            entries.Add(entry);
            Notify(stat);
            return new StatModifierHandle(entry.Id);
        }

        public bool Remove(StatModifierHandle handle)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id != handle.Id) continue;
                PlayerStat stat = entries[i].Stat;
                entries.RemoveAt(i); Notify(stat);
                return true;
            }
            return false;
        }

        /// <summary>같은 출처(스킬 노드, 버프 등)가 넣은 수정자를 모두 지운다. 지운 개수를 돌려준다.</summary>
        public int RemoveAll(object source)
        {
            if (source == null) return 0;
            int removed = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(entries[i].Source, source) && !Equals(entries[i].Source, source)) continue;
                PlayerStat stat = entries[i].Stat;
                entries.RemoveAt(i); removed++; Notify(stat);
            }
            return removed;
        }

        public void Clear()
        {
            if (entries.Count == 0) return;
            var stats = new HashSet<PlayerStat>();
            foreach (var entry in entries) stats.Add(entry.Stat);
            entries.Clear();
            foreach (var stat in stats) Notify(stat);
        }

        public float Evaluate(PlayerStat stat, float baseValue)
        {
            float flat = 0f, percent = 0f, multiply = 1f;
            foreach (var entry in entries)
            {
                if (entry.Stat != stat || entry.Expired) continue;
                switch (entry.Kind)
                {
                    case StatModifierKind.Flat: flat += entry.Value; break;
                    case StatModifierKind.PercentAdd: percent += entry.Value; break;
                    default: multiply *= entry.Value; break;
                }
            }
            return Mathf.Max(0f, (baseValue + flat) * (1f + percent) * multiply);
        }

        /// <summary>개수형 스탯(공중 점프, 마나 비용)은 반올림해 0 이상 정수로 돌려준다.</summary>
        public int EvaluateCount(PlayerStat stat, int baseValue) => Mathf.Max(0, Mathf.RoundToInt(Evaluate(stat, baseValue)));

        public bool HasModifiers(PlayerStat stat)
        {
            foreach (var entry in entries) if (entry.Stat == stat && !entry.Expired) return true;
            return false;
        }

        /// <summary>해당 스탯에 살아 있는 수정자 목록을 채운다. UI 툴팁용.</summary>
        public void CopyModifiers(PlayerStat stat, List<StatModifierInfo> into)
        {
            foreach (var entry in entries)
            {
                if (entry.Stat != stat || entry.Expired) continue;
                into.Add(new StatModifierInfo(entry.Stat, entry.Kind, entry.Value, entry.Source, entry.Label, entry.ExpiresAt < 0f ? -1f : entry.ExpiresAt - Time.time));
            }
        }

        void Update()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!entries[i].Expired) continue;
                PlayerStat stat = entries[i].Stat;
                entries.RemoveAt(i); Notify(stat);
            }
        }

        void Notify(PlayerStat stat) { Version++; Changed?.Invoke(stat); }
    }
}
