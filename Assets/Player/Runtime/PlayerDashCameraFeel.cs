using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 대시의 카메라 연출. 시야각이 대시 속도 곡선(<see cref="PlayerMotor.DashSpeedFactor"/>)을 따라 출발 때 넓어지고, 대시가 끝난 뒤 한 박자 늦게(fallSmoothTime) 돌아온다.
    /// 이동은 사다리꼴 곡선이라 뚝 끊기지만 시야각 복귀가 그 여운을 만든다. 옆 대시에는 그쪽으로 살짝 기우는 롤을 더한다. 끝에서는 SmoothDamp라 반동 없이 붙는다.
    /// 시야각만 바꾸므로 화면 중앙 광선(조준)은 움직이지 않는다. 몸이 먼저 튀어나가는 뒤처짐은 <see cref="PlayerCameraRig"/>의 followSmoothTime·maxFollowLag가 이미 만든다.
    /// 값은 리그의 연출 입력(<see cref="PlayerCameraRig.AddFov"/>)으로만 전달하므로 다른 연출과 합산된다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // 모터(-200) 뒤, 카메라 리그 LateUpdate 전
    public sealed class PlayerDashCameraFeel : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerCameraRig rig;
        [Min(0f), Tooltip("대시 정점에서 시야각이 넓어지는 양(도). 상승 기류 발사 펀치(9도)보다 작게 둔다")]
        public float widenDegrees = 8f;
        [Min(0f), Tooltip("넓어질 때 따라붙는 시간. 대시 정점이 5%(약 0.01초)라 아주 짧아야 한다")]
        public float riseSmoothTime = 0.02f;
        [Min(0f), Tooltip("좁아질 때 따라붙는 시간. 이동은 대시가 끝나는 순간 끊기지만 시야각은 이 시간에 걸쳐 돌아와 눈이 느끼는 감속을 만든다")]
        public float fallSmoothTime = 0.18f;
        [Min(0f), Tooltip("옆으로 대시할 때 카메라가 그쪽으로 기우는 양(도). 앞뒤 대시는 0, 대각선은 비례. 시야각과 같은 박자로 오르내린다")]
        public float rollDegrees = 2.5f;
        public float CurrentFovOffset { get; private set; }
        public float CurrentRoll { get; private set; }
        float velocity, rollVelocity;

        void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (rig == null) rig = GetComponentInChildren<PlayerCameraRig>(true);
        }
        void OnDisable() { CurrentFovOffset = 0f; velocity = 0f; CurrentRoll = 0f; rollVelocity = 0f; } // 매 프레임 넣는 방식이라 그만 넣으면 리그에서도 사라진다

        void Update()
        {
            if (rig == null) return;
            float target = motor != null && motor.enabled && motor.IsDashing ? widenDegrees * motor.DashSpeedFactor : 0f;
            float smooth = target > CurrentFovOffset ? riseSmoothTime : fallSmoothTime;
            CurrentFovOffset = smooth <= 0f ? target : Mathf.SmoothDamp(CurrentFovOffset, target, ref velocity, smooth, Mathf.Infinity, Time.deltaTime);
            if (CurrentFovOffset < 0.001f && target <= 0f) { CurrentFovOffset = 0f; velocity = 0f; }
            if (CurrentFovOffset > 0f) rig.AddFov(CurrentFovOffset);

            // 옆 대시 롤. 카메라 기준 오른쪽 성분만큼 그쪽으로 기운다(오른쪽 대시 = 시계 방향 = 음수 z).
            float lateral = 0f;
            if (motor != null && motor.enabled && motor.IsDashing)
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized;
                if (flatForward.sqrMagnitude > 0.01f) lateral = Vector3.Dot(motor.DashDirection, Vector3.Cross(Vector3.up, flatForward));
            }
            float rollTarget = -rollDegrees * lateral * (motor != null && motor.IsDashing ? motor.DashSpeedFactor : 0f);
            float rollSmooth = Mathf.Abs(rollTarget) > Mathf.Abs(CurrentRoll) ? riseSmoothTime : fallSmoothTime;
            CurrentRoll = rollSmooth <= 0f ? rollTarget : Mathf.SmoothDamp(CurrentRoll, rollTarget, ref rollVelocity, rollSmooth, Mathf.Infinity, Time.deltaTime);
            if (Mathf.Abs(CurrentRoll) < 0.001f && rollTarget == 0f) { CurrentRoll = 0f; rollVelocity = 0f; }
            if (CurrentRoll != 0f) rig.AddRoll(CurrentRoll);
        }
    }
}
