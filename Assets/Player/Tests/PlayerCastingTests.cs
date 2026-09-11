#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SandGuard.Player.Tests
{
    public sealed class PlayerCastingTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        InputSettings original, settings;
        Keyboard keyboard;
        Mouse mouse;
        GameObject player;
        PlayerBasicAttack attack;
        PlayerSpellcasting casting;
        Animator animator;
        PlayerLampEquipment lamp;
        Camera aimCamera;
        int fired;
        readonly List<float> shotTimes = new List<float>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        [UnitySetUp] public IEnumerator Setup()
        {
            original = InputSystem.settings; settings = Object.Instantiate(original);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1f; fired = 0; shotTimes.Clear();
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(100, 1, 100);
            Physics.SyncTransforms();
            player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            attack = player.GetComponent<PlayerBasicAttack>();
            aimCamera = Track(new GameObject("Aim Camera")).AddComponent<Camera>();
            aimCamera.enabled = false; aimCamera.transform.position = new Vector3(0, 1.5f, -3);
            attack.aimer.viewCamera = aimCamera;
            yield return new WaitForSeconds(.3f);
            casting = player.GetComponentInChildren<PlayerSpellcasting>(); animator = player.GetComponentInChildren<Animator>();
            lamp = player.GetComponentInChildren<PlayerLampEquipment>();
            Assert.NotNull(casting); Assert.True(casting.enabled);
            attack.visuals.onFired.AddListener(() =>
            {
                fired++; shotTimes.Add(Time.time);
                Vector3 muzzle = attack.visuals.FirePoint.position;
                Assert.Less(Vector3.Distance(muzzle, attack.visuals.fireEffectAnchor.position), .001f, "Cast VFX share the palm origin.");
                Assert.True(Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)
                    .Any(b => Vector3.Distance(b.transform.position, muzzle) < .001f), "The bolt starts at this frame's animated palm.");
            });
        }

        void Fire(bool held) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, held));

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            foreach (var bolt in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Object.Destroy(bolt.gameObject);
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); InputSystem.settings = original; Object.Destroy(settings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [UnityTest] public IEnumerator MovingCastUsesPalmKeepsLegsAndRestoresLamp()
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.True(attack.visuals.FirePoint.IsChildOf(hand));
            lamp.SetHeld(true);
            Vector3 start = player.transform.position;
            Quaternion legStart = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation;
            Fire(true); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.07f);
            Assert.AreEqual(0, fired, "Windup must precede the first projectile.");
            yield return new WaitForSeconds(.65f);
            Assert.GreaterOrEqual(fired, 2, $"Input={attack.input.PrimaryAttackHeld}, accepts={attack.input.AcceptsInput}, mouse={mouse.leftButton.isPressed}, life={(attack.lifeSource as ILifeState)?.State}, projectile={attack.projectilePrefab != null}, combat={attack.CombatEnabled}, weight={casting.Weight}, ready={casting.ReadyToFire}");
            Assert.Greater(player.transform.position.z - start.z, 2f);
            Assert.Greater(Quaternion.Angle(legStart, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation), 1f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.Greater(animator.GetFloat("Speed"), 4f);
            Assert.True(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Run" && c.weight > .8f));
            Assert.That(casting.Weight, Is.GreaterThan(.95f));
            Assert.False(lamp.IsHeld); Assert.True(lamp.RequestedHeld); Assert.AreSame(lamp.beltSocket, lamp.lamp.parent);
            Assert.Greater(Vector3.Dot(attack.visuals.FirePoint.forward, casting.AimDirection), .9f);
            for (int i = 1; i < shotTimes.Count; i++) Assert.GreaterOrEqual(shotTimes[i] - shotTimes[i - 1], attack.attackInterval - .025f);
            Fire(false); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            int count = fired;
            yield return new WaitForSeconds(.55f);
            Assert.AreEqual(count, fired); Assert.Less(casting.Weight, .01f);
            Assert.False(lamp.IsHeld); Assert.AreSame(lamp.beltSocket, lamp.lamp.parent);
        }

        [UnityTest] public IEnumerator TapFiresOnceAndInterruptedWindupCannotReleaseLater()
        {
            Fire(true); yield return null; yield return null;
            Fire(false); yield return new WaitForSeconds(.25f);
            Assert.AreEqual(1, fired, "A short click survives the windup and produces one bolt.");
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(1, fired);
            Fire(true); yield return null;
            attack.CombatEnabled = false; Fire(false);
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(1, fired);
            attack.CombatEnabled = true;
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(1, fired);
            Assert.Less(casting.Weight, .01f);
            Fire(true); yield return null;
            Time.timeScale = 0f; Fire(false);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(1, fired);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(1, fired, "Pausing cancels queued shots.");
        }

        /// <summary>시전 마스크에 Body가 없어야 공격하며 달릴 때 척추가 계속 걷는다. 손은 IK가 어깨 기준으로 잡으므로 조준을 유지한다.</summary>
        [UnityTest] public IEnumerator AttackingWhileRunningKeepsSpineWalkingAndPalmAimed()
        {
            var mask = animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller
                ? controller.layers.First(l => l.name == casting.layerName).avatarMask : null;
            Assert.NotNull(mask);
            Assert.False(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), "The casting mask must leave the spine to locomotion.");
            Assert.True(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm) && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK));
            Fire(true); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.8f);
            Assert.Greater(fired, 0);
            int layer = animator.GetLayerIndex(casting.layerName);
            Assert.Greater(animator.GetLayerWeight(layer), .95f, "Casting layer is fully raised while attacking.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Quaternion first = Quaternion.Inverse(hips.rotation) * chest.rotation;
            float chestRange = 0f, worstPalm = 1f, worstReach = 1f;
            for (float t = 0f; t < .7f; t += Time.deltaTime)
            {
                chestRange = Mathf.Max(chestRange, Quaternion.Angle(first, Quaternion.Inverse(hips.rotation) * chest.rotation));
                worstPalm = Mathf.Min(worstPalm, Vector3.Dot(attack.visuals.FirePoint.forward, casting.AimDirection));
                worstReach = Mathf.Min(worstReach, Vector3.Dot((hand.position - shoulder.position).normalized, casting.AimDirection));
                Assert.Greater(animator.GetFloat("Speed"), 4f, "Keeps running while attacking.");
                yield return null;
            }
            Assert.Greater(chestRange, 5f, "The chest keeps moving relative to the pelvis while attacking on the move.");
            Assert.Greater(worstPalm, .9f, "The palm keeps facing the aim direction every frame.");
            Assert.Greater(worstReach, .75f, "The arm keeps pointing from the shoulder toward the aim (recoil frames blend in some clip pose).");
            Fire(false); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        [UnityTest] public IEnumerator AirCastingUsesUprightJumpAndVisualReplacementRebinds()
        {
            Fire(true);
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.2f);
            Assert.False(player.GetComponent<PlayerMotor>().IsGrounded);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Air Cast"));
            Assert.Greater(Vector3.Dot(attack.visuals.FirePoint.forward, casting.AimDirection), .9f);
            Fire(false); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(1.3f); // 착지 동작(Landing)이 끝날 시간을 포함한다
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            attack.visuals.RebuildVisual();
            yield return null;
            Assert.AreNotSame(casting, attack.visuals.Spellcasting);
            Assert.True(attack.visuals.FirePoint.IsChildOf(attack.visuals.Spellcasting.transform));
            int count = fired;
            Fire(true); yield return new WaitForSeconds(.2f); Fire(false);
            Assert.Greater(fired, count);
        }

        [UnityTest] public IEnumerator RenderCastingPoses()
        {
            var sun = Track(new GameObject("Casting Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2; sun.transform.rotation = Quaternion.Euler(40, -35, 0);
            Fire(true); yield return new WaitForSeconds(.22f);
            Capture("cast-idle");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.25f);
            Capture("cast-run");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.2f);
            Capture("cast-air");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.8f);
            aimCamera.transform.rotation = Quaternion.Euler(-30, 0, 0);
            yield return new WaitForSeconds(.2f);
            Capture("cast-up");
            Assert.Greater(casting.AimDirection.y, .2f);
            aimCamera.transform.rotation = Quaternion.identity;
            casting.castStyle = PlayerSpellcasting.CastStyle.GreatSwordSpell;
            yield return new WaitForSeconds(.2f);
            Capture("cast-great-sword");
            casting.castStyle = PlayerSpellcasting.CastStyle.SwordShieldSpell;
            yield return new WaitForSeconds(.2f);
            Capture("cast-sword-shield");
        }

        [UnityTest] public IEnumerator PackCastingVariantsUseImportedClipsAndKeepPalmAim()
        {
            Fire(true);
            string[] names = { "Cast Magic", "Cast Great Sword", "Cast Sword Shield" };
            for (int i = 0; i < names.Length; i++)
            {
                casting.castStyle = (PlayerSpellcasting.CastStyle)i;
                yield return new WaitForSeconds(.25f);
                int layer = animator.GetLayerIndex("Upper Body Casting");
                Assert.True(animator.GetCurrentAnimatorClipInfo(layer).Any(c => c.clip.name == names[i] && c.weight > .8f));
                Assert.Greater(Vector3.Dot(attack.visuals.FirePoint.forward, casting.AimDirection), .9f);
                Assert.False(animator.applyRootMotion);
            }
            Assert.GreaterOrEqual(fired, 2, $"Input={attack.input.PrimaryAttackHeld}, accepts={attack.input.AcceptsInput}, mouse={mouse.leftButton.isPressed}, life={(attack.lifeSource as ILifeState)?.State}, projectile={attack.projectilePrefab != null}, combat={attack.CombatEnabled}, weight={casting.Weight}, ready={casting.ReadyToFire}");
        }

        [UnityTest] public IEnumerator CombatMovementSelectsForwardBackAndStrafeClips()
        {
            Fire(true);
            Key[] keys = { Key.W, Key.S, Key.A, Key.D };
            string[] clips = { "Run", "Run Back", "Run Left", "Run Right" };
            for (int i = 0; i < keys.Length; i++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                yield return new WaitForSeconds(.35f);
                Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
                Assert.True(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == clips[i] && c.weight > .8f), clips[i]);
                Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.forward), .95f, "Keep facing the aim while strafing.");
                // 클립이 몸을 반대로 굽지 않았는지: 어깨선으로 구한 상체 정면이 캐릭터 정면과 같은 쪽을 본다.
                Vector3 shoulders = animator.GetBoneTransform(HumanBodyBones.RightShoulder).position - animator.GetBoneTransform(HumanBodyBones.LeftShoulder).position;
                Vector3 torsoForward = Vector3.Cross(shoulders, Vector3.up).normalized;
                Assert.Greater(Vector3.Dot(torsoForward, player.transform.forward), .5f, "The torso keeps facing the aim while moving " + clips[i] + ".");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            yield return new WaitForSeconds(.35f);
            Assert.Greater(animator.GetFloat("MoveX"), .5f);
            Assert.Greater(animator.GetFloat("MoveZ"), .5f);
            var diagonal = animator.GetCurrentAnimatorClipInfo(0);
            Assert.True(diagonal.Any(c => c.clip.name == "Run" && c.weight > .2f));
            Assert.True(diagonal.Any(c => c.clip.name == "Run Right" && c.weight > .2f));
        }

        [UnityTest] public IEnumerator HitKeepsMovementAndDeathOverridesCastingAndJump()
        {
            player.GetComponent<PlayerRespawner>().enabled = false; // 이 검사는 사망 상태 자체를 본다. 부활은 PlayerRespawnTests가 다룬다.
            Fire(true); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.25f);
            var health = player.GetComponent<PlayerHealth>();
            health.TakeDamage(new DamageInfo(10f, "Enemy"));
            Vector3 before = player.transform.position;
            yield return new WaitForSeconds(.1f);
            int reaction = animator.GetLayerIndex("Damage Reactions");
            Assert.True(animator.GetCurrentAnimatorStateInfo(reaction).IsName("Hit"));
            Assert.Greater(Vector3.Distance(before, player.transform.position), .1f);
            yield return new WaitForSeconds(.6f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(reaction).IsName("Empty"));
            health.TakeDamage(new DamageInfo(200f, "Enemy"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.2f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"));
            Assert.AreEqual(0f, animator.GetLayerWeight(reaction));
            Assert.AreEqual(0f, casting.Weight);
            Assert.False(attack.TryFire());
            int count = fired;
            yield return new WaitForSeconds(3f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"));
            Assert.AreEqual(count, fired);
            Capture("death");
        }

        void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Track(new GameObject("Casting Capture")).AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.25f;
            var center = player.transform.position + Vector3.up;
            camera.transform.position = center + new Vector3(3, .4f, 4); camera.transform.LookAt(center);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .18f);
            var rt = new RenderTexture(1000, 1000, 24); var previous = RenderTexture.active;
            var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); texture.Apply();
                Directory.CreateDirectory("Logs/player-casting-captures");
                File.WriteAllBytes("Logs/player-casting-captures/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; camera.enabled = false;
                rt.Release(); Object.Destroy(rt); Object.Destroy(texture);
            }
        }
    }
}
#endif

