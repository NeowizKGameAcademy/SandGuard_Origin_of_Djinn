using UnityEngine;

namespace SandGuard.Player
{
    [DefaultExecutionOrder(0)]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        public PlayerInputReader input;
        public Transform target;
        public Transform owner;
        [Min(0.2f)] public float distance = 6f;
        [Tooltip("양수면 오른쪽 어깨 너머로 본다")] public float shoulderOffset = 0.5f;
        public float pitch = 15f;
        public Vector2 pitchLimits = new Vector2(-35f, 70f);
        [Min(0f)] public float mouseSensitivity = 0.18f;
        [Min(0f)] public float stickDegreesPerSecond = 220f;
        [Min(0.05f)] public float collisionRadius = 0.25f;
        public LayerMask obstructionMask = ~0;
        [Header("부드러움")]
        [Min(0f), Tooltip("가려졌다 풀릴 때 원래 거리로 돌아가는 시간. 가까워지는 쪽은 즉시 반응한다")]
        public float collisionRecoverTime = 0.12f;
        [Min(0f), Tooltip("가로로 플레이어를 따라오는 시간. 0이면 즉시 붙는다")]
        public float followSmoothTime = 0.12f;
        [Min(0f), Tooltip("점프·착지 때 세로 위치가 따라오는 시간")]
        public float verticalSmoothTime = 0.08f;
        [Min(0f), Tooltip("피벗이 플레이어에서 이 거리보다 뒤처지지 않는다 (대시 때 화면 밖으로 나가는 것을 막는다)")]
        public float maxFollowLag = 0.8f;
        [Min(0f), Tooltip("순간이동처럼 이 거리 이상 튀면 즉시 따라간다")]
        public float snapDistance = 3f;
        [Min(0f), Tooltip("아래를 볼수록 피벗을 이만큼 올려 캐릭터가 화면 아래로 몰리지 않게 한다")]
        public float pitchLift = 0.5f;
        float yaw, currentLength = -1f;
        Vector3 pivot, pivotVelocity;
        /// <summary>지금 카메라가 바라보는 기준점(부드럽게 따라온 위치).</summary>
        public Vector3 Pivot => pivot;
        void Start() { yaw = owner != null ? owner.eulerAngles.y : transform.eulerAngles.y; }
        /// <summary>순간이동·부활처럼 바라보는 방향을 바꿔야 할 때 카메라 회전을 즉시 맞춘다.</summary>
        public void SetYaw(float degrees) { yaw = degrees; currentLength = -1f; }
        void LateUpdate()
        {
            if (target == null) return;
            if (input != null && input.AcceptsInput)
            {
                Vector2 look = input.Look;
                float scale = input.LookIsPointer ? mouseSensitivity : stickDegreesPerSecond * Time.deltaTime;
                yaw += look.x * scale;
                pitch = Mathf.Clamp(pitch - look.y * scale, pitchLimits.x, pitchLimits.y);
            }
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 goal = target.position;
            if (currentLength < 0f || Vector3.Distance(goal, pivot) > snapDistance) { pivot = goal; pivotVelocity = Vector3.zero; } // 첫 프레임·순간이동은 즉시
            float dt = Time.deltaTime;
            Vector3 next = pivot;
            next.x = Smooth(pivot.x, goal.x, ref pivotVelocity.x, followSmoothTime, dt);
            next.z = Smooth(pivot.z, goal.z, ref pivotVelocity.z, followSmoothTime, dt);
            next.y = Smooth(pivot.y, goal.y, ref pivotVelocity.y, verticalSmoothTime, dt);
            Vector3 lag = next - goal;
            if (maxFollowLag > 0f && lag.magnitude > maxFollowLag) next = goal + lag.normalized * maxFollowLag; // 빠른 이동에도 화면 안에 둔다
            pivot = next;
            Vector3 origin = pivot;
            origin.y += Mathf.Clamp01(pitch / Mathf.Max(1f, pitchLimits.y)) * pitchLift;
            Vector3 displacement = rotation * new Vector3(shoulderOffset, 0f, -distance);
            float length = displacement.magnitude;
            Vector3 direction = displacement / length;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, collisionRadius, direction, length,
                obstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.transform.IsChildOf(owner)) continue;
                length = Mathf.Min(length, Mathf.Max(0f, hit.distance - 0.05f));
            }
            if (currentLength < 0f || length < currentLength || collisionRecoverTime <= 0f) currentLength = length;
            else currentLength = Mathf.Lerp(currentLength, length, 1f - Mathf.Exp(-Time.deltaTime / collisionRecoverTime));
            transform.SetPositionAndRotation(origin + direction * currentLength, rotation);
        }

        static float Smooth(float current, float goal, ref float velocity, float smoothTime, float dt)
        {
            if (smoothTime <= 0f || dt <= 0f) { velocity = 0f; return goal; }
            return Mathf.SmoothDamp(current, goal, ref velocity, smoothTime, Mathf.Infinity, dt);
        }
    }
}
