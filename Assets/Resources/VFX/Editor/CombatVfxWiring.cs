using DesertTower.VFX;
using SandGuard.Enemy;
using SandGuard.Player;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 전투 이펙트를 데모 프리팹·씬에 꽂는다. 여러 번 실행해도 안전하다(있으면 값만 갱신).
    /// 적 프리팹: VfxHitReaction(피격·사망) + 발자국. 플레이어 프리팹: 이동 이펙트, 지팡이 시전(onFired), 피격 화면·셰이크, 발자국.
    /// 적 테스트 씬: 벽·코어·플레이어 대역에 종류별 피격·파괴 이펙트.
    /// </summary>
    public static class CombatVfxWiring
    {
        const string EnemyPrefabPath = "Assets/Enemy/Generated/Enemy.prefab";
        const string EnemyScenePath = "Assets/Enemy/Generated/EnemyTest.unity";
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";

        /// <summary>배치용: 전체 빌드 뒤 데모 에셋에 연결한다.</summary>
        public static void BuildAndWire() { VfxBatch.BuildAll(); WireAll(); }

        /// <summary>배치용: 플레이어 조작감 튜닝값을 프리팹에 적용한 뒤 이펙트를 연결한다.</summary>
        public static void TuneAndWire() { SandGuard.Player.Editor.PlayerFeelTuning.Apply(); WireAll(); }

        [MenuItem("DesertTower/VFX/Wire Combat VFX Into Demo Assets")]
        public static void WireAll()
        {
            WireEnemyPrefab();
            WirePlayerPrefab();
            WireEnemyScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[VFX] Combat VFX wired into demo assets.");
        }

        public static void WireEnemyPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null) { Debug.LogWarning("[VFX] Enemy prefab missing; run SandGuard/Enemy/Create Missing Demo Assets first."); return; }
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                var reaction = Ensure<VfxHitReaction>(root);
                reaction.HitPrefab = Load(CombatVfxBuilder.EnemyHitPath);
                reaction.DeathPrefab = Load(ImpactVfxBuilder.EnemyDeathPath);
                reaction.FlinchTarget = root.transform.Find("VisualRoot");
                reaction.TintDeathWithRenderer = true;
                reaction.FlashRenderers = null; // 자식 렌더러 자동 수집 (교체된 외형도 포함)
                EnsureFootsteps(root);
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void WirePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null) { Debug.LogWarning("[VFX] Player prefab missing; run SandGuard/Player/Create Missing Demo Assets first."); return; }
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var movement = Ensure<VfxCharacterMovement>(root);
                movement.JumpPrefab = Load(CombatVfxBuilder.JumpDustPath);
                movement.AirJumpPrefab = Load(CombatVfxBuilder.AirJumpRingPath);
                movement.LandPrefab = Load(CombatVfxBuilder.LandDustPath);

                var cast = Ensure<VfxOneShot>(root);
                cast.Prefab = Load(CombatVfxBuilder.StaffCastPath);
                cast.Anchor = root.transform.Find("DefaultFirePoint");
                cast.Lifetime = 1.5f;
                var visuals = root.GetComponent<PlayerVisuals>();
                if (visuals != null && !HasListener(visuals.onFired, cast))
                    UnityEventTools.AddPersistentListener(visuals.onFired, new UnityAction(cast.Fire));

                var reaction = Ensure<VfxHitReaction>(root); // 플레이어가 IDamageEvents를 갖게 되면 바로 동작한다
                reaction.HitPrefab = Load(CombatVfxBuilder.PlayerHitPath);
                reaction.ScreenPrefab = Load(CombatVfxBuilder.PlayerHitScreenPath);
                reaction.CameraShake = 0.08f;
                reaction.FlinchTarget = null;
                reaction.FlashRenderers = null;

                var afterimage = Ensure<VfxAfterimage>(root); // 대시 시작 → 0.25초 동안 잔상
                afterimage.Material = AssetDatabase.LoadAssetAtPath<Material>(VfxBuildKit.MatMeshAdditivePath);
                if (visuals != null && !HasListener(visuals.onDashStarted, afterimage))
                    UnityEventTools.AddFloatPersistentListener(visuals.onDashStarted, new UnityAction<float>(afterimage.Play), 0.25f);

                var kick = Ensure<VfxCameraKick>(root); // 발사 → 아주 약한 카메라 킥
                kick.Strength = 0.02f; kick.Duration = 0.08f;
                if (visuals != null && !HasListener(visuals.onFired, kick))
                    UnityEventTools.AddPersistentListener(visuals.onFired, new UnityAction(kick.Kick));

                EnsureFootsteps(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void WireEnemyScene()
        {
            if (!System.IO.File.Exists(EnemyScenePath)) { Debug.LogWarning("[VFX] Enemy test scene missing."); return; }
            var scene = EditorSceneManager.OpenScene(EnemyScenePath, OpenSceneMode.Single);
            foreach (var target in Object.FindObjectsByType<EnemyTestTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var reaction = Ensure<VfxHitReaction>(target.gameObject);
                reaction.FlinchTarget = null;
                switch (target.kind)
                {
                    case CombatTargetKind.Wall:
                    case CombatTargetKind.Tower:
                    case CombatTargetKind.Core:
                        reaction.HitPrefab = Load(CombatVfxBuilder.WallHitPath);
                        reaction.DeathPrefab = Load(CombatVfxBuilder.WallDestroyPath);
                        reaction.FitDeathToBounds = true;
                        reaction.TintDeathWithRenderer = true;
                        reaction.DeathLifetime = 5f;
                        reaction.CameraShake = target.kind == CombatTargetKind.Core ? 0.1f : 0.04f;
                        break;
                    default:
                        reaction.HitPrefab = Load(CombatVfxBuilder.PlayerHitPath);
                        reaction.DeathPrefab = Load(ImpactVfxBuilder.EnemyDeathPath);
                        reaction.FlinchTarget = target.transform;
                        break;
                }
                EditorUtility.SetDirty(reaction);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EnsureFootsteps(GameObject root)
        {
            if (root.transform.Find("Footsteps") != null) return;
            var prefab = Load(CombatVfxBuilder.FootstepSandPath);
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            instance.name = "Footsteps";
            instance.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var value = go.GetComponent<T>();
            return value != null ? value : go.AddComponent<T>();
        }

        static bool HasListener(UnityEvent unityEvent, Object target)
        {
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
                if (unityEvent.GetPersistentTarget(i) == target) return true;
            return false;
        }

        static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogWarning($"[VFX] Prefab missing: {path}. Run DesertTower > VFX > Build All first.");
            return prefab;
        }
    }
}
