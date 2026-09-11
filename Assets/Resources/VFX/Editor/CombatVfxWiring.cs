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
                var restraint = root.GetComponent<EnemyRestraint>(); // 모래 족쇄 → 발목을 감는 모래
                if (restraint != null) restraint.vfxPrefab = Load(SandRootVfxBuilder.Path);
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
                var handAnchor = root.transform.Find("CastingEffectAnchor");
                cast.Anchor = handAnchor != null ? handAnchor : root.transform.Find("DefaultFirePoint");
                cast.SpawnScale = Vector3.one * (handAnchor != null ? .35f : 1f);
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

                // 대시 잔상(틸 실루엣)은 눈이 아파서 뺐다. 예전 프리팹에 남아 있으면 리스너와 컴포넌트를 걷어낸다.
                var afterimage = root.GetComponent<VfxAfterimage>();
                if (afterimage != null)
                {
                    if (visuals != null) RemoveListeners(visuals.onDashStarted, afterimage);
                    Object.DestroyImmediate(afterimage, true);
                }

                var attack = root.GetComponent<PlayerBasicAttack>(); // 공격 마법 스킬: 관통 빔, 꿰뚫은 적 착탄, 모래 폭발
                if (attack != null)
                {
                    attack.beamPrefab = Load(BeamVfxBuilder.PierceBeamPath);
                    attack.beamHitPrefab = Load(ImpactVfxBuilder.ManaBoltImpactPath);
                    attack.burstPrefab = Load(ImpactVfxBuilder.SandBurstPath);
                }
                var caster = root.GetComponent<PlayerSkillCaster>(); // E 모래 소용돌이, R 사막 폭풍 지역 연출
                if (caster != null)
                {
                    caster.vortexPrefab = Load(SandZoneVfxBuilder.VortexPath);
                    caster.stormPrefab = Load(SandZoneVfxBuilder.StormPath);
                }

                var kick = Ensure<VfxCameraKick>(root); // 발사 → 아주 약한 카메라 킥
                kick.Strength = 0.02f; kick.Duration = 0.08f;
                if (visuals != null && !HasListener(visuals.onFired, kick))
                    UnityEventTools.AddPersistentListener(visuals.onFired, new UnityAction(kick.Kick));

                var updraft = root.GetComponent<PlayerUpdraft>(); // 상승 기류 발사 → 발밑 충격파·모래먼지
                if (updraft != null)
                {
                    var anchor = root.transform.Find("UpdraftVfx");
                    if (anchor == null)
                    {
                        anchor = new GameObject("UpdraftVfx").transform;
                        anchor.SetParent(root.transform, false);
                        anchor.localPosition = new Vector3(0f, 0.02f, 0f);
                    }
                    var launch = Ensure<VfxOneShot>(anchor.gameObject);
                    launch.Prefab = Load(UpdraftVfxBuilder.LaunchPath);
                    launch.Anchor = anchor; launch.ParentToAnchor = false; launch.Lifetime = 2.5f; launch.SpawnScale = Vector3.one;
                    if (!HasListener(updraft.onLaunched, launch))
                        UnityEventTools.AddPersistentListener(updraft.onLaunched, new UnityAction(launch.Fire));
                    movement.MaxJumpVelocity = 12f; // 발사(14m/s 이상)는 점프 먼지를 내지 않는다

                    // 충전 기류: 몸 중심에 자식으로 두고 충전 시작/세기/끝을 꽂는다.
                    var chargePrefab = Load(UpdraftVfxBuilder.ChargePath);
                    if (chargePrefab != null)
                    {
                        var chargeInstance = root.transform.Find("UpdraftCharge");
                        if (chargeInstance == null)
                        {
                            chargeInstance = ((GameObject)PrefabUtility.InstantiatePrefab(chargePrefab, root.transform)).transform;
                            chargeInstance.name = "UpdraftCharge";
                            chargeInstance.localPosition = new Vector3(0f, 0.95f, 0f);
                        }
                        var loop = chargeInstance.GetComponent<VfxChargeLoop>();
                        if (loop != null)
                        {
                            if (!HasListener(updraft.onChargeStarted, loop)) UnityEventTools.AddPersistentListener(updraft.onChargeStarted, new UnityAction(loop.Begin));
                            if (!HasListener(updraft.onCharging, loop)) UnityEventTools.AddPersistentListener(updraft.onCharging, new UnityAction<float>(loop.SetIntensity));
                            if (!HasListener(updraft.onChargeCancelled, loop)) UnityEventTools.AddPersistentListener(updraft.onChargeCancelled, new UnityAction(loop.End));
                            if (!HasListener(updraft.onLaunched, loop)) UnityEventTools.AddPersistentListener(updraft.onLaunched, new UnityAction(loop.End));
                        }
                    }

                    // 볼록 렌즈: 몸 중심의 굴절 구체. 충전으로 부풀고 발사 때 펄스.
                    var lensPrefab = Load(UpdraftVfxBuilder.LensPath);
                    if (lensPrefab != null)
                    {
                        var lensInstance = root.transform.Find("UpdraftLens");
                        if (lensInstance == null)
                        {
                            lensInstance = ((GameObject)PrefabUtility.InstantiatePrefab(lensPrefab, root.transform)).transform;
                            lensInstance.name = "UpdraftLens";
                            lensInstance.localPosition = new Vector3(0f, 0.95f, 0f);
                        }
                        var bubble = lensInstance.GetComponent<VfxRefractionBubble>();
                        if (bubble != null)
                        {
                            if (!HasListener(updraft.onChargeStarted, bubble)) UnityEventTools.AddPersistentListener(updraft.onChargeStarted, new UnityAction(bubble.Begin));
                            if (!HasListener(updraft.onCharging, bubble)) UnityEventTools.AddPersistentListener(updraft.onCharging, new UnityAction<float>(bubble.SetIntensity));
                            if (!HasListener(updraft.onChargeCancelled, bubble)) UnityEventTools.AddPersistentListener(updraft.onChargeCancelled, new UnityAction(bubble.End));
                            if (!HasListener(updraft.onLaunched, bubble)) UnityEventTools.AddPersistentListener(updraft.onLaunched, new UnityAction(bubble.Pulse));
                        }
                    }
                }

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

        static void RemoveListeners(UnityEventBase unityEvent, Object target)
        {
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                if (unityEvent.GetPersistentTarget(i) == target) UnityEventTools.RemovePersistentListener(unityEvent, i);
        }

        static bool HasListener(UnityEventBase unityEvent, Object target)
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
