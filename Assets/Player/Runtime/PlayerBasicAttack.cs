using UnityEngine;

namespace SandGuard.Player
{
    [DefaultExecutionOrder(100)]
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

        void LateUpdate()
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            IPlayerInput controls = input;
            if (controls != null && controls.PrimaryAttackHeld) TryFire();
        }

        public bool TryFire()
        {
            if (lifeSource != null && (lifeSource as ILifeState)?.State != global::LifeState.Alive) return false;
            if (!CombatEnabled || Time.timeScale <= 0f || CooldownRemaining > 0f || projectilePrefab == null
                || aimer == null || visuals == null || visuals.FirePoint == null || string.IsNullOrWhiteSpace(factionId)) return false;
            Vector3 muzzle = visuals.FirePoint.position;
            Vector3 aim = aimer.GetAimPoint();
            Vector3 direction = (aim - muzzle).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            Vector3 origin = shotOrigin != null ? shotOrigin.position : transform.position + Vector3.up;
            PlayerProjectile projectile = Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(direction));
            projectile.Launch(transform, (lifeSource as ICombatTarget)?.FactionId ?? factionId, damage, direction, origin);
            CooldownRemaining = attackInterval;
            if (motor != null) motor.FaceCamera();
            visuals.PlayFire();
            return true;
        }
    }
}
