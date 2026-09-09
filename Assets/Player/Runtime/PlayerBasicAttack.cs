using UnityEngine;

namespace SandGuard.Player
{
    // Release after the spell visual has applied its final wrist rotation in LateUpdate.
    [DefaultExecutionOrder(200)]
    public sealed class PlayerBasicAttack : MonoBehaviour
    {
        public PlayerInputReader input;
        [Tooltip("ILifeState 및 ICombatTarget을 제공하는 컴포넌트")]
        public MonoBehaviour lifeSource;
        public PlayerAimer aimer;
        public PlayerVisuals visuals;
        [Tooltip("발사하면 잠시 카메라 정면을 보게 한다. 비워도 된다")]
        public PlayerMotor motor;
        public PlayerProjectile projectilePrefab;
        [Tooltip("모델의 발사 위치가 벽을 넘어갔는지 확인하는 몸통 기준점")]
        public Transform shotOrigin;
        [Min(0.02f)] public float attackInterval = 0.3f;
        [Min(0f)] public float damage = 10f;
        public string factionId = "Ally";
        public bool CombatEnabled { get; set; } = true;
        public float CooldownRemaining { get; private set; }
        bool pendingShot;
        PlayerSpellcasting Casting => visuals != null && visuals.Spellcasting != null && visuals.Spellcasting.isActiveAndEnabled
            ? visuals.Spellcasting : null;

        void OnEnable() { if (input != null) input.PrimaryActionPressed += RequestShot; }
        void OnDisable()
        {
            if (input != null) input.PrimaryActionPressed -= RequestShot;
            pendingShot = false;
            if (Casting != null) Casting.SetCasting(false);
        }

        void RequestShot()
        {
            if (Available() && CooldownRemaining <= 0f) pendingShot = true;
        }

        void Update()
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            if (!Available()) pendingShot = false;
            bool wantsShot = Available() && (pendingShot || (input != null && input.PrimaryAttackHeld));
            if (Casting != null) Casting.SetCasting(wantsShot);
            if (wantsShot && motor != null) motor.FaceCamera();
        }

        void LateUpdate()
        {
            if (pendingShot || (input != null && input.PrimaryAttackHeld)) TryFire();
        }

        bool Available() => isActiveAndEnabled && CombatEnabled && Time.timeScale > 0f
            && (input == null || input.AcceptsInput)
            && (lifeSource == null || (lifeSource as ILifeState)?.State == global::LifeState.Alive)
            && projectilePrefab != null && aimer != null && visuals != null && visuals.FirePoint != null
            && !string.IsNullOrWhiteSpace(factionId);

        /// <summary>Returns true only when a bolt is released. A casting visual queues the shot during its short windup.</summary>
        public bool TryFire()
        {
            if (!Available() || CooldownRemaining > 0f) return false;
            if (Casting != null)
            {
                pendingShot = true;
                Casting.SetCasting(true);
                if (!Casting.ReadyToFire) return false;
            }
            Vector3 muzzle = visuals.FirePoint.position;
            Vector3 aim = aimer.GetAimPoint();
            Vector3 direction = (aim - muzzle).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            Vector3 origin = shotOrigin != null ? shotOrigin.position : transform.position + Vector3.up;
            PlayerProjectile projectile = Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(direction));
            projectile.Launch(transform, (lifeSource as ICombatTarget)?.FactionId ?? factionId, damage, direction, origin);
            CooldownRemaining = attackInterval;
            pendingShot = false;
            if (motor != null) motor.FaceCamera();
            visuals.PlayFire();
            return true;
        }
    }
}
