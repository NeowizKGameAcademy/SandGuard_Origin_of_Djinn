using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 피해·사망 알림을 소리로 바꾼다. VfxHitReaction의 소리판이며 같은 오브젝트에 나란히 붙는다.
    /// IDamageEvents.Damaged → HitCue (맞은 위치, 없으면 몸 기준점), ILifeState.Died → DeathCue.
    /// 적·플레이어·벽·타워·코어 공용이고, 대상 종류별 큐는 배선 메뉴가 넣는다.
    /// </summary>
    public sealed class SfxHitReaction : MonoBehaviour
    {
        public SfxCue HitCue;
        public SfxCue DeathCue;
        [Tooltip("피격음을 몸에 붙여 같이 움직이게 한다. 끄면 맞은 자리에 남는다")] public bool AttachHit = true;

        IDamageEvents events; ILifeState life; ICombatTarget target;
        public int HitCount { get; private set; }
        public int DeathCount { get; private set; }

        void Awake()
        {
            events = GetComponent<IDamageEvents>();
            life = GetComponent<ILifeState>();
            target = GetComponent<ICombatTarget>();
        }

        void OnEnable()
        {
            if (events != null) events.Damaged += OnDamaged;
            if (life != null) life.Died += OnDied;
        }

        void OnDisable()
        {
            if (events != null) events.Damaged -= OnDamaged;
            if (life != null) life.Died -= OnDied;
        }

        void OnDamaged(DamageAppliedInfo info)
        {
            HitCount++;
            if (HitCue == null) return;
            if (!HitCue.spatial) { SfxPlayer.Play2D(HitCue); return; }
            if (AttachHit) SfxPlayer.PlayAttached(HitCue, transform);
            else SfxPlayer.Play(HitCue, info.Damage.HitPosition ?? (target != null ? target.HitPosition : transform.position));
        }

        void OnDied(DeathInfo info)
        {
            DeathCount++;
            if (DeathCue == null) return;
            if (!DeathCue.spatial) SfxPlayer.Play2D(DeathCue);
            else SfxPlayer.Play(DeathCue, target != null ? target.HitPosition : transform.position);
        }
    }
}
