using System.Collections.Generic;
using DesertTower.LevelIntegration;
using DesertTower.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace SandGuard.Facility.Editor
{
    /// <summary>
    /// Level.unity에서 타워를 설치할 수 있게 연결한다. 레벨 담당자가 놓은 임시 받침 메시(Temp/Mesh.prefab) 자리를 건설 슬롯으로 바꾼다.
    /// ① 메시마다 건설 슬롯(레벨 에디터와 같은 "건설 슬롯" 오브젝트, 층별 바닥 ID, 허용 cobra·obelisk, 크기 4×4)을 만든다.
    /// ② 받침 외형은 설치 받침(TowerBaseAnchor)에 들어 있으므로 임시 메시는 지우지 않고 끈다.
    /// ③ 슬롯마다 설치 받침, 씬에 건설 서비스·메뉴를 넣는다(WireIntoScene).
    /// ④ 받침은 적을 막는 NavMesh 장애물이라, 받침 안에 들어간 경로 노드는 적이 닿지 못한다.
    ///    그런 노드의 도착 반경을 받침 반폭 + 적 반경 + 여유로 넓힌다(코어와 같은 방식). 노드는 GraphIntegration 프리팹 데이터라 원본을 고친다.
    /// 여러 번 실행해도 슬롯을 중복으로 만들지 않는다.
    /// </summary>
    public static class LevelTowerSlotSetup
    {
        public const string LevelScenePath = "Assets/1.Scene/Level.unity";
        const string TempBaseMeshPath = "Assets/2.Model/Prefabs/Level/Temp/Mesh.prefab";
        const float SlotSize = 4f;
        const float ArrivalMargin = .5f;

        [MenuItem("SandGuard/Facility/Set Up Level Tower Slots (Level.unity)")]
        public static void SetUp()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FacilitySetupBuilder.TowerBaseAnchorPath) == null)
                FacilitySetupBuilder.CreateTowerBuildAssets(); // 다른 씬을 여니까 Level을 열기 전에 한다.

            var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            var level = Object.FindFirstObjectByType<LevelRoot>();
            if (level == null) { Debug.LogError("Level.unity에 LevelRoot가 없습니다."); return; }
            var tempAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TempBaseMeshPath);

            int created = 0, hidden = 0;
            foreach (var go in AllGameObjects(scene))
            {
                if (tempAsset == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) != tempAsset) continue;
                Vector3 position = go.transform.position;
                if (!HasSlotNear(level, position))
                {
                    var slotObject = new GameObject("건설 슬롯");
                    slotObject.transform.SetParent(level.transform, false);
                    slotObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, go.transform.eulerAngles.y, 0f));
                    var slot = slotObject.AddComponent<LevelBuildSlot>();
                    slot.id = System.Guid.NewGuid().ToString("N");
                    int floorHeight = Mathf.RoundToInt(position.y);
                    slot.label = "타워 슬롯 (높이 " + floorHeight + ")";
                    slot.occupancySurfaceId = "tower-floor-" + floorHeight; // 위아래 층은 서로 다른 바닥 ID
                    slot.footprint = new Vector2(SlotSize, SlotSize);
                    slot.allowedFacilityIds = new List<string> { FacilitySetupBuilder.CobraId, FacilitySetupBuilder.ObeliskId };
                    created++;
                }
                if (go.activeSelf) { go.SetActive(false); hidden++; }
            }

            FacilitySetupBuilder.WireIntoScene(scene);
            int widened = WidenBlockedNodes(level);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"LEVEL_TOWER_SLOTS_READY slots={level.BuildSlots.Length} created={created} hiddenTempMeshes={hidden} widenedNodes={widened}");
        }

        static IEnumerable<GameObject> AllGameObjects(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) yield return t.gameObject;
        }

        static bool HasSlotNear(LevelRoot level, Vector3 position)
        {
            foreach (var slot in level.BuildSlots)
                if ((slot.transform.position - position).sqrMagnitude < .25f) return true;
            return false;
        }

        /// <summary>받침(장애물) 안이나 바로 옆에 있는 경로 노드의 도착 반경을 넓힌다. 이미 더 넓으면 그대로 둔다.</summary>
        static int WidenBlockedNodes(LevelRoot level)
        {
            float radius = SlotSize * .5f + NavMesh.GetSettingsByID(0).agentRadius + ArrivalMargin;
            var targets = new Dictionary<string, RouteNode>();
            foreach (var node in Object.FindObjectsByType<RouteNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var slot in level.BuildSlots)
                {
                    Vector3 delta = node.transform.position - slot.transform.position;
                    if (Mathf.Abs(delta.y) > 2f) continue;
                    if (new Vector2(delta.x, delta.z).magnitude < radius && node.arrivalRadius < radius) targets[node.id] = node;
                }
            if (targets.Count == 0) return 0;

            var sample = default(RouteNode);
            foreach (var node in targets.Values) { sample = node; break; }
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sample);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (string.IsNullOrEmpty(path))
            {
                foreach (var node in targets.Values) { Undo.RecordObject(node, "Widen node"); node.arrivalRadius = radius; EditorUtility.SetDirty(node); }
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var node in root.GetComponentsInChildren<RouteNode>(true))
                        if (targets.ContainsKey(node.id) && node.arrivalRadius < radius) node.arrivalRadius = radius;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var node in targets.Values) Debug.Log("LEVEL_TOWER_NODE " + node.label + " arrivalRadius=" + radius.ToString("0.00"));
            return targets.Count;
        }
    }
}
