using UnityEngine;

namespace Tower
{
    [DisallowMultipleComponent]
    public class SlowArea : TowerAreaEffect
    {
        protected override bool HasConfig => Status.obelisk != null;
        protected override float TickInterval => Status.obelisk.tickInterval;

        protected override void Awake()
        {
            base.Awake();

            SyncRadius();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SyncRadius();
        }

        private void LateUpdate()
        {
            SyncRadius();
        }

        protected override void ApplyEffect(ICombatTarget target, Collider other, float interval)
        {
            float ratio = Status.obelisk.slowRatio;

            if (float.IsNaN(ratio) || float.IsInfinity(ratio))
                return;

            var slowable = other.GetComponentInParent<ISlowable>();

            if (slowable == null)
                return;

            // SlowRatio는 남는 속도 비율, ISlowable은 감소 비율을 받는다.
            slowable.Slow(1f - Mathf.Clamp01(ratio), interval * 2f + 0.05f);
        }

        private void SyncRadius()
        {
            if (Status == null || Area is not CapsuleCollider slowCollider)
                return;

            slowCollider.radius = Mathf.Max(0f, Status.detectRange);
        }
    }
}
