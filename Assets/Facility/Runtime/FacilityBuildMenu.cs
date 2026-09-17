using System.Collections.Generic;
using SandGuard.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SandGuard.Facility
{
    /// <summary>
    /// 받침대 근처에서 뜨는 시설 메뉴. 빈 받침대면 건설(번호 키 Slot1~9 = 카탈로그 순서, 마나 비용 표시),
    /// 체력이 깎인 시설이면 수리(번호 키 1, 비용 표시)를 한다.
    /// </summary>
    /// <remarks>UI는 Docs/ui/facillity-implementation-ui.png에서 잘라 낸 스프라이트로 코드에서 만든다. 캔버스는 이 오브젝트 아래에 생성된다.</remarks>
    public sealed class FacilityBuildMenu : MonoBehaviour
    {
        public enum MenuMode { Build, Repair }
        [Tooltip("비우면 씬에서 찾는다")]
        public FacilityBuildService service;
        [Tooltip("거리 판정에 쓰는 플레이어. 비우면 PlayerInputReader 또는 PlayerMotor를 찾는다")]
        public Transform player;
        public Font font;
        public Sprite discSprite, ringSprite;
        public Sprite[] numberSprites = new Sprite[0];
        public Vector2 tileSize = new Vector2(104f, 104f);
        public float tileSpacing = 14f;
        public string title = "건설  —  번호 키";
        public string repairTitle = "수리  —  1";
        public FacilityAnchor Current { get; private set; }
        public bool IsOpen => Current != null;
        public MenuMode Mode { get; private set; }
        public BuildResult LastResult { get; private set; }
        public ActionResult LastRepairResult { get; private set; }
        Text repairCostText;
        int shownRepairCost = -1;
        PlayerInputReader input;
        Canvas canvas;
        RectTransform panel;
        Text titleText;
        readonly List<GameObject> tiles = new List<GameObject>();
        FacilityAnchor[] anchors;
        float nextScan, nextUnlockRefresh;
        readonly List<bool> shownUnlocks=new List<bool>();
        void RefreshUnlocks()
        {
            if(Mode!=MenuMode.Build || Current==null || service.catalog==null)return;
            var defs=service.catalog.facilities;bool changed=shownUnlocks.Count!=defs.Count;
            for(int i=0;!changed && i<defs.Count;i++)changed=shownUnlocks[i]!=service.IsUnlocked(defs[i].id);
            if(changed)RebuildTiles();
        }

        void Awake()
        {
            if (service == null) service = FindFirstObjectByType<FacilityBuildService>();
            input = FindFirstObjectByType<PlayerInputReader>();
            if (player == null) player = input != null ? input.transform : FindFirstObjectByType<PlayerMotor>()?.transform;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            panel.gameObject.SetActive(false);
        }

        void OnEnable() { if (input != null) input.SlotSelected += Select; }
        void OnDisable() { if (input != null) input.SlotSelected -= Select; Close(); }

        void Update()
        {
            if (service == null || player == null) return;
            if (anchors == null || Time.time >= nextScan)
            { anchors = FindObjectsByType<FacilityAnchor>(FindObjectsSortMode.None); nextScan = Time.time + 1f; }
            FacilityAnchor nearest = null; float best = float.MaxValue;
            foreach (var anchor in anchors)
            {
                if (anchor == null || string.IsNullOrEmpty(anchor.SlotId)) continue;
                if (anchor.IsOccupied && !NeedsRepair(anchor)) continue;
                Vector3 delta = anchor.transform.position - player.position; delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= anchor.interactionRadius && distance < best) { best = distance; nearest = anchor; }
            }
            MenuMode wanted = nearest != null && nearest.IsOccupied ? MenuMode.Repair : MenuMode.Build;
            if (nearest != Current || (nearest != null && wanted != Mode)) { if (nearest != null) Open(nearest); else Close(); }
            if (Current != null) { Follow(); RefreshRepairCost(); }
            if(Time.unscaledTime>=nextUnlockRefresh){RefreshUnlocks();nextUnlockRefresh=Time.unscaledTime+.15f;}
            if (input == null && Current != null && Keyboard.current != null) // 플레이어 입력기가 없는 씬(테스트·데모)용
                for (int i = 0; i < 9; i++)
                    if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { Select(i); break; }
        }

        static bool NeedsRepair(FacilityAnchor anchor)
        {
            var health = anchor.Occupant != null ? anchor.Occupant.Health : null;
            return health != null && health.IsAlive && health.CurrentHealth < health.MaxHealth - .01f;
        }

        void Open(FacilityAnchor anchor)
        {
            Current = anchor;
            Mode = anchor.IsOccupied ? MenuMode.Repair : MenuMode.Build;
            titleText.text = Mode == MenuMode.Repair ? repairTitle : title;
            RebuildTiles();
            panel.gameObject.SetActive(true);
            Follow();
        }

        void Close()
        {
            Current = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>건설 모드: 카탈로그 순서(0부터)의 시설을 현재 받침대에 짓는다. 수리 모드: 0번이면 수리한다. 번호 키와 테스트가 부른다.</summary>
        public void Select(int index)
        {
            if (Current == null || service == null) return;
            if (Mode == MenuMode.Repair)
            {
                if (index != 0 || Current.Occupant == null) return;
                LastRepairResult = service.TryRepair(Current.Occupant.EntityId);
                if (!LastRepairResult.Succeeded) Debug.Log("수리 실패: " + LastRepairResult.Failure);
                return; // 다 고쳐지면 다음 Update에서 메뉴가 닫힌다.
            }
            if (service.catalog == null) return;
            if (index < 0 || index >= service.catalog.facilities.Count) return;
            var definition = service.catalog.facilities[index];
            LastResult = service.TryBuild(PlacementRequest.AtSlot(definition.id, Current.SlotId));
            if (LastResult.Outcome.Succeeded) Close();
            else if(LastResult.Outcome.Failure==ActionFailure.Locked){titleText.text="먼저 스킬트리에서 타워를 해금하세요";}
            else Debug.Log("건설 실패: " + LastResult.Placement.Failure + " / " + LastResult.Outcome.Failure);
        }

        /// <summary>메뉴 캔버스. 기본은 화면 오버레이이며, 렌더 텍스처 캡처가 필요하면 카메라 모드로 바꿔도 위치 계산이 유지된다.</summary>
        public Canvas Canvas => canvas;

        void Follow()
        {
            var camera = Camera.main;
            if (camera == null) { panel.anchoredPosition = Vector2.zero; return; }
            Vector3 screen = camera.WorldToScreenPoint(Current.transform.position + Vector3.up * Current.menuHeight);
            bool visible = screen.z > 0f;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, uiCamera, out Vector2 local))
                panel.anchoredPosition = local;
        }

        void BuildCanvas()
        {
            var canvasObject = new GameObject("Facility Build Menu Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
            panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(.5f, 0f);
            titleText = Label(panel, title, 22, FontStyle.Bold);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, tileSize.y + 44f);
            titleText.rectTransform.sizeDelta = new Vector2(420f, 30f);
        }

        void RefreshRepairCost()
        {
            if (Mode != MenuMode.Repair || repairCostText == null || Current == null || Current.Occupant == null) return;
            var quote = service.GetRepairQuote(Current.Occupant.EntityId);
            int cost = quote.Availability.Succeeded ? quote.ManaAmount : 0;
            if (cost == shownRepairCost) return;
            shownRepairCost = cost;
            repairCostText.text = "마나 " + cost;
        }

        void RebuildTiles()
        {
            foreach (var tile in tiles) Destroy(tile);
            tiles.Clear();
            shownUnlocks.Clear();
            repairCostText = null; shownRepairCost = -1;
            if (Mode == MenuMode.Repair)
            {
                panel.sizeDelta = new Vector2(tileSize.x + 80f, tileSize.y + 80f);
                var repair = new GameObject("Tile Repair", typeof(RectTransform)).GetComponent<RectTransform>();
                repair.SetParent(panel, false);
                repair.anchorMin = repair.anchorMax = new Vector2(.5f, 0f); repair.pivot = new Vector2(.5f, 0f);
                repair.sizeDelta = tileSize; repair.anchoredPosition = new Vector2(0f, 40f);
                Image(repair, "Disc", discSprite, Vector2.zero, tileSize, new Color(1f, 1f, 1f, .92f));
                if (numberSprites.Length > 0 && numberSprites[0] != null)
                    Image(repair, "Number", numberSprites[0], new Vector2(-tileSize.x * .38f, tileSize.y * .38f), new Vector2(40f, 40f), Color.white);
                var repairLabel = Label(repair, "수리", 22, FontStyle.Bold);
                repairLabel.rectTransform.sizeDelta = new Vector2(tileSize.x, 30f);
                repairCostText = Label(repair, "", 16, FontStyle.Normal);
                repairCostText.rectTransform.anchoredPosition = new Vector2(0f, -22f);
                repairCostText.rectTransform.sizeDelta = new Vector2(tileSize.x + 40f, 26f);
                tiles.Add(repair.gameObject);
                RefreshRepairCost();
                return;
            }
            var catalog = service.catalog;
            int count = catalog != null ? catalog.facilities.Count : 0;
            float width = count * tileSize.x + Mathf.Max(0, count - 1) * tileSpacing;
            panel.sizeDelta = new Vector2(width + 40f, tileSize.y + 90f);
            for (int i = 0; i < count; i++)
            {
                var definition = catalog.facilities[i];
                bool unlocked=service.IsUnlocked(definition.id);shownUnlocks.Add(unlocked);
                var tile = new GameObject("Tile " + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
                tile.SetParent(panel, false);
                tile.anchorMin = tile.anchorMax = new Vector2(.5f, 0f); tile.pivot = new Vector2(.5f, 0f);
                tile.sizeDelta = tileSize;
                tile.anchoredPosition = new Vector2(-width / 2f + tileSize.x / 2f + i * (tileSize.x + tileSpacing), 50f);
                Image(tile, "Disc", discSprite, Vector2.zero, tileSize, new Color(1f, 1f, 1f, .92f));
                Image(tile, "Icon", definition.icon, Vector2.zero, tileSize * .72f, unlocked?Color.white:new Color(.3f,.3f,.3f,.7f));
                if (i < numberSprites.Length && numberSprites[i] != null)
                    Image(tile, "Number", numberSprites[i], new Vector2(-tileSize.x * .38f, tileSize.y * .38f), new Vector2(40f, 40f), Color.white);
                var nameLabel = Label(tile, definition.displayName, 15, FontStyle.Bold);
                nameLabel.rectTransform.anchoredPosition = new Vector2(0f, -18f);
                nameLabel.rectTransform.sizeDelta = new Vector2(tileSize.x + 8f, 22f);
                var detailLabel = Label(tile, !unlocked ? "[잠김]" : definition.manaCost > 0 ? "마나 " + definition.manaCost : "", 14, FontStyle.Normal);
                detailLabel.rectTransform.anchoredPosition = new Vector2(0f, -39f);
                detailLabel.rectTransform.sizeDelta = new Vector2(tileSize.x, 20f);
                tiles.Add(tile.gameObject);
            }
        }

        static Image Image(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
            image.sprite = sprite; image.color = color; image.raycastTarget = false; image.preserveAspect = true;
            if (sprite == null) image.enabled = false;
            return image;
        }

        Text Label(RectTransform parent, string value, int size, FontStyle style)
        {
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            text.font = font; text.fontSize = size; text.fontStyle = style; text.text = value;
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.95f, .93f, .85f); text.raycastTarget = false;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .8f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }
    }
}
