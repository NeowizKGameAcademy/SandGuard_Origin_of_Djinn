using System;
using System.Collections.Generic;
using SandGuard.GameFlow;
using SandGuard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DesertTower.LevelIntegration
{
    /// <summary>Scene-authored opening shots, followed by a blend to the live player camera.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class LevelOpeningCinematic : MonoBehaviour
    {
        [Serializable]
        public sealed class Shot
        {
            public string title;
            public string description;
            public Vector3 from, to, lookAt;
            [Range(20f, 100f)] public float fieldOfView = 58f;
            [Min(.2f)] public float seconds = 1.3f;
        }

        public bool playOnStart = true;
        public PlayerCameraRig playerCamera;
        public TMP_FontAsset font;
        public Shot[] shots = Array.Empty<Shot>();
        [Min(.1f)] public float returnSeconds = 1.2f;
        [Min(.1f)] public float skipReturnSeconds = .45f;
        public bool IsPlaying { get; private set; }
        public bool HasCompleted { get; private set; }
        public int CurrentShotIndex => shotIndex;

        readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        Camera view;
        GameManager manager;
        Canvas overlay;
        CanvasGroup captions;
        Image curtain;
        TMP_Text titleText, descriptionText, countText;
        RectTransform topBar, bottomBar;
        Vector3 gameplayPosition, returnPosition;
        Quaternion gameplayRotation, returnRotation;
        float gameplayFov, returnFov, elapsed, blendElapsed, blendDuration;
        CursorLockMode previousLock;
        bool previousVisible, ownsPause, returning, skipRequested, hasCameraPose, hasPresentation;
        int shotIndex;

        void Awake()
        {
            // Awake runs before every Start/Update, including the automatic wave countdown.
            if (!playOnStart) return;
            manager = GameManager.Instance;
            manager.RequestPause(this);
            ownsPause = true;
        }

        void Start()
        {
            if (!playOnStart) return;
            if (!playerCamera)
                foreach (var candidate in FindObjectsByType<PlayerCameraRig>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene && candidate.isActiveAndEnabled)
                    { playerCamera = candidate; break; }
            view = playerCamera ? playerCamera.GetComponent<Camera>() : null;
            if (!view || !view.isActiveAndEnabled || shots == null || shots.Length == 0
                || Array.Exists(shots, shot => shot == null))
            {
                Debug.LogWarning("Level opening: a player camera and at least one shot are required.", this);
                Finish();
                return;
            }

            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            hasPresentation = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.gameObject.scene == gameObject.scene && canvas.renderMode != RenderMode.WorldSpace && canvas.enabled)
                { hiddenCanvases.Add(canvas); canvas.enabled = false; }
            BuildOverlay();
            IsPlaying = true;
            ShowShot();
        }

        void Update()
        {
            // Ignore the click that entered the scene. Resume only in LateUpdate, after input consumers.
            if (!IsPlaying || returning || (shotIndex == 0 && elapsed < .25f)) return;
            if ((Keyboard.current?.spaceKey.wasPressedThisFrame ?? false)
                || (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
                || (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false)) Skip();
        }

        public void Skip()
        {
            if (IsPlaying && !returning) skipRequested = true;
        }

        void LateUpdate()
        {
            if (!IsPlaying) return;
            if (!view || !playerCamera || !playerCamera.isActiveAndEnabled) { Finish(); return; }

            // PlayerCameraRig has already calculated the real, collision-corrected pose this frame.
            // Overriding only the rendered pose keeps its yaw, smoothing and camera effects intact.
            gameplayPosition = view.transform.position;
            gameplayRotation = view.transform.rotation;
            gameplayFov = view.fieldOfView;
            // The first frame can include scene loading/shader warm-up. Do not spend a shot
            // on that work, or jump through it after a hitch before the player can see it.
            float dt = hasCameraPose ? Mathf.Min(Time.unscaledDeltaTime, .1f) : 0f;
            hasCameraPose = true;
            if (skipRequested && !returning)
            {
                EvaluateShot(shots[shotIndex], elapsed, out var position, out var rotation);
                BeginReturn(position, rotation, shots[shotIndex].fieldOfView, skipReturnSeconds);
            }
            if (returning)
            {
                blendElapsed += dt;
                float t = Mathf.Clamp01(blendElapsed / blendDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                view.transform.SetPositionAndRotation(Vector3.Lerp(returnPosition, gameplayPosition, ease),
                    Quaternion.Slerp(returnRotation, gameplayRotation, ease));
                view.fieldOfView = Mathf.Lerp(returnFov, gameplayFov, ease);
                SetPresentation(1f - ease, 0f);
                // A held skip must not become a jump/attack on the first gameplay frame.
                if (t >= 1f && !SkipHeld()) Finish();
                return;
            }

            elapsed += dt;
            var shot = shots[shotIndex];
            float duration = Mathf.Max(.2f, shot.seconds);
            EvaluateShot(shot, elapsed, out var cameraPosition, out var cameraRotation);
            view.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            view.fieldOfView = shot.fieldOfView;
            float fade = 1f - Mathf.Clamp01(elapsed / (shotIndex == 0 ? .35f : .12f));
            if (shotIndex < shots.Length - 1) fade = Mathf.Max(fade, 1f - Mathf.Clamp01((duration - elapsed) / .12f));
            SetPresentation(1f, fade);
            if (elapsed < duration) return;
            if (shotIndex == shots.Length - 1)
                BeginReturn(cameraPosition, cameraRotation, shot.fieldOfView, returnSeconds);
            else { shotIndex++; elapsed = 0f; ShowShot(); }
        }

        static void EvaluateShot(Shot shot, float time, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Lerp(shot.from, shot.to, Mathf.SmoothStep(0f, 1f, time / Mathf.Max(.2f, shot.seconds)));
            Vector3 direction = shot.lookAt - position;
            rotation = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        }

        static bool SkipHeld() => (Keyboard.current?.spaceKey.isPressed ?? false)
            || (Mouse.current?.leftButton.isPressed ?? false) || (Gamepad.current?.buttonSouth.isPressed ?? false);

        void BeginReturn(Vector3 position, Quaternion rotation, float fov, float duration)
        {
            returning = true;
            returnPosition = position; returnRotation = rotation; returnFov = fov;
            blendDuration = Mathf.Max(.1f, duration); blendElapsed = 0f;
            titleText.text = "방어를 준비하세요";
            descriptionText.text = "곧 전투 준비가 시작됩니다";
        }

        void ShowShot()
        {
            titleText.text = shots[shotIndex].title;
            descriptionText.text = shots[shotIndex].description;
            countText.text = $"SAND GUARD   /   {shotIndex + 1:00} — {shots.Length:00}";
        }

        void SetPresentation(float amount, float black)
        {
            captions.alpha = amount;
            topBar.sizeDelta = new Vector2(0f, 82f * amount);
            bottomBar.sizeDelta = new Vector2(0f, 126f * amount);
            curtain.color = new Color(0f, 0f, 0f, black);
        }

        void Finish()
        {
            bool completed = IsPlaying;
            IsPlaying = false;
            if (hasCameraPose && view)
            {
                view.transform.SetPositionAndRotation(gameplayPosition, gameplayRotation);
                view.fieldOfView = gameplayFov;
            }
            hasCameraPose = false;
            foreach (var canvas in hiddenCanvases) if (canvas) canvas.enabled = true;
            hiddenCanvases.Clear();
            if (overlay) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); overlay = null; }
            if (ownsPause && manager) manager.ReleasePause(this);
            ownsPause = false;
            if (hasPresentation)
            {
                // Another menu may still own a pause. Do not steal its cursor or unpause it.
                bool paused = manager && manager.IsPaused;
                Cursor.lockState = paused ? CursorLockMode.None : previousLock;
                Cursor.visible = paused || previousVisible;
                hasPresentation = false;
            }
            HasCompleted |= completed;
        }

        void OnDisable() => Finish();

        void BuildOverlay()
        {
            var go = new GameObject("Level Opening Overlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            overlay = go.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
            var blocker = Panel("Input Blocker", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            blocker.raycastTarget = true;
            topBar = Panel("Top Letterbox", go.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, 82), Color.black).rectTransform;
            bottomBar = Panel("Bottom Letterbox", go.transform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 126), Color.black).rectTransform;
            var textRoot = new GameObject("Captions", typeof(RectTransform), typeof(CanvasGroup));
            textRoot.transform.SetParent(go.transform, false);
            var rect = (RectTransform)textRoot.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            captions = textRoot.GetComponent<CanvasGroup>(); captions.blocksRaycasts = false;
            var gold = new Color(.94f, .78f, .48f);
            countText = Text("Chapter", rect, new Vector2(56, -40), new Vector2(800, 42), true, 22, gold);
            titleText = Text("Location", rect, new Vector2(56, 77), new Vector2(1150, 52), false, 34, gold);
            descriptionText = Text("Description", rect, new Vector2(58, 36), new Vector2(1150, 38), false, 22, new Color(.84f, .82f, .77f));
            var hint = Text("Skip Hint", rect, new Vector2(-56, 56), new Vector2(520, 40), false, 21, new Color(.84f, .82f, .77f));
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.pivot = new Vector2(1, .5f); hint.alignment = TextAlignmentOptions.Right;
            hint.text = "클릭 / SPACE  ·  건너뛰기";
            curtain = Panel("Shot Fade", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Color.black);
        }

        static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            var rect = image.rectTransform; rect.anchorMin = min; rect.anchorMax = max;
            rect.pivot = new Vector2(.5f, min.y); rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
            image.color = color; image.raycastTarget = false;
            return image;
        }

        TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, bool top, int fontSize, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = font ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize; text.color = color; text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, top ? 1 : 0);
            rect.pivot = new Vector2(0, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            return text;
        }
    }
}
