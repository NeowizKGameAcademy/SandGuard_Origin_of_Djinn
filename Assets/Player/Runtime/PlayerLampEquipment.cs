using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>Independent lamp attachment, bounded sway and state-driven emission.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerLampEquipment : MonoBehaviour
    {
        public Transform lamp;
        public Transform beltSocket;
        public Transform handSocket;
        public Renderer lampRenderer;
        public Light lampLight;
        public int emissiveMaterialIndex;
        public Vector3 handGripOffset;
        public Vector3 heldEuler = new Vector3(0, 90, 0);
        public Vector3 palmOffset = new Vector3(0f, 0f, 0.065f);
        public Color glowColor = new Color(1f, 0.36f, 0.035f);
        [Min(0)] public float glowStrength = 3f;
        [Range(0, 25)] public float swayDegrees = 10f;
        [SerializeField] bool held;
        [SerializeField] bool glowing;
        public bool IsHeld => held && !handSuppressed;
        public bool RequestedHeld => held;
        public bool IsGlowing => glowing;
        Animator animator;
        PlayerMotor motor;
        MaterialPropertyBlock block;
        Vector3 sway, swayVelocity;
        float phase, handWeight;
        bool handSuppressed;
        PlayerSpellcasting spellcasting;

        void Awake()
        {
            animator = GetComponent<Animator>();
            motor = GetComponentInParent<PlayerMotor>();
            spellcasting = GetComponent<PlayerSpellcasting>();
            block = new MaterialPropertyBlock();
            SetHeld(held);
            SetGlowing(glowing);
        }

        public void SetHeld(bool value)
        {
            held = value;
            Attach();
        }

        public void SetHandSuppressed(bool value)
        {
            if (handSuppressed == value) return;
            handSuppressed = value;
            Attach();
        }

        void Attach()
        {
            if (lamp == null) return;
            Transform socket = IsHeld ? handSocket : beltSocket;
            if (socket == null) return;
            lamp.SetParent(socket, false);
            sway = swayVelocity = Vector3.zero;
            ApplyPose();
        }

        public void SetGlowing(bool value)
        {
            glowing = value;
            if (lampRenderer != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                lampRenderer.GetPropertyBlock(block, emissiveMaterialIndex);
                block.SetColor("_EmissionColor", glowing ? glowColor * glowStrength : Color.black);
                lampRenderer.SetPropertyBlock(block, emissiveMaterialIndex);
            }
            if (lampLight != null) lampLight.enabled = glowing;
        }

        void LateUpdate()
        {
            if (lamp == null) return;
            float speed = motor != null ? Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude : 0f;
            phase += Time.deltaTime * Mathf.Lerp(3f, 11f, Mathf.Clamp01(speed / 5f));
            float amount = Mathf.Clamp01(speed / 5f) * swayDegrees * (IsHeld ? 0.2f : 1f);
            Vector3 target = new Vector3(Mathf.Sin(phase) * amount, 0, Mathf.Sin(phase * 0.5f) * amount * 0.35f);
            sway = Vector3.SmoothDamp(sway, target, ref swayVelocity, 0.1f);
            ApplyPose();
        }

        void ApplyPose()
        {
            if (IsHeld)
            {
                Transform owner = motor != null ? motor.transform : transform;
                // Keep the vessel upright and its spout forward instead of inheriting
                // arbitrary wrist roll from locomotion. Align the handle with the palm.
                lamp.rotation = owner.rotation * Quaternion.Euler(heldEuler) * Quaternion.Euler(sway);
                Vector3 palm = handSocket.position + owner.TransformVector(palmOffset);
                lamp.position = palm - lamp.TransformVector(handGripOffset);
            }
            else
            {
                lamp.localRotation = Quaternion.Euler(sway);
                lamp.localPosition = Vector3.zero;
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || animator == null || !animator.isHuman) return;
            if (spellcasting != null && spellcasting.isActiveAndEnabled && spellcasting.UsesRightHand)
            {
                handWeight = 0f;
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }
            handWeight = Mathf.MoveTowards(handWeight, IsHeld ? 1f : 0f, Time.deltaTime * 6f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handWeight * 0.8f);
            Transform owner = motor != null ? motor.transform : transform;
            animator.SetIKPosition(AvatarIKGoal.RightHand, owner.TransformPoint(new Vector3(0.40f, 1.20f, 0.30f)));
            animator.SetIKRotation(AvatarIKGoal.RightHand, owner.rotation * Quaternion.Euler(0, 0, 90));
        }
    }
}
