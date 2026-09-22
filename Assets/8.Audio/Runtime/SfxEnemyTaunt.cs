using SandGuard.Enemy;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>Idle/walk 도발. 애니메이션 루프와 독립된 랜덤 간격으로 재생한다.</summary>
    [DisallowMultipleComponent]
    public sealed class SfxEnemyTaunt : MonoBehaviour
    {
        public SfxCue Cue;
        [Min(0.1f)] public float MinDelay = 0.8f;
        [Min(0.1f)] public float MaxDelay = 2.5f;
        public string LocomotionState = "Locomotion";

        EnemyHealth health;
        EnemyBrain brain;
        EnemyVisuals visuals;
        EnemyMeleeAttack attack;
        EnemyFall fall;
        EnemyRestraint restraint;
        AudioListener listener;
        SfxVoice voice;
        float remaining;
        bool wasEligible;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            brain = GetComponent<EnemyBrain>();
            visuals = GetComponent<EnemyVisuals>();
            attack = GetComponent<EnemyMeleeAttack>();
            fall = GetComponent<EnemyFall>();
            restraint = GetComponent<EnemyRestraint>();
        }

        void OnEnable() { wasEligible = false; Schedule(); }
        void OnDisable() { StopVoice(); wasEligible = false; }

        void Schedule() => remaining = Random.Range(Mathf.Max(0.1f, MinDelay), Mathf.Max(0.1f, MinDelay, MaxDelay));

        bool Eligible()
        {
            if (health == null || !health.IsAlive || brain == null || !brain.isActiveAndEnabled || !brain.AIEnabled) return false;
            if (attack != null && attack.IsAttacking || fall != null && fall.IsOffMesh || restraint != null && restraint.IsRestrained) return false;
            var animator = visuals != null ? visuals.Animator : null;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(LocomotionState)) return false;
            return !animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).IsName(LocomotionState);
        }

        // LateUpdate observes this frame's AI decisions and animation transitions.
        void LateUpdate()
        {
            if (!Eligible())
            {
                StopVoice();
                if (wasEligible) Schedule();
                wasEligible = false;
                return;
            }
            wasEligible = true;
            if (Time.deltaTime <= 0f || OwnsVoice()) return;
            remaining -= Time.deltaTime;
            if (remaining > 0f) return;
            Schedule();
            if (Cue == null || !Cue.HasClips) return;
            if (listener == null || !listener.isActiveAndEnabled) listener = FindFirstObjectByType<AudioListener>();
            if (listener == null || !listener.isActiveAndEnabled ||
                (listener.transform.position - transform.position).sqrMagnitude > Cue.maxDistance * Cue.maxDistance) return;
            // Skip a crowded interval instead of stealing/cutting an existing taunt.
            if (SfxPlayer.Exists && SfxPlayer.Instance.ActiveVoiceCountFor(Cue) >= Cue.maxVoices) return;
            voice = SfxPlayer.PlayAttached(Cue, transform);
        }

        bool OwnsVoice() => voice != null && voice.Active && voice.Follow == transform && voice.Cue == Cue;

        void StopVoice()
        {
            if (OwnsVoice()) SfxPlayer.Stop(voice);
            voice = null;
        }
    }
}
