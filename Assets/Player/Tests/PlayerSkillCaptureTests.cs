#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SandGuard.Player.Effects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>
    /// 실제 Level.unity에서 스킬을 하나씩 쓰며 플레이어 카메라(와 폭풍은 상공 카메라)로 스크린샷을 찍는다. 눈으로 확인하는 용도라 단정은 최소다.
    /// 출력: Logs/skill-captures/*.png. 그래픽 장치가 필요하므로 -nographics 없이 -batchmode로 돌린다(Level은 -nographics에서 죽는다).
    /// 스킬트리 게이트는 풀고(SkillTreeAllowed 등 null) 효과를 직접 붙여 해금한다. 적은 Enemy.prefab을 플레이어 앞 NavMesh에 놓는다.
    /// </summary>
    [Category("Capture")]
    public sealed class PlayerSkillCaptureTests
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";
        const string EnemyPrefabPath = "Assets/Enemy/Generated/Enemy.prefab";
        static readonly string OutDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "skill-captures");
        readonly List<GameObject> spawned = new List<GameObject>();
        GameObject player; PlayerSkillCaster caster; PlayerBasicAttack attack; PlayerEffects effects; PlayerPierceCharge pierce; PlayerRecall recall; PlayerMotor motor; IManaWallet mana;
        PlayerHealth health; PlayerInputReader input;
        Camera overview, playerCam, side;
        readonly List<string> problems = new List<string>();

        /// <summary>실패해도 끝까지 찍는다. 문제는 모아서 마지막에 보고한다.</summary>
        void Check(bool ok, string what) { if (!ok) { problems.Add(what); Debug.LogWarning("[Capture] FAIL " + what + " | " + State()); } }
        string State() => "pos=" + player.transform.position + " grounded=" + motor.IsGrounded + " hp=" + (health != null ? health.CurrentHealth : -1f)
            + " canFire=" + attack.CanFire + " combat=" + attack.CombatEnabled + " accepts=" + input.AcceptsInput + " gameplay=" + input.GameplayEnabled
            + " timeScale=" + Time.timeScale + " firePoint=" + (attack.visuals != null && attack.visuals.FirePoint != null) + " mana=" + mana.CurrentMana;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            foreach (var go in spawned) if (go != null) Object.Destroy(go);
            spawned.Clear();
            var empty = SceneManager.CreateScene("Empty " + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        IEnumerator Load()
        {
            // -nographics(그래픽 장치 없음)에서는 Level 재생이 죽고 캡처도 의미가 없다. 일반 배치 테스트 묶음에서는 건너뛴다.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("그래픽 장치가 없어 캡처를 건너뛴다(-nographics). -batchmode만으로 실행하세요.");
            Directory.CreateDirectory(OutDir);
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = true; // 눈으로 보는 캡처다. 웨이브·오디오 등 다른 시스템의 오류 로그로 실패하지 않는다
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            float deadline = Time.realtimeSinceStartup + 20f;
            PlayerInputReader input = null;
            while (input == null && Time.realtimeSinceStartup < deadline) { input = Object.FindFirstObjectByType<PlayerInputReader>(); yield return null; }
            Assert.NotNull(input, "Level에 플레이어가 없다.");
            player = input.gameObject; input.captureCursor = false; this.input = input;
            caster = player.GetComponent<PlayerSkillCaster>(); attack = player.GetComponent<PlayerBasicAttack>(); effects = player.GetComponent<PlayerEffects>();
            pierce = player.GetComponent<PlayerPierceCharge>(); recall = player.GetComponent<PlayerRecall>(); motor = player.GetComponent<PlayerMotor>(); mana = player.GetComponent<IManaWallet>();
            health = player.GetComponent<PlayerHealth>();
            playerCam = player.GetComponentInChildren<Camera>(true);
            Debug.Log("[Capture] playerCam=" + (playerCam != null ? playerCam.name : "(none)") + " Camera.main=" + (Camera.main != null ? Camera.main.name + "@" + Camera.main.transform.position : "(none)"));
            Assert.NotNull(caster); Assert.NotNull(attack); Assert.NotNull(effects); Assert.NotNull(pierce, "Player.prefab에 PlayerPierceCharge가 배선돼야 한다."); Assert.NotNull(recall, "Player.prefab에 PlayerRecall이 배선돼야 한다.");
            yield return new WaitForSecondsRealtime(2.5f); // 리스폰 지점 이동·HUD·웨이브 준비가 자리 잡을 시간
            deadline = Time.realtimeSinceStartup + 15f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log("[Capture] after settle: " + State());
            // 스킬트리 게이트를 풀고 효과로 해금한다. Level의 스킬트리 실행기는 미장착 스킬의 충전·흔적을 매 프레임 취소하므로 꺼 둔다.
            foreach (var behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name == "PlayerSkillTreeExecutor") behaviour.enabled = false;
            // 웨이브는 돌리지 않는다(준비 시간이 끝나면 적을 쏟아내 캡처를 가린다).
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour != null && behaviour.GetType().Name == "WaveDirector") behaviour.enabled = false;
            yield return null;
            caster.SkillTreeAllowed = null; caster.SkillTreeInput = null; attack.SkillTreePierceAllowed = null; pierce.Allowed = null; pierce.input = null;
            effects.Apply(new SandBurstEffect()); effects.Apply(new SandVortexEffect()); effects.Apply(new SandStormEffect());
            mana.Gain(100000);
            attack.CombatEnabled = true;
            // 상공 카메라: 코어(없으면 플레이어) 위 비스듬히.
            var origin = caster.Core != null ? caster.Core.position : player.transform.position;
            overview = new GameObject("Capture Overview").AddComponent<Camera>();
            spawned.Add(overview.gameObject);
            overview.enabled = false; overview.fieldOfView = 60f; overview.nearClipPlane = 0.5f; overview.farClipPlane = 1500f;
            overview.transform.position = origin + new Vector3(-90f, 110f, -150f);
            overview.transform.LookAt(origin - Vector3.up * 20f);
            // 시작 지점(코어 바로 옆 꼭대기)은 카메라가 코어 크리스탈 안에 들어가 화면이 온통 청록이다. 평평한 마당 바닥(계단 아님)을 찾아 내려가 코어를 바라본다.
            Vector3 ground = origin + new Vector3(0f, 0f, -26f);
            bool foundFlat = false;
            float searchDeadline = Time.realtimeSinceStartup + 10f; // NavMesh가 늦게 준비될 수 있어 찾을 때까지 잠시 반복한다
            while (!foundFlat && Time.realtimeSinceStartup < searchDeadline)
            {
            foreach (var dir in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
            {
                bool found = false;
                for (float d = 30f; d <= 70f && !found; d += 6f)
                {
                    Vector3 probe = origin + dir * d;
                    if (!Physics.Raycast(probe + Vector3.up * 120f, Vector3.down, out var probeHit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (probeHit.normal.y < 0.97f) continue; // 계단·경사 제외
                    if (!NavMesh.SamplePosition(probeHit.point, out var probeNav, 1.5f, NavMesh.AllAreas)) continue;
                    // 앞쪽 6m도 같은 높이의 평지여야 적을 세울 수 있다.
                    Vector3 ahead = probeNav.position + (origin - probeNav.position).normalized * 6f; ahead.y = probeNav.position.y + 1f;
                    if (!Physics.Raycast(ahead + Vector3.up * 5f, Vector3.down, out var aheadHit, 20f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(aheadHit.point.y - probeNav.position.y) > 0.6f) continue;
                    ground = probeNav.position; found = true; foundFlat = true;
                }
                if (found) break;
            }
            if (!foundFlat) yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.Log("[Capture] flat spot " + (foundFlat ? "found" : "NOT found, using fallback") + " at " + ground);
            motor.Teleport(ground + Vector3.up * 0.1f);
            Vector3 toCore = origin - ground; toCore.y = 0f;
            if (toCore.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(toCore.normalized);
            var rig = player.GetComponentInChildren<PlayerCameraRig>(true);
            if (rig != null && toCore.sqrMagnitude > 0.01f) rig.SetYaw(Quaternion.LookRotation(toCore.normalized).eulerAngles.y);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[Capture] moved to courtyard: " + State() + " core=" + origin);
            SpawnEnemies(player.transform.position + player.transform.forward * 6f);
            // 옆 카메라: 1인칭 구도에서 벗어난 지역 스킬·적 반응을 본다.
            side = new GameObject("Capture Side").AddComponent<Camera>();
            spawned.Add(side.gameObject);
            side.enabled = false; side.fieldOfView = 55f; side.nearClipPlane = 0.3f; side.farClipPlane = 1500f;
            side.transform.position = player.transform.position + player.transform.right * 9f + player.transform.forward * 3f + Vector3.up * 4f;
            side.transform.LookAt(player.transform.position + player.transform.forward * 4f + Vector3.up * 1f);
            caster.maxCastDistance = 9f; // 지역 스킬이 화면 안(적 근처)에 떨어지게
            yield return null;
        }

        void SpawnEnemies(Vector3 around)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (prefab == null) { Debug.LogWarning("Enemy.prefab이 없어 적 없이 찍는다."); return; }
            Vector3[] offsets = { new Vector3(0, 0, 0), new Vector3(2.2f, 0, 1f), new Vector3(-2f, 0, 1.5f), new Vector3(0.8f, 0, 3f), new Vector3(-1.2f, 0, -1.5f) };
            foreach (var offset in offsets)
            {
                if (!NavMesh.SamplePosition(around + offset, out var hit, 4f, NavMesh.AllAreas)) continue;
                var enemy = Object.Instantiate(prefab, hit.position, Quaternion.LookRotation(-player.transform.forward));
                spawned.Add(enemy);
                // 표적 역할만: 두뇌·공격·표적 선택을 꺼서 플레이어를 때리지 않게 한다(밀림·띄움·둔화는 모터·낙하·상태이상이 받는다).
                foreach (var behaviour in enemy.GetComponentsInChildren<MonoBehaviour>())
                {
                    string type = behaviour != null ? behaviour.GetType().Name : "";
                    if (type == "EnemyBrain" || type == "EnemyMeleeAttack" || type == "EnemyTargetSelector") behaviour.enabled = false;
                }
            }
        }

        void Shoot(string name, Camera cam = null)
        {
            if (cam == null) cam = playerCam != null ? playerCam : Camera.main;
            if (cam == null) { Debug.LogWarning("카메라가 없어 " + name + "을 건너뛴다."); return; }
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create();
            var prevTarget = cam.targetTexture; bool wasEnabled = cam.enabled;
            cam.targetTexture = rt; cam.enabled = true;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = prev; cam.targetTexture = prevTarget; cam.enabled = wasEnabled;
            rt.Release(); Object.Destroy(rt); Object.Destroy(tex);
            Debug.Log("[Capture] " + name);
        }

        IEnumerator Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }

        [UnityTest, Timeout(600000)] public IEnumerator CaptureEverySkillInTheLevel()
        {
            yield return Load();
            Shoot("00_start");
            Shoot("01_start_overview", overview);
            Debug.Log("[Capture] start: " + State());
            // 화면이 이상할 때를 위한 진단: 카메라 설정, 카메라 주변·정면의 콜라이더.
            if (playerCam != null)
            {
                var comps = new List<string>(); foreach (var c in playerCam.GetComponents<Component>()) comps.Add(c.GetType().Name);
                Debug.Log("[Capture] cam clear=" + playerCam.clearFlags + " near=" + playerCam.nearClipPlane + " far=" + playerCam.farClipPlane + " mask=" + playerCam.cullingMask + " fov=" + playerCam.fieldOfView + " comps=" + string.Join(",", comps) + " fog=" + RenderSettings.fog + " " + RenderSettings.fogColor);
                var near = new List<string>(); foreach (var c in Physics.OverlapSphere(playerCam.transform.position, 1.5f)) near.Add(c.name + "@" + c.transform.root.name);
                Debug.Log("[Capture] around cam: " + string.Join(", ", near));
                if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward, out var hit, 200f)) Debug.Log("[Capture] cam forward hit: " + hit.collider.name + "@" + hit.collider.transform.root.name + " d=" + hit.distance);
                var renderers = new List<string>(); foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.isVisible && r.bounds.Contains(playerCam.transform.position)) renderers.Add(r.name + "(" + (r.sharedMaterial != null ? r.sharedMaterial.name : "-") + ")");
                Debug.Log("[Capture] renderers containing cam: " + string.Join(", ", renderers));
            }

            // 관통탄: 탭 → 충전 → 만충 발사
            mana.Gain(1000);
            var tap = pierce.Fire(0f); Check(tap.Succeeded, "관통탄 탭 " + tap.Failure);
            yield return Wait(0.05f); Shoot("10_pierce_tap_a");
            yield return Wait(0.12f); Shoot("11_pierce_tap_b");
            yield return Wait(2.2f); pierce.ResetCooldown(); mana.Gain(1000);
            Check(pierce.BeginHold(0), "관통탄 홀드 시작");
            for (int i = 0; i < 7; i++) { yield return Wait(0.2f); Debug.Log("[Capture] charge t=" + (0.2f * (i + 1)) + " holding=" + pierce.IsHolding + " charging=" + pierce.IsCharging + " charge=" + pierce.Charge + " | " + State()); if (i == 2) Shoot("12_pierce_charging_half"); }
            Shoot("13_pierce_charging_full");
            Check(pierce.IsCharging && pierce.Charge >= 0.99f, "만충 (charging=" + pierce.IsCharging + " charge=" + pierce.Charge + ")");
            var charged = pierce.IsHolding ? pierce.EndHold(0) : pierce.Fire(1f); Check(charged.Succeeded, "만충 발사 " + charged.Failure);
            yield return Wait(0.05f); Shoot("14_pierce_charged_a"); Shoot("16_pierce_charged_side", side);
            yield return Wait(0.15f); Shoot("15_pierce_charged_b");
            yield return Wait(0.5f); Shoot("17_pierce_hold"); Shoot("18_pierce_hold_side", side);
            Debug.Log("[Capture] charged beam len=" + attack.LastBeam.Length + " enemiesHit=" + attack.LastBeam.EnemiesHit + " landed=" + attack.LastBeam.Landed);
            yield return Wait(0.8f);

            // 모래 폭발
            bool burst = false; Vector3 burstAt = Vector3.zero; attack.Burst += p => { burst = true; burstAt = p; };
            mana.Gain(1000);
            var burstCast = caster.TryCast(PlayerSkillCaster.Burst); Check(burstCast.Succeeded, "모래 폭발 " + burstCast.Failure);
            float deadline = Time.time + 2f; while (!burst && Time.time < deadline) yield return null;
            Check(burst, "폭발이 터졌다");
            Debug.Log("[Capture] burst at " + burstAt + " launched=" + attack.LastLaunchCount + " player=" + player.transform.position);
            Shoot("20_burst_a"); Shoot("23_burst_side_a", side); yield return Wait(0.25f); Shoot("21_burst_b"); Shoot("24_burst_side_b", side); yield return Wait(0.5f); Shoot("22_burst_c");
            yield return Wait(1.2f);

            // 모래 소용돌이
            mana.Gain(1000);
            var vortexCast = caster.TryCast(PlayerSkillCaster.Vortex); Check(vortexCast.Succeeded, "모래 소용돌이 " + vortexCast.Failure);
            Debug.Log("[Capture] vortex at " + caster.LastCastPoint + " player=" + player.transform.position);
            yield return Wait(0.5f); Shoot("30_vortex_a"); Shoot("34_vortex_side_a", side); yield return Wait(1f); Shoot("31_vortex_b"); Shoot("35_vortex_side_b", side);
            yield return Wait(0.95f); Shoot("32_vortex_end"); yield return Wait(0.3f); Shoot("33_vortex_launch"); Shoot("36_vortex_side_launch", side);
            if (caster.LastVortex != null) Debug.Log("[Capture] vortex pulled=" + caster.LastVortex.PulledCount + " launched=" + caster.LastVortex.LaunchedCount);
            yield return Wait(1.2f);

            // 흔적 귀환
            mana.Gain(1000);
            var mark = recall.TryMark(); Check(mark.Succeeded, "흔적 생성 " + mark.Failure);
            yield return Wait(0.4f); Shoot("40_recall_mark"); Shoot("44_recall_mark_side", side);
            motor.Teleport(player.transform.position - player.transform.forward * 4f + Vector3.right * 2f);
            yield return Wait(0.6f); Shoot("41_recall_mark_far");
            var warp = recall.TryRecall(); Check(warp.Succeeded, "흔적 귀환 " + warp.Failure);
            yield return Wait(0.05f); Shoot("42_recall_warp_a"); Shoot("45_recall_warp_side", side); yield return Wait(0.35f); Shoot("43_recall_warp_b");
            yield return Wait(1.5f);

            // 사막 폭풍: 플레이어 시점 + 상공
            mana.Gain(1000);
            var stormCast = caster.TryCast(PlayerSkillCaster.Storm); Check(stormCast.Succeeded, "사막 폭풍 " + stormCast.Failure);
            var storm = caster.LastStorm;
            if (storm == null) { Assert.Fail("폭풍 시전 실패: " + string.Join(" / ", problems)); yield break; }
            Debug.Log("[Capture] storm origin=" + storm.transform.position + " core=" + (caster.Core != null ? caster.Core.name : "(none)") + " player=" + player.transform.position);
            yield return Wait(0.3f); Shoot("50_storm_player_a"); Shoot("55_storm_over_a", overview);
            yield return Wait(1.2f); Shoot("51_storm_player_b"); Shoot("56_storm_over_b", overview);
            yield return Wait(1.5f); Shoot("52_storm_player_c"); Shoot("57_storm_over_c", overview);
            yield return Wait(2f); Shoot("53_storm_player_d"); Shoot("58_storm_over_d", overview);
            yield return Wait(2f); Shoot("54_storm_player_e"); Shoot("59_storm_over_e", overview); Shoot("60_storm_side_e", side);
            Debug.Log("[Capture] storm front=" + storm.Front + " struck=" + storm.TotalStruck + " hits=" + storm.TotalHits);
            Check(storm.Front > 20f, "링이 퍼졌다 (front=" + storm.Front + ")");
            Assert.IsEmpty(problems, "캡처 중 문제: " + string.Join(" / ", problems));
        }
    }
}
#endif
