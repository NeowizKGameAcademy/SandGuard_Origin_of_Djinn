using UnityEngine;
using UnityEngine.InputSystem;

namespace SandGuard.Player
{
    /// <summary>Preview scene only. Leaves the player's input actions and movement tuning intact.</summary>
    public sealed class PlayerArtPreviewControls : MonoBehaviour
    {
        PlayerLampEquipment lamp;
        PlayerMotor motor;
        bool walking;
        float normalSpeed;
        void Start()
        {
            motor = FindFirstObjectByType<PlayerMotor>();
            if (motor != null) normalSpeed = motor.moveSpeed;
        }
        void Update()
        {
            if (lamp == null) lamp = FindFirstObjectByType<PlayerLampEquipment>();
            if (Keyboard.current == null || Time.timeScale <= 0 || motor == null || motor.input == null || !motor.input.AcceptsInput) return;
            if (Keyboard.current.lKey.wasPressedThisFrame && lamp != null) lamp.SetHeld(!lamp.RequestedHeld);
            if (Keyboard.current.gKey.wasPressedThisFrame && lamp != null) lamp.SetGlowing(!lamp.IsGlowing);
            var casting = motor.GetComponent<PlayerVisuals>()?.Spellcasting;
            if (Keyboard.current.bKey.wasPressedThisFrame && casting != null)
                casting.castStyle = (PlayerSpellcasting.CastStyle)(((int)casting.castStyle + 1) % 3);
            if (Keyboard.current.kKey.wasPressedThisFrame)
                motor.GetComponent<PlayerHealth>()?.TakeDamage(new DamageInfo(10f, "Enemy"));
            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                walking = !walking;
                motor.moveSpeed = walking ? 2f : normalSpeed;
            }
        }
        void OnGUI()
        {
            var casting = motor != null ? motor.GetComponent<PlayerVisuals>()?.Spellcasting : null;
            string style = casting != null ? casting.castStyle.ToString() : "None";
            GUI.Box(new Rect(12, Screen.height - 100, 580, 88), "Character preview: WASD + mouse | V: walk/run\nL: lamp belt/hand | G: lamp light | Esc: release cursor\nB: casting style (" + style + ") | K: test hit (10 damage)");
        }
    }
}
