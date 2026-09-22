using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertTower.Levels;
using DesertTower.VFX;
using SandGuard.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SandGuard.Facility.Editor
{
    /// <summary>타워 받침대·화염 코브라 모델, UI 스프라이트, 카탈로그, 메뉴/서비스 프리팹을 만들고 레벨 슬롯에 받침대를 놓는다.</summary>
    /// <remarks>
    /// 원본: <c>Docs/model-art/Tower/Tower-1.obj</c>(받침대, 4×1.8×4) 와 <c>Tower-0.obj</c>(코브라, 원점 y 1.3~4.7).
    /// 두 모델은 같은 좌표계라 받침대 원점에 코브라를 그대로 놓으면 맞물린다. 팔레트 텍스처(.mtl)는 원본에 없어 단색 재질을 쓴다.
    /// </remarks>
    public static class FacilitySetupBuilder
    {
        const string Art = "Assets/Facility/Art";
        const string Generated = "Assets/Facility/Generated";
        public const string AnchorPrefabPath = Generated + "/TowerAnchor.prefab";
        public const string CobraPrefabPath = Generated + "/FireCobraTower.prefab";
        public const string CatalogPath = Generated + "/FacilityCatalog.asset";
        public const string MenuPrefabPath = Generated + "/FacilityBuildMenu.prefab";
        public const string ServicePrefabPath = Generated + "/FacilityBuildService.prefab";
        const string BuildCompleteVfxPath = "Assets/Resources/VFX/Prefabs/VFX_Build_Complete.prefab";
        const string BuildPoofVfxPath = "Assets/Resources/VFX/Prefabs/VFX_Build_Poof.prefab";
        const string FontPath = "Assets/9.Font/public/static/alternative/Pretendard-Medium.ttf";
        /// <summary>받침대 모델 폭(4m)을 슬롯 자리 크기에 맞추는 기준.</summary>
        const float BaseModelWidth = 4f;
        public const string CobraId = "cobra";
        /// <summary>팀원이 만든 레벨 배치용 타워(Level.unity, Tower.unity). 테스트용으로 체력과 몸통 충돌체를 붙인다.</summary>
        public const string TeamTowerPrefabPath = "Assets/2.Model/Prefabs/Tower Base.prefab";
        /// <summary>
        /// 시설 몸통 충돌체를 받침대 충돌체보다 조금 넓게 잡는다. 같은 면이면 적의 시야 레이가 받침대에 먼저 맞아
        /// 공격 대상이 가려진 것으로 판정된다.
        /// </summary>
        const float CombatBodyMargin = .2f;

        /// <summary>담당자 타워 씬. 본체(Tower (Cobra) / Tower (Obelisk))는 아직 프리팹이 아니라 이 씬의 받침 아래에만 있다.</summary>
        public const string TeamTowerScenePath = "Assets/1.Scene/Tower.unity";
        /// <summary>받침 아래 본체 이름의 앞부분. "Tower (Cobra)", "Tower (Obelisk)".</summary>
        public const string TeamTowerBodyPrefix = "Tower (";
        /// <summary>받침 충돌체의 최소 높이. 받침 모델이 낮아도 플레이어(키 2m)가 올라서거나 넘지 못하게 한다.</summary>
        const float BaseBlockMinHeight = 2.2f;
        const string FacilityHitVfxPath = "Assets/Resources/VFX/Prefabs/VFX_Facility_Hit.prefab";
        const string CobraDestructionVfxPath = "Assets/Resources/VFX/Prefabs/VFX_Cobra_Destruction.prefab";
        const string DisabledLoopVfxPath = "Assets/Resources/VFX/Prefabs/VFX_Facility_Disabled_Loop.prefab";

        /// <summary>
        /// 담당자 타워에 게임 연결용 컴포넌트를 붙인다. 담당자 코드는 바꾸지 않고 컴포넌트만 추가한다(패치 문서 참고).
        /// 받침(프리팹과 씬): 통과 막기. 본체(씬): 체력·판정 상자·정지 수신. 코브라 본체만: 피격·파괴·정지 VFX.
        /// </summary>
        [MenuItem("SandGuard/Facility/Add Combat Health To Towers")]
        public static void AddCombatHealthToTowers()
        {
            Patch(CobraPrefabPath, root =>
            {
                EnsureCombatTarget(root, BaseModelWidth + CombatBodyMargin, root.transform);
                EnsureHitVfx(root);
            });
            Patch(TeamTowerPrefabPath, root => BlockTower(root));

            var scene = EditorSceneManager.OpenScene(TeamTowerScenePath, OpenSceneMode.Single);
            int bodies = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Tower Base") continue;
                // 받침 프리팹 인스턴스라면 위에서 프리팹에 붙인 것을 그대로 받는다.
                if (!PrefabUtility.IsPartOfPrefabInstance(root)) BlockTower(root);
                foreach (Transform child in root.transform)
                    if (child.name.StartsWith(TeamTowerBodyPrefix)) { SetUpTeamTowerBody(root, child); bodies++; }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("FACILITY_COMBAT_READY bodies=" + bodies);
        }

        /// <summary>
        /// 카탈로그가 실제로 짓는 타워 프리팹에 정지 요청 수신기를 붙인다.
        /// </summary>
        /// <remarks>
        /// 우두머리의 철거 폭탄은 <c>CombatEffectSignals</c>로 "이 타워를 N초 멈춰 달라"는 요청만 보내고,
        /// 실제로 멈추는 일은 본체의 <c>TowerDisableReceiver</c>가 한다. 수신기가 없으면 요청이 그냥 사라진다.
        /// 담당자 코드는 바꾸지 않고 컴포넌트만 더한다(패치 문서 참고).
        /// 어느 오브젝트가 본체인지는 체력 컴포넌트(IHealth)가 붙은 곳으로 정한다. 타워 팀 프리팹은 루트가 아니라 자식이다.
        /// </remarks>
        [MenuItem("SandGuard/Facility/Wire Catalog Towers For Combat")]
        public static void WireCatalogTowersForCombat()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FacilityCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("카탈로그가 없습니다: " + CatalogPath); return; }
            var receiverType = System.Type.GetType("TowerDisableReceiver, Assembly-CSharp");
            if (receiverType == null) { Debug.LogError("TowerDisableReceiver 타입을 찾지 못했습니다."); return; }
            var disabledVfx = AssetDatabase.LoadAssetAtPath<GameObject>(DisabledLoopVfxPath);
            int wired = 0, failed = 0;

            foreach (var definition in catalog.facilities)
            {
                if (definition == null || definition.prefab == null)
                { Debug.LogWarning((definition != null ? definition.id : "?") + ": 프리팹이 비어 있습니다."); failed++; continue; }
                string path = AssetDatabase.GetAssetPath(definition.prefab);
                bool ok = false;
                Patch(path, root =>
                {
                    var health = root.GetComponentInChildren<IHealth>(true) as Component;
                    if (health == null)
                    {
                        Debug.LogError(definition.id + ": 체력 컴포넌트(IHealth)가 없어 건설·파괴·수리가 동작하지 않습니다. " + path, definition.prefab);
                        return;
                    }
                    // "짠" 등장 연출. 건설 서비스는 VfxPopIn이 없으면 붙여 주지만 연막(PoofPrefab)까지는 채우지 못한다.
                    var popIn = root.GetComponent<VfxPopIn>(); if (popIn == null) popIn = root.AddComponent<VfxPopIn>();
                    if (popIn.PoofPrefab == null) popIn.PoofPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildPoofVfxPath);
                    if (popIn.RevealPrefab == null) popIn.RevealPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildCompleteVfxPath);
                    popIn.PlayOnEnable = false; // 건설 서비스가 생성 직후에 직접 Play한다.

                    var body = health.gameObject;
                    EnsureHitVfx(body); // 프리팹이 바뀌며 피격 연출도 함께 빠졌다.
                    var receiver = body.GetComponent(receiverType);
                    if (receiver == null) receiver = body.AddComponent(receiverType);
                    if (disabledVfx != null)
                    {
                        var serialized = new SerializedObject(receiver);
                        var property = serialized.FindProperty("Disabled_Vfx");
                        if (property != null && property.objectReferenceValue == null)
                        { property.objectReferenceValue = disabledVfx; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                    }
                    ok = true;
                });
                if (ok) wired++; else failed++;
            }

            // 받침의 "점유 시에만 켜는 오브젝트" 목록에서 끊긴 참조를 치운다.
            // 담당자가 Tower Base.prefab에서 Range Circle을 없애 참조만 남았다(사거리 표시는 본체의 RangeVisualizer로 옮겨 갔다).
            Patch(TowerBaseAnchorPath, root =>
            {
                var anchor = root.GetComponent<FacilityAnchor>();
                if (anchor == null) return;
                var kept = new List<GameObject>();
                foreach (var value in anchor.onlyWhenOccupied) if (value != null) kept.Add(value);
                if (kept.Count == anchor.onlyWhenOccupied.Length) return;
                Debug.Log("TowerBaseAnchor: 끊긴 onlyWhenOccupied 참조 " + (anchor.onlyWhenOccupied.Length - kept.Count) + "개를 치웠습니다.");
                anchor.onlyWhenOccupied = kept.ToArray();
            });

            AssetDatabase.SaveAssets();
            Debug.Log("FACILITY_TOWERS_WIRED wired=" + wired + " failed=" + failed);
        }

        /// <summary>
        /// 본체: 체력(적의 공격 대상), 판정 상자, 정지 요청 수신. 코브라만 피격·파괴·정지 VFX(다른 타워용 VFX는 아직 없음).
        /// </summary>
        static void SetUpTeamTowerBody(GameObject root, Transform body)
        {
            // 판정 상자는 받침 충돌체보다 넓게 잡는다. 면이 겹치면 적의 시야 레이가 받침에 먼저 맞아 공격하지 못한다.
            Bounds footing = BaseFooting(root);
            float footprint = Mathf.Max(footing.size.x, footing.size.z) + CombatBodyMargin;
            var health = EnsureCombatTarget(body.gameObject, footprint, root.transform);
            health.removal = FacilityHealth.RemovalMode.Deactivate; // 받침의 ShowRange가 본체의 DetectRange를 계속 참조한다
            string kind = body.name.Substring(TeamTowerBodyPrefix.Length).TrimEnd(')').Trim().ToLowerInvariant();
            health.definitionId = "tower." + kind;

            var receiverType = System.Type.GetType("TowerDisableReceiver, Assembly-CSharp");
            Component receiver = null;
            if (receiverType == null) Debug.LogWarning("TowerDisableReceiver 타입을 찾지 못했습니다.");
            else { receiver = body.GetComponent(receiverType); if (receiver == null) receiver = body.gameObject.AddComponent(receiverType); }

            if (kind != "cobra") return;
            EnsureHitVfx(body.gameObject);
            var destruction = body.GetComponent<VfxDestructionOnDeath>(); if (destruction == null) destruction = body.gameObject.AddComponent<VfxDestructionOnDeath>();
            destruction.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CobraDestructionVfxPath);
            destruction.Lifetime = 3f;
            if (receiver != null)
            {
                var serialized = new SerializedObject(receiver);
                serialized.FindProperty("Disabled_Vfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DisabledLoopVfxPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Bounds BaseFooting(GameObject root)
        {
            var baseMesh = root.transform.Find("Mesh");
            return baseMesh != null ? MeshBounds(baseMesh.gameObject, root.transform) : MeshBounds(root, root.transform);
        }

        /// <summary>
        /// 본체가 부서져도 남는 받침을 통과하지 못하게 한다. 플레이어(CharacterController)는 받침 충돌체에,
        /// 적(NavMeshAgent는 충돌체를 무시한다)은 같은 크기의 NavMesh 장애물(깎기)에 막힌다.
        /// </summary>
        static void BlockTower(GameObject root)
        {
            Bounds footing = BaseFooting(root);
            float height = Mathf.Max(footing.size.y, BaseBlockMinHeight);
            var center = new Vector3(footing.center.x, footing.min.y + height * .5f, footing.center.z);
            var size = new Vector3(footing.size.x, height, footing.size.z);
            var solid = root.GetComponent<BoxCollider>();
            if (solid == null) solid = root.AddComponent<BoxCollider>();
            solid.isTrigger = false; solid.center = center; solid.size = size;
            var obstacle = root.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) obstacle = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = center; obstacle.size = size;
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
        }

        static void EnsureHitVfx(GameObject body)
        {
            var reaction = body.GetComponent<VfxHitReaction>(); if (reaction == null) reaction = body.AddComponent<VfxHitReaction>();
            reaction.HitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FacilityHitVfxPath);
            reaction.HitLifetime = 1.5f;
        }

        static void Patch(string path, System.Action<GameObject> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("프리팹이 없습니다: " + path); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// 본체에 FacilityHealth와, 본체 메시를 감싸는 BoxCollider(트리거 아님)를 붙인다. 이미 있으면 그대로 둔다.
        /// 충돌체 바닥은 ground 높이(받침 원점)까지 내린다. 받침이 높아도 땅에 선 적의 근접 공격이 닿게 한다.
        /// </summary>
        public static FacilityHealth EnsureCombatTarget(GameObject body, float minFootprint, Transform ground)
        {
            var health = body.GetComponent<FacilityHealth>(); if (health == null) health = body.AddComponent<FacilityHealth>();
            if (body.GetComponents<Collider>().Any(c => !c.isTrigger)) return health;
            Bounds bounds = MeshBounds(body, body.transform);
            Vector3 min = bounds.min, max = bounds.max;
            if (ground != null) min.y = Mathf.Min(min.y, body.transform.InverseTransformPoint(ground.position).y);
            Vector3 size = max - min;
            size.x = Mathf.Max(size.x, minFootprint); size.z = Mathf.Max(size.z, minFootprint);
            var collider = body.AddComponent<BoxCollider>();
            collider.center = new Vector3(bounds.center.x, (min.y + max.y) * .5f, bounds.center.z);
            collider.size = size;
            return health;
        }

        /// <summary>root 아래 메시를 space 좌표계로 감싼 경계. 사거리 표시 같은 "Range" 오브젝트는 뺀다.</summary>
        static Bounds MeshBounds(GameObject root, Transform space)
        {
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            var bounds = new Bounds(); bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name.Contains("Range")) continue;
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 matrix = toSpace * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (any) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); any = true; }
                }
            }
            if (!any) { Debug.LogWarning(root.name + ": 메시가 없어 충돌체 크기를 정하지 못했습니다."); bounds = new Bounds(Vector3.up, Vector3.one * 2f); }
            return bounds;
        }

        [MenuItem("SandGuard/Facility/Create Missing Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material baseMaterial = LitMaterial(Art + "/Tower/TowerBase.mat", new Color(.80f, .70f, .52f));
            Material cobraMaterial = LitMaterial(Art + "/Tower/FireCobra.mat", new Color(.64f, .36f, .22f));
            AssetDatabase.DeleteAsset(Art + "/Tower/FacilityGhost.mat"); // 홀로그램 미리보기는 뺐다.
            // 등장 연막은 VFX 모듈(Assembly-CSharp-Editor)이 만든다. 없으면 메뉴로 실행한다.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BuildPoofVfxPath) == null) EditorApplication.ExecuteMenuItem("DesertTower/VFX/Build Facility Pop-In Poof");
            ImportModel(Art + "/Tower/TowerBase.obj", baseMaterial);
            ImportModel(Art + "/Tower/FireCobra.obj", cobraMaterial);

            var sprites = new Dictionary<string, Sprite>();
            foreach (var png in Directory.GetFiles(Art + "/UI", "*.png"))
            {
                string path = png.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 256; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                sprites[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            // 받침대: 루트(스케일은 씬에서 슬롯 크기로 맞춤) + 모델 자식.
            var anchorRoot = new GameObject("TowerAnchor");
            try
            {
                anchorRoot.AddComponent<FacilityAnchor>();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Tower/TowerBase.obj"));
                model.name = "Base"; model.transform.SetParent(anchorRoot.transform, false);
                var collider = anchorRoot.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, .9f, 0f); collider.size = new Vector3(BaseModelWidth, 1.8f, BaseModelWidth);
                PrefabUtility.SaveAsPrefabAsset(anchorRoot, AnchorPrefabPath);
            }
            finally { Object.DestroyImmediate(anchorRoot); }

            // 시설: 루트에 FacilityInstance + VfxPopIn("짠" 등장), 모델 자식. 받침대와 같은 원점.
            var cobraRoot = new GameObject("FireCobraTower");
            try
            {
                cobraRoot.AddComponent<FacilityInstance>();
                var popIn = cobraRoot.AddComponent<VfxPopIn>();
                popIn.PoofPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildPoofVfxPath);
                popIn.RevealPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildCompleteVfxPath);
                popIn.PlayOnEnable = false; // 건설 서비스가 Play()를 부른다.
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Tower/FireCobra.obj"));
                model.name = "Cobra"; model.transform.SetParent(cobraRoot.transform, false);
                EnsureCombatTarget(cobraRoot, BaseModelWidth + CombatBodyMargin, cobraRoot.transform); EnsureHitVfx(cobraRoot);
                PrefabUtility.SaveAsPrefabAsset(cobraRoot, CobraPrefabPath);
            }
            finally { Object.DestroyImmediate(cobraRoot); }

            var catalog = AssetDatabase.LoadAssetAtPath<FacilityCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<FacilityCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var cobra = catalog.Find(CobraId);
            if (cobra == null) { cobra = new FacilityDefinition { id = CobraId }; catalog.facilities.Insert(0, cobra); }
            cobra.displayName = "화염 코브라"; cobra.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerBodyPath("Cobra"));
            cobra.icon = sprites.TryGetValue("UI_Icon_Cobra", out var icon) ? icon : null;
            EditorUtility.SetDirty(catalog);

            var serviceRoot = new GameObject("Facility Build Service");
            try
            {
                var service = serviceRoot.AddComponent<FacilityBuildService>();
                service.catalog = catalog; service.buildCompleteVfx = AssetDatabase.LoadAssetAtPath<GameObject>(BuildCompleteVfxPath);
                PrefabUtility.SaveAsPrefabAsset(serviceRoot, ServicePrefabPath);
            }
            finally { Object.DestroyImmediate(serviceRoot); }

            var menuRoot = new GameObject("Facility Build Menu");
            try
            {
                var menu = menuRoot.AddComponent<FacilityBuildMenu>();
                menu.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                menu.discSprite = sprites.GetValueOrDefault("UI_Disc_Dark"); menu.ringSprite = sprites.GetValueOrDefault("UI_Ring_Cyan");
                menu.numberSprites = Enumerable.Range(1, 5).Select(i => sprites.GetValueOrDefault("UI_Number_" + i)).ToArray();
                PrefabUtility.SaveAsPrefabAsset(menuRoot, MenuPrefabPath);
            }
            finally { Object.DestroyImmediate(menuRoot); }
            AssetDatabase.SaveAssets();
            Debug.Log("FACILITY_ASSETS_READY " + Generated);
        }

        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPrefabPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath) == null) Build();
        }

        // ---- 담당자 타워로 짓기 ----

        /// <summary>건설용 타워 에셋 폴더. 담당자 파일은 건드리지 않고 여기에 복제·변형만 둔다.</summary>
        public const string TowersFolder = Generated + "/Towers";
        /// <summary>담당자 받침(Tower Base.prefab)의 프리팹 변형 + FacilityAnchor. 슬롯마다 이것을 놓는다.</summary>
        public const string TowerBaseAnchorPath = TowersFolder + "/TowerBaseAnchor.prefab";
        public const string ObeliskId = "obelisk";
        public const string SkeletonId = "skeleton";
        public const string AnubisId = "anubis";
        public static string TowerBodyPath(string kind) => "Assets/2.Model/Prefabs/Tower_" + kind + ".prefab";

        /// <summary>
        /// 건설 흐름에 담당자 타워를 쓴다.
        /// ① 공식 타워 프리팹을 직접 참조한다. 이후 원본 수정도 건설에 반영된다.
        /// ② 받침 앵커 = Tower Base.prefab의 변형 + FacilityAnchor. 사거리 원은 본체가 있을 때만 켠다(본체 없이 켜면 ShowRange가 오류).
        /// ③ 카탈로그: 화염 코브라 40, 모래시계 오벨리스크 55(임시 비용).
        /// Tower.unity와 Tower Base.prefab은 저장하지 않는다.
        /// </summary>
        [MenuItem("SandGuard/Facility/Create Tower Build Assets (from Prefabs)")]
        public static void CreateTowerBuildAssets()
        {
            Directory.CreateDirectory(TowersFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var kind in new[] { "Cobra", "Obelisk" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(TowerBodyPath(kind)) == null)
                    throw new System.InvalidOperationException("타워 프리팹이 없습니다: " + TowerBodyPath(kind));

            var baseInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TeamTowerPrefabPath));
            try
            {
                baseInstance.name = "TowerBaseAnchor";
                var anchor = baseInstance.GetComponent<FacilityAnchor>();
                if (anchor == null) anchor = baseInstance.AddComponent<FacilityAnchor>();
                anchor.menuHeight = 2.6f;
                var circle = baseInstance.transform.Find("Range Circle");
                anchor.onlyWhenOccupied = circle != null ? new[] { circle.gameObject } : new GameObject[0];
                PrefabUtility.SaveAsPrefabAsset(baseInstance, TowerBaseAnchorPath);
            }
            finally { Object.DestroyImmediate(baseInstance); }

            var catalog = AssetDatabase.LoadAssetAtPath<FacilityCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<FacilityCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            const string towerIconFolder = "Assets/4.Sprite/UI/Facility/TowerIcons";
            Upsert(catalog, 0, CobraId, "코브라 타워", TowerBodyPath("Cobra"), 40, towerIconFolder + "/TowerIcon_1_Cobra.png");
            Upsert(catalog, 1, ObeliskId, "오벨리스크", TowerBodyPath("Obelisk"), 55, towerIconFolder + "/TowerIcon_2_Obelisk.png");
            Upsert(catalog, 2, SkeletonId, "스켈레톤 관", "Assets/2.Model/Prefabs/Tower_Coffin.prefab", 70, towerIconFolder + "/TowerIcon_3_Skeleton.png");
            Upsert(catalog, 3, AnubisId, "아누비스 석상", "Assets/2.Model/Prefabs/Tower Anubis.prefab", 85, towerIconFolder + "/TowerIcon_4_Anubis.png");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("FACILITY_TOWER_ASSETS_READY bodies=Cobra,Obelisk");
        }

        static void Upsert(FacilityCatalog catalog, int index, string id, string displayName, string prefabPath, int cost, string iconPath)
        {
            var definition = catalog.Find(id);
            if (definition == null) { definition = new FacilityDefinition { id = id }; catalog.facilities.Insert(Mathf.Min(index, catalog.facilities.Count), definition); }
            definition.displayName = displayName;
            definition.requiredSkillId = "tower." + id;
            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            definition.manaCost = cost;
            definition.maxHealth = 150f;
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon != null) definition.icon = icon;
            if (definition.prefab == null) Debug.LogWarning("타워 본체 프리팹이 없습니다: " + prefabPath);
        }

        /// <summary>레벨의 모든 건설 슬롯에 받침대를 놓고 서비스·메뉴를 씬에 넣는다. 슬롯의 허용 목록이 비어 있으면 코브라를 넣는다.</summary>
        public static int WireIntoScene(Scene scene)
        {
            EnsureAssets();
            LevelRoot level = Object.FindFirstObjectByType<LevelRoot>();
            if (level == null) { Debug.LogWarning("FacilitySetupBuilder: LevelRoot가 없어 받침대를 놓지 않았습니다."); return 0; }
            var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerBaseAnchorPath);
            if (anchorPrefab == null) anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPrefabPath);
            int count = 0;
            foreach (var slot in level.BuildSlots)
            {
                // 허용 목록이 비었거나 예전 기본값(코브라만)이면 건설 가능한 타워를 모두 허용한다. 기획자가 정한 목록은 그대로 둔다.
                bool automatic = slot.allowedFacilityIds.Count == 0 || slot.allowedFacilityIds.All(id => id == CobraId || id == ObeliskId || id == SkeletonId || id == AnubisId);
                if (automatic)
                {
                    slot.allowedFacilityIds.Clear();
                    slot.allowedFacilityIds.Add(CobraId); slot.allowedFacilityIds.Add(ObeliskId);
                    slot.allowedFacilityIds.Add(SkeletonId); slot.allowedFacilityIds.Add(AnubisId);
                }
                EditorUtility.SetDirty(slot);
                foreach (var previous in slot.GetComponentsInChildren<FacilityAnchor>(true)) Object.DestroyImmediate(previous.gameObject);
                var anchor = (GameObject)PrefabUtility.InstantiatePrefab(anchorPrefab, scene);
                anchor.transform.SetParent(slot.transform, false);
                anchor.transform.localPosition = Vector3.zero; anchor.transform.localRotation = Quaternion.identity;
                anchor.transform.localScale = Vector3.one * (slot.footprint.x / BaseModelWidth);
                anchor.GetComponent<FacilityAnchor>().slot = slot;
                count++;
            }
            var service = Object.FindFirstObjectByType<FacilityBuildService>();
            if (service == null)
                service = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ServicePrefabPath), scene)).GetComponent<FacilityBuildService>();
            service.level = level; EditorUtility.SetDirty(service);
            var menu = Object.FindFirstObjectByType<FacilityBuildMenu>();
            if (menu == null)
                menu = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath), scene)).GetComponent<FacilityBuildMenu>();
            var player = Object.FindFirstObjectByType<PlayerMotor>();
            if (player != null) menu.player = player.transform;
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("FACILITY_WIRED anchors=" + count);
            return count;
        }


        static void ImportModel(string path, Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.useFileScale = true; importer.globalScale = 1f;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
            importer.SaveAndReimport();
        }

        static Material LitMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", .2f);
            EditorUtility.SetDirty(material); return material;
        }

    }
}
