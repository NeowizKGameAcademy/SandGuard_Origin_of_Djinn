using System.Collections.Generic;
using DesertTower.LevelIntegration;
using DesertTower.Levels;
using DesertTower.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    /// <summary>Preparation-only entrance warnings, resolved by the same binding as enemy spawning.</summary>
    public sealed class WaveSpawnPreview : MonoBehaviour
    {
        public Color warningColor = new Color(1f, .48f, .08f);
        [Min(.1f), Tooltip("Minimum radius; expands to enclose the spawn deck.")] public float ringRadius = 10f;
        [Min(.1f)] public float beaconHeight = 60f;
        const float FadeSeconds = .4f;
        sealed class Marker
        {
            public Transform spawn;
            public GameObject root;
            public LineRenderer ring;
            public VfxSpawnWarning cylinder;
            public CanvasGroup label, map;
            public RectTransform mapRect;
        }
        readonly List<Marker> markers = new List<Marker>();
        readonly HashSet<LevelMarker> entrances = new HashSet<LevelMarker>();
        WaveDirector director;
        MinimapController minimap;
        Material material;
        TMP_FontAsset font;
        int shownWave = -1;
        bool preparing;
        float opacity, pulseTime;

        public void Bind(WaveDirector source, MinimapController map)
        {
            Clear();
            director = source;
            minimap = map;
            shownWave = -1;
            preparing = false;
            var text = GetComponentInChildren<TMP_Text>(true);
            font = text != null ? text.font : TMP_Settings.defaultFontAsset;
        }

        void LateUpdate()
        {
            bool next = director != null && director.State == RunState.Preparing;
            if (next && (!preparing || shownWave != director.WaveIndex)) Rebuild();
            preparing = next;
            // Pause freezes both the countdown and warning animation.
            opacity = Mathf.MoveTowards(opacity, next ? 1f : 0f, Time.deltaTime / FadeSeconds);
            if (!next && (director == null || director.State != RunState.Running)) opacity = 0f;
            if (opacity <= 0f) { if (!next) Clear(); return; }
            pulseTime += Time.deltaTime * (next && director.PreparationRemaining <= 5f ? 2f : .65f);
            float pulse = .5f + .5f * Mathf.Sin(pulseTime * Mathf.PI * 2f);
            var camera = Camera.main;
            foreach (var marker in markers)
            {
                if (marker.spawn == null) { marker.root.SetActive(false); if (marker.map != null) marker.map.gameObject.SetActive(false); continue; }
                marker.root.transform.position = marker.spawn.position + Vector3.up * .12f;
                var color = warningColor; color.a = opacity * (.65f + .35f * pulse);
                marker.ring.startColor = marker.ring.endColor = color;
                marker.ring.transform.localScale = Vector3.one * (1f + .08f * pulse);
                marker.cylinder.SetAppearance(warningColor, opacity, pulseTime, pulse);
                marker.label.alpha = opacity;
                marker.label.gameObject.SetActive(camera != null);
                if (camera != null) marker.label.transform.rotation = camera.transform.rotation;
                if (marker.map == null && minimap != null && minimap.View != null) BuildMapMarker(marker);
                if (marker.map != null)
                {
                    marker.map.gameObject.SetActive(minimap != null && minimap.PlacePreview(marker.mapRect, marker.spawn.position));
                    marker.map.alpha = color.a;
                    marker.mapRect.localScale = Vector3.one * (1f + .12f * pulse);
                }
            }
        }

        void Rebuild()
        {
            Clear();
            shownWave = director.WaveIndex;
            pulseTime = 0f;
            var level = director.graph != null ? director.graph.level : null;
            if (level == null || level.waves == null || shownWave < 0 || shownWave >= level.waves.waves.Count) return;
            var wave = level.waves.waves[shownWave];
            if (wave == null) return;
            var decks = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            foreach (var group in wave.groups)
            {
                if (group == null || group.count <= 0 || !level.TryResolveSpawnGroup(group, out var binding, out _) || !entrances.Add(binding.Spawn)) continue;
                var marker = new Marker { spawn = binding.Spawn.transform, root = new GameObject("UpcomingSpawn") };
                float radius = DeckRadius(marker.spawn, decks);
                float height = Mathf.Max(beaconHeight, radius * 5f);
                // UI layer keeps world warnings out of the minimap camera.
                marker.root.layer = 5;
                marker.ring = Line(marker.root.transform, "Ring", .3f);
                marker.ring.loop = true;
                marker.ring.positionCount = 64;
                for (int i = 0; i < 64; i++)
                {
                    float angle = i * Mathf.PI * 2f / 64;
                    marker.ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                }
                var cylinder = new GameObject("Downward Chevron Cylinder");
                cylinder.layer = 5;
                cylinder.transform.SetParent(marker.root.transform, false);
                marker.cylinder = cylinder.AddComponent<VfxSpawnWarning>();
                marker.cylinder.Initialize(radius, height);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                label.layer = 5;
                label.transform.SetParent(marker.root.transform, false);
                label.transform.localPosition = Vector3.up * 8f;
                label.transform.localScale = Vector3.one * .015f;
                label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)label.transform).sizeDelta = new Vector2(240, 48);
                marker.label = label.GetComponent<CanvasGroup>();
                marker.label.blocksRaycasts = false;
                AddText(label.transform, "적 출현 예정", 28);
                markers.Add(marker);
            }
        }

        float DeckRadius(Transform spawn, MeshCollider[] decks)
        {
            float radius = ringRadius;
            float closestHeight = float.PositiveInfinity;
            Vector3 position = spawn.position;
            foreach (var deck in decks)
            {
                if (!deck.enabled || deck.gameObject.scene != spawn.gameObject.scene || !deck.sharedMesh) continue;
                if (!deck.name.EndsWith("_deck", System.StringComparison.OrdinalIgnoreCase)
                    && !deck.sharedMesh.name.EndsWith("_deck", System.StringComparison.OrdinalIgnoreCase)) continue;
                var bounds = deck.bounds;
                if (position.x < bounds.min.x - 2f || position.x > bounds.max.x + 2f
                    || position.z < bounds.min.z - 2f || position.z > bounds.max.z + 2f) continue;
                float vertical = Mathf.Abs(position.y - bounds.max.y);
                if (vertical > 6f || vertical >= closestHeight) continue;
                closestHeight = vertical;
                // Include the farthest deck corner even when the entrance is off-centre.
                float x = Mathf.Abs(position.x - bounds.center.x) + bounds.extents.x;
                float z = Mathf.Abs(position.z - bounds.center.z) + bounds.extents.z;
                radius = Mathf.Max(ringRadius, Mathf.Sqrt(x * x + z * z) + 1f);
            }
            return radius;
        }

        LineRenderer Line(Transform parent, string name, float width)
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            var go = new GameObject(name); go.layer = 5;
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void BuildMapMarker(Marker marker)
        {
            marker.mapRect = minimap.View.AddMarker("UpcomingSpawn", null, Color.clear, new Vector2(24, 24));
            marker.map = marker.mapRect.gameObject.AddComponent<CanvasGroup>();
            marker.map.blocksRaycasts = false;
            // Four thin bars form a hollow diamond; the exclamation mark stays upright.
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)); edge.layer = 5;
                var rect = (RectTransform)edge.transform; rect.SetParent(marker.mapRect, false);
                float angle = (45f + 90f * i) * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 8f;
                rect.sizeDelta = new Vector2(16, 2);
                rect.localRotation = Quaternion.Euler(0, 0, 135f + 90f * i);
                var image = edge.GetComponent<Image>(); image.color = warningColor; image.raycastTarget = false;
            }
            AddText(marker.mapRect, "!", 18);
        }

        void AddText(Transform parent, string value, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center; text.color = warningColor;
            text.raycastTarget = false;
        }

        void Clear()
        {
            foreach (var marker in markers)
            {
                if (marker.root != null) { marker.root.SetActive(false); Destroy(marker.root); }
                if (marker.map != null) { marker.map.gameObject.SetActive(false); Destroy(marker.map.gameObject); }
            }
            markers.Clear(); entrances.Clear(); opacity = 0f;
        }
        void OnDisable() { Clear(); preparing = false; }
        void OnDestroy() { Clear(); if (material != null) Destroy(material); }
    }
}
