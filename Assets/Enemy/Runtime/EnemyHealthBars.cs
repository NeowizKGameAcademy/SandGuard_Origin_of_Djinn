using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.Enemy
{
    /// <summary>피해를 받은 적 머리 위에 빨간 체력바를 띄운다. 화면 오버레이 캔버스 하나에 바를 풀링해 거리와 상관없이 같은 크기로 보인다.</summary>
    /// <remarks>
    /// 씬에 따로 놓지 않는다. EnemyHealth가 피해를 받으면 <see cref="Show"/>가 처음 한 번 스스로 만든다.
    /// 죽거나 풀로 돌아가면 바로 숨긴다. 벽에 가려져도 보인다. 보스는 HUD 보스 체력바가 있어 띄우지 않는다.
    /// </remarks>
    public sealed class EnemyHealthBars : MonoBehaviour
    {
        public Vector2 barSize = new Vector2(80f, 10f);
        [Tooltip("몸통 충돌체 윗면에서 바까지의 높이(m)")]
        public float heightOffset = .35f;
        [Tooltip("체력이 가득 찬 뒤 바를 숨기기까지의 시간")]
        public float hideDelay = 1.5f;
        public Color fillColor = new Color(.86f, .14f, .12f);
        public Color backColor = new Color(0f, 0f, 0f, .7f);

        sealed class Bar { public EnemyHealth health; public RectTransform root, fill; public float fullSince = -1f; }

        static EnemyHealthBars instance;
        readonly List<Bar> active = new List<Bar>();
        readonly Stack<Bar> pool = new Stack<Bar>();
        Canvas canvas;

        public static EnemyHealthBars Instance => instance;
        /// <summary>화면 오버레이 캔버스.</summary>
        public Canvas Canvas => canvas;

        /// <summary>적의 체력바를 표시한다. 이미 표시 중이면 위치·길이만 갱신한다.</summary>
        public static void Show(EnemyHealth health)
        {
            if (!Application.isPlaying || health == null || health.GetComponent<EnemyBossInfo>() != null) return;
            if (instance == null) instance = new GameObject("Enemy Health Bars").AddComponent<EnemyHealthBars>();
            instance.Track(health);
        }

        /// <summary>바가 목록에 있고 화면에 켜져 있는지.</summary>
        public bool IsShowing(EnemyHealth health)
        {
            var bar = Find(health);
            return bar != null && bar.root.gameObject.activeSelf;
        }

        void Awake()
        {
            if (instance == null) instance = this;
            var canvasObject = new GameObject("Enemy Health Bars Canvas", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5; // HUD(10)·건설 메뉴(40) 아래
            canvas.scaleFactor = ScreenScale();
        }

        /// <summary>
        /// 1920×1080 기준 배율(가로·세로 절반씩 반영, CanvasScaler와 같은 식). CanvasScaler는 Update에서야 적용돼
        /// 캔버스가 생긴 첫 프레임에 바 위치가 어긋나므로 직접 계산한다.
        /// </summary>
        static float ScreenScale() =>
            Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(Screen.width / 1920f, 2f), Mathf.Log(Screen.height / 1080f, 2f), .5f));

        void OnDestroy() { if (instance == this) instance = null; }

        void Track(EnemyHealth health)
        {
            var bar = Find(health);
            if (bar == null)
            {
                bar = pool.Count > 0 ? pool.Pop() : CreateBar();
                bar.health = health;
                active.Add(bar);
            }
            bar.fullSince = -1f;
            Place(bar, Camera.main);
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            canvas.scaleFactor = ScreenScale();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var bar = active[i];
                var health = bar.health;
                if (health == null || !health.isActiveAndEnabled || !health.IsAlive) { Release(i); continue; }
                if (health.CurrentHealth >= health.MaxHealth - .01f)
                {
                    if (bar.fullSince < 0f) bar.fullSince = Time.time;
                    else if (Time.time - bar.fullSince >= hideDelay) { Release(i); continue; }
                }
                else bar.fullSince = -1f;
                Place(bar, camera);
            }
        }

        void Place(Bar bar, Camera camera)
        {
            float ratio = Mathf.Clamp01(bar.health.CurrentHealth / bar.health.MaxHealth);
            bar.fill.sizeDelta = new Vector2((barSize.x - 4f) * ratio, barSize.y - 4f);
            bool visible = false;
            if (camera != null)
            {
                Vector3 screen = camera.WorldToScreenPoint(bar.health.TopPosition + Vector3.up * heightOffset);
                if (screen.z > 0f)
                {
                    // 캔버스 중심 기준 좌표 = (화면 픽셀 - 화면 중심) / 배율
                    bar.root.anchoredPosition = ((Vector2)screen - new Vector2(Screen.width, Screen.height) * .5f) / ScreenScale();
                    visible = true;
                }
            }
            if (bar.root.gameObject.activeSelf != visible) bar.root.gameObject.SetActive(visible);
        }

        void Release(int index)
        {
            var bar = active[index];
            active.RemoveAt(index);
            bar.health = null;
            bar.root.gameObject.SetActive(false);
            pool.Push(bar);
        }

        Bar Find(EnemyHealth health)
        {
            foreach (var bar in active) if (bar.health == health) return bar;
            return null;
        }

        Bar CreateBar()
        {
            var back = new GameObject("Health Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            back.rectTransform.SetParent(canvas.transform, false);
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(.5f, .5f);
            back.rectTransform.pivot = new Vector2(.5f, 0f);
            back.rectTransform.sizeDelta = barSize;
            back.color = backColor; back.raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.rectTransform.SetParent(back.rectTransform, false);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = fill.rectTransform.pivot = new Vector2(0f, .5f);
            fill.rectTransform.anchoredPosition = new Vector2(2f, 0f);
            fill.color = fillColor; fill.raycastTarget = false;
            return new Bar { root = back.rectTransform, fill = fill.rectTransform };
        }
    }
}
