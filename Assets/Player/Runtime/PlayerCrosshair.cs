using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.Player
{
    /// <summary>
    /// 화면 중앙 조준점. <see cref="PlayerAimer"/>가 화면 중앙 광선으로 조준하므로 여기가 실제 탄착 방향이다.
    /// 필요한 Canvas와 이미지는 실행 시 스스로 만들며, 커서가 풀리거나 사망하면 숨긴다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerCrosshair : MonoBehaviour
    {
        [Tooltip("커서 잠금이 풀리거나 일시정지면 숨긴다. 비워도 된다")]
        public PlayerInputReader input;
        [Tooltip("이동·공중에서 조준점을 벌린다. 비워도 된다")]
        public PlayerMotor motor;
        [Tooltip("ILifeState 구현 컴포넌트. 사망하면 숨긴다")]
        public MonoBehaviour lifeSource;
        [Header("모양 (1080p 기준 픽셀)")]
        public Color color = new Color(1f, 1f, 1f, 0.9f);
        public Color outlineColor = new Color(0f, 0f, 0f, 0.7f);
        [Min(0f)] public float lineLength = 9f;
        [Min(1f)] public float thickness = 2f;
        [Min(0f), Tooltip("중앙에서 선까지의 기본 간격")] public float gap = 5f;
        [Min(0f), Tooltip("0이면 중앙 점을 그리지 않는다")] public float dotSize = 2.5f;
        [Header("퍼짐")]
        [Min(0f), Tooltip("최고 속도로 이동할 때 더해지는 간격")] public float moveSpread = 5f;
        [Min(0f), Tooltip("공중에서 더해지는 간격")] public float airSpread = 7f;
        [Min(0f)] public float spreadSmoothTime = 0.08f;
        public bool Visible => group != null && group.alpha > 0.5f;
        public float Spread => spread;
        public RectTransform Root => root;
        Canvas canvas;
        CanvasGroup group;
        RectTransform root, dot;
        readonly RectTransform[] lines = new RectTransform[4];
        float spread, spreadVelocity;

        void Awake() => Build();
        void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }

        void Build()
        {
            if (canvas != null) return;
            var canvasObject = new GameObject("Crosshair Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            root = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasObject.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero; root.sizeDelta = Vector2.zero;
            for (int i = 0; i < 4; i++) lines[i] = Image("Line " + i);
            dot = Image("Dot");
            Layout();
        }

        RectTransform Image(string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<Image>();
            image.raycastTarget = false; image.color = color;
            var outline = image.GetComponent<Outline>();
            outline.effectColor = outlineColor; outline.effectDistance = new Vector2(1f, -1f); outline.useGraphicAlpha = true;
            var rect = image.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return rect;
        }

        void LateUpdate()
        {
            if (canvas == null) return;
            bool alive = !(lifeSource is ILifeState life) || life.State == global::LifeState.Alive;
            bool visible = alive && (input == null || input.AcceptsInput);
            group.alpha = visible ? 1f : 0f;
            float target = 0f;
            if (motor != null)
            {
                float speed = Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude;
                target += moveSpread * Mathf.Clamp01(speed / Mathf.Max(0.01f, motor.MoveSpeed));
                if (!motor.IsGrounded) target += airSpread;
            }
            spread = spreadSmoothTime <= 0f ? target : Mathf.SmoothDamp(spread, target, ref spreadVelocity, spreadSmoothTime);
            Layout();
        }

        void Layout()
        {
            float offset = gap + spread + lineLength * 0.5f;
            Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            for (int i = 0; i < 4; i++)
            {
                bool vertical = directions[i].x == 0f;
                lines[i].sizeDelta = vertical ? new Vector2(thickness, lineLength) : new Vector2(lineLength, thickness);
                lines[i].anchoredPosition = directions[i] * offset;
                lines[i].GetComponent<Image>().color = color;
            }
            dot.gameObject.SetActive(dotSize > 0f);
            dot.sizeDelta = Vector2.one * dotSize;
            dot.anchoredPosition = Vector2.zero;
            dot.GetComponent<Image>().color = color;
        }
    }
}
