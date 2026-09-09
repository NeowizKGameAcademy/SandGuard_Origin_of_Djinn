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
        [UnityTest] public IEnumerator TexturedPlayerMovesAndLampChangesAttachmentAndLight()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(50, 1, 50);
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); floorMat.color = new Color(.26f,.23f,.18f);
            floor.GetComponent<Renderer>().material = floorMat;
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
            Assert.AreSame(originalLamp,lamp.lamp); Assert.AreSame(lamp.handSocket,lamp.lamp.parent);
            Assert.Less(Vector3.Distance(beltScale,lamp.lamp.lossyScale),.001f,"Attachment changes must preserve the lamp's size.");
            Assert.True(lamp.lampLight.enabled);
            var block = new MaterialPropertyBlock(); lamp.lampRenderer.GetPropertyBlock(block,lamp.emissiveMaterialIndex);
            Assert.Greater(block.GetColor("_EmissionColor").maxColorComponent,1f);
            Capture(player,"lamp-hand-lit");
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
            AssertClip(animator, "Jump");
            float firstJumpTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.08f);
            Assert.AreEqual(2, jumps);
            Assert.Less(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, firstJumpTime, "Air jump restarts the animation.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(.06f);
            Assert.AreEqual(2, jumps, "Rejected third jump must not retrigger animation.");

            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(.07f);
            Assert.True(motor.IsDashing);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"));
            AssertClip(animator, "Run");
            Assert.False(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(.23f);
            Assert.False(motor.IsDashing);
            Assert.False(motor.IsGrounded);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(1.2f);
            Assert.True(motor.IsGrounded);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(.07f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"));
            yield return new WaitForSeconds(.35f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
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
