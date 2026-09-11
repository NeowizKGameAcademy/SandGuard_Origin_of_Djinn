using UnityEngine;
using UnityEngine.InputSystem;

namespace SandGuard.Player
{
    /// <summary>테스트용 안내. 실제 HUD가 연결되면 제거할 수 있다.</summary>
    [DefaultExecutionOrder(-400)]
    public sealed class PlayerDemoOverlay : MonoBehaviour
    {
        public PlayerMotor motor;
        [Tooltip("씬에 PlayerCrosshair가 있으면 이 임시 + 표시는 그리지 않는다")]
        public bool showCrosshair = true;
        bool hasCrosshairComponent;
        PlayerSkillCaster caster;
        void Start()
        {
            hasCrosshairComponent = FindAnyObjectByType<PlayerCrosshair>() != null;
            if (motor != null) caster = motor.GetComponent<PlayerSkillCaster>();
        }
        string SkillStatus()
        {
            if (caster == null) return "Q/E/R skills: no caster";
            string One(string key, int slot) => !caster.Unlocked(slot) ? key + " locked"
                : caster.CooldownRemaining(slot) > 0f ? $"{key} {caster.CooldownRemaining(slot):F1}s" : key + " ready";
            return $"{One("Q Burst", PlayerSkillCaster.Burst)} | {One("E Vortex", PlayerSkillCaster.Vortex)} | {One("R Storm", PlayerSkillCaster.Storm)}";
        }
        void Update()
        {
            if (motor == null || motor.input == null) return;
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1, -1);
            motor.input.SuppressCursorCapture = new Rect(16, 16, 490, 190).Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }
        void OnDisable() { if (motor != null && motor.input != null) motor.input.SuppressCursorCapture = false; }
        void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 490, 190), "SandGuard | Player Test");
            GUI.Label(new Rect(28, 40, 430, 24), "WASD Move | Mouse Look | Space Jump (x2, hold on ground: Updraft) | LMB Fire");
            GUI.Label(new Rect(28, 64, 460, 24), "Shift Dash (10 MP) | Q/E/R Skills (if unlocked) | Esc Release Cursor");
            if (motor != null)
            {
                IPlayerMobilityReader mobility = motor;
                var state = mobility.State;
                var health = motor.lifeSource as IHealth;
                var life = motor.lifeSource as ILifeState;
                var mana = motor.manaSource as IManaReader;
                GUI.Label(new Rect(28, 88, 460, 24), $"Air jumps: {state.RemainingAirJumps} | Dash: {state.DashCooldownRemaining:F1}s");
                GUI.Label(new Rect(28, 112, 460, 24), $"HP: {health?.CurrentHealth:F0}/{health?.MaxHealth:F0} | MP: {mana?.CurrentMana}/{mana?.MaxMana}");
                GUI.Label(new Rect(28, 136, 460, 24), life?.State == LifeState.Incapacitated ? "Incapacitated - respawning at the spawn point." : SkillStatus());
                GUI.enabled = Cursor.lockState != CursorLockMode.Locked && Time.timeScale > 0f;
                if (GUI.Button(new Rect(28, 164, 160, 28), "Test: Take 25 damage"))
                    (motor.lifeSource as IDamageable)?.TakeDamage(new DamageInfo(25f, "Enemy", causeId: "test.button"));
                if (GUI.Button(new Rect(200, 164, 160, 28), "Test: Refill mana"))
                    (motor.manaSource as IManaWallet)?.Gain(mana?.MaxMana ?? 0);
                GUI.enabled = true;
            }
            if (showCrosshair && !hasCrosshairComponent) GUI.Label(new Rect(Screen.width / 2f - 5f, Screen.height / 2f - 10f, 20f, 24f), "+");
        }
    }
}
