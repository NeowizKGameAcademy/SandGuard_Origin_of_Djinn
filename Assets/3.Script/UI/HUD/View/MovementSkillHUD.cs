using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class MovementSkillHUD : MonoBehaviour
    {
        [SerializeField] private MovementSkillSlotHUD dash;
        [SerializeField] private MovementSkillSlotHUD doubleJump;
        [SerializeField] private MovementSkillSlotHUD recall;
        public MovementSkillSlotHUD Dash => dash;
        public MovementSkillSlotHUD DoubleJump => doubleJump;
        /// <summary>Mouse2 (right click) slot. Null on HUD prefabs built before the recall slot existed.</summary>
        public MovementSkillSlotHUD Recall => recall;
    }
}
