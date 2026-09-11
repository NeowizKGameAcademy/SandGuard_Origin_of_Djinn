using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    public static class PlayerLampBuilder
    {
        public static void Configure(PlayerLampEquipment equipment)
        {
            var animator = equipment.GetComponent<Animator>();
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            animator.Rebind(); animator.Update(0f);
            var model = animator.transform;
            Vector3 localSocket = equipment.beltSocket.parent.InverseTransformPoint(model.TransformPoint(new Vector3(-0.18f, 1.12f, -0.13f)));
            // Sampling the animator must not save a new pose into the model's prefab overrides.
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = positions[i];
                transforms[i].localRotation = rotations[i];
                transforms[i].localScale = scales[i];
            }
            equipment.beltSocket.localPosition = localSocket;
            equipment.allowHandAttachment = false;
            equipment.manaDriven = true;
            equipment.beltEuler = new Vector3(0f, 0f, 12f);
            equipment.swayDegrees = 10f;
            equipment.maxLightIntensity = 0.18f;
            equipment.absorptionRiseTime = 0.1f;
            equipment.absorptionFadeTime = 0.65f;
            equipment.absorptionEmissionBoost = 18f;
            equipment.absorptionLightBoost = 0.22f;
            var attach = equipment.lamp.GetComponentsInChildren<Transform>(true).First(t => t.name == "BeltAttach");
            equipment.beltAttachOffset = equipment.lamp.InverseTransformPoint(attach.position);
            equipment.SetHeld(false);
            equipment.lamp.rotation = Quaternion.Euler(0f, model.eulerAngles.y, 0f) * Quaternion.Euler(equipment.beltEuler);
            equipment.lamp.position = equipment.beltSocket.position - equipment.lamp.TransformVector(equipment.beltAttachOffset);
        }

        [MenuItem("SandGuard/Player/Connect Belt Lamp")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(ProtagonistArtBuilder.VisualPath);
            try
            {
                Configure(root.GetComponentInChildren<PlayerLampEquipment>(true));
                PrefabUtility.SaveAsPrefabAsset(root, ProtagonistArtBuilder.VisualPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void ApplyWithSandRoot()
        {
            Apply();
            var builder = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("DesertTower.VFX.Editor.SandRootVfxBuilder"))
                .First(t => t != null);
            builder.GetMethod("Build").Invoke(null, null);
        }
    }
}
