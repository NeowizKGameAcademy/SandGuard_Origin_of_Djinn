using System;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Player
{
    /// <summary>
    /// ③ 상승 기류. 해금되면 지상에서 왼쪽 Ctrl + Space를 누르는 즉시 웅크림 충전이 시작된다. Ctrl 없이 누른 Space는 평소 점프 그대로다.
    /// 공중에서 Ctrl + Space를 누른 채 착지하면 그대로 충전으로 이어진다. Space를 놓으면 충전량에 따라 <see cref="minHeight"/>~<see cref="maxHeight"/>로 발사되듯 솟아오른다.
    /// 충전 중에는 모터를 붙들어(<see cref="PlayerMotor.Anchored"/>) 이동·점프·대시를 막고, 공중에 뜨거나 대시·사망하면 취소된다.
    /// 연출(카메라 떨림, 기류, 충격파)은 <see cref="Charge"/>와 이벤트만 구독한다.
    /// </summary>
    [DefaultExecutionOrder(-150)] // 모터(-200) 다음, 비주얼(0) 전에
    public sealed class PlayerUpdraft : MonoBehaviour
    {
        public PlayerInputReader input;
        public PlayerMotor motor;
        [Tooltip("마나 비용을 낼 IManaWallet. 비우면 같은 오브젝트에서 찾는다")]
        public MonoBehaviour manaSource;
        [Header("해금")]
        [Tooltip("스킬 ③ 상승 기류가 UpdraftEffect로 켠다. 꺼져 있으면 Ctrl + Space도 보통 점프다")]
        public bool unlocked;
        [Header("충전")]
        [Min(0.05f), Tooltip("충전이 가득 차는 시간")] public float chargeTime = 0.8f;
        [Header("도약")]
        [Min(0.1f), Tooltip("충전 0일 때 높이")] public float minHeight = 6.76f;
        [Min(0.1f), Tooltip("충전 1일 때 높이")] public float maxHeight = 28.73f;
        [Min(0), Tooltip("발사 시 마나 비용. 부족하면 충전이 취소된다")] public int manaCost = 0;
        /// <summary>충전 중인지. 애니메이터 Charging.</summary>
        public bool IsCharging { get; private set; }
        /// <summary>충전량 0~1. 애니메이터 Charge, 카메라 떨림 강도.</summary>
        public float Charge { get; private set; }
        public float LastLaunchHeight { get; private set; }
        public int LaunchCount { get; private set; }
        public event Action ChargeStarted;
        public event Action ChargeCancelled;
        /// <summary>발사 순간. 인자는 (충전량, 높이).</summary>
        public event Action<float, float> Launched;
        [Header("연출 훅 (인스펙터 연결용)")]
        public UnityEvent onChargeStarted = new UnityEvent();
        [Tooltip("충전 중 매 프레임 충전량(0~1)을 보낸다. 기류 파티클 세기 등")]
        public UnityEvent<float> onCharging = new UnityEvent<float>();
        public UnityEvent onChargeCancelled = new UnityEvent();
        public UnityEvent onLaunched = new UnityEvent();
        bool Held => input != null && input.JumpHeld;
        bool Modifier => input != null && input.ChargeModifierHeld;
        bool Alive => motor == null || motor.lifeSource == null || (motor.lifeSource as ILifeState)?.State == global::LifeState.Alive;
        bool Ready => unlocked && isActiveAndEnabled && motor != null && motor.MovementEnabled && !motor.HardLandingLocked && input != null && input.AcceptsInput
            && Time.timeScale > 0f && Alive;
        IManaWallet Mana => (manaSource as IManaWallet) ?? (motor != null ? motor.manaSource as IManaWallet : null);
        /// <summary>충전 중 발사했을 때 나올 높이. UI·연출 미리보기용.</summary>
        public float HeightForCharge(float charge) => Mathf.Lerp(minHeight, maxHeight, Mathf.Clamp01(charge));

        void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (manaSource == null) manaSource = GetComponent<IManaWallet>() as MonoBehaviour;
        }
        void OnEnable() { if (input != null) input.JumpPressed += OnJumpPressed; }
        void OnDisable()
        {
            if (input != null) input.JumpPressed -= OnJumpPressed;
            Cancel();
            if (motor != null) motor.DeferGroundJumps = false;
        }
        // 입력 리더(-300)가 이벤트를 쏜 직후, 모터(-200)가 점프 버퍼를 처리하기 전이라 같은 프레임의 Ctrl + Space도 지상 점프로 새지 않는다.
        void OnJumpPressed() => RefreshDeferral();
        /// <summary>Ctrl + Space일 때만 모터의 지상 점프를 막는다. 해금이 바뀐 직후에도 불러 바로 맞춘다.</summary>
        public void RefreshDeferral() { if (motor != null) motor.DeferGroundJumps = Ready && Modifier; }

        void Update()
        {
            if (!Ready)
            {
                if (IsCharging) Cancel();
                if (motor != null) motor.DeferGroundJumps = false;
                return;
            }
            motor.DeferGroundJumps = Modifier;
            if (IsCharging)
            {
                if (!motor.IsGrounded || motor.IsDashing) { Cancel(); return; }
                if (!Held) { Launch(); return; }
                Charge = Mathf.Clamp01(Charge + Time.deltaTime / chargeTime);
                onCharging.Invoke(Charge);
                return;
            }
            if (Held && Modifier && motor.IsGrounded && !motor.IsDashing) StartCharge();
        }

        void StartCharge()
        {
            IsCharging = true; Charge = 0f;
            motor.Anchored = true;
            ChargeStarted?.Invoke();
            onChargeStarted.Invoke();
        }

        /// <summary>충전을 버린다. 발사하지 않는다.</summary>
        public void Cancel()
        {
            if (!IsCharging) return;
            IsCharging = false; Charge = 0f;
            if (motor != null) motor.Anchored = false;
            ChargeCancelled?.Invoke();
            onChargeCancelled.Invoke();
        }

        void Launch()
        {
            float charge = Charge;
            float height = HeightForCharge(charge);
            if (manaCost > 0 && (Mana == null || !Mana.TrySpend(manaCost))) { Cancel(); return; }
            IsCharging = false; Charge = 0f;
            motor.Anchored = false;
            motor.LaunchVertical(height);
            LastLaunchHeight = height; LaunchCount++;
            Launched?.Invoke(charge, height);
            onLaunched.Invoke();
        }
    }
}
