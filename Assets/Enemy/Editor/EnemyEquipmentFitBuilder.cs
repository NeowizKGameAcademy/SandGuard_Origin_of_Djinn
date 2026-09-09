using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Editor
{
    public static class EnemyEquipmentFitBuilder
    {
        static readonly string[] Names = { "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" };

        [MenuItem("SandGuard/Enemy/Fit Hands and Equipment")]
        public static void Build()
        {
            foreach (string name in Names)
            {
                string path = EnemyCombatArtBuilder.VisualPath(name);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(root.GetComponentInChildren<Animator>(), name);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in new[] { EnemyCombatArtBuilder.BasePrefab }.Concat(Names.Select(EnemyCombatArtBuilder.VariantPath)))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { root.GetComponent<EnemyVisuals>().RebuildVisual(); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ENEMY_EQUIPMENT_FIT_COMPLETE");
        }

        public static void Configure(Animator animator, string owner)
        {
            // Calibration must use imported rest transforms, never a currently sampled animation frame.
            string rigPath = "Assets/Enemy/Art/Characters/" + owner + "/" + owner + "_Rig.fbx";
            var probe = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(rigPath));
            try
            {
                var reference = probe.GetComponent<Animator>();
                probe.transform.SetPositionAndRotation(animator.transform.position, animator.transform.rotation);
                var fit = animator.GetComponent<EnemyEquipmentGrip>() ?? animator.gameObject.AddComponent<EnemyEquipmentGrip>();
                fit.primaryGrip = fit.supportGrip = fit.shieldFrame = null;
                fit.characterHeight = owner == "ShieldGuard" ? 1.84f : owner == "Swordsman" ? 1.75f : 1.94f;
                fit.rightPalm = Palm(animator, reference, true);
                fit.leftPalm = Palm(animator, reference, false);
                foreach (var wrapper in animator.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("_Placement")).ToArray())
                {
                    if (wrapper.name == "ChiefCape_Placement") continue;
                    bool right = wrapper.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand));
                    var hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                    var palm = right ? fit.rightPalm : fit.leftPalm;
                    Frame(reference, right, out Vector3 fingers, out Vector3 thumb, out Vector3 normal);
                    var refHand = reference.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                    thumb = hand.TransformDirection(refHand.InverseTransformDirection(thumb));
                    normal = hand.TransformDirection(refHand.InverseTransformDirection(normal));
                    bool shield = wrapper.name.Contains("Shield"), hammer = wrapper.name.StartsWith("Warhammer");
                    Transform Marker(string prefix) => wrapper.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith(prefix, StringComparison.Ordinal));
                    Transform grip = Marker("HandGrip");
                    var mesh = wrapper.GetComponentInChildren<MeshFilter>();
                    Vector3 longAxis = MeshAxis(mesh, true), thinAxis = MeshAxis(mesh, false);
                    Vector3 targetLong = thumb, targetThin = normal;
                    if (shield)
                    {
                        thinAxis = (Marker("ShieldFace").position - grip.position).normalized;
                        // The handle runs across the shield, parallel to the thumb/radial axis of a closed grip.
                        targetLong = -fingers;
                        targetLong = hand.TransformDirection(refHand.InverseTransformDirection(targetLong));
                        targetThin = normal;
                    }
                    else
                    {
                        Vector3 shaft = hammer ? Marker("OffHandGrip").position - Marker("HandGrip").position : Marker("BladeTip").position - grip.position;
                        if (Vector3.Dot(longAxis, shaft) < 0f) longAxis = -longAxis;
                    }
                    wrapper.rotation = Quaternion.LookRotation(targetLong, targetThin) * Quaternion.Inverse(Quaternion.LookRotation(longAxis, thinAxis)) * wrapper.rotation;
                    wrapper.position += palm.position - grip.position;
                    if (right) fit.primaryGrip = grip;
                    if (shield)
                    {
                        var frame = hand.Find("ShieldGuardFrame") ?? new GameObject("ShieldGuardFrame").transform;
                        frame.SetParent(hand, false);
                        Vector3 face = (Marker("ShieldFace").position - grip.position).normalized;
                        frame.rotation = Quaternion.LookRotation(face, MeshAxis(mesh, true));
                        fit.shieldFrame = frame;
                        fit.leftGripBasis = frame.localRotation;
                    }
                    if (hammer)
                    {
                        fit.supportGrip = Marker("OffHandGrip");
                        Frame(reference, false, out _, out Vector3 leftThumb, out Vector3 leftNormal);
                        var left = reference.GetBoneTransform(HumanBodyBones.LeftHand);
                        fit.leftGripBasis = Quaternion.Inverse(left.rotation) * Quaternion.LookRotation(leftThumb, leftNormal);
                    }
                    Debug.Log("GEAR_FIT " + owner + " " + wrapper.name + " palm=" + hand.InverseTransformPoint(palm.position).ToString("F4"));
                }
                // Only finger rotations are baked; locomotion, attack and wrist animation remain intact.
                using (var handler = new HumanPoseHandler(reference.avatar, reference.transform))
                {
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    for (int i = 0; i < HumanTrait.MuscleCount; i++)
                    {
                        string muscle = HumanTrait.MuscleName[i];
                        if (!muscle.Contains("Stretched") && !muscle.Contains("Spread")) continue;
                        if (!muscle.StartsWith("Left") && !muscle.StartsWith("Right")) continue;
                        if (owner == "Chief" && muscle.StartsWith("Left")) continue;
                        pose.muscles[i] = muscle.Contains("Spread") ? 0f : muscle.Contains("Thumb") ? -.25f : -.65f;
                    }
                    handler.SetHumanPose(ref pose);
                    var fingerPoses = new System.Collections.Generic.List<EnemyEquipmentGrip.FingerPose>();
                    for (int i = (int)HumanBodyBones.LeftThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
                    {
                        var boneId = (HumanBodyBones)i;
                        if (owner == "Chief" && boneId.ToString().StartsWith("Left")) continue;
                        var source = reference.GetBoneTransform(boneId); var destination = animator.GetBoneTransform(boneId);
                        if (source != null && destination != null)
                            fingerPoses.Add(new EnemyEquipmentGrip.FingerPose { bone = destination, rotation = source.localRotation });
                    }
                    fit.fingers = fingerPoses.ToArray();
                }
            }
            finally { Object.DestroyImmediate(probe); }
        }

        static Transform Palm(Animator target, Animator reference, bool right)
        {
            var handId = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            var refHand = reference.GetBoneTransform(handId);
            Frame(reference, right, out Vector3 fingers, out _, out Vector3 normal);
            var knuckle = reference.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            float length = knuckle != null ? Vector3.Distance(knuckle.position, refHand.position) : .08f;
            Vector3 center = refHand.position + fingers * length * .72f + normal * .018f;
            var hand = target.GetBoneTransform(handId);
            var palm = hand.Find("EquipmentPalm") ?? new GameObject("EquipmentPalm").transform;
            palm.SetParent(hand, false); palm.localPosition = refHand.InverseTransformPoint(center); palm.localRotation = Quaternion.identity;
            return palm;
        }

        static void Frame(Animator animator, bool right, out Vector3 fingers, out Vector3 thumb, out Vector3 normal)
        {
            var hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var index = animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var forearm = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var thumbBone = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            fingers = ((index != null ? index.position : hand.position * 2 - forearm.position) - hand.position).normalized;
            thumb = Vector3.ProjectOnPlane(thumbBone != null ? thumbBone.position - hand.position : animator.transform.forward, fingers).normalized;
            normal = Vector3.Cross(fingers, thumb).normalized * (right ? 1f : -1f);
        }

        static Vector3 MeshAxis(MeshFilter mesh, bool longest)
        {
            Vector3 size = mesh.sharedMesh.bounds.size;
            int index = longest ? (size.x > size.y ? 0 : 1) : (size.x < size.y ? 0 : 1);
            if (longest ? size.z > size[index] : size.z < size[index]) index = 2;
            Vector3 axis = Vector3.zero; axis[index] = 1;
            return mesh.transform.TransformDirection(axis).normalized;
        }
    }
}
