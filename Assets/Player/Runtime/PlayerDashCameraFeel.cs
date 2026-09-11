using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 대시의 카메라 연출. 시야각이 대시 속도 곡선(<see cref="PlayerMotor.DashSpeedFactor"/>)을 그대로 따라 출발 때 살짝 넓어졌다가 감속하며 돌아온다.
    /// 곡선을 바꾸면 카메라도 같이 따라오고, 끝에서는 SmoothDamp라 반동 없이 붙는다.
    /// 시야각만 바꾸므로 화면 중앙 광선(조준)은 움직이지 않는다. 몸이 먼저 튀어나가는 뒤처짐은 <see cref="PlayerCameraRig"/>의 followSmoothTime·maxFollowLag가 이미 만든다.
    /// 값은 리그의 연출 입력(<see cref="PlayerCameraRig.AddFov"/>)으로만 전달하므로 다른 연출과 합산된다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // 모터(-200) 뒤, 카메라 리그 LateUpdate 전
    public sealed class PlayerDashCameraFeel : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerCameraRig rig;
        [Min(0f), Tooltip("대시 정점에서 시야각이 넓어지는 양(도). 상승 기류 발사 펀치(9도)보다 작게 둔다")]
        public float widenDegrees = 4f;
        [Min(0f), Tooltip("넓어질 때 따라붙는 시간. 대시 정점이 12%(약 0.04초)라 아주 짧아야 한다")]
        public float riseSmoothTime = 0.02f;
        [Min(0f), Tooltip("좁아질 때 따라붙는 시간. 대시 감속 구간과 종료 뒤 복귀 모두 여기를 쓴다")]
        public float fallSmoothTime = 0.08f;
        public float CurrentFovOffset { get; private set; }
        float velocity;

        void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (rig == null) rig = GetComponentInChildren<PlayerCameraRig>(true);
        }
        void OnDisable() { CurrentFovOffset = 0f; velocity = 0f; } // 매 프레임 넣는 방식이라 그만 넣으면 리그에서도 사라진다

        void Update()
        {
            if (rig == null) return;
            float target = motor != null && motor.enabled && motor.IsDashing ? widenDegrees * motor.DashSpeedFactor : 0f;
            float smooth = target > CurrentFovOffset ? riseSmoothTime : fallSmoothTime;
            CurrentFovOffset = smooth <= 0f ? target : Mathf.SmoothDamp(CurrentFovOffset, target, ref velocity, smooth, Mathf.Infinity, Time.deltaTime);
            if (CurrentFovOffset < 0.001f && target <= 0f) { CurrentFovOffset = 0f; velocity = 0f; }
            if (CurrentFovOffset > 0f) rig.AddFov(CurrentFovOffset);
        }
    }
}
