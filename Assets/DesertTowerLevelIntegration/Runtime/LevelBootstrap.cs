using System.Collections.Generic;
using DesertTower.Levels;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>프리팹 생성만 담당합니다. 시설 구매/마나/점유는 기존 건설 시스템에서 처리하세요.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class LevelBootstrap : MonoBehaviour
    {
        public LevelRoot level;
        public PrefabCatalog catalog;
        public WaveDirector director;
        public bool createOnStart = true;
        readonly List<GameObject> owned = new List<GameObject>();
        void Start() { if (createOnStart) CreateActors(); }
        [ContextMenu("Create registered actors (Play mode)")]
        public void CreateActors()
        {
            if (!Application.isPlaying || !level || !catalog || owned.Count > 0) return;
            // Validate all requested marker bindings before mutating the scene.
            foreach (var marker in level.Markers)
            {
                if (!Spawnable(marker) || !marker.definition) continue;
                var entry = catalog.Find(marker.definition.gameKey);
                if (entry == null || !entry.prefab || entry.role != Role(marker.kind))
                { Debug.LogError(marker.name + ": 프리팹 등록/역할 오류", this); return; }
            }
            foreach (var marker in level.Markers)
            {
                if (!Spawnable(marker) || !marker.definition) continue;
                var entry = catalog.Find(marker.definition.gameKey);
                var go = Instantiate(entry.prefab, marker.transform.position, marker.transform.rotation);
                owned.Add(go);
                if (marker.kind == MarkerKind.Core && director)
                    foreach (var component in go.GetComponents<MonoBehaviour>())
                        if (component is ILevelCoreReceiver) { director.coreReceiver = component; break; }
            }
        }
        static bool Spawnable(LevelMarker m) => m.kind == MarkerKind.PlayerStart || m.kind == MarkerKind.Core || m.kind == MarkerKind.InitialFacility;
        static PrefabRole Role(MarkerKind kind) => kind == MarkerKind.PlayerStart ? PrefabRole.Player : kind == MarkerKind.Core ? PrefabRole.Core : PrefabRole.Facility;
        void OnDestroy() { foreach (var go in owned) if (go) Destroy(go); }
    }
}
