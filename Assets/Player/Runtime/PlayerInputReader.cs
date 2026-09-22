using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SandGuard.Player
{
    [DefaultExecutionOrder(-300)]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [Tooltip("원본은 변경하지 않고 런타임 복제본을 사용합니다.")]
        public InputActionAsset actions;
        public bool captureCursor = true;
        InputActionAsset instance;
        InputActionMap map;
        InputAction move, look, attack, jump;
        bool focused = true;
        public bool GameplayEnabled { get; set; } = true;
        public bool SuppressCursorCapture { get; set; }
        public bool AcceptsInput => GameplayEnabled && focused && Time.timeScale > 0f
            && (!captureCursor || Cursor.lockState == CursorLockMode.Locked);
        public Vector2 Move => AcceptsInput ? move?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
        public Vector2 Look => AcceptsInput ? look?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
        public bool LookIsPointer => look?.activeControl?.device is Pointer;
        public bool PrimaryAttackHeld => AcceptsInput && (attack?.IsPressed() ?? false);
        /// <summary>점프 버튼을 계속 누르고 있는지. 가변 점프 높이에 쓴다.</summary>
        public bool JumpHeld => AcceptsInput && (jump?.IsPressed() ?? false);
        /// <summary>왼쪽 Ctrl을 누르고 있는지. Ctrl + Space가 차지 점프(상승 기류)다.</summary>
        public bool ChargeModifierHeld => AcceptsInput && Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        /// <summary>스킬 키 개수(Q, E, R, F). 스킬트리의 장착 칸과 순서가 같다.</summary>
        public const int SkillKeyCount = 4;
        /// <summary>공격 스킬 키(0 = Q, 1 = E, 2 = R, 3 = F)를 계속 누르고 있는지. 입력이 막히면 false.</summary>
        public bool SkillHeld(int slot) => AcceptsInput && map != null && (map.FindAction("Skill" + (slot + 1))?.IsPressed() ?? false);
        public event Action PrimaryActionPressed;
        /// <summary>마우스 우클릭(Spell 액션). 지금은 흔적 귀환(move.recall)이 쓴다.</summary>
        public event Action SpellPressed;
        public event Action JumpPressed;
        public event Action DashPressed;
        /// <summary>공격 스킬 키. 0 = Q(Skill1), 1 = E(Skill2), 2 = R(Skill3), 3 = F(Skill4). 입력 에셋에 해당 액션이 없으면 오지 않는다.</summary>
        public event Action<int> SkillPressed;
        /// <summary>공격 스킬 키를 뗀 순간. 충전형 스킬(관통탄)이 발사 시점으로 쓴다. 입력이 막힌 채로 떼면 오지 않으니 충전 쪽은 <see cref="SkillHeld"/>도 같이 봐야 한다.</summary>
        public event Action<int> SkillReleased;
        public event Action<int> SlotSelected;
        public event Action BuildModeToggled;
        /// <summary>인터페이스 호환용. 회전 입력은 기획에 없어 발생하지 않는다.</summary>
        public event Action<int> BuildRotationRequested { add { } remove { } }
        public event Action CancelPressed;
        public event Action PausePressed;

        void OnEnable()
        {
            if (actions == null) { Debug.LogError("Player Input: Input Actions를 연결하세요.", this); return; }
            instance = Instantiate(actions);
            map = instance.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            attack = map.FindAction("Attack", true);
            jump = map.FindAction("Jump", true);
            map.Enable();
            if (captureCursor) SetCursor(true);
        }

        void Update()
        {
            if (map == null || !focused) return;
            if (Pressed("Pause"))
            {
                if (captureCursor) SetCursor(false);
                PausePressed?.Invoke();
                CancelPressed?.Invoke();
                return;
            }
            if (captureCursor && Cursor.lockState != CursorLockMode.Locked)
            {
                if (!SuppressCursorCapture && attack.WasPressedThisFrame() && GameplayEnabled && Time.timeScale > 0f) SetCursor(true);
                return;
            }
            if (!AcceptsInput) return;
            if (attack.WasPressedThisFrame()) PrimaryActionPressed?.Invoke();
            if (jump.WasPressedThisFrame()) JumpPressed?.Invoke();
            if (Pressed("Spell")) SpellPressed?.Invoke();
            if (Pressed("Dash")) DashPressed?.Invoke();
            for (int i = 0; i < SkillKeyCount; i++)
            {
                if (Pressed("Skill" + (i + 1))) SkillPressed?.Invoke(i);
                if (Released("Skill" + (i + 1))) SkillReleased?.Invoke(i);
            }
            if (Pressed("BuildMode")) BuildModeToggled?.Invoke();
            if (Pressed("Cancel")) CancelPressed?.Invoke();
            for (int i = 0; i < 9; i++) if (Pressed("Slot" + (i + 1))) SlotSelected?.Invoke(i);
        }

        bool Pressed(string name) => map.FindAction(name)?.WasPressedThisFrame() ?? false;
        bool Released(string name) => map.FindAction(name)?.WasReleasedThisFrame() ?? false;
        void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && captureCursor) SetCursor(false);
        }
        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        void OnDisable()
        {
            if (instance != null) { instance.Disable(); Destroy(instance); }
            instance = null; map = null; move = look = attack = jump = null;
            if (captureCursor) SetCursor(false);
        }
    }
}
