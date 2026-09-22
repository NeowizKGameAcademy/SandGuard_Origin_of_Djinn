using DesertTower.LevelIntegration;
using SandGuard.Enemy;
using SandGuard.Player;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    /// <summary>
    /// 씬의 게임 상태를 GameHUD 뷰에 옮긴다. 시작할 때 소스를 한 번 찾고(비어 있는 칸만), 매 프레임 값이 바뀐 칸만 다시 그린다.
    /// 플레이어: 체력·마나·레벨·경험치, Q/E/R 스킬(해금·쿨타임), 대시 쿨타임·공중 점프. 코어 안정도, 웨이브 번호, 보스(EnemyBossInfo).
    /// 찾지 못한 칸(플레이어 단독 씬의 코어·웨이브 등)은 숨긴다. 미니맵 내용은 MinimapController가 채운다. F(타워 계열) 슬롯은 잠김.
    /// 플레이어를 찾으면 같은 오브젝트의 HUDDebugController 더미 값을 끈다. 못 찾으면(HUD.unity) 더미가 그대로 보인다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameHUDPresenter : MonoBehaviour
    {
        [SerializeField] private GameHUDController hud;
        [Tooltip("비우면 씬에서 PlayerHealth를 찾는다")] public GameObject player;
        [Tooltip("CoreReceiver 또는 Core 종류 EnemyTestTarget. 비우면 씬에서 찾는다")] public MonoBehaviour core;
        [Tooltip("IWaveStateReader 또는 LevelIntegration.WaveDirector. 비우면 씬에서 찾는다")] public MonoBehaviour wave;

        PlayerHealth health; PlayerManaWallet mana; PlayerProgression progression; PlayerMotor motor; PlayerSkillCaster caster;
        EnemyBossInfo boss;
        SkillTreeHUDLink skillTreeLink;
        // 마지막으로 그린 값. NaN이면 아직 그리지 않았다.
        float hp = float.NaN, hpMax, coreValue = float.NaN, coreMax, bossHp = float.NaN, bossMax;
        int mp = -1, mpMax, level = -1, exp, expNeed, waveNumber = -1, remainingEnemies = -1, announcedWave = -1;
        RunState lastWaveState = RunState.Idle;
        readonly float[] slotRemaining = { -1, -1, -1, -1, -1, -1 };
        readonly bool[] slotBlocked = new bool[6];

        void Awake() { if (hud == null) hud = GetComponent<GameHUDController>(); }

        void Start()
        {
            // The link owns only skill slots. Health, mana, XP and encounter HUD remain here.
            skillTreeLink=GetComponent<SkillTreeHUDLink>();
            if(skillTreeLink==null)skillTreeLink=gameObject.AddComponent<SkillTreeHUDLink>();
            if (player == null) { var found = FindAnyObjectByType<PlayerHealth>(); if (found != null) player = found.gameObject; }
            if (player != null)
            {
                health = player.GetComponent<PlayerHealth>(); mana = player.GetComponent<PlayerManaWallet>();
                progression = player.GetComponent<PlayerProgression>(); motor = player.GetComponent<PlayerMotor>();
                caster = player.GetComponent<PlayerSkillCaster>();
                var debug = GetComponent<HUDDebugController>();
                if (debug != null) debug.enabled = false; // 실제 값이 들어오므로 더미를 막는다 (이 Start가 먼저 돈다)
            }
            if (core == null) core = FindCore();
            if (wave == null) wave = FindWave();
            if (wave is WaveDirector director)
            {
                var preview = GetComponent<WaveSpawnPreview>();
                if (preview == null) preview = gameObject.AddComponent<WaveSpawnPreview>();
                preview.Bind(director, GetComponent<MinimapController>());
            }
            if (hud == null) return;
            hud.Minimap.SetVisible(true); // 지도는 같은 오브젝트의 MinimapController가 채운다
            if (player == null) return;
            hud.CoreStatus.SetVisible(core != null);
            hud.Wave.gameObject.SetActive(wave != null);
            hud.BossStatus.HideBoss();
            hud.CombatSkills.F.SetLocked(true);
            hud.CombatSkills.F.SetCooldown(0f, 1f);
            Refresh();
        }

        void LateUpdate() { if (hud != null && player != null) Refresh(); }

        void Refresh()
        {
            RefreshPlayer();
            RefreshSkills();
            RefreshCore();
            RefreshWave();
            RefreshBoss();
        }

        void RefreshPlayer()
        {
            if (health != null && (health.CurrentHealth != hp || health.MaxHealth != hpMax))
                hud.PlayerStatus.SetHealth(hp = health.CurrentHealth, hpMax = health.MaxHealth);
            if (mana != null && (mana.CurrentMana != mp || mana.MaxMana != mpMax))
                hud.PlayerStatus.SetMana(mp = mana.CurrentMana, mpMax = mana.MaxMana);
            if (progression == null) return;
            // 최대 레벨에서는 마지막 구간을 가득 찬 막대로 보인다.
            int need = progression.IsMaxLevel && progression.table != null ? progression.table.ExperienceToNext(progression.Level - 1) : progression.ExperienceToNextLevel;
            int have = progression.IsMaxLevel ? need : progression.ExperienceInLevel;
            if (progression.Level != level) hud.PlayerStatus.SetLevel(level = progression.Level);
            if (have != exp || need != expNeed) hud.PlayerStatus.SetExperience(exp = have, expNeed = need);
        }

        void RefreshSkills()
        {
            if(skillTreeLink!=null && skillTreeLink.OwnsSlots)return;
            if (caster != null)
            {
                Combat(0, hud.CombatSkills.Q, PlayerSkillCaster.Burst);
                Combat(1, hud.CombatSkills.E, PlayerSkillCaster.Vortex);
                Combat(2, hud.CombatSkills.R, PlayerSkillCaster.Storm);
            }
            if (motor == null) return;
            float dash = motor.DashCooldownRemaining;
            if (Changed(3, dash, false)) hud.MovementSkills.Dash.SetCooldown(dash, motor.ActiveDashCooldown);
            // 더블 점프는 쿨타임이 없다: 해금 전이거나 공중에서 다 썼으면 막힘.
            bool jumpBlocked = motor.ExtraAirJumps == 0 || (!motor.IsGrounded && motor.RemainingAirJumps == 0);
            if (Changed(4, 0f, jumpBlocked))
            {
                if (jumpBlocked) hud.MovementSkills.DoubleJump.SetUnavailable();
                else hud.MovementSkills.DoubleJump.SetReady(true);
            }
        }

        void Combat(int index, CombatSkillSlotHUD slot, int skill)
        {
            bool locked = !caster.Unlocked(skill);
            float remaining = locked ? 0f : caster.CooldownRemaining(skill);
            if (!Changed(index, remaining, locked)) return;
            slot.SetLocked(locked);
            slot.SetCooldown(remaining, Mathf.Max(caster.Cooldown(skill), remaining));
        }

        // 숫자는 0.1초 단위로 보이므로 그만큼 바뀌었을 때만 다시 그린다. 0으로 끝나는 순간은 항상 그린다.
        bool Changed(int index, float remaining, bool blocked)
        {
            float shown = Mathf.Ceil(remaining * 10f) / 10f;
            if (shown == slotRemaining[index] && blocked == slotBlocked[index]) return false;
            slotRemaining[index] = shown; slotBlocked[index] = blocked;
            return true;
        }

        void RefreshCore()
        {
            if (core == null) { hud.CoreStatus.HideAttackWarning(); return; }
            float current, max;
            if (core is CoreReceiver receiver) { current = receiver.Current; max = receiver.maximum; }
            else if (core is EnemyTestTarget target) { current = target.CurrentHealth; max = target.maxHealth; }
            else if (core is IHealth reader) { current = reader.CurrentHealth; max = reader.MaxHealth; }
            else return;
            // Initial binding, healing and capacity changes must not look like an attack.
            if (current <= 0f) hud.CoreStatus.HideAttackWarning();
            else if (!float.IsNaN(coreValue) && current < coreValue && max == coreMax)
                hud.CoreStatus.ShowAttackWarning();
            if (current != coreValue || max != coreMax) hud.CoreStatus.SetStability(coreValue = current, coreMax = max);
        }

        void RefreshWave()
        {
            if (wave is not IWaveStateReader reader) return;
            int number = reader.WaveNumber;
            if (number >= 0 && number != waveNumber) hud.Wave.SetWave(waveNumber = number);
            int remaining = reader.AliveEnemyCount + reader.PendingEnemyCount;
            if (remaining != remainingEnemies) hud.Wave.SetRemainingEnemies(remainingEnemies = remaining);

            if (wave is WaveDirector director)
            {
                hud.Wave.SetRemainingEnemiesVisible(director.State == RunState.Running);
                if (director.State == RunState.Running && lastWaveState != RunState.Running && number != announcedWave)
                {
                    announcedWave = number;
                    if (hud.WaveAlert != null) hud.WaveAlert.ShowWave(number);
                }
                lastWaveState = director.State;
            }
        }

        void RefreshBoss()
        {
            var current = EnemyBossInfo.Active.Count > 0 ? EnemyBossInfo.Active[0] : null;
            if (current != boss)
            {
                boss = current;
                bossHp = float.NaN;
                if (boss == null) { hud.BossStatus.HideBoss(); return; }
                hud.BossStatus.ShowBoss(boss.displayName, boss.icon, boss.Health.MaxHealth);
            }
            if (boss == null) return;
            if (boss.Health.CurrentHealth != bossHp || boss.Health.MaxHealth != bossMax)
                hud.BossStatus.SetHealth(bossHp = boss.Health.CurrentHealth, bossMax = boss.Health.MaxHealth);
        }

        internal static MonoBehaviour FindCore()
        {
            var receiver = FindAnyObjectByType<CoreReceiver>();
            if (receiver != null) return receiver;
            foreach (var target in FindObjectsByType<EnemyTestTarget>(FindObjectsSortMode.None))
                if (target.kind == CombatTargetKind.Core) return target;
            return null;
        }

        static MonoBehaviour FindWave() { return FindAnyObjectByType<WaveDirector>(); }
    }
}
