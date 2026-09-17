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
        [Min(0f), Tooltip("가로로 플레이어를 따라오는 시간. 0이면 즉시 붙는다. 대시 때 몸이 먼저 튀어나가는 뒤처짐도 여기서 나온다")]
        public float followSmoothTime = 0.08f;
        [Min(0f), Tooltip("점프·착지 때 세로 위치가 따라오는 시간")]
        public float verticalSmoothTime = 0.08f;
        [Min(0f), Tooltip("피벗이 플레이어에서 이 거리보다 뒤처지지 않는다 (대시 때 화면 밖으로 나가는 것을 막는다). 대시 속도면 늘 여기 걸리므로 사실상 대시 뒤처짐의 크기다")]
        public float maxFollowLag = 0.5f;
        [Min(0f), Tooltip("순간이동처럼 이 거리 이상 튀면 즉시 따라간다")]
        public float snapDistance = 3f;
        [Min(0f), Tooltip("아래를 볼수록 피벗을 이만큼 올려 캐릭터가 화면 아래로 몰리지 않게 한다")]
        public float pitchLift = 0.5f;
        [Header("연출 입력 — 다른 컴포넌트가 매 프레임 넣는다")]
        [Min(0f), Tooltip("흔들림 진폭 1m당 카메라 기울기(도). 흔들림에 살짝 굴러 힘이 실린 느낌을 준다")]
        public float rollDegreesPerMeter = 30f;
        /// <summary>지속 흔들림 진폭(m). <see cref="SetShake"/>로 넣는다. 0이면 흔들리지 않는다.</summary>
        public float ShakeAmplitude { get; private set; }
        public float ShakeFrequency { get; private set; } = 20f;
        /// <summary><see cref="Kick"/>가 남긴 감쇠 흔들림의 현재 진폭(m).</summary>
        public float KickAmplitude => kickTotal > 0f && kickRemaining > 0f ? kickStrength * (kickRemaining / kickTotal) : 0f;
        /// <summary>이번 프레임에 적용한 시야각 변화(도)의 합. 음수면 당겨 조이고 양수면 넓어진다. <see cref="AddFov"/>로 넣는다.</summary>
        public float FovOffset { get; private set; }
        /// <summary>이번 프레임에 적용한 거리 변화(m)의 합. 음수면 카메라가 플레이어 쪽으로 당겨 온다. <see cref="AddDistance"/>로 넣는다.</summary>
        public float DistanceOffset { get; private set; }
        /// <summary>이번 프레임에 적용한 연출 롤(도)의 합. 마지막에 곱하므로 위치·조준선은 바뀌지 않는다. <see cref="AddRoll"/>로 넣는다.</summary>
        public float RollOffset { get; private set; }
        float pendingFov, pendingDistance, pendingRoll;
        /// <summary>흔들림을 더하기 전의 카메라 위치.</summary>
        public Vector3 UnshakenPosition { get; private set; }
        /// <summary>이번 프레임에 더한 흔들림 오프셋(월드). 흔들리지 않으면 0.</summary>
        public Vector3 LastShakeOffset { get; private set; }
        public float BaseFieldOfView { get; private set; }
        float yaw, currentLength = -1f;
        float kickStrength, kickRemaining, kickTotal, noiseSeed;
        Vector3 pivot, pivotVelocity;
        Camera cachedCamera;
        /// <summary>지금 카메라가 바라보는 기준점(부드럽게 따라온 위치).</summary>
        public Vector3 Pivot => pivot;
        void Awake()
        {
            cachedCamera = GetComponent<Camera>();
            BaseFieldOfView = cachedCamera != null ? cachedCamera.fieldOfView : 60f;
            noiseSeed = Random.value * 100f;
        }
        void Start() { yaw = owner != null ? owner.eulerAngles.y : transform.eulerAngles.y; }
        /// <summary>지속 흔들림. 충전처럼 상태가 이어지는 동안 매 프레임 넣고, 끝나면 0으로 넣는다.</summary>
        public void SetShake(float amplitude, float frequency) { ShakeAmplitude = Mathf.Max(0f, amplitude); ShakeFrequency = Mathf.Max(0.01f, frequency); }
        /// <summary>감쇠 흔들림 한 번(발사·충격). 진행 중인 것보다 강할 때만 덮어쓴다.</summary>
        public void Kick(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f) return;
            if (strength >= KickAmplitude) { kickStrength = strength; kickTotal = kickRemaining = duration; }
        }
        /// <summary>시야각 변화(도)를 이번 프레임에 더한다. 여러 연출이 같은 프레임에 넣으면 합산되고, LateUpdate가 쓰고 나면 비워지므로 유지하려면 매 프레임 넣는다.</summary>
        public void AddFov(float degrees) { if (!float.IsNaN(degrees)) pendingFov += degrees; }
        /// <summary>거리 변화(m)를 이번 프레임에 더한다. 규칙은 <see cref="AddFov"/>와 같다.</summary>
        public void AddDistance(float meters) { if (!float.IsNaN(meters)) pendingDistance += meters; }
        /// <summary>카메라 롤(도)을 이번 프레임에 더한다. 양수면 화면이 반시계로 기운다. 규칙은 <see cref="AddFov"/>와 같다.</summary>
        public void AddRoll(float degrees) { if (!float.IsNaN(degrees)) pendingRoll += degrees; }
        /// <summary>연출 입력을 전부 지운다 (부활·씬 전환).</summary>
        public void ClearFeel() { ShakeAmplitude = 0f; kickRemaining = 0f; FovOffset = DistanceOffset = RollOffset = pendingFov = pendingDistance = pendingRoll = 0f; }
        /// <summary>순간이동·부활처럼 바라보는 방향을 바꿔야 할 때 카메라 회전을 즉시 맞춘다.</summary>
        public void SetYaw(float degrees) { yaw = degrees; currentLength = -1f; }
        void LateUpdate()
        {
            FovOffset = pendingFov; DistanceOffset = pendingDistance; RollOffset = pendingRoll; pendingFov = pendingDistance = pendingRoll = 0f; // 이번 프레임 기여분을 모아 쓰고 비운다
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
            Vector3 displacement = rotation * new Vector3(shoulderOffset, 0f, -Mathf.Max(0.2f, distance + DistanceOffset));
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
            UnshakenPosition = origin + direction * currentLength;
            if (kickRemaining > 0f) kickRemaining = Mathf.Max(0f, kickRemaining - dt);
            float amplitude = ShakeAmplitude + KickAmplitude;
            Vector3 position = UnshakenPosition;
            LastShakeOffset = Vector3.zero;
            if (amplitude > 0f)
            {
                float t = Time.time * ShakeFrequency;
                // Perlin은 대부분 0.25~0.75에 머물러 ×2-1만으로는 진폭의 절반도 안 쓴다. 3배로 펴서 진폭 값이 실제 흔들림에 가깝게 한다.
                Vector3 noise = new Vector3(Noise(noiseSeed, t), Noise(noiseSeed + 7.3f, t), 0f);
                LastShakeOffset = rotation * (noise * amplitude);
                position += LastShakeOffset;
                float roll = Noise(noiseSeed + 13.1f, t) * amplitude * rollDegreesPerMeter;
                rotation *= Quaternion.Euler(0f, 0f, roll);
            }
            if (RollOffset != 0f) rotation *= Quaternion.Euler(0f, 0f, RollOffset); // 연출 롤은 위치 계산이 끝난 뒤 곱해 조준선을 건드리지 않는다
            transform.SetPositionAndRotation(position, rotation);
            if (cachedCamera != null) cachedCamera.fieldOfView = Mathf.Clamp(BaseFieldOfView + FovOffset, 10f, 150f);
        }

        static float Noise(float x, float y) => Mathf.Clamp((Mathf.PerlinNoise(x, y) - 0.5f) * 3f, -1f, 1f);

        static float Smooth(float current, float goal, ref float velocity, float smoothTime, float dt)
        {
            if (smoothTime <= 0f || dt <= 0f) { velocity = 0f; return goal; }
            return Mathf.SmoothDamp(current, goal, ref velocity, smoothTime, Mathf.Infinity, dt);
        }
    }
}
