using System.Collections.Generic;
using SandGuard.Enemy;
using SandGuard.Player;
using Tower;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SandGuard.UI.HUD
{
    /// <summary>
    /// 미니맵. 코어를 중심으로 고정된 북쪽 고정(위 = +Z) 직교 카메라가 전체 전장을 그리고, 그 위에 마커를 얹는다.
    /// 마커: 플레이어(화살표, 바라보는 방향으로 회전), 적, 코어, 보스, 종류별 타워.
    /// 플레이어(PlayerHealth)가 없는 씬(HUD.unity)에서는 아무것도 하지 않아 빈 판이 그대로 보인다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class MinimapController : MonoBehaviour
    {
        [SerializeField] private GameHUDController hud;
        [Tooltip("전체 전장을 담는 지도 반경(m). 카메라 orthographicSize")] public float viewRadius = 75f;
        [Tooltip("고정 미니맵 카메라 높이(m)")] public float cameraHeight = 120f;
        [Tooltip("코어 기준 미니맵 중심 보정값(XZ)")] public Vector2 mapCenterOffset;
        public int textureSize = 512;
        public Color groundColor = new Color(.16f, .12f, .09f, 1f);
        [Tooltip("지도에 그릴 레이어. 기본은 UI 제외 전부")] public LayerMask cullingMask = ~(1 << 5);
        [Tooltip("적 목록을 다시 찾는 간격(초)")] public float enemyScanInterval = .25f;
        [Tooltip("지도 배경을 다시 그리는 간격(실시간 초). 카메라가 고정이고 움직이는 것은 마커로 그리므로 매 프레임 그릴 필요가 없다. 0이면 매 프레임 그린다")]
        public float redrawInterval = 3f;

        [Header("타워 마커")]
        public Sprite cobraMarker, obeliskMarker, coffinMarker, anubisMarker;
        public Vector2 towerMarkerSize = new Vector2(20f, 20f);
        public Sprite towerBackground;
        [Min(0f)] public float towerBackgroundPadding = 4f;
        public Color cobraBackgroundColor = new Color(.12f, .62f, .28f, 1f);
        public Color obeliskBackgroundColor = new Color(.12f, .42f, .88f, 1f);
        public Color coffinBackgroundColor = new Color(.62f, .25f, .82f, 1f);
        public Color anubisBackgroundColor = new Color(.06f, .66f, .70f, 1f);
        [Min(.05f)] public float towerScanInterval = .25f;

        [Header("마커")]
        public Sprite playerMarker; public Sprite enemyMarker; public Sprite coreMarker; public Sprite bossMarker;
        public Vector2 playerMarkerSize = new Vector2(20, 20), enemyMarkerSize = new Vector2(8, 8), coreMarkerSize = new Vector2(14, 14), bossMarkerSize = new Vector2(18, 18);
        public Color playerColor = new Color(1f, .93f, .7f), enemyColor = new Color(1f, .3f, .22f), coreColor = new Color(.35f, .9f, 1f), bossColor = new Color(1f, .12f, .1f);
        [Tooltip("가장자리에 붙는 마커가 틀에서 떨어지는 여백(px)")] public float edgeInset = 8f;

        Transform player, core; Camera cam; RenderTexture texture;
        RectTransform playerDot, coreDot, bossDot;
        readonly List<RectTransform> enemyDots = new List<RectTransform>();
        readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
        readonly List<TowerHealth> towers = new List<TowerHealth>();
        readonly List<Image> towerDots = new List<Image>();
        readonly List<Image> towerIcons = new List<Image>();
        float nextTowerScan;
        float nextScan, nextRedraw;
        bool drawing;
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<Renderer> scratch = new List<Renderer>();

        public Camera MapCamera => cam;
        public Texture MapTexture => texture;
        public MinimapHUD View => hud != null ? hud.Minimap : null;
        /// <summary>다음 프레임에 지도 배경을 다시 그린다. 시설이 생기거나 부서지는 등 지형이 바뀌었을 때 부른다.</summary>
        public void RequestRedraw() => nextRedraw = 0f;
        public bool PlacePreview(RectTransform marker, Vector3 world) => cam != null && Place(marker, world, true);
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
            // 평소에는 꺼 두고 redrawInterval마다 한 프레임만 켠다. 전장 전체를 그리는 데 2~3ms가 들어 웨이브 중 매 프레임 그리면 아깝다.
            cam.enabled = redrawInterval <= 0f;
            RenderPipelineManager.beginCameraRendering += HideMovingBodies;
            RenderPipelineManager.endCameraRendering += RestoreMovingBodies;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 위에서 아래로, 화면 위 = 세계 +Z
            PositionFixedCamera();
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
            UpdateMarkers();
            UpdateTowerMarkers();
            ScheduleRedraw();
        }

        // 켠 프레임의 렌더가 끝났으면 다음 LateUpdate에서 끈다. RenderTexture는 마지막 그림을 유지한다.
        void ScheduleRedraw()
        {
            if (cam == null) return;
            if (redrawInterval <= 0f) { cam.enabled = true; return; }
            if (drawing) { cam.enabled = false; drawing = false; }
            if (Time.unscaledTime < nextRedraw) return;
            nextRedraw = Time.unscaledTime + redrawInterval;
            cam.enabled = true; drawing = true;
        }

        // 배경을 가끔만 그리므로 적·플레이어 몸이 찍히면 멈춘 채 남는다. 이 카메라가 그리는 동안에만 숨긴다(마커가 대신 보여 준다).
        void HideMovingBodies(ScriptableRenderContext context, Camera camera)
        {
            if (camera != cam || redrawInterval <= 0f) return;
            hidden.Clear();
            foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) Hide(enemy.transform);
            if (player != null) Hide(player);
        }

        void Hide(Transform root)
        {
            root.GetComponentsInChildren(scratch);
            foreach (var renderer in scratch)
                if (!renderer.forceRenderingOff) { renderer.forceRenderingOff = true; hidden.Add(renderer); }
        }

        void RestoreMovingBodies(ScriptableRenderContext context, Camera camera)
        {
            if (camera != cam) return;
            foreach (var renderer in hidden) if (renderer != null) renderer.forceRenderingOff = false;
            hidden.Clear();
        }

        void PositionFixedCamera()
        {
            Vector3 center = core != null ? core.position : player.position;
            cam.transform.position = new Vector3(
                center.x + mapCenterOffset.x,
                center.y + cameraHeight,
                center.z + mapCenterOffset.y);
        }

        // 세계 좌표 → 고정된 전체 지도 픽셀(지도 중심 기준).
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
            Place(playerDot, player.position, true);
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

        void UpdateTowerMarkers()
        {
            // 비일시정지 시간으로 스캔하여 건설 중에도 새 타워가 표시된다.
            if (Time.unscaledTime >= nextTowerScan)
            {
                nextTowerScan = Time.unscaledTime + Mathf.Max(.05f, towerScanInterval);
                towers.Clear();
                foreach (var tower in FindObjectsByType<TowerHealth>(FindObjectsSortMode.None))
                    if (tower.isActiveAndEnabled && tower.CurrentHealth > 0f) towers.Add(tower);
            }

            while (towerDots.Count < towers.Count)
            {
                var dot = Map.AddMarker("Tower", towerBackground, Color.white,
                    towerMarkerSize + Vector2.one * towerBackgroundPadding);
                dot.SetAsFirstSibling(); // 주요 마커를 가리지 않는다.
                var outline = dot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.04f, .05f, .08f, 1f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                var icon = Map.AddMarker("Icon", null, Color.white, towerMarkerSize);
                icon.SetParent(dot, false);
                towerDots.Add(dot.GetComponent<Image>());
                towerIcons.Add(icon.GetComponent<Image>());
            }

            for (int i = 0; i < towerDots.Count; i++)
            {
                var image = towerDots[i];
                var tower = i < towers.Count ? towers[i] : null;
                bool show = tower != null && tower.isActiveAndEnabled && tower.CurrentHealth > 0f;
                if (show)
                {
                    var status = tower.GetComponent<TowerStatus>();
                    var icon = towerIcons[i];
                    icon.sprite = status != null ? TowerSprite(status.towerType) : null;
                    icon.rectTransform.sizeDelta = towerMarkerSize;
                    image.color = status != null ? TowerBackgroundColor(status.towerType) : Color.white;
                    image.rectTransform.sizeDelta = towerMarkerSize + Vector2.one * towerBackgroundPadding;
                    show = icon.sprite != null && Place(image.rectTransform, tower.transform.position, false);
                }
                image.gameObject.SetActive(show);
            }
        }

        Sprite TowerSprite(TowerType type)
        {
            switch (type)
            {
                case TowerType.Cobra: return cobraMarker;
                case TowerType.Obelisk: return obeliskMarker;
                case TowerType.Coffin: return coffinMarker;
                case TowerType.Anubis: return anubisMarker;
                default: return null;
            }
        }

        Color TowerBackgroundColor(TowerType type)
        {
            switch (type)
            {
                case TowerType.Cobra: return cobraBackgroundColor;
                case TowerType.Obelisk: return obeliskBackgroundColor;
                case TowerType.Coffin: return coffinBackgroundColor;
                case TowerType.Anubis: return anubisBackgroundColor;
                default: return Color.white;
            }
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= HideMovingBodies;
            RenderPipelineManager.endCameraRendering -= RestoreMovingBodies;
            foreach (var renderer in hidden) if (renderer != null) renderer.forceRenderingOff = false;
            if (cam != null) Destroy(cam.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
