using DesertTower.VFX;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace DesertTower.LevelIntegration.Editor
{
    /// <summary>
    /// 레벨 코어의 외형을 Core Base로 두고 연출을 연결한다. 흡수로 안정도가 깎이면(onChanged) 발밑 피격 섬광,
    /// 안정도가 0이 되면(onDefeated) Core Base 파괴 연출. 코어 판정(CoreReceiver)은 그대로 둔다.
    /// </summary>
    public static class CoreVfxSetup
    {
        const string LevelScene = "Assets/1.Scene/Level.unity";
        const string CoreBasePath = "Assets/2.Model/Prefabs/Core Base.prefab";
        const string DamageFlashPath = "Assets/Resources/VFX/Prefabs/VFX_Core_Damage_Flash.prefab";
        const string AbsorptionPath = "Assets/Resources/VFX/Prefabs/VFX_Core_Damage_Enemy.prefab";
        const string DestructionPath = "Assets/Resources/VFX/Prefabs/VFX_Core_Destruction.prefab";
        const string CoreBaseName = "Core Base";
        /// <summary>
        /// 원본은 받침 폭 11m·결정 높이 8m. 정상(약 18×23m)에서 3m 옆 플레이어 시작점과 겹치지 않게 받침 폭 3.3m로 줄인다.
        /// 파괴 연출은 원본 크기(lossyScale)를 따라 같이 줄어든다.
        /// </summary>
        public const float LevelCoreScale = .3f;

        [MenuItem("Tools/Desert Tower/Set Up Level Core (Core Base + VFX)")]
        public static void ConnectLevel()
        {
            var scene = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Single);
            int count = 0;
            foreach (var receiver in Object.FindObjectsByType<CoreReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform coreBase = UseCoreBase(receiver, LevelCoreScale);
                Connect(receiver, coreBase);
                float halfWidth = BlockCore(receiver, coreBase);
                WidenCoreArrival(receiver, halfWidth + NavMesh.GetSettingsByID(0).agentRadius + ArrivalMargin);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CORE_VFX_READY receivers=" + count);
        }

        /// <summary>장애물 가장자리에 선 적과 코어 노드 중심 사이 여유. 여러 마리가 둘러서도 도착으로 본다.</summary>
        const float ArrivalMargin = .5f;

        /// <summary>
        /// 코어를 통과하지 못하게 한다. 받침은 Core Base 루트, 결정은 Core 자식에 충돌체(결정이 파괴로 꺼지면 같이 꺼진다)를,
        /// 적(NavMeshAgent)은 코어 전체를 덮는 NavMesh 장애물(깎기)을 붙인다. 받침 반폭(월드 m)을 돌려준다.
        /// </summary>
        public static float BlockCore(CoreReceiver receiver, Transform coreBase)
        {
            var pedestal = coreBase.Find("Mesh");
            var crystal = coreBase.Find("Core");
            Bounds footing = MeshBounds(pedestal != null ? pedestal : coreBase, coreBase);
            var baseCollider = coreBase.GetComponent<BoxCollider>();
            if (baseCollider == null) baseCollider = Undo.AddComponent<BoxCollider>(coreBase.gameObject);
            baseCollider.isTrigger = false; baseCollider.center = footing.center; baseCollider.size = footing.size;
            if (crystal != null)
            {
                Bounds gem = MeshBounds(crystal, crystal);
                var gemCollider = crystal.GetComponent<BoxCollider>();
                if (gemCollider == null) gemCollider = Undo.AddComponent<BoxCollider>(crystal.gameObject);
                // 복셀 결정은 팔각 모양이라 경계 상자보다 조금 좁게 잡는다.
                gemCollider.isTrigger = false; gemCollider.center = gem.center; gemCollider.size = Vector3.Scale(gem.size, new Vector3(.75f, 1f, .75f));
            }
            Bounds whole = MeshBounds(coreBase, receiver.transform);
            Bounds footingWorld = MeshBounds(pedestal != null ? pedestal : coreBase, receiver.transform);
            var obstacle = receiver.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = Undo.AddComponent<NavMeshObstacle>(receiver.gameObject);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(footingWorld.center.x, whole.center.y, footingWorld.center.z);
            obstacle.size = new Vector3(footingWorld.size.x, whole.size.y, footingWorld.size.z);
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
            EditorUtility.SetDirty(baseCollider); EditorUtility.SetDirty(obstacle);
            return Mathf.Max(footingWorld.extents.x, footingWorld.extents.z) * receiver.transform.lossyScale.x;
        }

        /// <summary>
        /// 코어 노드의 도착 반경을 장애물 가장자리까지 넓힌다. 노드는 GraphIntegration 프리팹 데이터이므로 프리팹 원본을 고친다.
        /// </summary>
        public static void WidenCoreArrival(CoreReceiver receiver, float radius)
        {
            RouteNode best = null; float bestDistance = float.MaxValue;
            foreach (var node in Object.FindObjectsByType<RouteNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (node.marker == null || node.marker.kind != DesertTower.Levels.MarkerKind.Core) continue;
                float distance = (node.transform.position - receiver.transform.position).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = node; }
            }
            if (best == null) { Debug.LogWarning("코어 노드를 찾지 못했습니다."); return; }
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(best);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (string.IsNullOrEmpty(path))
            {
                Undo.RecordObject(best, "Core arrival"); best.arrivalRadius = radius; EditorUtility.SetDirty(best);
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var node in root.GetComponentsInChildren<RouteNode>(true))
                        if (node.id == best.id) node.arrivalRadius = radius;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                PrefabUtility.RevertPropertyOverride(new SerializedObject(best).FindProperty("arrivalRadius"), InteractionMode.AutomatedAction);
            }
            Debug.Log("CORE_ARRIVAL radius=" + radius.ToString("0.00") + " node=" + best.label);
        }

        /// <summary>root 아래 메시를 space 좌표계로 감싼 경계.</summary>
        static Bounds MeshBounds(Transform root, Transform space)
        {
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            var bounds = new Bounds(); bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 matrix = toSpace * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (any) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); any = true; }
                }
            }
            return any ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        /// <summary>
        /// 임시 큐브 외형(MeshFilter·MeshRenderer·BoxCollider)을 걷어 내고 코어를 바닥에 내린 뒤 Core Base를 자식으로 둔다.
        /// 이미 있으면 그대로 쓴다. 코어 위치는 코어 마커와 1m 안이어야 하므로 XZ는 유지한다.
        /// </summary>
        public static Transform UseCoreBase(CoreReceiver receiver, float scale)
        {
            var existing = receiver.transform.Find(CoreBaseName);
            if (existing != null) return existing;
            foreach (var placeholder in new Component[] { receiver.GetComponent<MeshRenderer>(), receiver.GetComponent<MeshFilter>(), receiver.GetComponent<BoxCollider>() })
                if (placeholder != null) Undo.DestroyObjectImmediate(placeholder);
            Vector3 position = receiver.transform.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit floor, 3f, NavMesh.AllAreas)) position.y = floor.position.y;
            Undo.RecordObject(receiver.transform, "Core on floor");
            receiver.transform.position = position;
            receiver.transform.localScale = Vector3.one;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CoreBasePath), receiver.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Core Base");
            instance.name = CoreBaseName;
            instance.transform.SetParent(receiver.transform, false);
            instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;
            return instance.transform;
        }

        /// <summary>피격 섬광(onChanged)과 파괴 연출(onDefeated)을 한 번씩만 연결한다. 섬광은 외형 경계의 바닥 중심에서 난다.</summary>
        public static void Connect(CoreReceiver receiver, Transform visual)
        {
            receiver.absorptionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbsorptionPath);
            var shot = receiver.GetComponent<VfxOneShot>();
            if (shot == null) shot = Undo.AddComponent<VfxOneShot>(receiver.gameObject);
            shot.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamageFlashPath);
            shot.Anchor = receiver.transform;
            shot.Lifetime = 2f;
            var renderers = receiver.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                shot.LocalOffset = receiver.transform.InverseTransformPoint(new Vector3(receiver.transform.position.x, bounds.min.y, receiver.transform.position.z));
            }
            Listen(receiver.onChanged, shot, shot.Fire, nameof(VfxOneShot.Fire));

            var destruction = receiver.GetComponent<VfxDestructionOnDeath>();
            if (destruction == null) destruction = Undo.AddComponent<VfxDestructionOnDeath>(receiver.gameObject);
            destruction.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestructionPath);
            destruction.Target = visual;
            destruction.Lifetime = 3f;
            Listen(receiver.onDefeated, destruction, destruction.Fire, nameof(VfxDestructionOnDeath.Fire));
            EditorUtility.SetDirty(shot); EditorUtility.SetDirty(destruction); EditorUtility.SetDirty(receiver);
        }

        static void Listen(UnityEvent unityEvent, Object target, UnityAction action, string method)
        {
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == method)
                    UnityEventTools.RemovePersistentListener(unityEvent, i);
            UnityEventTools.AddPersistentListener(unityEvent, action);
        }
    }
}
