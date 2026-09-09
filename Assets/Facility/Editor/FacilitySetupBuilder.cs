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
        const string PlayerAndEnemyScene = "Assets/PlayerAndEnemy/Generated/PlayerAndEnemyTest.unity";
        /// <summary>받침대 모델 폭(4m)을 슬롯 자리 크기에 맞추는 기준.</summary>
        const float BaseModelWidth = 4f;
        public const string CobraId = "cobra";

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
                PrefabUtility.SaveAsPrefabAsset(cobraRoot, CobraPrefabPath);
            }
            finally { Object.DestroyImmediate(cobraRoot); }

            var catalog = AssetDatabase.LoadAssetAtPath<FacilityCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<FacilityCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var cobra = catalog.Find(CobraId);
            if (cobra == null) { cobra = new FacilityDefinition { id = CobraId }; catalog.facilities.Insert(0, cobra); }
            cobra.displayName = "화염 코브라"; cobra.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CobraPrefabPath);
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

        /// <summary>레벨의 모든 건설 슬롯에 받침대를 놓고 서비스·메뉴를 씬에 넣는다. 슬롯의 허용 목록이 비어 있으면 코브라를 넣는다.</summary>
        public static int WireIntoScene(Scene scene)
        {
            EnsureAssets();
            LevelRoot level = Object.FindFirstObjectByType<LevelRoot>();
            if (level == null) { Debug.LogWarning("FacilitySetupBuilder: LevelRoot가 없어 받침대를 놓지 않았습니다."); return 0; }
            var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPrefabPath);
            int count = 0;
            foreach (var slot in level.BuildSlots)
            {
                if (slot.allowedFacilityIds.Count == 0) slot.allowedFacilityIds.Add(CobraId);
                EditorUtility.SetDirty(slot);
                var previous = slot.transform.Find("TowerAnchor");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
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

        [MenuItem("SandGuard/Facility/Wire Into Player And Enemy Scene")]
        public static void WirePlayerAndEnemyScene()
        {
            if (!File.Exists(PlayerAndEnemyScene)) { Debug.LogError("씬이 없습니다: " + PlayerAndEnemyScene); return; }
            var scene = EditorSceneManager.OpenScene(PlayerAndEnemyScene, OpenSceneMode.Single);
            WireIntoScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>배치 모드용: 에셋 생성 + 통합 씬 연결.</summary>
        public static void BuildAndWire() { Build(); WirePlayerAndEnemyScene(); }

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
