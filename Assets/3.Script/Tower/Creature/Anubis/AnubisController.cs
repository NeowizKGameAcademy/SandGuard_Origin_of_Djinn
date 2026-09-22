using UnityEngine;

namespace Tower
{
    public sealed class AnubisController : MonoBehaviour
    {
        [SerializeField] private TowerStatus status;
        [SerializeField] private TargetSelector selector;
        [SerializeField] private AnubisSkill skill;
        [SerializeField] private AnubisHealth health;
        [SerializeField] private Animator animator;
        [Min(0f)] [SerializeField] private float stopDistance = 1.5f;
        [Min(0f)] [SerializeField] private float deathDespawnDelay = 3f;

        private float attackTimer;
        private float knockbackTimer;
        private bool dying;

        private static readonly int IsMoving = Animator.StringToHash("isMoving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int UseSkill = Animator.StringToHash("UseSkill");
        private static readonly int Die = Animator.StringToHash("Die");

        public AnubisTowerConfig Config => status != null ? status.anubis : null;

        private void Awake()
        {
            if (status == null) 
                status = GetComponentInParent<TowerStatus>();

            if (selector == null) 
                TryGetComponent(out selector);

            if (skill == null) 
                TryGetComponent(out skill);

            if (health == null) 
                TryGetComponent(out health);

            if (animator == null)
                TryGetComponent(out animator);
        }

        private void Update()
        {
            if (Config == null || dying)
                return;

            knockbackTimer -= Time.deltaTime;

            var target = selector != null ? selector.Target : null;

            if (target == null)
            {
                SetMoving(false);
                ReturnToSpawnPoint();
                return;
            }

            Vector3 targetPosition = target.HitPosition;
            targetPosition.y = transform.position.y;
            Vector3 direction = targetPosition - transform.position;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Config.rotateSpeed * Time.deltaTime);

            if (direction.sqrMagnitude > stopDistance * stopDistance)
            {
                SetMoving(true);
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, Config.moveSpeed * Time.deltaTime);
                return;
            }

            SetMoving(false);

            attackTimer -= Time.deltaTime;

            if (attackTimer > 0f || target.DamageReceiver == null) 
                return;

            attackTimer = Config.attackInterval;

            target.DamageReceiver.TakeDamage(new DamageInfo(Config.attack, "Ally", null, "tower.anubis", target.HitPosition, direction.normalized));

            if (knockbackTimer > 0f)
            {
                animator?.SetTrigger(Attack);
                return;
            }

            animator?.SetTrigger(UseSkill);
            skill?.Cast(transform.position, transform.forward);
            knockbackTimer = Config.knockBackCooldown;
        }

        private void ReturnToSpawnPoint()
        {
            Transform spawnPoint = Config.spawnPoint;
            if (spawnPoint == null)
                return;

            Vector3 destination = spawnPoint.position;
            destination.y = transform.position.y;
            Vector3 direction = destination - transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                SetMoving(true);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction), Config.rotateSpeed * 5f * Time.deltaTime);
            }
            else
            {
                SetMoving(false);
            }

            transform.position = Vector3.MoveTowards(transform.position, destination,
                Config.moveSpeed * 2f * Time.deltaTime);
        }

        public void Died()
        {
            if (dying) return;
            dying = true;
            SetMoving(false);
            animator?.SetTrigger(Die);
            StartCoroutine(DespawnAfterDeath());
        }

        private System.Collections.IEnumerator DespawnAfterDeath()
        {
            yield return new WaitForSeconds(deathDespawnDelay);
            GetComponentInParent<AnubisSummoner>()?.Gone();
            Destroy(gameObject);
        }

        private void SetMoving(bool value)
        {
            if (animator != null) animator.SetBool(IsMoving, value);
        }
    }
}
