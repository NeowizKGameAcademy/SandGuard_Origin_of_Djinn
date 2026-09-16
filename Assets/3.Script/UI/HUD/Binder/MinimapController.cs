using System.Collections.Generic;
using SandGuard.Enemy;
using SandGuard.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SandGuard.UI.HUD
{
    /// <summary>
    /// 미니맵. 플레이어 위를 따라다니는 북쪽 고정(위 = +Z) 직교 카메라가 RenderTexture에 지형을 그리고, 그 위에 마커를 얹는다.
    /// 마커: 플레이어(화살표, 바라보는 방향으로 회전), 적(작은 점, 반경 밖이면 숨김), 코어·보스(반경 밖이면 가장자리에 붙임).
    /// 플레이어(PlayerHealth)가 없는 씬(HUD.unity)에서는 아무것도 하지 않아 빈 판이 그대로 보인다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class MinimapController : MonoBehaviour
    {
        [SerializeField] private GameHUDController hud;
        [Tooltip("지도에 보이는 반경(m). 카메라 orthographicSize")] public float viewRadius = 30f;
        [Tooltip("플레이어 위 카메라 높이(m)")] public float cameraHeight = 60f;
        public int textureSize = 512;
        public Color groundColor = new Color(.16f, .12f, .09f, 1f);
        [Tooltip("지도에 그릴 레이어. 기본은 UI 제외 전부")] public LayerMask cullingMask = ~(1 << 5);
        [Tooltip("적 목록을 다시 찾는 간격(초)")] public float enemyScanInterval = .25f;

        [Header("마커")]
        public Sprite playerMarker; public Sprite enemyMarker; public Sprite coreMarker; public Sprite bossMarker;
        public Vector2 playerMarkerSize = new Vector2(20, 20), enemyMarkerSize = new Vector2(8, 8), coreMarkerSize = new Vector2(14, 14), bossMarkerSize = new Vector2(18, 18);
        public Color playerColor = new Color(1f, .93f, .7f), enemyColor = new Color(1f, .3f, .22f), coreColor = new Color(.35f, .9f, 1f), bossColor = new Color(1f, .12f, .1f);
        [Tooltip("가장자리에 붙는 마커가 틀에서 떨어지는 여백(px)")] public float edgeInset = 8f;

        Transform player, core; Camera cam; RenderTexture texture;
        RectTransform playerDot, coreDot, bossDot;
        readonly List<RectTransform> enemyDots = new List<RectTransform>();
        readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
        float nextScan;

        public Camera MapCamera => cam;
        public Texture MapTexture => texture;
        MinimapHUD Map => hud.Minimap;

        void Awake() { if (hud == null) hud = GetComponent<GameHUDController>(); }

        void Start()
        {
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health == null || hud == null || hud.Minimap == null) return;
            player = health.transform;
            var coreBehaviour = GameHUDPresenter.FindCore();
            core = coreBehaviour != null ? coreBehaviour.transform : null;
            BuildCamera();
            BuildMarkers();
            Map.SetTexture(texture);
        }

        void BuildCamera()
        {
            var go = new GameObject("MinimapCamera");
            cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = viewRadius;
            cam.nearClipPlane = .3f; cam.farClipPlane = cameraHeight + 200f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = groundColor;
            cam.cullingMask = cullingMask; cam.depth = -50f; cam.allowMSAA = false; cam.useOcclusionCulling = false;
            // 일부 지형 셰이더가 알파를 1 미만으로 쓴다. RawImage 쪽 재질(SandGuard/UI/Minimap Opaque)이 알파를 무시하므로 여기서는 신경 쓰지 않는다.
            texture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32) { name = "MinimapRT" };
            cam.targetTexture = texture;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderShadows = false; data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 위에서 아래로, 화면 위 = 세계 +Z
            Follow();
        }

        void BuildMarkers()
        {
            coreDot = Map.AddMarker("Core", coreMarker, coreColor, coreMarkerSize);
            bossDot = Map.AddMarker("Boss", bossMarker, bossColor, bossMarkerSize);
            playerDot = Map.AddMarker("Player", playerMarker, playerColor, playerMarkerSize);
            coreDot.gameObject.SetActive(core != null);
            bossDot.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (player == null) return;
            Follow();
            UpdateMarkers();
        }

        void Follow()
        {
            var p = player.position;
            cam.transform.position = new Vector3(p.x, p.y + cameraHeight, p.z);
        }

        // 세계 좌표 → 지도 픽셀(지도 중심 기준). 카메라가 플레이어 위에 있으므로 플레이어가 항상 중심이다.
        Vector2 ToMap(Vector3 world)
        {
            var rect = Map.MarkerRoot.rect;
            var d = world - cam.transform.position;
            return new Vector2(d.x / viewRadius * rect.width * .5f, d.z / viewRadius * rect.height * .5f);
        }

        // 반경 안이면 그 자리에, 밖이면 clampToEdge일 때만 가장자리에 붙인다. 놓았으면 true.
        bool Place(RectTransform dot, Vector3 world, bool clampToEdge)
        {
            var rect = Map.MarkerRoot.rect;
            var p = ToMap(world);
            var half = new Vector2(rect.width * .5f - edgeInset, rect.height * .5f - edgeInset);
            bool inside = Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y;
            if (!inside && !clampToEdge) return false;
            dot.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
            return true;
        }

        void UpdateMarkers()
        {
            playerDot.anchoredPosition = Vector2.zero;
            playerDot.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y); // 화살표 위 = 북쪽(+Z)
            if (core != null) Place(coreDot, core.position, true);

            var boss = EnemyBossInfo.Active.Count > 0 ? EnemyBossInfo.Active[0] : null;
            bossDot.gameObject.SetActive(boss != null);
            if (boss != null) Place(bossDot, boss.transform.position, true);

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + enemyScanInterval;
                enemies.Clear();
                foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                    if (enemy.IsAlive && enemy.GetComponent<EnemyBossInfo>() == null) enemies.Add(enemy);
            }
            while (enemyDots.Count < enemies.Count)
            {
                var dot = Map.AddMarker("Enemy", enemyMarker, enemyColor, enemyMarkerSize);
                dot.SetAsFirstSibling(); // 코어·보스·플레이어 아래에 그린다
                enemyDots.Add(dot);
            }
            for (int i = 0; i < enemyDots.Count; i++)
            {
                bool show = i < enemies.Count && enemies[i] != null && enemies[i].IsAlive && Place(enemyDots[i], enemies[i].transform.position, false);
                enemyDots[i].gameObject.SetActive(show);
            }
        }

        void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
