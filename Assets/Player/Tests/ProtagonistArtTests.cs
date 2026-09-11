#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SandGuard.Player.Tests
{
    public sealed class ProtagonistArtTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
        InputSettings original, settings;
        GameObject Track(GameObject o) { objects.Add(o); return o; }
        [SetUp] public void Setup()
        {
            original = InputSystem.settings; settings = Object.Instantiate(original);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>(); Time.timeScale = 1;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var o in objects) if (o != null) Object.Destroy(o);
            objects.Clear(); InputSystem.RemoveDevice(keyboard); InputSystem.settings = original;
            Object.Destroy(settings); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }
        [UnityTest] public IEnumerator TexturedPlayerMovesAndLampStaysOnBeltWithLight()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(50, 1, 50);
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); floorMat.color = new Color(.26f,.23f,.18f);
            floor.GetComponent<Renderer>().material = floorMat;
            Physics.SyncTransforms(); // 바닥 콜라이더가 자리 잡기 전에 플레이어가 떨어져 착지 동작이 나오지 않게 한다
            var sun = Track(new GameObject("Verification Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2; sun.transform.rotation = Quaternion.Euler(40,-35,0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f,.55f,.58f);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            Assert.NotNull(asset);
            var player = Track(Object.Instantiate(asset));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(.4f);
            var animator = player.GetComponentInChildren<Animator>();
            Assert.NotNull(animator); Assert.True(animator.isHuman); Assert.True(animator.avatar.isValid);
            Assert.False(animator.applyRootMotion);
            var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.NotNull(skin.sharedMaterial.GetTexture("_BaseMap"));
            Assert.AreEqual(2048, skin.sharedMaterial.GetTexture("_BaseMap").width);
            Assert.AreEqual("Universal Render Pipeline/Lit", skin.sharedMaterial.shader.name);
            Assert.True(skin.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0));
            Debug.Log("ART_RUNTIME_BOUNDS " + skin.bounds + " modelScale=" + animator.transform.lossyScale + " skinScale=" + skin.transform.lossyScale);
            Assert.Less(animator.GetFloat("Speed"), .15f);
            AssertClip(animator, "Idle");
            Capture(player, "idle");
            Vector3 initial = player.transform.position;
            var leg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Quaternion initialLeg = leg.localRotation;
            motor.moveSpeed = 2f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.5f);
            Assert.Greater(player.transform.position.z - initial.z, .5f);
            Assert.That(animator.GetFloat("Speed"), Is.InRange(1.8f,2.2f));
            AssertClip(animator, "Walk");
            Assert.Greater(Quaternion.Angle(initialLeg, leg.localRotation), 1f);
            Capture(player, "walk");
            motor.moveSpeed = 5f;
            yield return new WaitForSeconds(.4f);
            Assert.That(animator.GetFloat("Speed"), Is.InRange(4.7f,5.3f));
            AssertClip(animator, "Run");
            Capture(player, "run");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.35f);
            Assert.Less(animator.GetFloat("Speed"), .15f);
            Vector3 settled = player.transform.position;
            yield return new WaitForSeconds(.4f);
            Assert.Less(Vector3.Distance(settled,player.transform.position), .03f, "Root motion must not move the controller.");
            var lamp = player.GetComponentInChildren<PlayerLampEquipment>();
            Assert.NotNull(lamp); Assert.False(lamp.IsHeld);
            var originalLamp = lamp.lamp;
            Vector3 beltScale = lamp.lamp.lossyScale;
            Assert.AreSame(lamp.beltSocket, lamp.lamp.parent);
            lamp.SetHeld(true); lamp.SetGlowing(true);
            yield return new WaitForSeconds(.6f);
            Assert.AreSame(originalLamp,lamp.lamp); Assert.AreSame(lamp.beltSocket,lamp.lamp.parent);
            Assert.False(lamp.IsHeld);
            Assert.Less(Vector3.Distance(beltScale,lamp.lamp.lossyScale),.001f,"Attachment changes must preserve the lamp's size.");
            Assert.True(lamp.lampLight.enabled);
            var block = new MaterialPropertyBlock(); lamp.lampRenderer.GetPropertyBlock(block,lamp.emissiveMaterialIndex);
            Assert.Greater(block.GetColor("_EmissionColor").maxColorComponent,1f);
            Capture(player,"lamp-belt-lit");
            lamp.SetHeld(false); lamp.SetGlowing(false);
            Assert.AreSame(lamp.beltSocket,lamp.lamp.parent); Assert.False(lamp.lampLight.enabled);
            lamp.lampRenderer.GetPropertyBlock(block,lamp.emissiveMaterialIndex);
            Assert.AreEqual(Color.black,block.GetColor("_EmissionColor"));
            Object.Destroy(floorMat);
        }
        [UnityTest] public IEnumerator JumpAndDashFollowMotorAndReturnToLocomotion()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -.5f, 0);
            floor.transform.localScale = new Vector3(100, 1, 100);
            var sun = Track(new GameObject("Mobility Verification Sun")).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(40, -35, 0);
            Physics.SyncTransforms();
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            var motor = player.GetComponent<PlayerMotor>();
            var effects = player.GetComponent<PlayerEffects>(); // 기본값은 공중 점프·공중 대시 0
            effects.Apply(new SandGuard.Player.Effects.DoubleJumpEffect()); effects.Apply(new SandGuard.Player.Effects.AirDashEffect());
            int jumps = 0;
            motor.Jumped += () => jumps++;
            yield return new WaitForSeconds(.4f);
            var animator = player.GetComponentInChildren<Animator>();
            Assert.True(motor.IsGrounded);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            var modelOrigin = animator.transform.localPosition;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(1, jumps);
            Assert.False(motor.IsGrounded);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"));
            AssertClip(animator, "Jump Up");
            Assert.False(motor.LastJumpWasAirJump);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.08f);
            Assert.AreEqual(2, jumps);
            Assert.True(motor.LastJumpWasAirJump);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Double Jump"), "Air jump plays the flip.");
            AssertClip(animator, "Flip");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.06f);
            Assert.AreEqual(2, jumps, "Rejected third jump must not retrigger animation.");

            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(.07f);
            Assert.True(motor.IsDashing);
            int dashLayer = animator.GetLayerIndex("Dash Legs");
            Assert.GreaterOrEqual(dashLayer, 0, "The dash lives on its own lower-body layer.");
            Assert.True(animator.GetCurrentAnimatorStateInfo(dashLayer).IsName("Dash"));
            Assert.AreEqual(1f, animator.GetLayerWeight(dashLayer), "Dashing raises the leg layer.");
            Assert.True(System.Array.Exists(animator.GetCurrentAnimatorClipInfo(dashLayer), c => c.clip.name == "Push Legs" && c.weight > .8f), "Legs play the push clip.");
            Assert.False(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(motor.dashDuration);
            Assert.False(motor.IsDashing);
            Assert.False(motor.IsGrounded);
            // 하체 레이어가 대시를 맡으므로 Base Layer는 공중 동작(플립 → 낙하)을 그대로 이어 간다.
            for (float t = 0f; t < .6f && !animator.GetCurrentAnimatorStateInfo(0).IsName("Falling"); t += Time.deltaTime) yield return null;
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Falling"), "An air dash ends in the falling pose.");
            AssertClip(animator, "Falling");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            bool sawLanding = false;
            for (float t = 0f; t < 2.5f && !(motor.IsGrounded && animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")); t += Time.deltaTime)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Landing")) sawLanding = true;
                yield return null;
            }
            Assert.True(motor.IsGrounded);
            Assert.True(sawLanding, "Touching down plays the landing before locomotion resumes.");
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(.07f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(dashLayer).IsName("Dash"));
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "The base layer keeps running under the dash legs.");
            yield return new WaitForSeconds(motor.dashDuration + .3f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.True(animator.GetCurrentAnimatorStateInfo(dashLayer).IsName("Empty"));
            Assert.AreEqual(0f, animator.GetLayerWeight(dashLayer), "After the dash the leg layer drops to 0 so the legs run again.");
            Assert.False(animator.applyRootMotion);
            Assert.Less(Vector3.Distance(modelOrigin, animator.transform.localPosition), .01f);

            // Rendering can stall a frame; capture after the timing-sensitive assertions.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.2f);
            Capture(player, "jump");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(1.2f);
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(.07f);
            Capture(player, "dash");
        }

        /// <summary>회귀 검사: 피격 레이어가 항상 1이면 Empty 상태라도 척추·머리 근육이 고정되어 상체가 골반과 함께 막대처럼 흔들린다.</summary>
        [UnityTest] public IEnumerator SpineAnimatesWhileRunningAndReactionLayerRisesOnlyForHits()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(200, 1, 200);
            Physics.SyncTransforms();
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            var health = player.GetComponent<PlayerHealth>();
            yield return new WaitForSeconds(.4f);
            var animator = player.GetComponentInChildren<Animator>();
            int reaction = animator.GetLayerIndex("Damage Reactions");
            Assert.GreaterOrEqual(reaction, 0);
            Assert.AreEqual(0f, animator.GetLayerWeight(reaction), "The reaction layer stays off while nothing hits the player.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.8f);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            float chestRange = 0f, headRange = 0f;
            Quaternion chestFirst = Quaternion.Inverse(hips.rotation) * chest.rotation, headFirst = Quaternion.Inverse(hips.rotation) * head.rotation;
            for (float t = 0f; t < .7f; t += Time.deltaTime)
            {
                chestRange = Mathf.Max(chestRange, Quaternion.Angle(chestFirst, Quaternion.Inverse(hips.rotation) * chest.rotation));
                headRange = Mathf.Max(headRange, Quaternion.Angle(headFirst, Quaternion.Inverse(hips.rotation) * head.rotation));
                Assert.AreEqual(0f, animator.GetLayerWeight(reaction), "Running must not raise the reaction layer.");
                yield return null;
            }
            Assert.Greater(chestRange, 5f, "The chest must keep moving relative to the pelvis while running (spine not frozen).");
            Assert.Greater(headRange, 3f, "The head must keep moving relative to the pelvis while running.");
            health.TakeDamage(new DamageInfo(10f, "Enemy"));
            yield return null;
            Assert.AreEqual(1f, animator.GetLayerWeight(reaction), "A hit raises the reaction layer immediately.");
            yield return new WaitForSeconds(.1f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(reaction).IsName("Hit"));
            yield return new WaitForSeconds(1.2f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(reaction).IsName("Empty"));
            Assert.AreEqual(0f, animator.GetLayerWeight(reaction), "After the reaction ends the layer drops back to 0.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        static void AssertClip(Animator animator, string name)
        {
            foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
                if (info.clip.name == name && info.weight > .8f) return;
            Assert.Fail("Expected dominant clip " + name);
        }
        void Capture(GameObject player,string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Track(new GameObject("Art Verification Camera")).AddComponent<Camera>();
            camera.backgroundColor=new Color(.14f,.16f,.19f); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.orthographic=true; camera.orthographicSize=1.3f;
            camera.nearClipPlane=.01f; camera.farClipPlane=100;
            var center=player.transform.position+Vector3.up*1.0f;
            camera.transform.position=center+new Vector3(2f,.45f,4f); camera.transform.LookAt(center);
            var rt=new RenderTexture(1200,1000,24); camera.targetTexture=rt;
            camera.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
            var texture=new Texture2D(1200,1000,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1200,1000),0,0); texture.Apply();
            Directory.CreateDirectory("Logs/player-animation-captures");
            File.WriteAllBytes("Logs/player-animation-captures/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=previous; camera.targetTexture=null; rt.Release();
            Object.Destroy(rt); Object.Destroy(texture); camera.enabled=false;
        }
    }
}
#endif
