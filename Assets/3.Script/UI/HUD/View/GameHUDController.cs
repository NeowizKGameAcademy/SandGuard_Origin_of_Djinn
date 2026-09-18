using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class GameHUDController : MonoBehaviour
    {
        [SerializeField] private PlayerStatusHUD playerStatus;
        [SerializeField] private CoreStatusHUD coreStatus;
        [SerializeField] private BossStatusHUD bossStatus;
        [SerializeField] private WaveHUD wave;
        [SerializeField] private WaveAlertHUD waveAlert;
        [SerializeField] private CombatSkillHUD combatSkills;
        [SerializeField] private MovementSkillHUD movementSkills;
        [SerializeField] private MinimapHUD minimap;
        public PlayerStatusHUD PlayerStatus => playerStatus;
        public CoreStatusHUD CoreStatus => coreStatus;
        public BossStatusHUD BossStatus => bossStatus;
        public WaveHUD Wave => wave;
        public WaveAlertHUD WaveAlert => waveAlert;
        public CombatSkillHUD CombatSkills => combatSkills;
        public MovementSkillHUD MovementSkills => movementSkills;
        public MinimapHUD Minimap => minimap;
    }
}
