using UnityEngine;

namespace Tower
{
    /// <summary>풀에서 꺼낸 스켈레톤 한 마리의 소환 슬롯과 귀환 지점을 관리한다.</summary>
    public sealed class SkeletonController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TowerStatus status;
        [SerializeField] private SkeletonMovement movement;
        [SerializeField] private SkeletonHealth health;
        [SerializeField] private Animator animator;

        [Header("Animation")]
        [Min(0f)] [SerializeField] private float deathDespawnDelay = 5f;

        private SummonManager owner;
        private int summonIndex = -1;
        private bool initialized;
        private bool dying;

        private static readonly int IsMoving = Animator.StringToHash("isMoving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Die = Animator.StringToHash("Die");

        public CoffinTowerConfig Config => status != null ? status.coffin : null;
        public bool IsInitialized => initialized && !dying;

        private void Awake()
        {
            if (status == null) status = GetComponentInParent<TowerStatus>();
            if (movement == null) TryGetComponent(out movement);
            if (health == null) TryGetComponent(out health);
            if (animator == null) TryGetComponent(out animator);
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        public void Initialize(SummonManager summonOwner, int index, Vector3 point)
        {
            owner = summonOwner;
            summonIndex = index;
            initialized = true;
            dying = false;
            if (status == null) status = GetComponentInParent<TowerStatus>();
            ResetAnimation();
            movement?.Initialize(this, point);
            health?.ResetForSpawn(Config != null ? Config.maxHP : 1f);
        }

        public void SetMoving(bool value)
        {
            if (animator != null && !dying)
                animator.SetBool(IsMoving, value);
        }

        public void TriggerAttack()
        {
            if (animator != null && !dying)
                animator.SetTrigger(Attack);
        }

        public void Despawn()
        {
            initialized = false;
            dying = false;
            owner = null;
            summonIndex = -1;
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private void OnDied(DeathInfo _)
        {
            if (!initialized || dying) return;
            dying = true;
            if (animator != null)
            {
                animator.SetBool(IsMoving, false);
                animator.ResetTrigger(Attack);
                animator.SetTrigger(Die);
            }
            StartCoroutine(DespawnAfterDeath());
        }

        private System.Collections.IEnumerator DespawnAfterDeath()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, deathDespawnDelay));

            var manager = owner;
            int index = summonIndex;
            initialized = false;
            dying = false;
            owner = null;
            summonIndex = -1;
            gameObject.SetActive(false);
            manager?.NotifyDeath(index, this);
        }

        private void ResetAnimation()
        {
            if (animator == null) return;
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool(IsMoving, false);
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Die);
        }
    }
}
