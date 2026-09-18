using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 체력이 EnterRatio 아래로 떨어지면 루프(심장 박동)를 켜고, ExitRatio 위로 회복하거나 죽으면 끈다.
    /// 두 경계를 떨어뜨려 두어(20%에 켜고 25%에 끔) 경계에서 켜졌다 꺼졌다 반복하지 않는다. 같은 오브젝트(없으면 자식)의 IHealth를 본다.
    /// </summary>
    public sealed class SfxLowHealthLoop : MonoBehaviour
    {
        public SfxCue Cue;
        [Range(0f, 1f)] public float EnterRatio = 0.2f;
        [Range(0f, 1f)] public float ExitRatio = 0.25f;

        public bool Active { get; private set; }
        IHealth health; ILifeState life; SfxVoice voice;

        void Awake()
        {
            health = GetComponent<IHealth>() ?? GetComponentInChildren<IHealth>(true);
            life = GetComponent<ILifeState>() ?? GetComponentInChildren<ILifeState>(true);
        }

        void OnDisable() => SetActive(false);

        void Update()
        {
            if (health == null || health.MaxHealth <= 0f) return;
            bool alive = life == null || life.State == LifeState.Alive;
            float ratio = health.CurrentHealth / health.MaxHealth;
            if (!alive) SetActive(false);
            else if (!Active && ratio > 0f && ratio < EnterRatio) SetActive(true);
            else if (Active && ratio >= ExitRatio) SetActive(false);
        }

        void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;
            if (on) { if (Cue != null && Application.isPlaying) voice = SfxPlayer.PlayLoop(Cue, transform); }
            else if (voice != null) { SfxPlayer.Stop(voice); voice = null; }
        }
    }
}
