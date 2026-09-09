using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SandGuard.Enemy.Editor
{
    public static class EnemySetupBuilder
    {
        const string Root = "Assets/Enemy/Generated";

        [MenuItem("SandGuard/Enemy/Create Missing Demo Assets")]
        public static void CreateMissingAssets()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            Material body = MaterialAt("Enemy", new Color(0.8f, 0.2f, 0.15f));
            Material blade = MaterialAt("Blade", new Color(0.85f, 0.85f, 0.9f));
            Material ground = MaterialAt("Ground", new Color(0.36f, 0.31f, 0.23f));
            Material obstacle = MaterialAt("Obstacle", new Color(0.65f, 0.52f, 0.33f));
            Material blocker = MaterialAt("Blocker", new Color(0.9f, 0.75f, 0.2f));
            Material core = MaterialAt("Core", new Color(0.2f, 0.8f, 0.9f));
            Material dummy = MaterialAt("Dummy", new Color(0.15f, 0.55f, 0.8f));

            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/EnemyVisual.prefab");
            if (visual == null)
            {
                GameObject root = new GameObject("EnemyVisual");
                Primitive("Body", PrimitiveType.Capsule, root.transform, new Vector3(0, 1, 0), new Vector3(0.7f, 1, 0.7f), body, false);
                Primitive("Facing", PrimitiveType.Cube, root.transform, new Vector3(0, 1.55f, 0.36f), new Vector3(0.25f, 0.1f, 0.1f), blade, false);
                Transform pivot = Child(root.transform, "SwordPivot", new Vector3(0.45f, 1.1f, 0.15f));
                pivot.localRotation = Quaternion.Euler(20, 0, 0);
                Primitive("Sword", PrimitiveType.Cube, pivot, new Vector3(0, 0.55f, 0), new Vector3(0.05f, 1.1f, 0.12f), blade, false);
                Primitive("Guard", PrimitiveType.Cube, pivot, new Vector3(0, 0.05f, 0), new Vector3(0.28f, 0.04f, 0.06f), blade, false);
                root.AddComponent<EnemyVisualBindings>().weaponPivot = pivot;
                visual = PrefabUtility.SaveAsPrefabAsset(root, Root + "/EnemyVisual.prefab");
                Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Enemy.prefab");
            if (prefab == null)
            {
                GameObject root = new GameObject("Enemy");
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.up; capsule.height = 2f; capsule.radius = 0.35f;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f; agent.height = 2f; agent.speed = 3.5f; agent.acceleration = 16f;
                agent.angularSpeed = 540f; agent.stoppingDistance = 0f; agent.avoidancePriority = 50;
                Transform visualRoot = Child(root.transform, "VisualRoot", Vector3.zero);
                Transform attackOrigin = Child(root.transform, "AttackOrigin", new Vector3(0, 1.1f, 0.3f));
                var health = root.AddComponent<EnemyHealth>();
                var motor = root.AddComponent<EnemyMotor>();
                var visuals = root.AddComponent<EnemyVisuals>();
                visuals.visualRoot = visualRoot; visuals.visualPrefab = visual; visuals.motor = motor; visuals.health = health;
                var attack = root.AddComponent<EnemyMeleeAttack>();
                attack.self = health; attack.visuals = visuals; attack.attackOrigin = attackOrigin;
                var selector = root.AddComponent<EnemyTargetSelector>();
                selector.self = health; selector.motor = motor; selector.attack = attack;
                var brain = root.AddComponent<EnemyBrain>();
                brain.health = health; brain.motor = motor; brain.selector = selector; brain.attack = attack;
                visuals.RebuildVisual();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Enemy.prefab");
                Object.DestroyImmediate(root);
            }

            if (!File.Exists(Root + "/EnemyTest.unity"))
            {
                Scene previous = SceneManager.GetActiveScene();
                // 저장되지 않은 빈 씬(배치 모드 등)에서는 추가 씬을 열 수 없으므로 단일 씬으로 만든다.
                bool additive = previous.IsValid() && !string.IsNullOrEmpty(previous.path);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                GameObject level = new GameObject("Level");
                Primitive("Ground", PrimitiveType.Cube, level.transform, new Vector3(0, -0.5f, 15), new Vector3(30, 1, 50), ground, true);
                Primitive("Detour wall - go around", PrimitiveType.Cube, level.transform, new Vector3(-2, 1.5f, 10), new Vector3(12, 3, 0.5f), obstacle, true);
                Primitive("Blocking wall L", PrimitiveType.Cube, level.transform, new Vector3(-9.5f, 1.5f, 20), new Vector3(11, 3, 0.5f), obstacle, true);
                Primitive("Blocking wall R", PrimitiveType.Cube, level.transform, new Vector3(9.5f, 1.5f, 20), new Vector3(11, 3, 0.5f), obstacle, true);
                var surface = level.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
                    AssetDatabase.CreateAsset(surface.navMeshData, Root + "/EnemyTestNavMesh.asset");
                // 파괴 가능한 대상은 굽기에서 제외하고 NavMeshObstacle로 길을 막는다. 부서지면 길이 열린다.
                Target("Destructible wall - attack to pass", PrimitiveType.Cube, level.transform, new Vector3(0, 1.5f, 20), new Vector3(8, 3, 0.5f), blocker, CombatTargetKind.Wall, 40f);
                Target("Core", PrimitiveType.Cube, level.transform, new Vector3(0, 1, 32), new Vector3(2, 2, 2), core, CombatTargetKind.Core, 200f).AddComponent<EnemyObjective>();
                Target("Player dummy", PrimitiveType.Capsule, level.transform, new Vector3(6, 1, 2), Vector3.one, dummy, CombatTargetKind.Player, 30f);
                Transform spawn = new GameObject("Enemy Spawn").transform;
                spawn.position = new Vector3(0, 0, -5);
                for (int i = -1; i <= 1; i++)
                {
                    GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    enemy.transform.position = new Vector3(i * 2f, 0, -5);
                    enemy.name = "Enemy " + (i + 2);
                }
                var overlay = new GameObject("Test HUD").AddComponent<EnemyDemoOverlay>();
                overlay.enemyPrefab = prefab; overlay.spawnPoint = spawn;
                var cameraObject = new GameObject("Demo Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0, 28, 0);
                cameraObject.transform.rotation = Quaternion.Euler(62, 0, 0);
                cameraObject.AddComponent<Camera>().fieldOfView = 60f;
                cameraObject.AddComponent<AudioListener>();
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
                EditorSceneManager.SaveScene(scene, Root + "/EnemyTest.unity");
                if (additive)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ENEMY_SETUP_COMPLETE: " + Root + "/EnemyTest.unity");
        }

        static GameObject Target(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, CombatTargetKind kind, float health)
        {
            GameObject value = Primitive(name, type, parent, position, scale, material, true);
            var target = value.AddComponent<EnemyTestTarget>();
            target.kind = kind; target.maxHealth = health; target.factionId = "Ally";
            value.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var obstacle = value.AddComponent<NavMeshObstacle>();
            obstacle.carving = true; obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one;
            return value;
        }
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
