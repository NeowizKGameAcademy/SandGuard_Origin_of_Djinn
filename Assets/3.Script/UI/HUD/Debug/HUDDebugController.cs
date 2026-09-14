using UnityEngine;

namespace SandGuard.UI.HUD
{
    [ExecuteAlways]
    public sealed class HUDDebugController : MonoBehaviour
    {
        [SerializeField] private GameHUDController hud;
        [SerializeField] private bool dummyMode = true;
        [SerializeField] private int level = 12;
        [SerializeField] private float hp = 320f, maxHP = 320f, experience = 380f, requiredExperience = 1000f, mana = 120f, maxMana = 120f;
        [SerializeField] private float coreStability = 70f, maxCoreStability = 100f;
        [SerializeField] private bool showBoss = true;
        [SerializeField] private string bossName = "사막의 수호자";
        [SerializeField] private float bossHP = 7500f, bossMaxHP = 10000f;
        [SerializeField] private int wave = 3;
        [SerializeField] private float qCooldown, eCooldown = 2.4f, rCooldown, fCooldown = 6.2f;
        [SerializeField] private float dashCooldown = 4.2f, doubleJumpCooldown = 1.8f;

        private void Start() { if (Application.isPlaying && dummyMode) Apply(); }
        private void OnValidate() { if (!Application.isPlaying && dummyMode) Apply(); }
        [ContextMenu("Apply Dummy Data")]
        public void Apply()
        {
            if (hud == null) return;
            hud.PlayerStatus.SetLevel(level);
            hud.PlayerStatus.SetHealth(hp, maxHP);
            hud.PlayerStatus.SetExperience(experience, requiredExperience);
            hud.PlayerStatus.SetMana(mana, maxMana);
            hud.CoreStatus.SetStability(coreStability, maxCoreStability);
            hud.Wave.SetWave(wave);
            if (showBoss) { hud.BossStatus.ShowBoss(bossName, null, bossMaxHP); hud.BossStatus.SetHealth(bossHP, bossMaxHP); }
            else hud.BossStatus.HideBoss();
            hud.CombatSkills.Q.SetCooldown(qCooldown, Mathf.Max(qCooldown, 8f));
            hud.CombatSkills.E.SetCooldown(eCooldown, 8f);
            hud.CombatSkills.R.SetCooldown(rCooldown, Mathf.Max(rCooldown, 8f));
            hud.CombatSkills.F.SetCooldown(fCooldown, 10f);
            hud.MovementSkills.Dash.SetCooldown(dashCooldown, 6f);
            hud.MovementSkills.DoubleJump.SetCooldown(doubleJumpCooldown, 3f);
        }
    }
}
