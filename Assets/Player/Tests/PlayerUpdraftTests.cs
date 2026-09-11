#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SandGuard.Player.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>③ 상승 기류: Space 홀드 충전 → 놓으면 발사. 해금 여부, 충전 중 붙들림, 높이, 취소, 애니메이터 상태.</summary>
    public sealed class PlayerUpdraftTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
        InputSettings originalSettings, testSettings;
        GameObject player; PlayerMotor motor; PlayerUpdraft updraft; PlayerEffects effects; Animator animator;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static AnimatorStateInfo State(Animator a) => a.GetCurrentAnimatorStateInfo(0);
        IEnumerator Until(System.Func<bool> condition, float timeout, string message)
        {
            for (float t = 0f; t < timeout && !condition(); t += Time.deltaTime) yield return null;
            Assert.True(condition(), message);
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); Time.timeScale = 1f;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(60, 1, 60);
            Physics.SyncTransforms();
            player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            motor = player.GetComponent<PlayerMotor>(); updraft = player.GetComponent<PlayerUpdraft>(); effects = player.GetComponent<PlayerEffects>();
            Assert.NotNull(updraft, "Player.prefab carries a PlayerUpdraft.");
            yield return new WaitForSeconds(0.4f);
            animator = player.GetComponentInChildren<Animator>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        /// <summary>지상에서 Space를 누른 채 유지: 점프 없이 곧장 충전이 시작될 때까지 기다린다.</summary>
        IEnumerator HoldSpaceUntilCharging()
        {
            Keys(Key.Space);
            yield return Until(() => updraft.IsCharging, updraft.holdToCharge + 0.3f, "Holding on the ground starts the charge directly.");
            Assert.True(motor.IsGrounded, "No hop before charging.");
        }

        [UnityTest] public IEnumerator TapJumpsOnReleaseAtFullHeightAndAirJumpsStayImmediate()
        {
            effects.Apply(new UpdraftEffect()); effects.Apply(new DoubleJumpEffect());
            int jumps = 0; motor.Jumped += () => jumps++;
            Keys(Key.Space);
            yield return new WaitForSeconds(0.06f);
            Assert.True(motor.IsGrounded, "A press is held back until it is known to be a tap.");
            Assert.AreEqual(0, jumps); Assert.False(updraft.IsCharging);
            Keys(); // 탭: 놓는 순간 점프
            yield return Until(() => !motor.IsGrounded, 0.15f, "Release performs the jump.");
            Assert.AreEqual(1, jumps); Assert.False(motor.LastJumpWasAirJump);
            float peak = 0f;
            for (float t = 0f; t < 1.2f && !(motor.IsGrounded && t > 0.2f); t += Time.deltaTime) { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
            Assert.Greater(peak, motor.JumpHeight - 0.25f, "A tap jump reaches full height even though the button is already released.");
            Assert.False(updraft.IsCharging);
            yield return Until(() => motor.IsGrounded, 1f, "Lands.");
            // 공중 점프는 누르는 즉시. 착지까지 계속 누르고 있으면 그대로 충전으로 이어진다.
            Keys(Key.Space); yield return new WaitForSeconds(0.05f); Keys();
            yield return Until(() => !motor.IsGrounded, 0.15f, "Second tap jumps.");
            yield return new WaitForSeconds(0.15f);
            Keys(Key.Space);
            yield return Until(() => jumps == 3, 0.1f, "The air jump is not deferred.");
            Assert.True(motor.LastJumpWasAirJump);
            yield return Until(() => updraft.IsCharging, 2f, "Holding through the landing charges.");
            Assert.AreEqual(3, jumps, "Landing while held does not add another jump.");
            Keys(); yield return null;
        }

        [UnityTest] public IEnumerator LockedUpdraftNeverChargesAndSpaceStillJumps()
        {
            Assert.False(updraft.unlocked);
            int launches = 0; updraft.Launched += (_, __) => launches++;
            Keys(Key.Space);
            yield return Until(() => !motor.IsGrounded, 0.3f, "Normal jump.");
            yield return Until(() => motor.IsGrounded, 1.5f, "Lands.");
            yield return new WaitForSeconds(0.6f);
            Assert.False(updraft.IsCharging, "Locked: holding Space does nothing.");
            Assert.False(motor.Anchored);
            Keys(); yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(0, launches);
            Assert.True(State(animator).IsName("Locomotion"));
        }

        [UnityTest] public IEnumerator HoldChargesCrouchInPlaceAndReleaseLaunchesByCharge()
        {
            effects.Apply(new UpdraftEffect());
            Assert.True(updraft.unlocked);
            int started = 0, launches = 0; float launchedCharge = -1f, launchedHeight = -1f;
            updraft.ChargeStarted += () => started++;
            updraft.Launched += (c, h) => { launches++; launchedCharge = c; launchedHeight = h; };
            yield return HoldSpaceUntilCharging();
            Assert.AreEqual(1, started);
            Assert.True(motor.Anchored, "Charging anchors the motor.");
            Vector3 anchoredAt = player.transform.position;
            Keys(Key.Space, Key.W); // 충전 중 이동 입력은 무시된다
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Distance(anchoredAt, player.transform.position), 0.05f, "No movement while charging.");
            Assert.AreEqual(ActionFailure.Locked, motor.TryDash().Failure, "No dash while charging.");
            Assert.That(updraft.Charge, Is.InRange(0.25f, 0.6f));
            Assert.True(State(animator).IsName("Charge"), "Charging shows the crouch blend.");
            Assert.That(animator.GetFloat("Charge"), Is.InRange(0.2f, 0.7f));
            yield return new WaitForSeconds(updraft.chargeTime);
            Assert.AreEqual(1f, updraft.Charge, 0.0001f, "Charge caps at 1.");
            Keys(); // 놓으면 발사
            yield return Until(() => launches == 1, 0.2f, "Release launches.");
            Assert.AreEqual(1f, launchedCharge, 0.0001f); Assert.AreEqual(updraft.maxHeight, launchedHeight, 0.0001f);
            Assert.False(updraft.IsCharging); Assert.False(motor.Anchored);
            Assert.Greater(motor.VerticalSpeed, 10f);
            yield return Until(() => State(animator).IsName("Fly"), 0.3f, "Launch plays the fly pose.");
            float peak = 0f;
            for (float t = 0f; t < 1.5f && !(motor.IsGrounded && t > 0.3f); t += Time.deltaTime) { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
            Assert.That(peak, Is.InRange(updraft.maxHeight - 0.8f, updraft.maxHeight + 0.5f), "Full charge reaches about maxHeight even with the button released.");
            yield return Until(() => motor.IsGrounded, 2f, "Comes back down.");
            yield return Until(() => State(animator).IsName("Hard Landing") || State(animator).IsName("Landing"), 0.3f, "Lands with a landing pose.");
            yield return Until(() => State(animator).IsName("Locomotion"), 2.5f, "Recovers.");
        }

        [UnityTest] public IEnumerator EarlyReleaseGivesMinHeightAndShortHoldDoesNothing()
        {
            effects.Apply(new UpdraftEffect());
            int launches = 0; float height = -1f; updraft.Launched += (_, h) => { launches++; height = h; };
            // holdToCharge 전에 놓으면 충전 없이 보통 점프만 한다
            Keys(Key.Space); yield return new WaitForSeconds(0.05f); Keys();
            yield return Until(() => !motor.IsGrounded, 0.15f, "Tap jumps.");
            yield return Until(() => motor.IsGrounded, 1.5f, "Lands.");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, launches); Assert.False(updraft.IsCharging);
            // 충전이 막 시작됐을 때 놓으면 최소 높이
            yield return HoldSpaceUntilCharging();
            Keys();
            yield return Until(() => launches == 1, 0.2f, "Launches at low charge.");
            Assert.That(height, Is.InRange(updraft.minHeight, updraft.minHeight + 0.6f));
            Assert.Greater(height, motor.JumpHeight, "Even the weakest updraft beats a normal jump.");
        }

        [UnityTest] public IEnumerator ChargeCancelsWhenTheEffectIsRemovedOrTheAbilityIsCancelled()
        {
            var effect = new UpdraftEffect();
            effects.Apply(effect);
            int cancelled = 0, launches = 0; updraft.ChargeCancelled += () => cancelled++; updraft.Launched += (_, __) => launches++;
            yield return HoldSpaceUntilCharging();
            updraft.Cancel();
            Assert.False(updraft.IsCharging); Assert.False(motor.Anchored); Assert.AreEqual(1, cancelled);
            Keys(); yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, launches, "A cancelled charge does not launch on release.");
            yield return HoldSpaceUntilCharging();
            effects.Remove(effect);
            Assert.False(updraft.unlocked); Assert.False(updraft.IsCharging); Assert.AreEqual(2, cancelled);
            Keys(); yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, launches);
            Assert.True(State(animator).IsName("Locomotion") || animator.IsInTransition(0));
        }

        [UnityTest] public IEnumerator LaunchSpawnsTheUpdraftVfxAtTheFeetInsteadOfJumpDust()
        {
            effects.Apply(new UpdraftEffect());
            Assert.NotNull(player.transform.Find("UpdraftVfx"), "Player.prefab is wired with the updraft VFX one-shot (DesertTower > VFX > Wire Combat VFX Into Demo Assets).");
            yield return HoldSpaceUntilCharging();
            Vector3 feet = player.transform.position;
            Keys();
            yield return Until(() => !updraft.IsCharging, 0.2f, "Launched.");
            yield return null; yield return null;
            var vfx = GameObject.Find("VFX_Updraft_Launch(Clone)");
            Assert.NotNull(vfx, "Launch spawns the updraft effect.");
            Assert.Less(Vector3.Distance(vfx.transform.position, feet), 0.3f, "The effect sits where the feet were at launch.");
            Assert.IsNull(vfx.transform.parent, "It stays on the ground instead of following the player up.");
            foreach (string layer in new[] { "Shockwave", "Dust", "Debris", "Column" })
                Assert.NotNull(vfx.transform.Find(layer), "Layer present: " + layer);
            Assert.Greater(vfx.GetComponentsInChildren<ParticleSystem>().Length, 4);
            Assert.IsNull(GameObject.Find("VFX_Jump_Dust(Clone)"), "A launch is not a jump: no ordinary jump dust.");
            yield return new WaitForSeconds(2.8f);
            Assert.IsNull(GameObject.Find("VFX_Updraft_Launch(Clone)"), "The one-shot cleans itself up.");
        }

        [UnityTest] public IEnumerator ChargingRunsTheWindLoopAndItIntensifiesThenStopsOnLaunchOrCancel()
        {
            effects.Apply(new UpdraftEffect());
            var charge = player.transform.Find("UpdraftCharge");
            Assert.NotNull(charge, "Player.prefab is wired with the charge wind loop.");
            var systems = charge.GetComponentsInChildren<ParticleSystem>(true);
            Assert.GreaterOrEqual(systems.Length, 3);
            yield return null;
            foreach (var ps in systems) Assert.False(ps.isEmitting, "Idle: the wind loop is silent.");
            yield return HoldSpaceUntilCharging();
            yield return Until(() => updraft.Charge >= 0.25f, 1f, "Charge builds.");
            var emitting = System.Array.FindAll(systems, ps => ps.emission.enabled && ps.emission.rateOverTime.constant > 0f);
            Assert.GreaterOrEqual(emitting.Length, 3, "Three emitting layers (swirl, pull, feet).");
            foreach (var ps in emitting) Assert.True(ps.isEmitting, "Charging turns the wind on: " + ps.name);
            float rateLow = emitting[0].emission.rateOverTimeMultiplier, speedLow = emitting[0].main.startSpeedMultiplier;
            Assert.Greater(charge.GetComponentInChildren<ParticleSystem>().particleCount + emitting[0].particleCount, 0, "Particles are alive while charging.");
            yield return Until(() => updraft.Charge >= 1f, 1.5f, "Full charge.");
            yield return null;
            Assert.Greater(emitting[0].emission.rateOverTimeMultiplier, rateLow * 1.3f, "The wind intensifies with charge.");
            Assert.Greater(emitting[0].main.startSpeedMultiplier, speedLow, "…and speeds up.");
            Keys(); // 발사
            yield return Until(() => !updraft.IsCharging, 0.2f, "Launched.");
            yield return null;
            foreach (var ps in emitting) Assert.False(ps.isEmitting, "Launch stops the wind: " + ps.name);
            yield return Until(() => motor.IsGrounded, 3f, "Lands.");
            yield return new WaitForSeconds(0.3f);
            yield return HoldSpaceUntilCharging();
            yield return null; yield return null;
            Assert.True(emitting[0].isEmitting, "A new charge restarts the wind.");
            updraft.Cancel(); Keys(); yield return null;
            foreach (var ps in emitting) Assert.False(ps.isEmitting, "Cancel stops the wind: " + ps.name);
        }

        [UnityTest] public IEnumerator LensBubbleGrowsWithChargePulsesOnLaunchAndHidesWhenIdle()
        {
            effects.Apply(new UpdraftEffect());
            var lens = player.transform.Find("UpdraftLens");
            Assert.NotNull(lens, "Player.prefab is wired with the refraction bubble.");
            var renderer = lens.GetComponent<MeshRenderer>();
            Assert.NotNull(renderer);
            Assert.AreEqual("SandGuard/VFX/ScreenRefraction", renderer.sharedMaterial.shader.name);
            Assert.False(renderer.sharedMaterial.shader.isSupported == false, "The refraction shader compiles for this pipeline.");
            var block = new MaterialPropertyBlock();
            float Strength() { renderer.GetPropertyBlock(block); return block.GetFloat("_Strength"); }
            yield return null;
            Assert.False(renderer.enabled, "Idle: the bubble is hidden.");
            yield return HoldSpaceUntilCharging();
            yield return Until(() => updraft.Charge >= 0.3f, 1f, "Charge builds.");
            Assert.True(renderer.enabled, "Charging shows the bubble.");
            float scaleLow = lens.localScale.x, strengthLow = Strength();
            Assert.Greater(scaleLow, 1f); Assert.Greater(strengthLow, 0f);
            yield return Until(() => updraft.Charge >= 1f, 1.5f, "Full charge.");
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(lens.localScale.x, scaleLow * 1.2f, "The bubble grows with charge.");
            Assert.Greater(Strength(), strengthLow * 1.5f, "…and refracts harder.");
            float scaleFull = lens.localScale.x;
            yield return CaptureView(player, "lens-charge-full");
            yield return null; yield return null; yield return null; // 캡처 직후의 긴 프레임이 펄스를 삼키지 않게 한 템포 쉰다
            Keys(); // 발사
            yield return Until(() => !updraft.IsCharging, 0.2f, "Launched.");
            yield return new WaitForSeconds(0.08f);
            Assert.True(renderer.enabled);
            Assert.Greater(lens.localScale.x, scaleFull, "Launch pulses the bubble outward.");
            yield return CaptureView(player, "lens-launch-pulse");
            yield return new WaitForSeconds(0.5f);
            Assert.False(renderer.enabled, "The pulse ends and the bubble hides.");
            Assert.AreEqual(0f, Strength(), 0.0001f);
            yield return Until(() => motor.IsGrounded, 3f, "Lands.");
            yield return new WaitForSeconds(0.3f);
            yield return HoldSpaceUntilCharging();
            yield return null; yield return null;
            Assert.True(renderer.enabled, "A new charge shows it again.");
            updraft.Cancel(); Keys();
            yield return new WaitForSeconds(0.4f);
            Assert.False(renderer.enabled, "Cancel fades it out and hides it.");
        }

        /// <summary>실제 플레이어 카메라 시점을 저장한다. -nographics 배치에서는 건너뛴다.</summary>
        IEnumerator CaptureView(GameObject target, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var camera = target.GetComponentInChildren<Camera>();
            var sun = Track(new GameObject("Capture Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2f; sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            // 굴절이 보이도록 뒤에 기둥 몇 개를 세운다.
            for (int i = 0; i < 4; i++)
            {
                var pillar = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
                pillar.transform.position = new Vector3(-3f + i * 2f, 1.5f, 5f + (i % 2) * 2f); pillar.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
            }
            yield return null;
            var rt = new RenderTexture(1600, 900, 24); camera.targetTexture = rt;
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            System.IO.Directory.CreateDirectory("Logs/player-updraft-captures");
            System.IO.File.WriteAllBytes("Logs/player-updraft-captures/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
            Object.Destroy(rt); Object.Destroy(texture);
        }

        [UnityTest] public IEnumerator ManaCostIsChargedAndBlocksLaunchWhenShort()
        {
            effects.Apply(new UpdraftEffect());
            updraft.manaCost = 30;
            IManaWallet mana = player.GetComponent<PlayerManaWallet>();
            int launches = 0; updraft.Launched += (_, __) => launches++;
            yield return HoldSpaceUntilCharging();
            Keys();
            yield return Until(() => launches == 1, 0.2f, "Launch with enough mana.");
            Assert.AreEqual(70, mana.CurrentMana);
            yield return Until(() => motor.IsGrounded, 3f, "Lands.");
            Assert.True(mana.TrySpend(50)); Assert.AreEqual(20, mana.CurrentMana);
            yield return HoldSpaceUntilCharging();
            Keys(); yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, launches, "Not enough mana: the charge is dropped instead of launching.");
            Assert.AreEqual(20, mana.CurrentMana); Assert.False(updraft.IsCharging); Assert.True(motor.IsGrounded);
        }
    }
}
#endif
