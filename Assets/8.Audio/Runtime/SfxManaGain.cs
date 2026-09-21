using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 마나가 늘어날 때마다 원샷. 기획상 회복은 코어와 오벨리스크 타워 근처에서만,
    /// 1~1.5초 간격의 틱으로 들어온다. 두 곳이 겹치는 자리에서 같은 순간에 두 번 울리지 않도록
    /// MinInterval로 솎아 낸다. 램프 반짝임(PlayerLampEquipment)과 같은 신호를 듣는다.
    /// </summary>
    public sealed class SfxManaGain : MonoBehaviour
    {
        public SfxCue Cue;
        [Min(0f), Tooltip("이 시간 안의 회복은 소리를 내지 않는다. 회복 틱(1초)보다 조금 짧게")]
        public float MinInterval = 0.8f;

        /// <summary>진단용: 실제로 낸 소리 수.</summary>
        public int Played { get; private set; }
        IManaReader mana;
        ILifeState life;
        float lastTime;

        void OnEnable()
        {
            // 스폰 직후 지갑이 가득 차는 것은 회복이 아니다
            lastTime = Time.time;
            mana = GetComponentInParent<IManaReader>();
            life = GetComponentInParent<ILifeState>();
            if (mana != null) mana.Changed += OnManaChanged;
            if (life != null) life.StateChanged += OnLifeChanged;
        }

        void OnDisable()
        {
            if (mana != null) mana.Changed -= OnManaChanged;
            if (life != null) life.StateChanged -= OnLifeChanged;
        }

        /// <summary>부활 직후의 일괄 회복은 부활음이 맡는다. 살아나는 순간부터 한 템포 쉬었다가 다시 듣는다.</summary>
        void OnLifeChanged(LifeStateChangedInfo info) { if (info.CurrentState == LifeState.Alive) lastTime = Time.time; }

        void OnManaChanged(ManaChangedInfo info)
        {
            if (info.CurrentMana <= info.PreviousMana || Cue == null) return;
            if (Time.time - lastTime < MinInterval) return;
            lastTime = Time.time;
            Played++;
            if (Cue.spatial) SfxPlayer.Play(Cue, transform.position);
            else SfxPlayer.Play2D(Cue);
        }
    }
}
