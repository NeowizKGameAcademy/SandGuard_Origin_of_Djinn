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
        [Tooltip("피격 음성(신음). 타격음과 함께 난다")] public SfxCue HitVoiceCue;
        [Tooltip("사망 음성. 사망음과 함께 난다")] public SfxCue DeathVoiceCue;
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
            Vector3 at = info.Damage.HitPosition ?? (target != null ? target.HitPosition : transform.position);
            PlayHit(HitCue, at);
            PlayHit(HitVoiceCue, at);
        }

        void PlayHit(SfxCue cue, Vector3 at)
        {
            if (cue == null) return;
            if (!cue.spatial) SfxPlayer.Play2D(cue);
            else if (AttachHit) SfxPlayer.PlayAttached(cue, transform);
            else SfxPlayer.Play(cue, at);
        }

        void OnDied(DeathInfo info)
        {
            DeathCount++;
            PlayAt(DeathCue); PlayAt(DeathVoiceCue);
        }

        void PlayAt(SfxCue cue)
        {
            if (cue == null) return;
            if (!cue.spatial) SfxPlayer.Play2D(cue);
            else SfxPlayer.Play(cue, target != null ? target.HitPosition : transform.position);
        }
    }
}
