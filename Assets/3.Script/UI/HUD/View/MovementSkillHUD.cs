using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class MovementSkillHUD : MonoBehaviour
    {
        [SerializeField] private MovementSkillSlotHUD dash;
        [SerializeField] private MovementSkillSlotHUD doubleJump;
        public MovementSkillSlotHUD Dash => dash;
        public MovementSkillSlotHUD DoubleJump => doubleJump;
    }
}
