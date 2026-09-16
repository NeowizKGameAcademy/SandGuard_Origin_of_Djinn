using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>Keeps a shield in guard and a two-handed weapon's support palm on its handle.</summary>
    [DefaultExecutionOrder(180)]
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyEquipmentGrip : MonoBehaviour
    {
        [System.Serializable] public struct FingerPose { public Transform bone; public Quaternion rotation; }
        public FingerPose[] fingers = new FingerPose[0];
        public Transform rightPalm, leftPalm;
        public Transform primaryGrip, supportGrip;
        public Transform shieldFrame;
        public Quaternion leftGripBasis = Quaternion.identity;
        public Vector3 shieldOffset = new Vector3(-.27f, -.16f, .32f);
        public float characterHeight = 1.8f;
        public int PoseFrame { get; private set; } = -1;
        public float SupportError { get; private set; }
        Animator animator;
        EnemyHealth health;
        Transform upper, lower, hand, chest, rightUpper, rightLower, rightHand;

        void Awake()
        {
            animator = GetComponent<Animator>();
            health = GetComponentInParent<EnemyHealth>();
            if (!animator.isHuman) { enabled = false; return; }
            upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        void LateUpdate() => ApplyPose();

        public void ApplyPose()
        {
            if (animator == null || hand == null || leftPalm == null || !animator.enabled) return;
            if (health != null && health.State != global::LifeState.Alive) return;
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Dead") ||
                (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Dead"))) return;
            Quaternion rotation;
            Vector3 grip;
            var shield = health != null ? health.GetComponent<EnemyShield>() : null;
            if (shieldFrame != null && shield != null && shield.IsBroken) { CloseFingers(); return; }
            if (shieldFrame != null)
            {
                rotation = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Inverse(leftGripBasis);
                // 오프셋은 모델 공간 값이라 모델 스케일을 따라야 한다(손바닥 오프셋이 lossyScale을 쓰는 것과 같은 이유).
                grip = chest.position + transform.TransformVector(shieldOffset * (characterHeight / 1.8f));
            }
            else if (supportGrip != null && primaryGrip != null)
            {
                Vector3 shaft = (supportGrip.position - primaryGrip.position).normalized;
                Vector3 palm = Vector3.ProjectOnPlane(transform.forward, shaft).normalized;
                if (palm.sqrMagnitude < .1f) palm = Vector3.ProjectOnPlane(transform.up, shaft).normalized;
                rotation = Quaternion.LookRotation(shaft, palm) * Quaternion.Inverse(leftGripBasis);
                grip = supportGrip.position;
            }
            else { CloseFingers(); return; }
            Vector3 palmOffset = hand.InverseTransformPoint(leftPalm.position);
            Vector3 wristTarget = grip - rotation * Vector3.Scale(palmOffset, hand.lossyScale);
            if (supportGrip != null && shieldFrame == null)
            {
                // Retargeted bodies have different arm spans. Bring the primary hand slightly inward
                // when the original swing places the support handle beyond the other arm's reach.
                float reach = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position) - .015f;
                Quaternion rightRotation = rightHand.rotation;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 delta = wristTarget - upper.position;
                    if (delta.magnitude <= reach) break;
                    Vector3 correction = upper.position + delta.normalized * reach - wristTarget;
                    SolveArm(rightUpper, rightLower, rightHand, rightHand.position + correction, null);
                    rightHand.rotation = rightRotation;
                    wristTarget = supportGrip.position - rotation * Vector3.Scale(palmOffset, hand.lossyScale);
                }
            }
            Vector3? elbowHint = shieldFrame != null
                ? upper.position + transform.TransformVector(new Vector3(-.16f, -.35f, -.3f) * (characterHeight / 1.8f))
                : (Vector3?)null;
            SolveArm(upper, lower, hand, wristTarget, elbowHint);
            hand.rotation = rotation;
            CloseFingers();
            PoseFrame = Time.frameCount;
            SupportError = supportGrip != null ? Vector3.Distance(leftPalm.position, supportGrip.position) : 0f;
        }

        void CloseFingers()
        {
            foreach (var finger in fingers) if (finger.bone != null) finger.bone.localRotation = finger.rotation;
        }

        // Positional two-bone solve after animation. Bone lengths and animated elbow side are preserved.
        static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3? elbowHint)
        {
            Vector3 origin = upper.position, delta = target - origin;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, hand.position);
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane((elbowHint ?? lower.position) - origin, direction).normalized;
            if (bend.sqrMagnitude < .1f) bend = Vector3.Cross(direction, Vector3.up).normalized;
            if (bend.sqrMagnitude < .1f) bend = Vector3.right;
            float along = (a * a + distance * distance - b * b) / (2f * distance);
            Vector3 elbow = origin + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
        }
    }
}
