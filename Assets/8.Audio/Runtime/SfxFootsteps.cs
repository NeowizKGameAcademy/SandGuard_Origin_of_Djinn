using UnityEngine;

namespace SandGuard.Audio
{
    public enum FootstepMode
    {
        /// <summary>수평 이동 거리 Stride마다 한 걸음. 발 뼈가 없는 것(해골 소환수)용.</summary>
        Distance,
        /// <summary>Humanoid 발 뼈의 높이로 접지를 감지. 애니메이션과 동기.</summary>
        FootBones,
    }

    /// <summary>
    /// 발 하나의 접지 감지기. 루트 기준 높이를 넣으면 "들렸다가 내려온 순간"에 true를 한 번 돌려준다.
    /// 바닥 기준은 지금까지의 최저 높이이며 아주 천천히 위로 회복해 경사·보정 오차를 따라간다.
    /// </summary>
    public sealed class FootPlantDetector
    {
        public float LiftHeight = 0.08f, PlantHeight = 0.03f;
        float groundRef = float.MaxValue; bool lifted;

        public bool Lifted => lifted;
        public void Reset() { groundRef = float.MaxValue; lifted = false; }

        public bool Update(float height)
        {
            if (height < groundRef) groundRef = height;
            else groundRef += (height - groundRef) * 0.002f;
            float rel = height - groundRef;
            if (!lifted) { if (rel > LiftHeight) lifted = true; return false; }
            if (rel < PlantHeight) { lifted = false; return true; }
            return false;
        }
    }

    /// <summary>
    /// 발소리. FootBones 모드는 Animator의 LeftFoot/RightFoot 뼈 높이로 접지 순간을 잡아 클립·블렌드와 무관하게 애니메이션과 맞는다.
    /// 임계값은 외형 배율(적은 1.5·2배)에 비례해 자동으로 커진다. 이동 속도 조건으로 제자리 발 구르기는 거른다.
    /// Distance 모드는 이전 방식(거리 보폭)이다.
    /// </summary>
    public sealed class SfxFootsteps : MonoBehaviour
    {
        public SfxCue Cue;
        public FootstepMode Mode = FootstepMode.FootBones;
        [Tooltip("비우면 자식에서 찾는다 (적 외형은 런타임에 생기므로 계속 다시 찾는다)")] public Animator Animator;
        [Min(0.01f), Tooltip("발이 이 높이(m, 배율 1 기준) 위로 올라가야 '들림'")] public float LiftHeight = 0.08f;
        [Min(0f), Tooltip("들린 발이 이 높이 아래로 내려오면 접지")] public float PlantHeight = 0.03f;
        [Min(0.1f), Tooltip("Distance 모드의 한 걸음 거리(m)")] public float Stride = 0.75f;
        [Min(0f), Tooltip("이보다 느리면 걸음으로 세지 않는다 (m/s)")] public float MinSpeed = 0.6f;
        [Tooltip("공중에서는 내지 않는다")] public bool RequireGround = true;
        [Min(0.05f)] public float GroundProbe = 0.35f;
        public LayerMask GroundMask = ~0;
        [Tooltip("소리를 각 발 위치에서 낸다. 끄면 루트 위치")] public bool PlayAtFoot = true;

        public int Steps { get; private set; }
        readonly FootPlantDetector left = new FootPlantDetector(), right = new FootPlantDetector();
        Vector3 last; float travelled, nextAnimatorSearch; CharacterController controller;
        Transform leftFoot, rightFoot;

        void Awake() { controller = GetComponent<CharacterController>(); }
        void OnEnable() { last = transform.position; travelled = 0f; left.Reset(); right.Reset(); leftFoot = rightFoot = null; nextAnimatorSearch = 0f; }

        void Update()
        {
            Vector3 now = transform.position;
            Vector3 delta = now - last; delta.y = 0f;
            last = now;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool moving = delta.magnitude / dt >= MinSpeed && (!RequireGround || Grounded());

            if (Mode == FootstepMode.Distance)
            {
                if (!moving) { travelled = Mathf.Min(travelled, Stride * 0.5f); return; }
                travelled += delta.magnitude;
                if (travelled < Stride) return;
                travelled = 0f;
                Step(now);
                return;
            }

            if (!EnsureFeet()) return;
            float scale = Mathf.Max(0.05f, Animator.transform.lossyScale.y / Mathf.Max(0.05f, transform.lossyScale.y));
            left.LiftHeight = right.LiftHeight = LiftHeight * scale;
            left.PlantHeight = right.PlantHeight = PlantHeight * scale;
            bool l = left.Update(transform.InverseTransformPoint(leftFoot.position).y);
            bool r = right.Update(transform.InverseTransformPoint(rightFoot.position).y);
            if (!moving) return; // 감지기는 계속 돌려 바닥 기준을 유지하되, 서 있을 때는 소리를 내지 않는다
            if (l) Step(PlayAtFoot ? leftFoot.position : now);
            if (r) Step(PlayAtFoot ? rightFoot.position : now);
        }

        bool EnsureFeet()
        {
            if (leftFoot != null && rightFoot != null && Animator != null && Animator.isActiveAndEnabled) return true;
            if (Time.time < nextAnimatorSearch) return false;
            nextAnimatorSearch = Time.time + 0.5f;
            if (Animator == null || !Animator.isActiveAndEnabled) Animator = GetComponentInChildren<Animator>(true);
            if (Animator == null || !Animator.isHuman) return false;
            leftFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null) return false;
            left.Reset(); right.Reset();
            return true;
        }

        void Step(Vector3 at)
        {
            Steps++;
            if (Cue != null) SfxPlayer.Play(Cue, at);
        }

        bool Grounded()
        {
            if (controller != null) return controller.isGrounded;
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, GroundProbe + 0.1f, GroundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
