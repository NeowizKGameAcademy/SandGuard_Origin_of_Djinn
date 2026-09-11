using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 상승 기류 충전의 카메라 연출. 충전량이 오를수록 흔들림이 거세지고 빨라지며 시야가 조여들고 카메라가 살짝 당겨 와서 힘이 응축되는 느낌을 낸다.
    /// 발사 순간에는 강한 감쇠 흔들림과 시야 펀치(넓어졌다 돌아옴)로 터뜨리고, 취소되면 짧게 풀린다.
    /// 값은 <see cref="PlayerCameraRig"/>의 연출 입력(SetShake / Kick / AddFov / AddDistance)으로만 전달한다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // PlayerUpdraft(-150) 뒤, 카메라 리그 LateUpdate 전
    public sealed class PlayerUpdraftCameraFeel : MonoBehaviour
    {
        public PlayerUpdraft updraft;
        public PlayerCameraRig rig;
        [Header("충전 — 응축")]
        [Min(0f), Tooltip("가득 찼을 때 흔들림 진폭(m)")] public float maxShake = 0.09f;
        [Tooltip("충전량→흔들림 비율. 초반엔 약하고 끝으로 갈수록 급격히 커진다")]
        public AnimationCurve shakeByCharge = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.6f, 0.25f, 0.9f, 0.9f), new Keyframe(1f, 1f, 2.4f, 0f));
        [Min(1f), Tooltip("충전 시작 때 초당 흔들림 횟수")] public float minFrequency = 9f;
        [Min(1f), Tooltip("가득 찼을 때 초당 흔들림 횟수")] public float maxFrequency = 26f;
        [Tooltip("가득 찼을 때 시야각 변화(도). 음수면 조여든다")] public float zoomInDegrees = -7f;
        [Min(0f), Tooltip("가득 찼을 때 카메라가 당겨 오는 거리(m)")] public float pullInMeters = 0.45f;
        [Header("발사 — 해방")]
        [Min(0f), Tooltip("발사 순간 감쇠 흔들림 진폭(m). 충전량 0이면 60%")] public float launchKick = 0.22f;
        [Min(0.01f)] public float launchKickDuration = 0.28f;
        [Tooltip("발사 순간 시야가 확 넓어지는 양(도)")] public float launchFovPunch = 9f;
        [Min(0.01f)] public float launchFovPunchDuration = 0.35f;
        [Header("복귀")]
        [Min(0.01f), Tooltip("취소·발사 뒤 조임과 당김이 풀리는 시간")] public float releaseSmoothTime = 0.12f;
        public float CurrentShake { get; private set; }
        public float CurrentFovOffset { get; private set; }
        public float CurrentDistanceOffset { get; private set; }
        float fovVelocity, distanceVelocity, punchRemaining;

        void Awake()
        {
            if (updraft == null) updraft = GetComponent<PlayerUpdraft>();
            if (rig == null) rig = GetComponentInChildren<PlayerCameraRig>(true);
        }
        void OnEnable() { if (updraft != null) updraft.Launched += OnLaunched; }
        void OnDisable()
        {
            if (updraft != null) updraft.Launched -= OnLaunched;
            CurrentShake = CurrentFovOffset = CurrentDistanceOffset = 0f; punchRemaining = 0f;
            if (rig != null) rig.SetShake(0f, rig.ShakeFrequency); // 시야·거리는 매 프레임 넣는 방식이라 그만 넣으면 사라진다
        }

        void OnLaunched(float charge, float height)
        {
            if (rig == null) return;
            rig.Kick(launchKick * Mathf.Lerp(0.6f, 1f, charge), launchKickDuration);
            punchRemaining = launchFovPunchDuration;
        }

        void Update()
        {
            if (rig == null) return;
            float dt = Time.deltaTime;
            bool charging = updraft != null && updraft.IsCharging;
            float k = charging ? shakeByCharge.Evaluate(updraft.Charge) : 0f;
            float targetShake = charging ? maxShake * k : 0f;
            float targetFov = charging ? zoomInDegrees * k : 0f;
            float targetDistance = charging ? -pullInMeters * k : 0f;
            if (punchRemaining > 0f)
            {
                punchRemaining = Mathf.Max(0f, punchRemaining - dt);
                targetFov += launchFovPunch * (punchRemaining / launchFovPunchDuration);
            }
            // 충전 중엔 충전량을 그대로 따라 오르고, 끝나면 짧게 풀린다.
            CurrentShake = charging ? targetShake : Mathf.MoveTowards(CurrentShake, 0f, dt * maxShake / Mathf.Max(0.01f, releaseSmoothTime));
            float smooth = charging ? 0.06f : releaseSmoothTime;
            CurrentFovOffset = punchRemaining > 0f ? targetFov : Mathf.SmoothDamp(CurrentFovOffset, targetFov, ref fovVelocity, smooth, Mathf.Infinity, dt);
            CurrentDistanceOffset = Mathf.SmoothDamp(CurrentDistanceOffset, targetDistance, ref distanceVelocity, smooth, Mathf.Infinity, dt);
            rig.SetShake(CurrentShake, Mathf.Lerp(minFrequency, maxFrequency, k));
            rig.AddFov(CurrentFovOffset);
            rig.AddDistance(CurrentDistanceOffset);
        }
    }
}
