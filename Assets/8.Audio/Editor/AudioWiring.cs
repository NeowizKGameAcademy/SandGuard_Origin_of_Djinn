using System;
using System.Collections.Generic;
using System.Reflection;
using DesertTower.LevelIntegration;
using DesertTower.VFX;
using SandGuard.Enemy;
using SandGuard.Player;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SandGuard.Audio.Editor
{
    /// <summary>
    /// 오디오 컴포넌트를 프리팹·씬에 멱등하게 꽂는다. CombatVfxWiring의 소리판. 프리팹을 다시 만들었으면 다시 실행한다.
    /// 큐는 참조만 넣는다. 클립·볼륨·거리는 담당자가 큐 에셋에서 바꾼다. 컴포넌트에 이미 큐가 있으면 바꾸지 않는다.
    ///
    ///   Player.prefab      onFired 마나탄 / onJumped·onAirJumped / onLevelUp / onDashStarted / onLanded / onHardLanded / onRespawned / 피격·사망 / 발소리 / 상승 기류 충전 루프
    ///   Enemy.prefab(+변형) 피격·사망 / onAttack 휘두름 / onDied 음성 / 발소리. 변형별 큐 오버라이드
    ///   Chief_Bomb, ExperienceOrb, Tower_Obelisk   이미터
    ///   VFX 프리팹         VfxTable에 따라 자식 Sfx 이미터
    ///   Level.unity        코어 험·피격, 사막 바람, 폭풍 벽·번개, MusicDirector
    ///   MainScene.unity    버튼, 메뉴 앰비언스, MusicDirector(MenuMode)
    /// </summary>
    public static class AudioWiring
    {
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";
        const string EnemyPrefabPath = "Assets/Enemy/Generated/Enemy.prefab";
        const string LevelScenePath = "Assets/1.Scene/Level.unity";
        const string MainScenePath = "Assets/1.Scene/MainScene.unity";
        const string SkillTreeUiPath = "Assets/SandGuardSkillTree/Assets/SkillTree/Generated/SkillTreeUI.prefab";
        const string VfxPrefabDir = "Assets/Resources/VFX/Prefabs";

        /// <summary>적 변형 → (휘두름, 사망 음성, 발소리, 보폭).</summary>
        static readonly (string path, string swing, string deathVoice, string footstep, float stride)[] EnemyVariants =
        {
            ("Assets/Enemy/Generated/Enemy_Swordsman.prefab", "Enemy_Swordsman_Swing", "Enemy_Voice_Death_Light", "Enemy_Footstep_Light", 0.8f),
            ("Assets/Enemy/Generated/Enemy_Assassin.prefab", "Enemy_Assassin_Swing", "Enemy_Voice_Death_Light", "Enemy_Footstep_Light", 0.7f),
            ("Assets/Enemy/Generated/Enemy_ShieldGuard.prefab", "Enemy_ShieldGuard_Swing", "Enemy_Voice_Death_Light", "Enemy_Footstep_Heavy", 1.0f),
            ("Assets/Enemy/Generated/Enemy_HammerBrute.prefab", "Enemy_HammerBrute_Swing", "Enemy_Voice_Death_Heavy", "Enemy_Footstep_Heavy", 1.05f),
            ("Assets/Enemy/Generated/Enemy_Chief.prefab", "Enemy_Chief_Swing", "Enemy_Voice_Chief_Death", "Enemy_Footstep_Heavy", 1.2f),
        };

        /// <summary>VFX 프리팹 → 큐. 이벤트로 이미 내는 소리(점프·대시·착지·발사·피격·레벨업)는 여기 넣지 않아 두 번 나지 않는다.</summary>
        static readonly (string prefab, string cue, SfxEmitterTrigger trigger)[] VfxTable =
        {
            ("VFX_ManaBolt_Projectile", "Player_ManaBolt_Flight_Loop", SfxEmitterTrigger.OnEnable),
            ("VFX_ManaBolt_Impact", "Player_ManaBolt_Impact", SfxEmitterTrigger.OnEnable),
            ("VFX_Pierce_Beam", "Player_PierceBeam_Fire", SfxEmitterTrigger.OnEnable),
            ("VFX_Sand_Burst", "Player_SandBurst", SfxEmitterTrigger.OnEnable),
            ("VFX_Sand_Root", "Player_SandShackle", SfxEmitterTrigger.OnEnable),
            ("VFX_Sand_Vortex", "Player_SandVortex_Loop", SfxEmitterTrigger.WhileParticlesEmit),
            ("VFX_Sand_Storm", "Player_SandStorm_Loop", SfxEmitterTrigger.WhileParticlesEmit),
            ("VFX_Updraft_Launch", "Player_Updraft_Launch", SfxEmitterTrigger.OnEnable),
            ("VFX_Mana_Charge", "Player_ManaCharge_Loop", SfxEmitterTrigger.WhileParticlesEmit),
            ("VFX_Mana_Charge_Complete", "Player_ManaCharge_Complete", SfxEmitterTrigger.OnEnable),
            ("VFX_Enemy_Spawn", "Enemy_Spawn", SfxEmitterTrigger.OnEnable),
            ("VFX_Shield_Front_Guard", "Enemy_ShieldBlock", SfxEmitterTrigger.OnEnable),
            ("VFX_Shield_Gold_Guard", "Enemy_Chief_ShieldBlock", SfxEmitterTrigger.OnEnable),
            ("VFX_Chief_Golden_Shield_Loop", "Enemy_Chief_Shield_Loop", SfxEmitterTrigger.OnEnable),
            ("VFX_Demolition_Bomb_Explosion", "Enemy_Chief_Bomb_Explosion", SfxEmitterTrigger.OnEnable),
            ("VFX_Burning_Loop", "Enemy_Burning", SfxEmitterTrigger.OnEnable),
            ("VFX_Build_Poof", "Facility_Build_Poof", SfxEmitterTrigger.OnEnable),
            ("VFX_Build_Complete", "Facility_Build_Complete", SfxEmitterTrigger.OnEnable),
            ("VFX_FlameCobra_Breath", "Facility_Cobra_Flame_Loop", SfxEmitterTrigger.WhileParticlesEmit),
            ("VFX_Fire_Impact", "Facility_Cobra_Flame_Impact", SfxEmitterTrigger.OnEnable),
            ("VFX_Facility_Hit", "Facility_Hit", SfxEmitterTrigger.OnEnable),
            ("VFX_Cobra_Destruction", "Facility_Cobra_Destroy", SfxEmitterTrigger.OnEnable),
            ("VFX_Facility_Disabled_Loop", "Facility_Disabled_Loop", SfxEmitterTrigger.OnEnable),
            ("VFX_Summon_Circle", "Facility_Summon_Circle_Loop", SfxEmitterTrigger.WhileParticlesEmit),
            ("VFX_Summon_Pillar", "Facility_Summon_Pillar", SfxEmitterTrigger.OnEnable),
            ("VFX_Wall_Hit", "Wall_Hit", SfxEmitterTrigger.OnEnable),
            ("VFX_Wall_Destroy", "Wall_Destroy", SfxEmitterTrigger.OnEnable),
            ("VFX_Core_Destruction", "Core_Destroy", SfxEmitterTrigger.OnEnable),
            ("VFX_Wave_Clear", "Wave_Clear", SfxEmitterTrigger.OnEnable),
            ("VFX_Torch", "Env_Torch_Loop", SfxEmitterTrigger.OnEnable),
        };

        /// <summary>배치 진입점: 믹서 → 자리표시 클립 → 큐 → 배선 → 검증.</summary>
        public static void All()
        {
            AudioMixerSetup.EnsureMixer();
            Synth.SynthSfxBaker.BakePlaceholders();
            SfxCueBuilder.Build();
            WireAll();
            Verify();
        }

        [MenuItem("SandGuard/Audio/Setup Everything (Mixer + Placeholders + Cues + Wire)")]
        public static void SetupMenu() => All();

        [MenuItem("SandGuard/Audio/Wire Audio Into Prefabs And Scenes")]
        public static void WireAll()
        {
            WirePlayerPrefab();
            WireEnemyPrefabs();
            WireEmitterOnPrefab("Assets/Enemy/Generated/Chief_Bomb.prefab", "Enemy_Chief_Bomb_Fuse_Loop");
            WireEmitterOnPrefab("Assets/Enemy/Generated/ExperienceOrb.prefab", "Player_Xp_Drop");
            WireEmitterOnPrefab("Assets/Facility/Generated/Towers/Tower_Obelisk.prefab", "Facility_Obelisk_Loop");
            WireVfxPrefabs();
            WireLevelScene();
            WireMainScene();
            WireButtonsInPrefab(SkillTreeUiPath);
            AssetDatabase.SaveAssets();
        }

        /* ================================================================ */
        /* Player                                                           */
        /* ================================================================ */

        public static void WirePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null) { Debug.LogWarning("[Audio] Player.prefab이 없다"); return; }
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var holder = Child(root, "Sfx");
                var visuals = root.GetComponentInChildren<PlayerVisuals>(true);
                var progression = root.GetComponentInChildren<PlayerProgression>(true);
                var respawner = root.GetComponentInChildren<PlayerRespawner>(true);
                var updraft = root.GetComponentInChildren<PlayerUpdraft>(true);

                Transform staffTip = null;
                foreach (var v in root.GetComponentsInChildren<VfxOneShot>(true))
                    if (v.Prefab != null && v.Prefab.name.Contains("Staff_Cast") && v.Anchor != null) staffTip = v.Anchor;

                Listen(visuals?.onFired, OneShot(holder, "Player_ManaBolt_Fire", staffTip));
                Listen(visuals?.onJumped, OneShot(holder, "Player_Jump"));
                Listen(visuals?.onAirJumped, OneShot(holder, "Player_AirJump"));
                Listen(progression?.onLevelUp, OneShot(holder, "Player_LevelUp"));
                Listen(visuals?.onDashStarted, OneShot(holder, "Player_Dash"));
                Listen(visuals?.onLanded, OneShot(holder, "Player_Land"));
                Listen(visuals?.onHardLanded, OneShot(holder, "Player_HardLand"));
                Listen(respawner?.onRespawned, OneShot(holder, "Player_Revive"));

                HitReaction(root, "Player_Hit", "Player_Death");
                Footsteps(root, "Player_Footstep_Sand", 0.75f, true);

                if (updraft != null)
                {
                    var toggle = holder.GetComponent<SfxLoopToggle>() ?? holder.AddComponent<SfxLoopToggle>();
                    if (toggle.LoopCue == null) toggle.LoopCue = SfxCueBuilder.Load("Player_Updraft_Charge_Loop");
                    if (!HasListener(updraft.onChargeStarted, toggle)) UnityEventTools.AddPersistentListener(updraft.onChargeStarted, new UnityAction(toggle.Begin));
                    if (!HasListener(updraft.onCharging, toggle)) UnityEventTools.AddPersistentListener(updraft.onCharging, new UnityAction<float>(toggle.SetIntensity));
                    if (!HasListener(updraft.onChargeCancelled, toggle)) UnityEventTools.AddPersistentListener(updraft.onChargeCancelled, new UnityAction(toggle.End));
                    if (!HasListener(updraft.onLaunched, toggle)) UnityEventTools.AddPersistentListener(updraft.onLaunched, new UnityAction(toggle.End));
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[Audio] Player.prefab 배선 완료");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /* ================================================================ */
        /* Enemy                                                            */
        /* ================================================================ */

        public static void WireEnemyPrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null) { Debug.LogWarning("[Audio] Enemy.prefab이 없다"); return; }
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                var holder = Child(root, "Sfx");
                var visuals = root.GetComponentInChildren<EnemyVisuals>(true);
                Listen(visuals?.onAttack, OneShot(holder, "Enemy_Swordsman_Swing"));
                Listen(visuals?.onDied, OneShot(holder, "Enemy_Voice_Death_Light"));
                HitReaction(root, "Enemy_Hit", "Enemy_Death");
                Footsteps(root, "Enemy_Footstep_Light", 0.8f, false);
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            int variants = 0;
            foreach (var v in EnemyVariants)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(v.path) == null) continue;
                var vroot = PrefabUtility.LoadPrefabContents(v.path);
                try
                {
                    bool changed = false;
                    foreach (var shot in vroot.GetComponentsInChildren<SfxOneShot>(true))
                    {
                        if (shot.Cue == null) continue;
                        if (shot.Cue.name.Contains("_Swing")) changed |= Set(shot, v.swing);
                        else if (shot.Cue.name.Contains("Voice_Death")) changed |= Set(shot, v.deathVoice);
                    }
                    var steps = vroot.GetComponentInChildren<SfxFootsteps>(true);
                    if (steps != null)
                    {
                        var cue = SfxCueBuilder.Load(v.footstep);
                        if (cue != null && steps.Cue != cue) { steps.Cue = cue; changed = true; }
                        if (!Mathf.Approximately(steps.Stride, v.stride)) { steps.Stride = v.stride; changed = true; }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(vroot, v.path);
                    variants++;
                }
                finally { PrefabUtility.UnloadPrefabContents(vroot); }
            }
            Debug.Log($"[Audio] Enemy.prefab + 변형 {variants}개 배선 완료");
        }

        static bool Set(SfxOneShot shot, string cueName)
        {
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null || shot.Cue == cue) return false;
            shot.Cue = cue; return true;
        }

        /* ================================================================ */
        /* 프리팹 이미터 (VFX, 폭탄, 경험치, 오벨리스크)                        */
        /* ================================================================ */

        [MenuItem("SandGuard/Audio/Wire VFX Prefabs Only")]
        public static void WireVfxPrefabs()
        {
            int n = 0;
            foreach (var row in VfxTable)
                if (WireEmitterOnPrefab($"{VfxPrefabDir}/{row.prefab}.prefab", row.cue, row.trigger)) n++;
            Debug.Log($"[Audio] VFX 프리팹 {n}/{VfxTable.Length} 배선");
        }

        /// <summary>프리팹 루트 아래 자식 "Sfx"에 SfxEmitter를 둔다. 이미 같은 큐의 이미터가 있으면 손대지 않는다.</summary>
        public static bool WireEmitterOnPrefab(string path, string cueName, SfxEmitterTrigger trigger = SfxEmitterTrigger.OnEnable)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[Audio] 프리팹이 없다: {path}"); return false; }
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); return false; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var e in root.GetComponentsInChildren<SfxEmitter>(true)) if (e.Cue == cue) return true;
                var holder = Child(root, "Sfx");
                var emitter = holder.GetComponent<SfxEmitter>();
                if (emitter != null && emitter.Cue != null) emitter = holder.AddComponent<SfxEmitter>();
                if (emitter == null) emitter = holder.AddComponent<SfxEmitter>();
                emitter.Cue = cue; emitter.Trigger = trigger;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /* ================================================================ */
        /* 씬                                                               */
        /* ================================================================ */

        public static void WireLevelScene()
        {
            if (!System.IO.File.Exists(LevelScenePath)) { Debug.LogWarning("[Audio] Level.unity가 없다"); return; }
            var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            bool changed = false;

            var core = UnityEngine.Object.FindFirstObjectByType<CoreReceiver>(FindObjectsInactive.Include);
            if (core != null)
            {
                changed |= EnsureEmitter(core.gameObject, "Core_Hum_Loop");
                var hit = core.GetComponent<SfxOneShot>();
                if (hit == null) { hit = OneShot(core.gameObject, "Core_Hit"); changed = true; }
                changed |= Listen(core.onChanged, hit);
            }
            else Debug.LogWarning("[Audio] Level.unity에 CoreReceiver가 없다");

            var ambience = GameObject.Find("Sfx Ambience");
            if (ambience == null) { ambience = new GameObject("Sfx Ambience"); changed = true; }
            changed |= EnsureEmitter(ambience, "Env_DesertWind_Loop");

            var lightning = UnityEngine.Object.FindFirstObjectByType<StormLightning>(FindObjectsInactive.Include);
            if (lightning != null)
            {
                changed |= EnsureEmitter(lightning.gameObject, "Env_StormWall_Loop");
                var strike = lightning.GetComponent<SfxOneShot>();
                if (strike == null) { strike = OneShot(lightning.gameObject, "Env_Lightning"); changed = true; }
                changed |= Listen(lightning.onStrike, strike);
            }

            changed |= EnsureMusic(false);

            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"[Audio] Level.unity 배선 {(changed ? "저장" : "변경 없음")}");
        }

        public static void WireMainScene()
        {
            if (!System.IO.File.Exists(MainScenePath)) return;
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= EnsureUiButton(b.gameObject);
            var ambience = GameObject.Find("Sfx Ambience");
            if (ambience == null) { ambience = new GameObject("Sfx Ambience"); changed = true; }
            changed |= EnsureEmitter(ambience, "Env_Menu_Ambience_Loop");
            changed |= EnsureMusic(true);
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"[Audio] MainScene.unity 배선 {(changed ? "저장" : "변경 없음")}");
        }

        public static void WireButtonsInPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = 0;
                foreach (var b in root.GetComponentsInChildren<Button>(true)) if (EnsureUiButton(b.gameObject)) n++;
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>"Sfx Music" 오브젝트의 MusicDirector. 비어 있는 큐 필드만 채운다.</summary>
        static bool EnsureMusic(bool menuMode)
        {
            var go = GameObject.Find("Sfx Music");
            bool changed = false;
            if (go == null) { go = new GameObject("Sfx Music"); changed = true; }
            var m = go.GetComponent<MusicDirector>();
            if (m == null) { m = go.AddComponent<MusicDirector>(); m.MenuMode = menuMode; changed = true; }
            changed |= Fill(ref m.Menu, "Music_Menu_Loop");
            changed |= Fill(ref m.Preparation, "Music_Preparation_Loop");
            changed |= Fill(ref m.Combat, "Music_Combat_Loop");
            changed |= Fill(ref m.Boss, "Music_Boss_Loop");
            changed |= Fill(ref m.Danger, "Music_Danger_Loop");
            changed |= Fill(ref m.Victory, "Music_Victory");
            changed |= Fill(ref m.Defeat, "Music_Defeat");
            changed |= Fill(ref m.WaveClearSting, "Music_WaveClear_Sting");
            changed |= Fill(ref m.BossSting, "Music_Boss_Sting");
            return changed;
        }

        static bool Fill(ref SfxCue field, string cueName)
        {
            if (field != null) return false;
            field = SfxCueBuilder.Load(cueName);
            return field != null;
        }

        /* ================================================================ */
        /* 공통                                                             */
        /* ================================================================ */

        static GameObject Child(GameObject root, string name)
        {
            var t = root.transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go;
        }

        /// <summary>같은 큐의 SfxOneShot이 있으면 그것, 없으면 추가. 큐가 없으면 null.</summary>
        static SfxOneShot OneShot(GameObject holder, string cueName, Transform anchor = null)
        {
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); return null; }
            SfxOneShot found = null;
            foreach (var s in holder.GetComponents<SfxOneShot>()) if (s.Cue == cue) { found = s; break; }
            if (found == null) { found = holder.AddComponent<SfxOneShot>(); found.Cue = cue; }
            if (anchor != null && found.Anchor == null) found.Anchor = anchor;
            return found;
        }

        static bool Listen(UnityEvent evt, SfxOneShot shot)
        {
            if (evt == null || shot == null || HasListener(evt, shot)) return false;
            UnityEventTools.AddPersistentListener(evt, new UnityAction(shot.Fire));
            return true;
        }

        static void HitReaction(GameObject root, string hitCue, string deathCue)
        {
            var events = root.GetComponentInChildren<IDamageEvents>(true) as Component;
            var host = events != null ? events.gameObject : root;
            var r = host.GetComponent<SfxHitReaction>() ?? host.AddComponent<SfxHitReaction>();
            if (r.HitCue == null) r.HitCue = SfxCueBuilder.Load(hitCue);
            if (r.DeathCue == null) r.DeathCue = SfxCueBuilder.Load(deathCue);
        }

        static void Footsteps(GameObject root, string cueName, float stride, bool requireGround)
        {
            var f = root.GetComponent<SfxFootsteps>();
            if (f == null) { f = root.AddComponent<SfxFootsteps>(); f.Stride = stride; f.RequireGround = requireGround; }
            if (f.Cue == null) f.Cue = SfxCueBuilder.Load(cueName);
        }

        static bool EnsureEmitter(GameObject go, string cueName)
        {
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); return false; }
            foreach (var e in go.GetComponents<SfxEmitter>()) if (e.Cue == cue) return false;
            go.AddComponent<SfxEmitter>().Cue = cue;
            return true;
        }

        static bool EnsureUiButton(GameObject go)
        {
            var hover = SfxCueBuilder.Load("UI_Hover"); var click = SfxCueBuilder.Load("UI_Click");
            var ui = go.GetComponent<SfxUiButton>();
            bool changed = ui == null;
            if (ui == null) ui = go.AddComponent<SfxUiButton>();
            if (ui.HoverCue == null && hover != null) { ui.HoverCue = hover; changed = true; }
            if (ui.ClickCue == null && click != null) { ui.ClickCue = click; changed = true; }
            return changed;
        }

        static bool HasListener(UnityEventBase e, UnityEngine.Object target)
        {
            for (int i = 0; i < e.GetPersistentEventCount(); i++) if (e.GetPersistentTarget(i) == target) return true;
            return false;
        }

        /// <summary>배선 결과를 다시 읽어 로그로 남긴다. 배치 로그에서 AUDIO_WIRED 줄을 확인한다.</summary>
        public static void Verify()
        {
            int playerShots = 0, enemyShots = 0, vfx = 0, levelEmitters = 0, buttons = 0, cues = 0, missingClips = 0;
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player != null) foreach (var s in player.GetComponentsInChildren<SfxOneShot>(true)) if (s.Cue != null && s.Cue.HasClips) playerShots++;
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemy != null) foreach (var s in enemy.GetComponentsInChildren<SfxOneShot>(true)) if (s.Cue != null && s.Cue.HasClips) enemyShots++;
            foreach (var row in VfxTable)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxPrefabDir}/{row.prefab}.prefab");
                if (p != null && p.GetComponentInChildren<SfxEmitter>(true) != null) vfx++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:SfxCue", new[] { SfxCueBuilder.CueRoot }))
            {
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(AssetDatabase.GUIDToAssetPath(guid));
                cues++; if (!cue.HasClips) missingClips++;
            }
            if (System.IO.File.Exists(LevelScenePath))
            {
                EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
                foreach (var e in UnityEngine.Object.FindObjectsByType<SfxEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (e.Cue != null && e.Cue.HasClips) levelEmitters++;
            }
            if (System.IO.File.Exists(MainScenePath))
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
                foreach (var b in UnityEngine.Object.FindObjectsByType<SfxUiButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (b.ClickCue != null) buttons++;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"AUDIO_WIRED cues={cues} cuesWithoutClips={missingClips} playerOneShots={playerShots} enemyOneShots={enemyShots} vfxPrefabs={vfx}/{VfxTable.Length} levelEmitters={levelEmitters} mainMenuButtons={buttons} mixer={(AssetDatabase.LoadAssetAtPath<AudioMixer>(SfxCueBuilder.MixerPath) != null)}");
        }
    }

    /// <summary>
    /// 믹서 에셋을 코드로 만든다 (Master > SFX / Environment / Music / UI / Voice).
    /// 공개 API가 없어 에디터 내부 AudioMixerController를 리플렉션으로 부른다. 실패하면 경고만 남기고 큐는 마스터로 나간다.
    /// 이미 있으면 건드리지 않는다 — 담당자가 인스펙터에서 그룹·스냅샷을 편집하는 곳이다.
    /// </summary>
    public static class AudioMixerSetup
    {
        static readonly string[] Groups = { "SFX", "Environment", "Music", "UI", "Voice" };

        [MenuItem("SandGuard/Audio/Create Mixer If Missing")]
        public static void EnsureMixer()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(SfxCueBuilder.MixerPath) != null) { Debug.Log("[Audio] 믹서가 이미 있다"); return; }
            try
            {
                var t = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
                var create = t.GetMethod("CreateMixerControllerAtPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var ctrl = create.Invoke(null, new object[] { SfxCueBuilder.MixerPath });
                var master = t.GetProperty("masterGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(ctrl);
                var createGroup = t.GetMethod("CreateNewGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(bool) }, null);
                var addChild = t.GetMethod("AddChildToParent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var name in Groups)
                {
                    var g = createGroup.Invoke(ctrl, new object[] { name, false });
                    addChild.Invoke(ctrl, new object[] { g, master });
                }
                EditorUtility.SetDirty((UnityEngine.Object)ctrl);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(SfxCueBuilder.MixerPath);
                Debug.Log($"[Audio] 믹서 생성: {SfxCueBuilder.MixerPath} ({string.Join(", ", Groups)})");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Audio] 믹서를 코드로 만들지 못했다 ({e.GetType().Name}: {e.Message}). 에디터에서 Create > Audio Mixer로 {SfxCueBuilder.MixerPath}를 만들고 그룹 {string.Join("/", Groups)}를 추가하면 큐 빌더가 연결한다.");
            }
        }
    }
}
