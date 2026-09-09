#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>Mixamo 리깅 몸체·클립·장비가 Enemy 프리팹에서 실제로 움직이는지 확인하고 렌더를 저장한다.</summary>
    public sealed class EnemyArtTests
    {
        const string Captures = "Docs/model-art/enemy-combat-art-v1/captures";
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        [SetUp] public void Setup() { Time.timeScale = 1f; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator Swordsman() => Check("Assets/Enemy/Generated/Enemy.prefab", "Swordsman", 1.75f, ("ShortSword", HumanBodyBones.RightHand), ("RoundShield", HumanBodyBones.LeftHand));
        [UnityTest] public IEnumerator Assassin() => Check("Assets/Enemy/Generated/Enemy_Assassin.prefab", "Assassin", 1.68f, ("AssassinDagger", HumanBodyBones.RightHand), ("AssassinDagger", HumanBodyBones.LeftHand));
        [UnityTest] public IEnumerator ShieldGuard() => Check("Assets/Enemy/Generated/Enemy_ShieldGuard.prefab", "ShieldGuard", 1.84f, ("ShortSword", HumanBodyBones.RightHand), ("TowerShield", HumanBodyBones.LeftHand));
        [UnityTest] public IEnumerator HammerBrute() => Check("Assets/Enemy/Generated/Enemy_HammerBrute.prefab", "HammerBrute", 1.94f, ("Warhammer", HumanBodyBones.RightHand));
        [UnityTest] public IEnumerator Chief() => Check("Assets/Enemy/Generated/Enemy_Chief.prefab", "Chief", 2.04f, ("ChiefScimitar", HumanBodyBones.RightHand), ("ChiefCape", HumanBodyBones.Chest));

        IEnumerator Check(string prefabPath, string name, float height, params (string asset, HumanBodyBones bone)[] gear)
        {
            var level = Track(new GameObject("Level"));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(level.transform, false); ground.transform.position = new Vector3(0, -.5f, 5); ground.transform.localScale = new Vector3(20, 1, 30);
            ground.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.30f, .26f, .19f) };
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            var sun = Track(new GameObject("Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(40, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .55f, .58f);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(asset, "Run SandGuard/Enemy/Connect Combat Art first: " + prefabPath);
            var enemy = Track(UnityEngine.Object.Instantiate(asset, Vector3.zero, Quaternion.identity));
            var brain = enemy.GetComponent<EnemyBrain>(); brain.AIEnabled = false;
            var motor = enemy.GetComponent<EnemyMotor>();
            var visuals = enemy.GetComponent<EnemyVisuals>();
            var attack = enemy.GetComponent<EnemyMeleeAttack>();
            var health = enemy.GetComponent<EnemyHealth>();
            var gripProbe = enemy.AddComponent<EquipmentPoseProbe>();
            yield return new WaitForSeconds(.5f);

            var animator = enemy.GetComponentInChildren<Animator>();
            Assert.NotNull(animator, "The combat visual must carry an Animator.");
            Assert.True(animator.isHuman && animator.avatar.isValid, name + " avatar must be a valid Humanoid.");
            Assert.False(animator.applyRootMotion);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "Idle should sit in Locomotion.");
            Assert.Less(animator.GetFloat("Speed"), .15f);
            // 망토도 SkinnedMeshRenderer이므로 몸체 재질(_Combat)을 쓰는 렌더러를 고른다.
            var skin = enemy.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.name.EndsWith("_Combat"));
            Assert.NotNull(skin, name + " body renderer with the _Combat material is missing.");
            var map = skin.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            Assert.NotNull(map, name + " body needs its base color texture.");
            Assert.AreEqual(2048, map.width, "Body texture should keep the original 2K resolution.");
            Assert.AreEqual("Universal Render Pipeline/Lit", skin.sharedMaterial.shader.name);
            Bounds bounds = skin.bounds;
            Assert.AreEqual(height, bounds.size.y, .25f, name + " height should match the preview height.");
            Assert.AreEqual(0f, bounds.min.y, .2f, name + " feet should be on the floor.");

            var equipmentRoots = enemy.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("_Placement")).ToArray();
            Assert.AreEqual(gear.Length, equipmentRoots.Length, name + " equipment count.");
            foreach (var (item, boneId) in gear)
            {
                var bone = animator.GetBoneTransform(boneId) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                var placement = equipmentRoots.FirstOrDefault(t => t.name == item + "_Placement" && t.IsChildOf(bone));
                Assert.NotNull(placement, item + " must hang under the " + boneId + " bone.");
                Assert.Greater(placement.GetComponentsInChildren<Renderer>().Length, 0, item + " must render.");
                if (boneId != HumanBodyBones.Chest)
                {
                    string marker = "HandGrip";
                    var grip = placement.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith(marker));
                    var palm = bone.Find("EquipmentPalm");
                    Assert.NotNull(palm, item + " needs a palm-centered socket.");
                    Assert.Less(Vector3.Distance(grip.position, palm.position), .002f, item + " handle should sit in the palm, not at the wrist.");
                }
            }
            // 손 축 표시(빨강=손가락, 초록=엄지, 파랑=손바닥 법선, 노랑=팔뚝)를 얹어 찍어 장비 그립 규칙을 눈으로 검증한다.
            var gizmos = new List<GameObject>();
            gizmos.AddRange(HandAxes(animator, HumanBodyBones.LeftHand));
            gizmos.AddRange(HandAxes(animator, HumanBodyBones.RightHand));
            Capture(enemy, name + "_idle_axes");
            foreach (var gizmo in gizmos) UnityEngine.Object.Destroy(gizmo);
            yield return null;
            Capture(enemy, name + "_idle");
            CheckGrip(animator);
            CaptureHands(enemy, animator, name + "_idle");

            Assert.True(motor.TrySetDestination(new Vector3(0, 0, 12)), "Destination must be reachable.");
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(enemy.transform.position.z, 1.5f, name + " should have moved forward.");
            Assert.Greater(animator.GetFloat("Speed"), motor.moveSpeed * .5f, "Speed parameter must follow the motor.");
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float drift = 0f;
            for (int i = 0; i < 20; i++)
            {
                Vector3 delta = hips.position - enemy.transform.position; delta.y = 0f;
                drift = Mathf.Max(drift, delta.magnitude);
                yield return null;
            }
            Assert.Less(drift, .45f, name + " hips drifted " + drift + "m from the root: root motion is leaking into the pose.");
            Capture(enemy, name + "_move");
            CheckGrip(animator);

            motor.Stop();
            yield return new WaitForSeconds(.4f);
            visuals.PlayAttack(attack.windup);
            yield return null; yield return null;
            Assert.True(InState(animator, "Attack"), name + " must enter Attack on the trigger.");
            yield return new WaitForSeconds(attack.windup);
            Capture(enemy, name + "_attack");
            CheckGrip(animator);
            CaptureHands(enemy, animator, name + "_attack");
            yield return new WaitForSeconds(attack.interval);
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), name + " must return to Locomotion within one attack interval.");
            Assert.Greater(gripProbe.samples, 20, "Observe the rendered pose across the complete movement and attack.");
            Assert.Less(gripProbe.supportError, .035f, name + " support hand lost the handle during an animation or transition.");
            Assert.Greater(gripProbe.guardAlignment, .95f, name + " shield fell out of guard during an animation or transition.");

            float removeDelay = health.removeDelay;
            health.TakeDamage(new DamageInfo(1000f, "Ally"));
            yield return new WaitForSeconds(.3f);
            Assert.True(InState(animator, "Dead"), name + " must enter Dead after dying.");
            int lastGripFrame = animator.GetComponent<EnemyEquipmentGrip>().PoseFrame;
            yield return new WaitForSeconds(Mathf.Max(.2f, removeDelay - .8f));
            Assert.NotNull(enemy, "Body must stay for the death animation.");
            Assert.AreEqual(lastGripFrame, animator.GetComponent<EnemyEquipmentGrip>().PoseFrame, "Death animation must release arm constraints.");
            Capture(enemy, name + "_death");
            yield return new WaitForSeconds(1.2f);
            Assert.True(enemy == null, name + " should be removed after removeDelay.");
        }

        /// <summary>애니메이터를 끈 바인드 포즈(T포즈)에서 장비 배치를 정면·위에서 찍는다. 그립 축 규칙을 눈으로 검증하기 위한 진단용.</summary>
        [UnityTest] public IEnumerator BindPoseEquipmentLayout()
        {
            var sun = Track(new GameObject("Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(40, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .55f, .58f);
            foreach (var name in new[] { "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Art/Characters/" + name + "/" + name + "_CombatVisual.prefab");
                Assert.NotNull(asset, name + " combat visual is missing.");
                var visual = Track(UnityEngine.Object.Instantiate(asset, Vector3.zero, Quaternion.identity));
                foreach (var animator in visual.GetComponentsInChildren<Animator>()) animator.enabled = false;
                yield return null;
                Capture(visual, name + "_bindpose_front", new Vector3(0, .3f, 4.5f), 1.3f);
                Capture(visual, name + "_bindpose_top", new Vector3(0, 4.5f, .01f), 1.3f);
                UnityEngine.Object.Destroy(visual);
                yield return null;
            }
        }

        IEnumerable<GameObject> HandAxes(Animator animator, HumanBodyBones hand)
        {
            bool right = hand == HumanBodyBones.RightHand;
            var handBone = animator.GetBoneTransform(hand);
            var forearm = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var knuckle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal)
                ?? animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var thumbBone = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Vector3 fingers = knuckle != null ? (knuckle.position - handBone.position).normalized : (handBone.position - forearm.position).normalized;
            Vector3 thumb = thumbBone != null ? Vector3.ProjectOnPlane(thumbBone.position - handBone.position, fingers).normalized : Vector3.ProjectOnPlane(Vector3.forward, fingers).normalized;
            Vector3 palm = Vector3.Cross(fingers, thumb).normalized * (right ? 1f : -1f);
            Vector3 arm = (handBone.position - forearm.position).normalized;
            yield return Axis(handBone.position, fingers, Color.red);
            yield return Axis(handBone.position, thumb, Color.green);
            yield return Axis(handBone.position, palm, Color.blue);
            yield return Axis(handBone.position, arm, Color.yellow);
        }

        GameObject Axis(Vector3 origin, Vector3 direction, Color color)
        {
            var cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            UnityEngine.Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.localScale = new Vector3(.03f, .03f, .3f);
            cube.transform.rotation = Quaternion.LookRotation(direction);
            cube.transform.position = origin + direction * .15f;
            cube.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
            return cube;
        }

        static bool InState(Animator animator, string state)
            => animator.GetCurrentAnimatorStateInfo(0).IsName(state) || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));

        static void CheckGrip(Animator animator)
        {
            var fit = animator.GetComponent<EnemyEquipmentGrip>();
            Assert.NotNull(fit);
            Assert.Less(Vector3.Distance(fit.primaryGrip.position, fit.rightPalm.position), .002f);
            if (fit.supportGrip != null)
            {
                Assert.GreaterOrEqual(fit.PoseFrame, Time.frameCount - 1, "Support grip must update after animation each frame.");
                var upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Assert.Less(fit.SupportError, .035f, "The support palm must stay on the hammer handle after LateUpdate. " +
                    "shoulder=" + upper.position + " elbow=" + lower.position + " wrist=" + hand.position + " palm=" + fit.leftPalm.position +
                    " target=" + fit.supportGrip.position + " primary=" + fit.primaryGrip.position + " scale=" + hand.lossyScale);
            }
            if (fit.shieldFrame != null)
            {
                Assert.Greater(Vector3.Dot(fit.shieldFrame.forward, animator.transform.forward), .95f, "Shield face must guard forward.");
                Assert.Greater(Vector3.Dot(fit.shieldFrame.up, animator.transform.up), .95f, "Shield must stay upright while moving.");
            }
        }

        void CaptureHands(GameObject enemy, Animator animator, string name)
        {
            var center = (animator.GetBoneTransform(HumanBodyBones.RightHand).position + animator.GetBoneTransform(HumanBodyBones.LeftHand).position) * .5f;
            Capture(enemy, name + "_hands", new Vector3(1.8f, .7f, 3f), .55f, center);
            if (animator.GetComponent<EnemyEquipmentGrip>().shieldFrame != null)
                Capture(enemy, name + "_shield_back", new Vector3(-2f, .4f, -2f), .48f, animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
        }

        void Capture(GameObject enemy, string name) => Capture(enemy, name, new Vector3(2.2f, .5f, 4f), 1.45f);
        void Capture(GameObject enemy, string name, Vector3 offset, float size, Vector3? focus = null)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            // Coroutine tests can render before LateUpdate; match the pose used by the game camera.
            enemy.GetComponentInChildren<EnemyEquipmentGrip>()?.ApplyPose();
            var camera = Track(new GameObject("Art Verification Camera")).AddComponent<Camera>();
            camera.backgroundColor = new Color(.14f, .16f, .19f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            var center = focus ?? enemy.transform.position + Vector3.up * 1.05f;
            camera.transform.position = center + enemy.transform.rotation * offset; camera.transform.LookAt(center);
            var rt = new RenderTexture(1200, 1000, 24); camera.targetTexture = rt;
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); texture.Apply();
            Directory.CreateDirectory(Captures);
            File.WriteAllBytes(Captures + "/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
            UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(texture); camera.enabled = false;
        }
    }

    [DefaultExecutionOrder(250)]
    public sealed class EquipmentPoseProbe : MonoBehaviour
    {
        public int samples;
        public float supportError, guardAlignment = 1f;
        EnemyEquipmentGrip fit;
        EnemyHealth health;
        void Start() { fit = GetComponentInChildren<EnemyEquipmentGrip>(); health = GetComponent<EnemyHealth>(); }
        void LateUpdate()
        {
            if (fit == null || health.State != global::LifeState.Alive) return;
            samples++;
            if (fit.supportGrip != null) supportError = Mathf.Max(supportError, Vector3.Distance(fit.leftPalm.position, fit.supportGrip.position));
            if (fit.shieldFrame != null)
                guardAlignment = Mathf.Min(guardAlignment, Vector3.Dot(fit.shieldFrame.forward, fit.transform.forward), Vector3.Dot(fit.shieldFrame.up, fit.transform.up));
        }
    }
}
#endif
