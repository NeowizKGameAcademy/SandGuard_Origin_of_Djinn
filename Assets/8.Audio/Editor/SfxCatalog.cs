using System.Collections.Generic;

namespace SandGuard.Audio.Editor
{
    /// <summary>자리표시 클립의 성격. 담당자가 어떤 소리를 넣어야 하는지 이름만으로 알 수 있게 종류를 나눈다.</summary>
    public enum PlaceholderKind { Impact, Whoosh, Zap, Ping, Loop, Drone, Sting, Voice, Tick }

    /// <summary>큐 하나의 이름·기본값·자리표시 종류.</summary>
    public sealed class CueEntry
    {
        public string Name;
        public PlaceholderKind Kind;
        public bool Spatial = true;
        public float Min = 2f, Max = 35f;
        public float Volume = 0.8f;
        public int MaxVoices = 4;
        public string Note;
        public bool Loop => Name.EndsWith("_Loop");
        public string Category => Name.Substring(0, Name.IndexOf('_'));

        public CueEntry(string name, PlaceholderKind kind, string note = null) { Name = name; Kind = kind; Note = note; }
        public CueEntry At(float min, float max) { Min = min; Max = max; return this; }
        public CueEntry Flat() { Spatial = false; return this; }
        public CueEntry Vol(float v) { Volume = v; return this; }
        public CueEntry Voices(int n) { MaxVoices = n; return this; }
    }

    /// <summary>
    /// 제안 목록(Docs/plan/SandGuard_SFX_제안목록_v0.1.md)의 모든 소리. 큐 이름·기본값·자리표시 종류의 원본이다.
    /// 여기 있는 이름으로 큐가 만들어지고, 클립이 없는 큐는 자리표시 클립이 들어간다. 담당자는 큐에서 클립만 바꾼다.
    /// 이름 규칙: &lt;분류&gt;_&lt;대상&gt;_&lt;동작&gt;[_Loop]. "_Voice"가 들어가면 Voice 믹서 그룹.
    /// </summary>
    public static class SfxCatalog
    {
        static CueEntry E(string name, PlaceholderKind kind, string note = null) => new CueEntry(name, kind, note);

        public static readonly CueEntry[] Entries =
        {
            /* ---------------- 플레이어 ---------------- */
            E("Player_ManaBolt_Fire", PlaceholderKind.Zap, "마나탄 발사. 지팡이 끝").At(3, 40),
            E("Player_ManaBolt_Flight_Loop", PlaceholderKind.Loop, "투사체 비행 윙윙").At(1, 15).Vol(0.3f),
            E("Player_ManaBolt_Impact", PlaceholderKind.Impact, "착탄: 크리스탈 파열 + 모래").At(2, 35).Voices(6),
            E("Player_PierceBeam_Fire", PlaceholderKind.Zap, "관통 빔 '즈왕'").At(3, 45),
            E("Player_SandBurst", PlaceholderKind.Impact, "모래 폭발 저역 붐").At(4, 50),
            E("Player_SandShackle", PlaceholderKind.Impact, "모래 족쇄 조임").At(2, 30),
            E("Player_SandVortex_Loop", PlaceholderKind.Loop, "소용돌이 회전 바람").At(4, 45),
            E("Player_SandStorm_Loop", PlaceholderKind.Loop, "사막 폭풍 강풍").At(5, 60),
            E("Player_Updraft_Charge_Loop", PlaceholderKind.Loop, "상승 기류 충전(세기에 따라 피치 상승)").At(2, 20),
            E("Player_Updraft_Launch", PlaceholderKind.Whoosh, "상승 기류 발사 모래 기둥").At(3, 40),
            E("Player_Dash", PlaceholderKind.Whoosh, "대시 whoosh + 저역 팝").At(2, 25),
            E("Player_AirJump", PlaceholderKind.Ping, "공중 점프 마력 링 팝").Flat(),
            E("Player_Jump", PlaceholderKind.Whoosh, "점프 옷 스침 + 모래").Flat().Vol(0.7f),
            E("Player_Land", PlaceholderKind.Impact, "착지 발 + 옷").At(2, 20).Vol(0.6f),
            E("Player_HardLand", PlaceholderKind.Impact, "무거운 착지").At(3, 30),
            E("Player_Footstep_Sand", PlaceholderKind.Tick, "모래 발소리 (걷기·달리기 공용, 변형 6~8개 권장)").At(1, 15).Vol(0.45f).Voices(3),
            E("Player_Hit", PlaceholderKind.Impact, "플레이어 피격 타격 + 심장 펄스").Flat().Vol(0.9f).Voices(2),
            E("Player_Death", PlaceholderKind.Sting, "플레이어 사망").Flat(),
            E("Player_Revive", PlaceholderKind.Sting, "부활 마력 응집").Flat(),
            E("Player_LevelUp", PlaceholderKind.Sting, "레벨업 금빛 아르페지오").Flat().Vol(0.9f).Voices(1),
            E("Player_Xp_Drop", PlaceholderKind.Ping, "경험치 조각 드롭 짤랑").At(2, 25).Vol(0.4f).Voices(6),
            E("Player_ManaCharge_Loop", PlaceholderKind.Loop, "코어 근처 마나 충전").At(3, 30),
            E("Player_ManaCharge_Complete", PlaceholderKind.Ping, "마나 충전 완료 차임").At(3, 30),
            E("Player_Voice_Cast", PlaceholderKind.Voice, "시전 기합 (음성 모델)").At(2, 25).Vol(0.6f),
            E("Player_Voice_Jump", PlaceholderKind.Voice, "점프 기합").At(2, 25).Vol(0.5f),
            E("Player_Voice_Hit", PlaceholderKind.Voice, "피격 음성").Flat().Vol(0.7f),
            E("Player_Voice_Death", PlaceholderKind.Voice, "사망 음성").Flat(),

            /* ---------------- 적 ---------------- */
            E("Enemy_Spawn", PlaceholderKind.Impact, "출현: 모래 뚫고 '푸슉' + 북").At(4, 50).Voices(3),
            E("Enemy_Hit", PlaceholderKind.Impact, "적 피격: 가죽 타격 + 마력 지짐").At(2, 35).Voices(6),
            E("Enemy_Death", PlaceholderKind.Whoosh, "적 사망: 모래 흩어짐 + 흡수 상승음").At(3, 40),
            E("Enemy_Footstep_Light", PlaceholderKind.Tick, "검병·암살자 발소리").At(1, 18).Vol(0.35f).Voices(4),
            E("Enemy_Footstep_Heavy", PlaceholderKind.Tick, "방패병·망치병·족장 발소리 + 갑옷").At(2, 25).Vol(0.5f).Voices(4),
            E("Enemy_Swordsman_Swing", PlaceholderKind.Whoosh, "검 휘두름 + 기합").At(2, 30),
            E("Enemy_Assassin_Swing", PlaceholderKind.Whoosh, "단검 빠른 2연 whoosh").At(2, 30),
            E("Enemy_ShieldGuard_Swing", PlaceholderKind.Whoosh, "검 휘두름 + 방패 덜컥").At(2, 30),
            E("Enemy_HammerBrute_Swing", PlaceholderKind.Whoosh, "망치 큰 whoosh → 지면 쿵 + 포효").At(3, 40),
            E("Enemy_Chief_Swing", PlaceholderKind.Whoosh, "곡도 whoosh + 보스 음성").At(3, 40),
            E("Enemy_Voice_Death_Light", PlaceholderKind.Voice, "검병·암살자·방패병 사망 음성").At(2, 30).Vol(0.7f),
            E("Enemy_Voice_Death_Heavy", PlaceholderKind.Voice, "망치병 사망 음성").At(3, 35).Vol(0.8f),
            E("Enemy_Voice_Chief_Death", PlaceholderKind.Voice, "족장 사망 음성 (길게)").At(4, 45).Vol(0.9f),
            E("Enemy_ShieldBlock", PlaceholderKind.Impact, "방패 방어 금속 '캉' + 육각 팝").At(2, 35).Voices(3),
            E("Enemy_Chief_ShieldBlock", PlaceholderKind.Impact, "황금 방패 방어 종 울림").At(3, 40).Voices(2),
            E("Enemy_Chief_Shield_Loop", PlaceholderKind.Loop, "황금 방패 소환→유지 험→해제").At(3, 40).Voices(1),
            E("Enemy_Chief_Bomb_Fuse_Loop", PlaceholderKind.Loop, "폭탄 도화선 지글 (비행 중)").At(2, 30).Voices(2),
            E("Enemy_Chief_Bomb_Explosion", PlaceholderKind.Impact, "철거 폭탄 폭발 + 잔향").At(6, 80).Voices(2),
            E("Enemy_Burning", PlaceholderKind.Loop, "불타는 적 지글 2초").At(2, 30).Voices(4),

            /* ---------------- 시설·벽·코어 ---------------- */
            E("Facility_Build_Poof", PlaceholderKind.Whoosh, "건설 연막 '퍽'").At(3, 40),
            E("Facility_Build_Complete", PlaceholderKind.Sting, "건설 완성 돌 '쿵' + 차임").At(3, 45),
            E("Facility_Cobra_Flame_Loop", PlaceholderKind.Loop, "코브라 화염 토치 루프").At(3, 40).Voices(4),
            E("Facility_Cobra_Flame_Impact", PlaceholderKind.Impact, "화염 착탄 불꽃 튐").At(2, 30).Voices(4),
            E("Facility_Obelisk_Loop", PlaceholderKind.Loop, "오벨리스크 둔화 필드 (모래시계 + 저역 펄스)").At(4, 35).Voices(4),
            E("Facility_Summon_Circle_Loop", PlaceholderKind.Loop, "소환진 룬 공명").At(3, 35).Voices(4),
            E("Facility_Summon_Pillar", PlaceholderKind.Sting, "빛기둥 소환 + 해골 달그락").At(3, 40),
            E("Facility_Hit", PlaceholderKind.Impact, "시설 피격 사암 타격").At(3, 40).Voices(4),
            E("Facility_Cobra_Destroy", PlaceholderKind.Impact, "코브라 파괴 붕괴 + 돌 파편").At(6, 70).Voices(2),
            E("Facility_Disabled_Loop", PlaceholderKind.Loop, "시설 정지 전기 지직 (5초)").At(3, 35).Voices(4),
            E("Wall_Hit", PlaceholderKind.Impact, "벽·타워·코어 표면 타격 (VFX_Wall_Hit)").At(3, 40).Voices(4),
            E("Wall_Destroy", PlaceholderKind.Impact, "벽 파괴 돌무더기 붕괴").At(6, 70).Voices(2),
            E("Core_Hum_Loop", PlaceholderKind.Loop, "코어 크리스탈 험").At(6, 60).Vol(0.7f).Voices(1),
            E("Core_Hit", PlaceholderKind.Impact, "코어 피격 균열 + 경고음").Flat().Vol(0.9f).Voices(2),
            E("Core_Destroy", PlaceholderKind.Sting, "코어 파괴 붕괴 → 정적 → 잔향").Flat().Voices(1),
            E("Core_Warning", PlaceholderKind.Ping, "코어 안정도 경고 (30 이하)").Flat().Voices(1),
            E("Wave_Clear", PlaceholderKind.Sting, "웨이브 클리어 팡파르 (VFX_Wave_Clear)").Flat().Voices(1),
            E("Wave_Start", PlaceholderKind.Impact, "웨이브 시작 북 1타").Flat().Voices(1),
            E("Wave_Countdown_Tick", PlaceholderKind.Tick, "카운트다운 틱").Flat().Voices(1),

            /* ---------------- 환경 ---------------- */
            E("Env_DesertWind_Loop", PlaceholderKind.Loop, "사막 바람 베드 (2D)").Flat().Vol(0.5f),
            E("Env_StormWall_Loop", PlaceholderKind.Loop, "외곽 모래폭풍 벽 (거리 기반은 후속)").Flat().Vol(0.35f).Voices(1),
            E("Env_Lightning", PlaceholderKind.Impact, "마법 번개 크랙 + 지연 천둥").Flat().Vol(0.7f).Voices(2),
            E("Env_Torch_Loop", PlaceholderKind.Loop, "횃불 타닥").At(1, 8).Vol(0.5f).Voices(4),
            E("Env_Altar_Loop", PlaceholderKind.Loop, "스킬 제단 룬 공명").At(2, 12).Vol(0.5f).Voices(1),
            E("Env_Menu_Ambience_Loop", PlaceholderKind.Loop, "메인 메뉴 앰비언스").Flat().Vol(0.4f).Voices(1),

            /* ---------------- UI ---------------- */
            E("UI_Click", PlaceholderKind.Ping, "버튼 클릭").Flat().Vol(0.6f).Voices(2),
            E("UI_Hover", PlaceholderKind.Ping, "버튼 호버").Flat().Vol(0.35f).Voices(2),
            E("UI_Menu_Open", PlaceholderKind.Ping, "건설 메뉴·스킬트리 열림").Flat().Vol(0.6f).Voices(1),
            E("UI_Menu_Close", PlaceholderKind.Ping, "닫힘").Flat().Vol(0.5f).Voices(1),
            E("UI_Fail", PlaceholderKind.Tick, "건설·구매·스킬 사용 실패 거절음").Flat().Vol(0.6f).Voices(1),
            E("UI_Skill_Learn", PlaceholderKind.Sting, "스킬 해금 금빛 차임").Flat().Vol(0.7f).Voices(1),
            E("UI_Skill_Equip", PlaceholderKind.Ping, "슬롯 장착 클릭").Flat().Vol(0.6f).Voices(1),
            E("UI_Cooldown_Ready", PlaceholderKind.Tick, "스킬 쿨다운 완료").Flat().Vol(0.5f).Voices(2),
            E("UI_Health_Low_Loop", PlaceholderKind.Loop, "체력 20% 이하 심장 박동").Flat().Vol(0.5f).Voices(1),

            /* ---------------- 음악 ---------------- */
            E("Music_Menu_Loop", PlaceholderKind.Drone, "메인 메뉴 테마").Flat().Vol(0.5f).Voices(1),
            E("Music_Preparation_Loop", PlaceholderKind.Drone, "준비 단계 루프").Flat().Vol(0.5f).Voices(1),
            E("Music_Combat_Loop", PlaceholderKind.Drone, "전투 루프").Flat().Vol(0.5f).Voices(1),
            E("Music_Boss_Loop", PlaceholderKind.Drone, "보스 테마").Flat().Vol(0.5f).Voices(1),
            E("Music_Danger_Loop", PlaceholderKind.Drone, "코어 위험 긴장 레이어 (전투 위에 겹침)").Flat().Vol(0.4f).Voices(1),
            E("Music_Victory", PlaceholderKind.Sting, "승리 팡파르").Flat().Vol(0.7f).Voices(1),
            E("Music_Defeat", PlaceholderKind.Sting, "패배 스팅어").Flat().Vol(0.7f).Voices(1),
            E("Music_WaveClear_Sting", PlaceholderKind.Sting, "웨이브 클리어 스팅어").Flat().Vol(0.6f).Voices(1),
            E("Music_Boss_Sting", PlaceholderKind.Sting, "보스 등장 스팅어").Flat().Vol(0.7f).Voices(1),
        };

        public static CueEntry Find(string name)
        {
            foreach (var e in Entries) if (e.Name == name) return e;
            return null;
        }

        public static IEnumerable<string> Names() { foreach (var e in Entries) yield return e.Name; }
    }
}
