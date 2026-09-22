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
    ///   Player.prefab      onFired 마나탄 / onJumped·onAirJumped / onLevelUp / onDashStarted / onLanded / onHardLanded / onRespawned / 피격·사망
    ///                      발 뼈 발소리 / 상승 기류 충전 루프 / Falling 상태 낙하 바람 / Death 클립 BodyFall 이벤트
    ///   Enemy.prefab(+변형) 피격·사망 / onDied 음성 / 발 뼈 발소리(변형별 큐)
    ///   <이름>_CombatVisual.prefab  SfxAnimationEvents(휘두름·임팩트·쓰러짐·던지기) + Attack/Death 클립 이벤트
    ///   Core.prefab        Circle Effect 애니메이터 루프 → Core_Ping
    ///   Chief_Bomb, ExperienceOrb, Tower_Obelisk   이미터
    ///   VFX 프리팹         VfxTable(이미터, 시작·끝 큐) + TimelineTable(단계 지연)
    ///   Level.unity        코어 험·피격, 사막 바람, 폭풍 벽·번개, MusicDirector
    ///   MainScene.unity    버튼, 메뉴 앰비언스, MusicDirector(MenuMode)
    /// </summary>
    public static class AudioWiring
    {
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";
        const string PlayerDeathFbx = "Assets/Player/Art/Protagonist/CombatAnimations/Death.fbx";
        const string EnemyPrefabPath = "Assets/Enemy/Generated/Enemy.prefab";
        const string EnemyArtDir = "Assets/Enemy/Art/Characters";
        const string CorePrefabPath = "Assets/2.Model/Prefabs/Core.prefab";
        const string LevelScenePath = "Assets/1.Scene/Level.unity";
        const string MainScenePath = "Assets/1.Scene/MainScene.unity";
        const string SkillTreeUiPath = "Assets/SandGuardSkillTree/Assets/SkillTree/Generated/SkillTreeUI.prefab";
        const string VfxPrefabDir = "Assets/Resources/VFX/Prefabs";

        /// <summary>휘두름 이벤트를 타격보다 이만큼 앞에 둔다.</summary>
        const float SwingLead = 0.15f;

        /// <summary>
        /// 휘두름 소리의 최고점이 타격 순간에 오도록 Swing 이벤트를 타격보다 이만큼 앞에 둔다.
        /// 휘두름 클립은 최고점이 가벼운 묶음 0.12초, 무거운 묶음 0.30초에 오도록 잘라 두었다(AudioResource/휘두름_*).
        /// </summary>
        static float SwingLeadFor(string enemy) => enemy == "HammerBrute" || enemy == "Chief" ? 0.30f : 0.12f;

        /// <summary>적 변형 → (이름, 프리팹, 휘두름, 임팩트, 사망 음성, 발소리).</summary>
        static readonly (string name, string path, string swing, string impact, string deathVoice, string footstep)[] EnemyVariants =
        {
            ("Swordsman", "Assets/Enemy/Generated/Enemy_Swordsman.prefab", "Enemy_Swordsman_Swing", null, "Enemy_Voice_Death_Light", "Enemy_Footstep_Light"),
            ("Assassin", "Assets/Enemy/Generated/Enemy_Assassin.prefab", "Enemy_Assassin_Swing", null, "Enemy_Voice_Death_Light", "Enemy_Footstep_Light"),
            ("ShieldGuard", "Assets/Enemy/Generated/Enemy_ShieldGuard.prefab", "Enemy_ShieldGuard_Swing", "Enemy_ShieldGuard_Impact", "Enemy_Voice_Death_Light", "Enemy_Footstep_Heavy"),
            ("HammerBrute", "Assets/Enemy/Generated/Enemy_HammerBrute.prefab", "Enemy_HammerBrute_Swing", "Enemy_HammerBrute_Impact", "Enemy_Voice_Death_Heavy", "Enemy_Footstep_Heavy"),
            ("Chief", "Assets/Enemy/Generated/Enemy_Chief.prefab", "Enemy_Chief_Swing", null, "Enemy_Voice_Chief_Death", "Enemy_Footstep_Heavy"),
        };

        /// <summary>VFX 프리팹 → 이미터. 이벤트로 이미 내는 소리(점프·대시·착지·발사·피격·레벨업)는 여기 넣지 않아 두 번 나지 않는다.</summary>
        static readonly (string prefab, string cue, SfxEmitterTrigger trigger, string begin, string end)[] VfxTable =
        {
            ("VFX_ManaBolt_Projectile", "Player_ManaBolt_Flight_Loop", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_ManaBolt_Impact", "Player_ManaBolt_Impact", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Pierce_Beam", "Player_PierceBeam_Fire", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Sand_Burst", "Player_SandBurst", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Sand_Root", "Player_SandShackle", SfxEmitterTrigger.WhileParticlesEmit, null, "Player_SandShackle_Release"),
            ("VFX_Sand_Vortex", "Player_SandVortex_Loop", SfxEmitterTrigger.WhileParticlesEmit, null, "Player_SandVortex_End"),
            ("VFX_Sand_Storm", "Player_SandStorm_Loop", SfxEmitterTrigger.WhileParticlesEmit, null, "Player_SandStorm_End"),
            ("VFX_Sand_Storm", "Player_SandStorm_Cast", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Pierce_Charge", "Player_Pierce_Charge_Loop", SfxEmitterTrigger.WhileParticlesEmit, "Player_Pierce_Charge", null),
            ("VFX_Pierce_Hit", "Player_ManaBolt_Impact", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Recall_Mark", "Player_Recall_Mark", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Recall_Depart", "Player_Recall_Warp", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Recall_Arrive", "Player_Recall_Arrive", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Core_Damage_Enemy", "Core_Absorb", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Updraft_Launch", "Player_Updraft_Launch", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Mana_Charge", "Player_ManaCharge_Loop", SfxEmitterTrigger.WhileParticlesEmit, null, null),
            ("VFX_Mana_Charge_Complete", "Player_ManaCharge_Complete", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Enemy_Spawn", "Enemy_Spawn", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Shield_Front_Guard", "Enemy_ShieldBlock", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Shield_Gold_Guard", "Enemy_Chief_ShieldBlock", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Chief_Golden_Shield_Loop", "Enemy_Chief_Shield_Loop", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Demolition_Bomb_Explosion", "Enemy_Chief_Bomb_Explosion", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Burning_Loop", "Enemy_Burning", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Build_Poof", "Facility_Build_Poof", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Build_Complete", "Facility_Build_Complete", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_FlameCobra_Breath", "Facility_Cobra_Flame_Loop", SfxEmitterTrigger.WhileParticlesEmit, "Facility_Cobra_Ignite", "Facility_Cobra_Extinguish"),
            ("VFX_Fire_Impact", "Facility_Cobra_Flame_Impact", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Facility_Hit", "Facility_Hit", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Facility_Disabled_Loop", "Facility_Disabled_Loop", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Summon_Circle", "Facility_Summon_Circle_Loop", SfxEmitterTrigger.WhileParticlesEmit, null, null),
            ("VFX_Summon_Pillar", "Facility_Summon_Pillar", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Wall_Hit", "Wall_Hit", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Wall_Destroy", "Wall_Destroy", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Wave_Clear", "Wave_Clear", SfxEmitterTrigger.OnEnable, null, null),
            ("VFX_Torch", "Env_Torch_Loop", SfxEmitterTrigger.OnEnable, null, null),
        };

        /// <summary>단계가 코드 상수인 연출 → 지연 목록. (VfxCoreDestruction: 충전 0.2초 → 폭발 → 파편 비행 0.9초 / VfxCobraDestruction: 0.08초 → 낙하 0.75~1.1초)</summary>
        static readonly (string prefab, (float delay, string cue)[] entries)[] TimelineTable =
        {
            ("VFX_Core_Destruction", new[] { (0f, "Core_Destroy_Charge"), (0.2f, "Core_Destroy"), (1.1f, "Core_Destroy_Debris") }),
            ("VFX_Cobra_Destruction", new[] { (0.08f, "Facility_Cobra_Destroy"), (0.9f, "Facility_Cobra_Destroy_Debris") }),
        };

        /// <summary>배치 진입점: 믹서 → 큐(카탈로그·Generated·AudioResource) → 배선 → 검증.</summary>
        public static void All()
        {
            AudioMixerSetup.EnsureMixer();
            SfxTestAssetBuilder.Build();
            SfxCueBuilder.Build();
            WireAll();
            Verify();
        }

        /// <summary>2026-09-18 오후 추가분 적용: 새 큐 생성 → 교체 표 → 배선 → 검증.</summary>
        public static void Update0918b()
        {
            SfxCueBuilder.Build();
            AudioResourceMap.ApplyOverrides0918b();
            WireAll();
            Verify();
        }

        [MenuItem("SandGuard/Audio/Setup Everything (Mixer + Cues + Wire)")]
        public static void SetupMenu() => All();

        [MenuItem("SandGuard/Audio/Wire Audio Into Prefabs And Scenes")]
        public static void WireAll()
        {
            WirePlayerPrefab();
            WireEnemyPrefabs();
            WireEnemyCombatVisuals();
            WireCorePrefab();
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
                var animator = root.GetComponentInChildren<Animator>(true);

                Transform staffTip = null;
                foreach (var v in root.GetComponentsInChildren<VfxOneShot>(true))
                    if (v.Prefab != null && v.Prefab.name.Contains("Staff_Cast") && v.Anchor != null) staffTip = v.Anchor;

                Listen(visuals?.onFired, OneShot(holder, "Player_ManaBolt_Fire", staffTip));
                Listen(visuals?.onJumped, OneShot(holder, "Player_Jump"));
                Listen(visuals?.onAirJumped, OneShot(holder, "Player_AirJump"));
                Listen(progression?.onLevelUp, OneShot(holder, "Player_LevelUp"));
                Listen(progression?.onLevelUp, OneShot(holder, "Player_LevelUp_Fanfare")); // 차임(고역)과 팡파레(저역)를 겹친다
                Listen(visuals?.onDashStarted, OneShot(holder, "Player_Dash"));
                Listen(visuals?.onLanded, OneShot(holder, "Player_Land"));
                Listen(visuals?.onHardLanded, OneShot(holder, "Player_HardLand"));
                Listen(respawner?.onRespawned, OneShot(holder, "Player_Revive"));

                // 마나 회복(코어·오벨리스크 근처 틱)은 UnityEvent가 없어 지갑을 직접 듣는다
                var manaGain = holder.GetComponent<SfxManaGain>() ?? holder.AddComponent<SfxManaGain>();
                if (manaGain.Cue == null) manaGain.Cue = SfxCueBuilder.Load("Player_ManaCharge_Complete");

                // 음성(기합·신음)은 같은 이벤트에 한 번 더 꽂는다. 확률은 큐의 Chance가 정한다
                var caster = root.GetComponentInChildren<PlayerSkillCaster>(true);
                var castVoice = OneShot(holder, "Player_Voice_Cast");
                Listen(visuals?.onFired, castVoice);
                Listen(caster?.onCast, castVoice);
                var jumpVoice = OneShot(holder, "Player_Voice_Jump");
                Listen(visuals?.onJumped, jumpVoice);
                Listen(visuals?.onAirJumped, jumpVoice);
                Listen(visuals?.onDashStarted, OneShot(holder, "Player_Voice_Dash"));
                Listen(visuals?.onHardLanded, OneShot(holder, "Player_Voice_HardLand"));

                HitReaction(root, "Player_Hit", "Player_Death", "Player_Voice_Hit", "Player_Voice_Death");
                Footsteps(root, "Player_Footstep_Sand", animator, true);

                if (updraft != null)
                {
                    var toggle = holder.GetComponent<SfxLoopToggle>() ?? holder.AddComponent<SfxLoopToggle>();
                    if (toggle.LoopCue == null) toggle.LoopCue = SfxCueBuilder.Load("Player_Updraft_Charge_Loop");
                    if (!HasListener(updraft.onChargeStarted, toggle)) UnityEventTools.AddPersistentListener(updraft.onChargeStarted, new UnityAction(toggle.Begin));
                    if (!HasListener(updraft.onCharging, toggle)) UnityEventTools.AddPersistentListener(updraft.onCharging, new UnityAction<float>(toggle.SetIntensity));
                    if (!HasListener(updraft.onChargeCancelled, toggle)) UnityEventTools.AddPersistentListener(updraft.onChargeCancelled, new UnityAction(toggle.End));
                    if (!HasListener(updraft.onLaunched, toggle)) UnityEventTools.AddPersistentListener(updraft.onLaunched, new UnityAction(toggle.End));
                }

                if (animator != null)
                {
                    // 예전 배선이 남긴 깨진 스크립트(파일명≠클래스명이던 SfxAnimatorState)를 지워야 프리팹이 저장된다
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(animator.gameObject);
                    // 클립 이벤트 수신(사망 쓰러짐)과 Falling 상태 낙하 바람은 Animator와 같은 오브젝트에
                    var events = animator.GetComponent<SfxAnimationEvents>() ?? animator.gameObject.AddComponent<SfxAnimationEvents>();
                    if (events.BodyFallCue == null) events.BodyFallCue = SfxCueBuilder.Load("Player_BodyFall");
                    var fall = animator.GetComponent<SfxAnimatorState>() ?? animator.gameObject.AddComponent<SfxAnimatorState>();
                    fall.StateName = "Falling";
                    if (fall.LoopCue == null) fall.LoopCue = SfxCueBuilder.Load("Player_Fall_Loop");
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[Audio] Player.prefab 배선 완료");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // 사망 클립: 몸이 바닥에 닿는 시각을 샘플링해 BodyFall 이벤트를 심는다
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var deathClip = ClipEventBuilder.LoadClip(PlayerDeathFbx);
            var fallAt = ClipEventBuilder.DetectBodyFallSeconds(playerPrefab, deathClip);
            if (fallAt.HasValue) { ClipEventBuilder.SetEvents(PlayerDeathFbx, (fallAt.Value, "BodyFall")); Debug.Log($"[Audio] Player Death BodyFall @ {fallAt.Value:0.00}s / {deathClip.length:0.00}s"); }
            else Debug.LogWarning("[Audio] Player Death 클립에서 쓰러지는 시각을 찾지 못했다");
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
                // 휘두름은 이제 클립 이벤트가 맡는다. 공격 시작(onAttack)에 걸려 있던 원샷은 떼어 낸다
                foreach (var shot in holder.GetComponents<SfxOneShot>())
                    if (shot.Cue != null && shot.Cue.name.Contains("_Swing")) { if (visuals != null) RemoveListeners(visuals.onAttack, shot); UnityEngine.Object.DestroyImmediate(shot); }
                Listen(visuals?.onDied, OneShot(holder, "Enemy_Voice_Death_Light"));
                HitReaction(root, "Enemy_Hit", "Enemy_Death");
                Footsteps(root, "Enemy_Footstep_Light", null, false);
                var taunt = root.GetComponent<SfxEnemyTaunt>() ?? root.AddComponent<SfxEnemyTaunt>();
                if (taunt.Cue == null) taunt.Cue = SfxCueBuilder.Load("Enemy_Voice_Taunt");
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
                        if (shot.Cue != null && shot.Cue.name.Contains("Voice_Death")) changed |= Set(shot, v.deathVoice);
                    var steps = vroot.GetComponentInChildren<SfxFootsteps>(true);
                    if (steps != null)
                    {
                        var cue = SfxCueBuilder.Load(v.footstep);
                        if (cue != null && steps.Cue != cue) { steps.Cue = cue; changed = true; }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(vroot, v.path);
                    variants++;
                }
                finally { PrefabUtility.UnloadPrefabContents(vroot); }
            }
            Debug.Log($"[Audio] Enemy.prefab + 변형 {variants}개 배선 완료");
        }

        /// <summary>
        /// 적 외형 프리팹(Animator가 있는 오브젝트)에 SfxAnimationEvents를 두고, Attack/Death 클립에 이벤트를 심는다.
        /// 타격 시각은 변형 프리팹의 EnemyMeleeAttack.windup, 쓰러짐 시각은 클립을 샘플링해 찾는다.
        /// </summary>
        [MenuItem("SandGuard/Audio/Wire Enemy Clip Events Only")]
        public static void WireEnemyCombatVisuals()
        {
            int done = 0;
            foreach (var v in EnemyVariants)
            {
                string visualPath = $"{EnemyArtDir}/{v.name}/{v.name}_CombatVisual.prefab";
                var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                if (visualPrefab == null) { Debug.LogWarning($"[Audio] 외형 프리팹이 없다: {visualPath}"); continue; }

                var vroot = PrefabUtility.LoadPrefabContents(visualPath);
                try
                {
                    var animator = vroot.GetComponentInChildren<Animator>(true);
                    if (animator == null) { Debug.LogWarning($"[Audio] Animator가 없다: {visualPath}"); continue; }
                    var events = animator.GetComponent<SfxAnimationEvents>() ?? animator.gameObject.AddComponent<SfxAnimationEvents>();
                    if (events.SwingCue == null) events.SwingCue = SfxCueBuilder.Load(v.swing);
                    if (events.ImpactCue == null && v.impact != null) events.ImpactCue = SfxCueBuilder.Load(v.impact);
                    if (events.BodyFallCue == null) events.BodyFallCue = SfxCueBuilder.Load("Enemy_BodyFall");
                    if (events.ThrowCue == null && v.name == "Chief") events.ThrowCue = SfxCueBuilder.Load("Enemy_Chief_Bomb_Throw");
                    PrefabUtility.SaveAsPrefabAsset(vroot, visualPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(vroot); }

                // 타격 시각: 변형 프리팹(오버라이드 없으면 기본 프리팹 값이 상속된다)
                float windup = 0.5f;
                var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(v.path);
                var melee = enemyPrefab != null ? enemyPrefab.GetComponentInChildren<EnemyMeleeAttack>(true) : null;
                if (melee != null) windup = melee.windup;

                string attackFbx = $"{EnemyArtDir}/{v.name}/{v.name}_Attack.fbx";
                float swingAt = Mathf.Max(0.02f, windup - SwingLeadFor(v.name));
                var attackEvents = new List<(float, string)> { (swingAt, "Swing") };
                if (v.impact != null) attackEvents.Add((windup, "Impact"));
                ClipEventBuilder.SetEvents(attackFbx, attackEvents.ToArray());

                string deathFbx = $"{EnemyArtDir}/{v.name}/{v.name}_Death.fbx";
                var deathClip = ClipEventBuilder.LoadClip(deathFbx);
                var fallAt = ClipEventBuilder.DetectBodyFallSeconds(visualPrefab, deathClip);
                if (fallAt.HasValue) ClipEventBuilder.SetEvents(deathFbx, (fallAt.Value, "BodyFall"));
                Debug.Log($"[Audio] {v.name}: Swing @ {swingAt:0.00}s, Impact @ {windup:0.00}s, BodyFall @ {(fallAt.HasValue ? fallAt.Value.ToString("0.00") : "-")}s");
                done++;
            }
            Debug.Log($"[Audio] 적 외형 {done}/{EnemyVariants.Length} 클립 이벤트 배선");
        }

        static bool Set(SfxOneShot shot, string cueName)
        {
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null || shot.Cue == cue) return false;
            shot.Cue = cue; return true;
        }

        /* ================================================================ */
        /* 코어                                                             */
        /* ================================================================ */

        static readonly string[] CorePrefabPaths = { CorePrefabPath, "Assets/2.Model/Prefabs/New Core.prefab" };

        /// <summary>
        /// 범위 원 애니메이션(Ground.anim 2초 루프)이 감길 때마다 Core_Ping. 코어 프리팹마다 "Ground" 상태를 가진 애니메이터·레이어를 찾아 건다.
        /// Core.prefab은 Circle Effect의 Core Range 컨트롤러(레이어 0), New Core.prefab은 루트 애니메이터의 "Circle" 레이어다.
        /// </summary>
        public static void WireCorePrefab()
        {
            foreach (var path in CorePrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[Audio] 코어 프리팹이 없다: {path}"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // 후보: Ground 상태를 가진 애니메이터. 같은 컨트롤러를 여러 애니메이터가 쓰면(New Core: 루트와 Circle Effect) 원을 실제로 움직이는 "Circle" 이름 쪽을 고른다
                    Animator target = null; int layer = -1;
                    var candidates = new List<(Animator a, int layer)>();
                    foreach (var a in root.GetComponentsInChildren<Animator>(true))
                    {
                        var controller = a.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                        if (controller == null) continue;
                        for (int i = 0; i < controller.layers.Length; i++)
                            foreach (var s in controller.layers[i].stateMachine.states)
                                if (s.state.name == "Ground") { candidates.Add((a, i)); goto next; }
                        next:;
                    }
                    foreach (var c in candidates) if (c.a.gameObject.name.Contains("Circle")) { target = c.a; layer = c.layer; break; }
                    if (target == null && candidates.Count > 0) { target = candidates[0].a; layer = candidates[0].layer; }
                    if (target == null) { Debug.LogWarning($"[Audio] {path}에서 Ground 상태를 가진 애니메이터를 찾지 못했다"); continue; }
                    // 같은 프리팹 안의 다른 애니메이터에 남은 예전 훅은 정리한다
                    foreach (var old in root.GetComponentsInChildren<SfxAnimatorLoop>(true)) if (old.gameObject != target.gameObject) UnityEngine.Object.DestroyImmediate(old);
                    var ping = target.GetComponent<SfxAnimatorLoop>() ?? target.gameObject.AddComponent<SfxAnimatorLoop>();
                    ping.StateName = "Ground"; ping.Layer = layer; ping.Animator = target;
                    if (ping.Cue == null) ping.Cue = SfxCueBuilder.Load("Core_Ping");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[Audio] {System.IO.Path.GetFileName(path)}: {target.gameObject.name} 애니메이터 레이어 {layer}의 Ground 루프에 핑 배선");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        /* ================================================================ */
        /* 프리팹 이미터·타임라인                                            */
        /* ================================================================ */

        /// <summary>자리를 옮긴 큐: 이 프리팹에 남은 이 큐의 이미터는 떼어 낸다. (워프음이 도착→출발로 이동)</summary>
        static readonly (string prefab, string cue)[] MovedAway =
        {
            ("VFX_Recall_Arrive", "Player_Recall_Warp"),
        };

        [MenuItem("SandGuard/Audio/Wire VFX Prefabs Only")]
        public static void WireVfxPrefabs()
        {
            foreach (var (prefab, cueName) in MovedAway) RemoveEmitterFromPrefab($"{VfxPrefabDir}/{prefab}.prefab", cueName);
            int n = 0;
            foreach (var row in VfxTable)
                if (WireEmitterOnPrefab($"{VfxPrefabDir}/{row.prefab}.prefab", row.cue, row.trigger, row.begin, row.end)) n++;
            foreach (var row in TimelineTable)
                if (WireTimelineOnPrefab($"{VfxPrefabDir}/{row.prefab}.prefab", row.entries)) n++;
            Debug.Log($"[Audio] VFX 프리팹 {n}/{VfxTable.Length + TimelineTable.Length} 배선");
        }

        public static void RemoveEmitterFromPrefab(string path, string cueName)
        {
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool removed = false;
                foreach (var e in root.GetComponentsInChildren<SfxEmitter>(true)) if (e.Cue == cue) { UnityEngine.Object.DestroyImmediate(e); removed = true; }
                if (removed) { PrefabUtility.SaveAsPrefabAsset(root, path); Debug.Log($"[Audio] {System.IO.Path.GetFileName(path)}에서 {cueName} 이미터 제거"); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>프리팹 루트 아래 자식 "Sfx"에 SfxEmitter를 둔다. 같은 큐의 이미터가 있으면 트리거·시작·끝 큐만 보완한다.</summary>
        public static bool WireEmitterOnPrefab(string path, string cueName, SfxEmitterTrigger trigger = SfxEmitterTrigger.OnEnable, string beginCue = null, string endCue = null)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[Audio] 프리팹이 없다: {path}"); return false; }
            var cue = SfxCueBuilder.Load(cueName);
            if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); return false; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SfxEmitter emitter = null;
                foreach (var e in root.GetComponentsInChildren<SfxEmitter>(true)) if (e.Cue == cue) { emitter = e; break; }
                if (emitter == null)
                {
                    var holder = Child(root, "Sfx");
                    emitter = holder.GetComponent<SfxEmitter>();
                    if (emitter != null && emitter.Cue != null) emitter = null;
                    if (emitter == null) emitter = holder.AddComponent<SfxEmitter>();
                    emitter.Cue = cue;
                }
                emitter.Trigger = trigger;
                if (emitter.BeginCue == null && beginCue != null) emitter.BeginCue = SfxCueBuilder.Load(beginCue);
                if (emitter.EndCue == null && endCue != null) emitter.EndCue = SfxCueBuilder.Load(endCue);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>단계 지연 목록. 같은 큐를 쓰던 단일 이미터는 떼어 낸다. Entries가 비어 있을 때만 채운다.</summary>
        public static bool WireTimelineOnPrefab(string path, (float delay, string cue)[] entries)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[Audio] 프리팹이 없다: {path}"); return false; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var holder = Child(root, "Sfx");
                var cueSet = new HashSet<string>(); foreach (var e in entries) cueSet.Add(e.cue);
                foreach (var em in holder.GetComponents<SfxEmitter>()) if (em.Cue != null && cueSet.Contains(em.Cue.name)) UnityEngine.Object.DestroyImmediate(em);
                var tl = holder.GetComponent<SfxTimeline>() ?? holder.AddComponent<SfxTimeline>();
                if (tl.Entries.Count == 0)
                    foreach (var e in entries) tl.Entries.Add(new SfxTimeline.Entry { Delay = e.delay, Cue = SfxCueBuilder.Load(e.cue) });
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
            if (!menuMode) changed |= Fill(ref m.WaveStart, "Wave_Start");
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

        static void RemoveListeners(UnityEventBase evt, UnityEngine.Object target)
        {
            if (evt == null) return;
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                if (evt.GetPersistentTarget(i) == target) UnityEventTools.RemovePersistentListener(evt, i);
        }

        static void HitReaction(GameObject root, string hitCue, string deathCue, string hitVoice = null, string deathVoice = null)
        {
            var events = root.GetComponentInChildren<IDamageEvents>(true) as Component;
            var host = events != null ? events.gameObject : root;
            var r = host.GetComponent<SfxHitReaction>() ?? host.AddComponent<SfxHitReaction>();
            if (r.HitCue == null) r.HitCue = SfxCueBuilder.Load(hitCue);
            if (r.DeathCue == null) r.DeathCue = SfxCueBuilder.Load(deathCue);
            if (r.HitVoiceCue == null && hitVoice != null) r.HitVoiceCue = SfxCueBuilder.Load(hitVoice);
            if (r.DeathVoiceCue == null && deathVoice != null) r.DeathVoiceCue = SfxCueBuilder.Load(deathVoice);
        }

        /// <summary>발 뼈 모드. Animator가 프리팹 안에 있으면 참조를 넣고, 런타임에 생기면(적) 비워 두어 스스로 찾게 한다.</summary>
        static void Footsteps(GameObject root, string cueName, Animator animator, bool requireGround)
        {
            var f = root.GetComponent<SfxFootsteps>();
            if (f == null) { f = root.AddComponent<SfxFootsteps>(); f.RequireGround = requireGround; }
            f.Mode = FootstepMode.FootBones;
            if (animator != null && f.Animator == null) f.Animator = animator;
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
            int playerShots = 0, vfx = 0, levelEmitters = 0, buttons = 0, cues = 0, cuesWithClips = 0, visualsWithEvents = 0, clipEvents = 0, corePing = 0;
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player != null) foreach (var s in player.GetComponentsInChildren<SfxOneShot>(true)) if (s.Cue != null) playerShots++;
            foreach (var row in VfxTable)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxPrefabDir}/{row.prefab}.prefab");
                if (p != null && p.GetComponentInChildren<SfxEmitter>(true) != null) vfx++;
            }
            foreach (var row in TimelineTable)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxPrefabDir}/{row.prefab}.prefab");
                if (p != null && p.GetComponentInChildren<SfxTimeline>(true) != null) vfx++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:SfxCue", new[] { SfxCueBuilder.CueRoot }))
            {
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(AssetDatabase.GUIDToAssetPath(guid));
                cues++; if (cue.HasClips) cuesWithClips++;
            }
            foreach (var v in EnemyVariants)
            {
                var vis = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyArtDir}/{v.name}/{v.name}_CombatVisual.prefab");
                if (vis != null && vis.GetComponentInChildren<SfxAnimationEvents>(true) != null) visualsWithEvents++;
                foreach (var fbx in new[] { $"{EnemyArtDir}/{v.name}/{v.name}_Attack.fbx", $"{EnemyArtDir}/{v.name}/{v.name}_Death.fbx" })
                {
                    var clip = ClipEventBuilder.LoadClip(fbx);
                    if (clip != null) foreach (var e in clip.events) if (Array.IndexOf(ClipEventBuilder.OurFunctions, e.functionName) >= 0) clipEvents++;
                }
            }
            var pd = ClipEventBuilder.LoadClip(PlayerDeathFbx);
            if (pd != null) foreach (var e in pd.events) if (e.functionName == "BodyFall") clipEvents++;
            foreach (var cp in CorePrefabPaths)
            {
                var core = AssetDatabase.LoadAssetAtPath<GameObject>(cp);
                if (core != null && core.GetComponentInChildren<SfxAnimatorLoop>(true) != null) corePing++;
            }
            if (System.IO.File.Exists(LevelScenePath))
            {
                EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
                foreach (var e in UnityEngine.Object.FindObjectsByType<SfxEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (e.Cue != null) levelEmitters++;
            }
            if (System.IO.File.Exists(MainScenePath))
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
                foreach (var b in UnityEngine.Object.FindObjectsByType<SfxUiButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (b.ClickCue != null) buttons++;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"AUDIO_WIRED cues={cues} cuesWithClips={cuesWithClips} playerOneShots={playerShots} vfxPrefabs={vfx}/{VfxTable.Length + TimelineTable.Length} enemyVisualsWithEvents={visualsWithEvents}/{EnemyVariants.Length} clipEvents={clipEvents} corePing={corePing} levelEmitters={levelEmitters} mainMenuButtons={buttons} mixer={(AssetDatabase.LoadAssetAtPath<AudioMixer>(SfxCueBuilder.MixerPath) != null)}");
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
