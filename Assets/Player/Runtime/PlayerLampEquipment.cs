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
        [Tooltip("꺼내는 애니메이션이 준비될 때까지 끈다")]
        public bool allowHandAttachment;
        public Vector3 beltAttachOffset;
        public Vector3 beltEuler = new Vector3(0, 0, 12);
        [Min(0.1f)] public float springStrength = 70f;
        [Min(0f)] public float damping = 7f;
        [Min(0f)] public float maxLightIntensity = 0.18f;
        [Min(0f)] public float brightnessSmoothTime = 0.2f;
        public bool manaDriven = true;
        [Header("마나 흡수 반짝임")]
        [Min(0.01f)] public float absorptionRiseTime = 0.1f;
        [Min(0.01f)] public float absorptionFadeTime = 0.65f;
        [Min(0f)] public float absorptionEmissionBoost = 18f;
        [Min(0f)] public float absorptionLightBoost = 0.22f;
        public float AbsorptionPulse => absorptionPulse;
        public bool IsHeld => allowHandAttachment && held && !handSuppressed;
        public bool RequestedHeld => held;
        public bool IsGlowing => !forcedOff && (manaDriven || glowing);
        public float Brightness => brightness;
        public Vector3 SwayAngles => sway;
        Animator animator;
        PlayerMotor motor;
        MaterialPropertyBlock block;
        Vector3 sway, swayVelocity;
        float handWeight, brightness, brightnessVelocity, targetBrightness;
        Vector3 lastPosition, lastVelocity, acceleration;
        bool sampled, forcedOff;
        Quaternion lastHeading;
        IManaReader mana;
        ILifeState life;
        bool handSuppressed;
        PlayerSpellcasting spellcasting;
        PlayerBasicAttack attack;
        float absorptionPulse, pulseStart, pulseElapsed;
        bool pulsing;
        Material[] originalLampMaterials, pulseMaterials;

        void Awake()
        {
            animator = GetComponent<Animator>();
            motor = GetComponentInParent<PlayerMotor>();
            spellcasting = GetComponent<PlayerSpellcasting>();
            block = new MaterialPropertyBlock();
            if (lampRenderer != null)
            {
                originalLampMaterials = lampRenderer.sharedMaterials;
                pulseMaterials = new Material[originalLampMaterials.Length];
                for (int i = 0; i < pulseMaterials.Length; i++)
                {
                    if (originalLampMaterials[i] == null) continue;
                    pulseMaterials[i] = new Material(originalLampMaterials[i]);
                    pulseMaterials[i].EnableKeyword("_EMISSION");
                }
                lampRenderer.sharedMaterials = pulseMaterials;
            }
            SetHeld(held);
        }

        void OnDestroy()
        {
            if (pulseMaterials == null) return;
            if (lampRenderer != null) lampRenderer.sharedMaterials = originalLampMaterials;
            foreach (var material in pulseMaterials) if (material != null) Destroy(material);
        }

        void OnEnable()
        {
            mana = GetComponentInParent<IManaReader>();
            life = GetComponentInParent<ILifeState>();
            attack = GetComponentInParent<PlayerBasicAttack>();
            if (attack != null) attack.ManaAbsorbed += OnManaAbsorbed;
            if (mana != null) mana.Changed += OnManaChanged;
            if (life != null) life.StateChanged += OnLifeChanged;
            if (motor != null) motor.Teleported += ResetSway;
            RefreshBrightness();
            brightness = targetBrightness;
            ApplyLight();
            ResetSway();
        }

        void Start()
        {
            // Wallet Awake order is not guaranteed across the visual hierarchy.
            RefreshBrightness(); brightness = targetBrightness; brightnessVelocity = 0f; ApplyLight();
        }

        void OnDisable()
        {
            if (mana != null) mana.Changed -= OnManaChanged;
            if (life != null) life.StateChanged -= OnLifeChanged;
            if (motor != null) motor.Teleported -= ResetSway;
            if (attack != null) attack.ManaAbsorbed -= OnManaAbsorbed;
            ClearAbsorptionPulse();
            ResetSway();
        }

        void OnManaChanged(ManaChangedInfo info) => RefreshBrightness();
        void OnLifeChanged(LifeStateChangedInfo info) { ResetSway(); ClearAbsorptionPulse(); }

        void OnManaAbsorbed(int recovered)
        {
            if (recovered <= 0 || !isActiveAndEnabled || forcedOff || Time.timeScale <= 0f
                || (life != null && life.State != LifeState.Alive)) return;
            pulseStart = absorptionPulse; pulseElapsed = 0f; pulsing = true;
        }

        /// <summary>미리보기 전용. 마나 수치를 바꾸거나 스킬을 해금하지 않고 연출만 확인한다.</summary>
        [ContextMenu("Preview Mana Absorption")]
        public void PreviewManaAbsorption() => OnManaAbsorbed(3);

        void ClearAbsorptionPulse()
        {
            pulsing = false; absorptionPulse = pulseStart = pulseElapsed = 0f;
            ApplyLight();
        }

        void StepAbsorptionPulse(float dt)
        {
            if (!pulsing) return;
            pulseElapsed += dt;
            float rise = Mathf.Max(0.01f, absorptionRiseTime);
            float fade = Mathf.Max(0.01f, absorptionFadeTime);
            absorptionPulse = pulseElapsed < rise
                ? Mathf.Lerp(pulseStart, 1f, Mathf.SmoothStep(0f, 1f, pulseElapsed / rise))
                : 1f - Mathf.SmoothStep(0f, 1f, (pulseElapsed - rise) / fade);
            if (pulseElapsed >= rise + fade) { absorptionPulse = 0f; pulsing = false; }
        }

        void RefreshBrightness()
        {
            float ratio = manaDriven ? (mana != null && mana.MaxMana > 0 ? Mathf.Clamp01((float)mana.CurrentMana / mana.MaxMana) : 0f) : (glowing ? 1f : 0f);
            targetBrightness = forcedOff ? 0f : ratio * ratio;
        }

        public void ResetSway()
        {
            sway = swayVelocity = acceleration = lastVelocity = Vector3.zero;
            sampled = false;
        }

        public void SetHeld(bool value)
        {
            bool wasHeld = IsHeld;
            held = value;
            if (wasHeld == IsHeld && lamp != null && lamp.parent == beltSocket) return;
            Attach();
        }

        public void SetHandSuppressed(bool value)
        {
            if (handSuppressed == value) return;
            bool wasHeld = IsHeld;
            handSuppressed = value;
            if (wasHeld != IsHeld) Attach();
        }

        void Attach()
        {
            if (lamp == null) return;
            Transform socket = IsHeld ? handSocket : beltSocket;
            if (socket == null) return;
            lamp.SetParent(socket, false);
            ResetSway();
            ApplyPose();
        }

        public void SetGlowing(bool value)
        {
            glowing = value;
            forcedOff = !value;
            RefreshBrightness();
            if (forcedOff) { brightness = brightnessVelocity = 0f; ClearAbsorptionPulse(); }
            ApplyLight();
        }

        void ApplyLight()
        {
            if (lampRenderer != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                int count = pulseMaterials != null ? pulseMaterials.Length : lampRenderer.sharedMaterials.Length;
                for (int i = 0; i < count; i++)
                {
                    lampRenderer.GetPropertyBlock(block, i);
                    float emission = i == emissiveMaterialIndex
                        ? glowStrength * brightness + absorptionEmissionBoost * absorptionPulse
                        : 2.5f * absorptionPulse;
                    block.SetColor("_EmissionColor", emission > 0f ? glowColor * emission : Color.black);
                    lampRenderer.SetPropertyBlock(block, i);
                }
            }
            if (lampLight != null)
            {
                lampLight.intensity = maxLightIntensity * brightness + absorptionLightBoost * absorptionPulse;
                lampLight.enabled = lampLight.intensity > 0.0001f;
            }
        }

        void LateUpdate()
        {
            if (lamp == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f || Time.timeScale <= 0f) return;
            brightness = Mathf.SmoothDamp(brightness, targetBrightness, ref brightnessVelocity, brightnessSmoothTime, Mathf.Infinity, dt);
            StepAbsorptionPulse(dt);
            ApplyLight();
            if (!IsHeld && beltSocket != null) StepSway(dt);
            ApplyPose();
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame && motor != null
                && motor.input != null && motor.input.GameplayEnabled && Time.timeScale > 0f)
                PreviewManaAbsorption();
#endif
        }

        Quaternion Heading => Quaternion.Euler(0f, (motor != null ? motor.transform : transform).eulerAngles.y, 0f);

        void StepSway(float dt)
        {
            Vector3 position = beltSocket.position;
            Quaternion heading = Heading;
            if (!sampled || dt > 0.1f || (position - lastPosition).sqrMagnitude > 1f || (life != null && life.State != LifeState.Alive))
            {
                ResetSway(); sampled = true; lastPosition = position; lastHeading = heading;
                return;
            }
            // Preserve the swing direction in world space while the player turns.
            Quaternion turn = Quaternion.Inverse(heading) * lastHeading;
            sway = turn * sway; swayVelocity = turn * swayVelocity;
            // Filter velocity before differentiating so a start/stop impulse lasts the same
            // time at every frame rate instead of being clipped away in one tiny frame.
            Vector3 velocity = Vector3.Lerp(lastVelocity, (position - lastPosition) / dt, 1f - Mathf.Exp(-12f * dt));
            acceleration = Vector3.ClampMagnitude((velocity - lastVelocity) / dt, 30f);
            Vector3 local = Quaternion.Inverse(heading) * acceleration;
            Vector3 target = new Vector3(local.z, 0f, -local.x) * 1.2f;
            target = Vector3.ClampMagnitude(target, swayDegrees);
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                swayVelocity += ((target - sway) * springStrength - swayVelocity * damping) * step;
                sway += swayVelocity * step;
                // Rear-left hip: allow outward travel and limit travel into the body/bag.
                float x = Mathf.Clamp(sway.x, -swayDegrees * 0.45f, swayDegrees);
                float z = Mathf.Clamp(sway.z, -swayDegrees, swayDegrees * 0.45f);
                if (x != sway.x) swayVelocity.x = 0f;
                if (z != sway.z) swayVelocity.z = 0f;
                sway = new Vector3(x, 0f, z);
            }
            lastPosition = position; lastVelocity = velocity; lastHeading = heading;
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
                if (beltSocket == null) return;
                lamp.rotation = Heading * Quaternion.Euler(sway) * Quaternion.Euler(beltEuler);
                lamp.position = beltSocket.position - lamp.TransformVector(beltAttachOffset);
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            // Do not write IK weights: casting owns the right arm while hand attachment is unavailable.
            if (!allowHandAttachment) return;
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
