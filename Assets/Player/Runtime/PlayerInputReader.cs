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
        public event Action PrimaryActionPressed;
        public event Action SpellPressed;
        public event Action JumpPressed;
        public event Action DashPressed;
        public event Action<int> SlotSelected;
        public event Action BuildModeToggled;
        public event Action<int> BuildRotationRequested;
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
            if (Pressed("BuildMode")) BuildModeToggled?.Invoke();
            if (Pressed("Rotate")) BuildRotationRequested?.Invoke(1);
            if (Pressed("Cancel")) CancelPressed?.Invoke();
            for (int i = 0; i < 9; i++) if (Pressed("Slot" + (i + 1))) SlotSelected?.Invoke(i);
        }

        bool Pressed(string name) => map.FindAction(name)?.WasPressedThisFrame() ?? false;
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
