using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>Upper-body pose, palm aiming and recoil. Gameplay owns shot timing and damage.</summary>
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerSpellcasting : MonoBehaviour
    {
        public enum CastStyle { Magic, GreatSwordSpell, SwordShieldSpell }
        public CastStyle castStyle;
        public string layerName = "Upper Body Casting";
        public Transform firePoint;
        public Renderer palmFlash;
        public Light palmLight;
        [Min(.01f)] public float raiseDuration = .12f;
        [Min(.01f)] public float lowerDuration = .2f;
        [Min(0f)] public float poseHoldDuration = .2f;
        [Min(.01f)] public float recoilDuration = .18f;
        [Min(0f)] public float recoilDistance = .09f;
        [Range(.5f, 1f)] public float armExtension = .88f;
        [Tooltip("Hand-local rotation whose forward points out of the palm and up follows the fingers.")]
        public Quaternion palmBasis = Quaternion.identity;
        public float Weight { get; private set; }
        /// <summary>true면 조준 자세·IK를 쉰다(관통탄 양팔 동작 중). 손바닥 플래시는 그대로 낸다.</summary>
        public bool Suppressed { get; set; }
        public bool UsesRightHand => requested || Weight > .001f;
        public bool ReadyToFire => requested && raisedTime >= raiseDuration && Weight >= .95f && poseFrame == Time.frameCount;
        public Vector3 AimDirection { get; private set; }
        Animator animator;
        PlayerBasicAttack attack;
        PlayerLampEquipment lamp;
        Transform hand, upperArm, lowerArm;
        int layer = -1, poseFrame = -1;
        bool requested;
        float raisedTime, holdTime, recoilTime = 100f, flashTime;
        Quaternion aimedHandRotation;
        bool hasPackMotion;

        void Awake()
        {
            animator = GetComponent<Animator>();
            attack = GetComponentInParent<PlayerBasicAttack>();
            lamp = GetComponent<PlayerLampEquipment>();
            if (!animator.isHuman) { enabled = false; return; }
            layer = animator.GetLayerIndex(layerName);
            foreach (var parameter in animator.parameters)
                if (parameter.name == "CastPhase" && parameter.type == AnimatorControllerParameterType.Float) hasPackMotion = true;
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (layer < 0 || hand == null || firePoint == null) { enabled = false; return; }
            SetFlash(false);
        }

        public void SetCasting(bool value) => requested = value;

        public void PlayFire()
        {
            holdTime = poseHoldDuration;
            recoilTime = 0f;
            flashTime = .065f;
            SetFlash(true);
        }

        /// <summary>자세 없이 손바닥 플래시만(관통탄 발사: 자세는 Pierce Casting 레이어가 맡는다).</summary>
        public void Flash() { flashTime = .065f; SetFlash(true); }

        void Update()
        {
            if (layer < 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            holdTime = Mathf.Max(0, holdTime - dt);
            recoilTime += dt;
            flashTime = Mathf.Max(0, flashTime - dt);
            SetFlash(flashTime > 0);
            raisedTime = requested && !Suppressed ? raisedTime + dt : 0f;
            bool active = !Suppressed && (requested || holdTime > 0f);
            Weight = Mathf.MoveTowards(Weight, active ? 1f : 0f, dt / (active ? raiseDuration : lowerDuration));
            animator.SetLayerWeight(layer, Mathf.SmoothStep(0, 1, Weight));
            animator.SetBool("Casting", UsesRightHand);
            if (hasPackMotion)
            {
                animator.SetInteger("CastStyle", (int)castStyle);
                // Scrub the useful preparation/release segment; retain the existing 0.3s cadence.
                float start = castStyle == CastStyle.Magic ? .22f : .08f;
                float release = castStyle == CastStyle.GreatSwordSpell ? .46f : .40f;
                float kick = recoilTime < recoilDuration ? Mathf.Sin(Mathf.PI * recoilTime / recoilDuration) : 0f;
                float phase = Mathf.Lerp(start, release, Mathf.Clamp01(raisedTime / raiseDuration));
                if (!requested && holdTime > 0f) phase = release;
                animator.SetFloat("CastPhase", phase - kick * .10f);
            }
            if (lamp != null) lamp.SetHandSuppressed(UsesRightHand);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != layer || Weight <= 0f || Suppressed || attack == null || attack.aimer == null) return;
            Transform owner = attack.transform;
            Vector3 direction = (attack.aimer.GetAimPoint() - upperArm.position).normalized;
            // Avoid an elbow folding backwards while the body catches up with a fast camera turn.
            direction = Vector3.RotateTowards(owner.forward, direction, 80f * Mathf.Deg2Rad, 0f).normalized;
            AimDirection = direction;
            float length = Vector3.Distance(upperArm.position, lowerArm.position) + Vector3.Distance(lowerArm.position, hand.position);
            float kick = recoilTime < recoilDuration ? Mathf.Sin(Mathf.PI * recoilTime / recoilDuration) : 0f;
            Vector3 target = upperArm.position + direction * (length * armExtension - recoilDistance * kick);
            float weight = Mathf.SmoothStep(0, 1, Weight);
            if (hasPackMotion)
                weight *= Mathf.Lerp(.25f, 1f, Mathf.Clamp01(raisedTime / raiseDuration)) * (1f - .65f * kick);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            // Humanoid IK goal axes are not necessarily the imported hand bone's axes.
            // Aim the actual wrist after the positional solve, before the bolt is released.
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKPosition(AvatarIKGoal.RightHand, target);
            aimedHandRotation = Quaternion.LookRotation(direction, owner.up) * Quaternion.Inverse(palmBasis);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, upperArm.position + owner.right * .4f - owner.up * .45f + direction * .15f);
            poseFrame = Time.frameCount;
        }

        void LateUpdate()
        {
            if (poseFrame != Time.frameCount || Time.deltaTime <= 0f) return;
            hand.rotation = Quaternion.Slerp(hand.rotation, aimedHandRotation, Mathf.SmoothStep(0, 1, Weight));
        }

        void SetFlash(bool value)
        {
            if (palmFlash != null) palmFlash.enabled = value;
            if (palmLight != null) palmLight.enabled = value;
        }

        void OnDisable()
        {
            Cancel();
        }

        public void Cancel()
        {
            requested = false; raisedTime = holdTime = Weight = 0f; poseFrame = -1;
            if (animator != null && layer >= 0) { animator.SetLayerWeight(layer, 0f); animator.SetBool("Casting", false); }
            if (lamp != null) lamp.SetHandSuppressed(false);
            SetFlash(false);
        }
    }
}
