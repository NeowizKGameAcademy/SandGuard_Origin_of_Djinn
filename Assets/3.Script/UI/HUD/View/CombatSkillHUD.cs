using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class CombatSkillHUD : MonoBehaviour
    {
        [SerializeField] private CombatSkillSlotHUD q;
        [SerializeField] private CombatSkillSlotHUD e;
        [SerializeField] private CombatSkillSlotHUD r;
        [SerializeField] private CombatSkillSlotHUD f;
        public CombatSkillSlotHUD Q => q;
        public CombatSkillSlotHUD E => e;
        public CombatSkillSlotHUD R => r;
        public CombatSkillSlotHUD F => f;
    }
}
