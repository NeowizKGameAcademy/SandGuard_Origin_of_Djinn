using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SandGuard.Player.Editor
{
    public static class PlayerSetupBuilder
    {
        const string Root = "Assets/Player/Generated";
        [MenuItem("SandGuard/Player/Create Missing Demo Assets")]
        public static void CreateMissingAssets()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            Material body = MaterialAt("Player", new Color(0.15f, 0.55f, 0.8f));
            Material bolt = MaterialAt("Bolt", new Color(0.15f, 0.9f, 1f));
            Material ground = MaterialAt("Ground", new Color(0.36f, 0.31f, 0.23f));
            Material obstacle = MaterialAt("Obstacle", new Color(0.65f, 0.52f, 0.33f));
            Material targetMaterial = MaterialAt("Target", new Color(0.9f, 0.25f, 0.15f));
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/PlayerInput.asset");
            if (input == null)
            {
                input = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/10.Input/InputSystem_Actions.inputactions"));
                input.name = "PlayerInput";
                var playerMap = input.FindActionMap("Player", true);
                AddButton(playerMap, "Pause", "<Keyboard>/escape");
                AddButton(playerMap, "Dash", "<Keyboard>/leftShift");
                AddButton(playerMap, "Spell", "<Mouse>/rightButton");
                AddButton(playerMap, "BuildMode", "<Keyboard>/b");
                AddButton(playerMap, "Rotate", "<Keyboard>/r");
                for (int i = 1; i <= 9; i++) AddButton(playerMap, "Slot" + i, "<Keyboard>/" + i);
                AssetDatabase.CreateAsset(input, Root + "/PlayerInput.asset");
            }
            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/PlayerVisual.prefab");
            if (visual == null)
            {
                GameObject root = new GameObject("PlayerVisual");
                var mesh = Primitive("Body", PrimitiveType.Capsule, root.transform, new Vector3(0, 1, 0), new Vector3(0.7f, 1, 0.7f), body, false);
                Primitive("Facing", PrimitiveType.Cube, root.transform, new Vector3(0, 1.6f, 0.36f), new Vector3(0.3f, 0.12f, 0.1f), bolt, false);
                visual = PrefabUtility.SaveAsPrefabAsset(root, Root + "/PlayerVisual.prefab");
                Object.DestroyImmediate(root);
            }
            var projectile = AssetDatabase.LoadAssetAtPath<PlayerProjectile>(Root + "/PlayerBolt.prefab");
            if (projectile == null)
            {
                GameObject root = new GameObject("PlayerBolt");
                root.AddComponent<PlayerProjectile>();
                Transform visualRoot = Child(root.transform, "VisualRoot", Vector3.zero);
                Primitive("BoltVisual", PrimitiveType.Sphere, visualRoot, Vector3.zero, Vector3.one * 0.16f, bolt, false);
                projectile = PrefabUtility.SaveAsPrefabAsset(root, Root + "/PlayerBolt.prefab").GetComponent<PlayerProjectile>();
                Object.DestroyImmediate(root);
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Player.prefab");
            if (prefab == null)
            {
                GameObject root = new GameObject("Player");
                var capsule = root.AddComponent<CharacterController>();
                capsule.height = 2f; capsule.radius = 0.35f; capsule.center = Vector3.up;
                capsule.stepOffset = 0.45f; capsule.slopeLimit = 50f; capsule.skinWidth = 0.025f; capsule.minMoveDistance = 0f;
                Transform visualRoot = Child(root.transform, "VisualRoot", Vector3.zero);
                Transform cameraTarget = Child(root.transform, "CameraTarget", new Vector3(0, 1.6f, 0));
                Transform firePoint = Child(root.transform, "DefaultFirePoint", new Vector3(0.4f, 1.3f, 0.65f));
                Transform hitPoint = Child(root.transform, "HitPoint", new Vector3(0, 1.3f, 0));
                var reader = root.AddComponent<PlayerInputReader>(); reader.actions = input;
                var motor = root.AddComponent<PlayerMotor>(); motor.input = reader;
                var visuals = root.AddComponent<PlayerVisuals>();
                visuals.visualRoot = visualRoot; visuals.visualPrefab = visual; visuals.fallbackFirePoint = firePoint; visuals.motor = motor;
                var cameraObject = Child(root.transform, "PlayerCamera", Vector3.zero).gameObject;
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>(); camera.nearClipPlane = 0.1f; camera.fieldOfView = 60f;
                cameraObject.AddComponent<AudioListener>();
                var rig = cameraObject.AddComponent<PlayerCameraRig>(); rig.input = reader; rig.target = cameraTarget; rig.owner = root.transform;
                motor.view = camera.transform;
                var aimer = root.AddComponent<PlayerAimer>(); aimer.viewCamera = camera; aimer.owner = root.transform;
                var attack = root.AddComponent<PlayerBasicAttack>();
                attack.input = reader; attack.aimer = aimer; attack.visuals = visuals; attack.projectilePrefab = projectile; attack.shotOrigin = hitPoint; attack.motor = motor;
                visuals.RebuildVisual();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Player.prefab");
                Object.DestroyImmediate(root);
            }
            if (!File.Exists(Root + "/PlayerTest.unity"))
            {
                Scene previous = SceneManager.GetActiveScene();
                if (!Application.isBatchMode && string.IsNullOrEmpty(previous.path))
                {
                    Debug.LogWarning("프리팹 생성 완료. 테스트 씬을 만들려면 현재 이름 없는 씬을 저장한 뒤 메뉴를 다시 실행하세요.");
                    return;
                }
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.transform.position = new Vector3(0, 0.1f, -6);
                var overlay = new GameObject("Test HUD").AddComponent<PlayerDemoOverlay>(); overlay.motor = player.GetComponent<PlayerMotor>();
                Primitive("Ground", PrimitiveType.Cube, null, new Vector3(0, -0.5f, 2), new Vector3(24, 1, 28), ground, true);
                Primitive("Wall - blocks projectiles", PrimitiveType.Cube, null, new Vector3(-3, 1.5f, 3), new Vector3(4, 3, 0.5f), obstacle, true);
                Primitive("Camera obstruction", PrimitiveType.Cube, null, new Vector3(5, 2, -6), new Vector3(0.5f, 4, 8), obstacle, true);
                for (int i = 0; i < 5; i++)
                    Primitive("Step " + i, PrimitiveType.Cube, null, new Vector3(-7, (i + 1) * 0.125f, -3 + i * 0.8f), new Vector3(2, (i + 1) * 0.25f, 0.8f), obstacle, true);
                for (int i = 0; i < 3; i++)
                    Primitive("Target " + (i + 1), PrimitiveType.Capsule, null, new Vector3(-3 + i * 3, 1, 8), Vector3.one, targetMaterial, true).AddComponent<PlayerTestTarget>();
                var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
                EditorSceneManager.SaveScene(scene, Root + "/PlayerTest.unity");
                if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            UpgradeResources();
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_SETUP_COMPLETE: " + Root + "/PlayerTest.unity");
        }

        [MenuItem("SandGuard/Player/Connect Health Mana and Dash")]
        public static void UpgradeResources()
        {
            string path = Root + "/Player.prefab";
            if (!File.Exists(path)) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var health = root.GetComponent<PlayerHealth>();
                if (health == null)
                {
                    health = root.AddComponent<PlayerHealth>();
                    health.hitPoint = root.transform.Find("HitPoint");
                    health.disableOnDeath = new Collider[] { root.GetComponent<CharacterController>() };
                }
                var mana = root.GetComponent<PlayerManaWallet>();
                if (mana == null) mana = root.AddComponent<PlayerManaWallet>();
                var motor = root.GetComponent<PlayerMotor>();
                if (motor.lifeSource == null) motor.lifeSource = health;
                if (motor.manaSource == null) motor.manaSource = mana;
                var attack = root.GetComponent<PlayerBasicAttack>();
                if (attack.lifeSource == null) attack.lifeSource = health;
                var visuals = root.GetComponent<PlayerVisuals>();
                if (visuals.healthSource == null) visuals.healthSource = health;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/PlayerInput.asset");
            if (input != null)
            {
                var map = input.FindActionMap("Player", true);
                AddButton(map, "Dash", "<Keyboard>/leftShift");
                var dash = map.FindAction("Dash", true);
                bool hasGamepad = false;
                foreach (var binding in dash.bindings) if (binding.path == "<Gamepad>/leftStickPress") hasGamepad = true;
                if (!hasGamepad) dash.AddBinding("<Gamepad>/leftStickPress");
                EditorUtility.SetDirty(input);
                AssetDatabase.SaveAssets();
            }
        }

        static void AddButton(InputActionMap map, string name, string binding)
        { if (map.FindAction(name) == null) map.AddAction(name, InputActionType.Button, binding); }
        static Transform Child(Transform parent, string name, Vector3 position)
        { var value = new GameObject(name).transform; value.SetParent(parent, false); value.localPosition = position; return value; }
        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var value = GameObject.CreatePrimitive(type); value.name = name;
            value.transform.SetParent(parent, false); value.transform.localPosition = position; value.transform.localScale = scale;
            value.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(value.GetComponent<Collider>());
            return value;
        }
        static Material MaterialAt(string name, Color color)
        {
            string path = Root + "/" + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value != null) return value;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            value = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(value, path); return value;
        }
    }
}
