using SandGuard.Player;
using UnityEngine;

namespace Tower
{
    public class ManaRecovery : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TargetDetector detector;
        [SerializeField] private PlayerManaWallet player;

        [Header("Mana Recovery")]
        [Min(0)][SerializeField] private int ManaGain = 10;
        [Min(0.01f)][SerializeField] private float GainTick = 1f;

        private bool is_HaveStatus;

        private ICombatTarget owner;
        private float gainTimer;

        private void Awake()
        {
            if (detector == null)
                detector = GetComponentInParent<TargetDetector>();

            owner = GetComponentInParent<ICombatTarget>();
        }

        private void OnEnable()
        {
            gainTimer = 0f;
        }

        private void Update()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerManaWallet>();

            if (detector == null || !detector.isActiveAndEnabled
                || player == null || !player.isActiveAndEnabled
                || (owner is Component component && component != null && !owner.IsTargetable)
                || !detector.Contains(player.transform.position, detector.Range))
            {
                gainTimer = 0f;
                return;
            }

            float interval = GainTick;

            if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0f)
                return;

            gainTimer -= Time.deltaTime;

            if (gainTimer > 0f)
                return;

            gainTimer = interval;
            player.Gain(Mathf.Max(0, ManaGain));
        }
    }
}
