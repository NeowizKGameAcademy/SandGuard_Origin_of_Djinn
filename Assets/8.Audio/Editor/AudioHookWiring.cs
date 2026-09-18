using DesertTower.LevelIntegration;
using SandGuard.Player;
using SandGuard.Skills.Unity;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Audio.Editor
{
    /// <summary>
    /// 2026-09-18 남은 훅 연결. 큐는 있었지만 재생하는 곳이 없던 소리를 모두 잇는다. 여러 번 실행해도 안전하다.
    ///   스킬트리 창(SkillTreeUI.prefab)   열림·닫힘 / 구매 / 장착·해제 / 거부
    ///   건설 메뉴                         열림·닫힘 / 건설 거부 — 담당자 파일을 건드리지 않고 Level의 SfxBuildMenuWatcher가 감시
    ///   Player.prefab                    스킬 시전 거부(쿨다운·마나) / 쿨다운 완료 / 체력 낮음 루프
    ///   Level.unity 코어                  공격당할 때 경고(큐 최소 간격 4초)
    ///   New Core.prefab 제단 앵커          제단 루프
    /// 배치: -executeMethod SandGuard.Audio.Editor.AudioHookWiring.All
    /// </summary>
    public static class AudioHookWiring
    {
        const string SkillTreeUiPath = "Assets/SandGuardSkillTree/Assets/SkillTree/Generated/SkillTreeUI.prefab";
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";
        const string NewCorePath = "Assets/2.Model/Prefabs/New Core.prefab";
        const string LevelScenePath = "Assets/1.Scene/Level.unity";
        const string CountdownCue = "Wave_Countdown_Tick";

        /// <summary>(큐, 클립 파일들). 비어 있는 큐에만 적용하지 않고, 이번에 처음 채우는 큐라 그대로 넣는다.</summary>
        public static readonly (string cue, string[] files)[] Clips =
        {
            ("Core_Warning", new[] { "코어핑" }),
            ("UI_Skill_Learn", new[] { "impactBell_heavy_001", "impactBell_heavy_002", "impactBell_heavy_004" }),
            ("UI_Skill_Equip", new[] { "impactMetal_medium_001", "impactMetal_medium_003" }),
            ("UI_Menu_Open", new[] { "impactGeneric_light_001" }),
            ("UI_Menu_Close", new[] { "impactGeneric_light_003" }),
            ("UI_Cooldown_Ready", new[] { "impactGlass_medium_003", "impactGlass_medium_004" }),
            ("UI_Health_Low_Loop", new[] { "Heartbeat_Loop" }),
        };

        /// <summary>(큐, 피치, 피치 지터, 볼륨, 최소 간격). 같은 원천을 쓰는 다른 소리와 구분되게 결을 바꾼다.</summary>
        public static readonly (string cue, float pitch, float jitter, float volume, float minInterval)[] Settings =
        {
            ("Core_Warning", 0.75f, 0.02f, 0.8f, 4f),      // 코어 핑을 낮게: "코어가 위험하다"
            ("UI_Skill_Learn", 1.2f, 0.03f, 0.7f, 0.1f),
            ("UI_Skill_Equip", 1.0f, 0.04f, 0.6f, 0.05f),
            ("UI_Menu_Open", 1.15f, 0.02f, 0.5f, 0.2f),
            ("UI_Menu_Close", 0.9f, 0.02f, 0.45f, 0.2f),
            ("UI_Cooldown_Ready", 1.5f, 0.03f, 0.4f, 0.15f),
            ("UI_Health_Low_Loop", 1.0f, 0f, 0.55f, 0f),
            ("UI_Fail", 1.0f, 0.03f, 0.6f, 0.3f),          // 쿨다운 중 연타해도 0.3초에 한 번
        };

        public static void All()
        {
            RemoveCountdown();
            ApplyClipsAndSettings();
            WireSkillTree();
            WireBuildMenu();
            WirePlayer();
            WireAltar();
            WireCoreWarning();
            AssetDatabase.SaveAssets();
            Verify();
        }

        [MenuItem("SandGuard/Audio/Wire Remaining Hooks 2026-09-18")]
        public static void Menu() => All();

        static void RemoveCountdown()
        {
            string path = SfxCueBuilder.PathOf(CountdownCue);
            if (AssetDatabase.LoadAssetAtPath<SfxCue>(path) != null && AssetDatabase.DeleteAsset(path))
                Debug.Log($"[Audio] 카운트다운 큐 삭제: {path}");
        }

        static void ApplyClipsAndSettings()
        {
            var files = new System.Collections.Generic.Dictionary<string, AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioResourceMap.Dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                files[System.IO.Path.GetFileNameWithoutExtension(p)] = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }
            foreach (var (cueName, names) in Clips)
            {
                var cue = SfxCueBuilder.Load(cueName);
                if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); continue; }
                var list = new System.Collections.Generic.List<AudioClip>();
                foreach (var n in names) { if (files.TryGetValue(n, out var c)) list.Add(c); else Debug.LogWarning($"[Audio] 파일이 없다: {cueName} ← {n}"); }
                cue.clips = list.ToArray();
                EditorUtility.SetDirty(cue);
            }
            foreach (var (cueName, pitch, jitter, volume, minInterval) in Settings)
            {
                var cue = SfxCueBuilder.Load(cueName);
                if (cue == null) continue;
                cue.pitch = pitch; cue.pitchJitter = jitter; cue.volume = volume; cue.minInterval = minInterval;
                EditorUtility.SetDirty(cue);
            }
        }

        static void WireSkillTree()
        {
            WithPrefab(SkillTreeUiPath, root =>
            {
                var w = root.GetComponentInChildren<SkillTreeWindow>(true);
                if (w == null) { Debug.LogWarning("[Audio] SkillTreeUI에 SkillTreeWindow가 없다"); return false; }
                var holder = Child(root, "Sfx");
                Listen(w.onOpened, OneShot(holder, "UI_Menu_Open"));
                Listen(w.onClosed, OneShot(holder, "UI_Menu_Close"));
                Listen(w.onLearned, OneShot(holder, "UI_Skill_Learn"));
                Listen(w.onEquipped, OneShot(holder, "UI_Skill_Equip"));
                Listen(w.onFailed, OneShot(holder, "UI_Fail"));
                return true;
            });
        }

        /// <summary>
        /// 건설 메뉴는 담당자가 통합 중이라 그 스크립트·프리팹을 고치지 않는다. Level 씬 루트의 "Sfx Hooks"에
        /// SfxBuildMenuWatcher를 두어 메뉴의 공개 상태만 보고 소리를 낸다.
        /// </summary>
        static void WireBuildMenu()
        {
            if (!System.IO.File.Exists(LevelScenePath)) return;
            var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            bool changed = false;
            var go = GameObject.Find("Sfx Hooks");
            if (go == null) { go = new GameObject("Sfx Hooks"); changed = true; }
            var w = go.GetComponent<SfxBuildMenuWatcher>();
            if (w == null) { w = go.AddComponent<SfxBuildMenuWatcher>(); changed = true; }
            if (w.OpenCue == null) { w.OpenCue = SfxCueBuilder.Load("UI_Menu_Open"); changed = true; }
            if (w.CloseCue == null) { w.CloseCue = SfxCueBuilder.Load("UI_Menu_Close"); changed = true; }
            if (w.FailCue == null) { w.FailCue = SfxCueBuilder.Load("UI_Fail"); changed = true; }
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"[Audio] Level 건설 메뉴 감시 {(changed ? "연결" : "변경 없음")}");
        }

        static void WirePlayer()
        {
            WithPrefab(PlayerPrefabPath, root =>
            {
                var holder = Child(root, "Sfx");
                var caster = root.GetComponentInChildren<PlayerSkillCaster>(true);
                if (caster != null) Listen(caster.onCastFailed, OneShot(holder, "UI_Fail"));
                var ready = root.GetComponent<SfxCooldownReady>() ?? root.AddComponent<SfxCooldownReady>();
                if (ready.Cue == null) ready.Cue = SfxCueBuilder.Load("UI_Cooldown_Ready");
                var low = root.GetComponent<SfxLowHealthLoop>() ?? root.AddComponent<SfxLowHealthLoop>();
                if (low.Cue == null) low.Cue = SfxCueBuilder.Load("UI_Health_Low_Loop");
                return true;
            });
        }

        /// <summary>스킬 제단 앵커(SkillAltarAnchor) 자식에 제단 루프. 코어 험과 같은 자리이므로 거리는 짧게(큐 기본 2~12m).</summary>
        static void WireAltar()
        {
            WithPrefab(NewCorePath, root =>
            {
                var altar = root.GetComponentInChildren<SkillAltarAnchor>(true);
                if (altar == null) { Debug.LogWarning("[Audio] New Core에 SkillAltarAnchor가 없다"); return false; }
                var cue = SfxCueBuilder.Load("Env_Altar_Loop");
                foreach (var e in altar.GetComponentsInChildren<SfxEmitter>(true)) if (e.Cue == cue) return false;
                var holder = Child(altar.gameObject, "Sfx");
                holder.AddComponent<SfxEmitter>().Cue = cue;
                return true;
            });
        }

        /// <summary>코어가 공격당할 때(CoreReceiver.onChanged = 흡수로 안정도 감소) 경고. 코어 오브젝트의 기존 Core_Hit 원샷 옆에.</summary>
        static void WireCoreWarning()
        {
            if (!System.IO.File.Exists(LevelScenePath)) return;
            var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            var core = Object.FindFirstObjectByType<CoreReceiver>(FindObjectsInactive.Include);
            if (core == null) { Debug.LogWarning("[Audio] Level에 CoreReceiver가 없다"); return; }
            bool changed = false;
            var cue = SfxCueBuilder.Load("Core_Warning");
            SfxOneShot shot = null;
            foreach (var s in core.GetComponents<SfxOneShot>()) if (s.Cue == cue) shot = s;
            if (shot == null) { shot = core.gameObject.AddComponent<SfxOneShot>(); shot.Cue = cue; changed = true; }
            changed |= Listen(core.onChanged, shot);
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"[Audio] Level 코어 경고 {(changed ? "연결" : "변경 없음")}");
        }

        /* ---------------------------------------------------------------- */

        static void WithPrefab(string path, System.Func<GameObject, bool> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[Audio] 프리팹이 없다: {path}"); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try { if (edit(root)) { PrefabUtility.SaveAsPrefabAsset(root, path); Debug.Log($"[Audio] {System.IO.Path.GetFileName(path)} 배선"); } }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static GameObject Child(GameObject root, string name)
        {
            var t = root.transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go;
        }

        static SfxOneShot OneShot(GameObject holder, string cueName)
        {
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); return null; }
            foreach (var s in holder.GetComponents<SfxOneShot>()) if (s.Cue == cue) return s;
            var added = holder.AddComponent<SfxOneShot>(); added.Cue = cue; return added;
        }

        static bool Listen(UnityEvent evt, SfxOneShot shot)
        {
            if (evt == null || shot == null) return false;
            for (int i = 0; i < evt.GetPersistentEventCount(); i++) if (evt.GetPersistentTarget(i) == shot) return false;
            UnityEventTools.AddPersistentListener(evt, new UnityAction(shot.Fire));
            return true;
        }

        static string LevelHasBuildWatcher()
        {
            if (!System.IO.File.Exists(LevelScenePath)) return "no-level";
            var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            var w = Object.FindFirstObjectByType<SfxBuildMenuWatcher>(FindObjectsInactive.Include);
            return w == null ? "none" : $"open={(w.OpenCue != null)},close={(w.CloseCue != null)},fail={(w.FailCue != null)}";
        }

        /// <summary>배선 결과를 다시 읽는다. 배치 로그의 AUDIO_HOOKS 줄.</summary>
        public static void Verify()
        {
            int Count(string path, string cueName)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); var cue = SfxCueBuilder.Load(cueName);
                if (go == null || cue == null) return 0;
                int n = 0;
                foreach (var s in go.GetComponentsInChildren<SfxOneShot>(true)) if (s.Cue == cue) n++;
                foreach (var e in go.GetComponentsInChildren<SfxEmitter>(true)) if (e.Cue == cue) n++;
                return n;
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var w = AssetDatabase.LoadAssetAtPath<GameObject>(SkillTreeUiPath)?.GetComponentInChildren<SkillTreeWindow>(true);
            string clips = "";
            foreach (var (cueName, _) in Clips) { var c = SfxCueBuilder.Load(cueName); clips += $"{cueName}:{(c != null ? c.clips.Length : -1)} "; }
            Debug.Log("AUDIO_HOOKS "
                + $"skillTree(open={w?.onOpened.GetPersistentEventCount()},close={w?.onClosed.GetPersistentEventCount()},learn={w?.onLearned.GetPersistentEventCount()},equip={w?.onEquipped.GetPersistentEventCount()},fail={w?.onFailed.GetPersistentEventCount()}) "
                + $"buildMenuWatcher={LevelHasBuildWatcher()} "
                + $"player(castFail={player?.GetComponentInChildren<PlayerSkillCaster>(true)?.onCastFailed.GetPersistentEventCount()},cooldownReady={(player?.GetComponent<SfxCooldownReady>() != null)},lowHealth={(player?.GetComponent<SfxLowHealthLoop>() != null)}) "
                + $"altar={Count(NewCorePath, "Env_Altar_Loop")} countdownCue={(SfxCueBuilder.Load(CountdownCue) != null)} | {clips}");
        }
    }
}
