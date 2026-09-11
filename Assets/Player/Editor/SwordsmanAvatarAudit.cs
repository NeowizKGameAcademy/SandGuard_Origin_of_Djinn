using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SandGuard.Player.Editor
{
    public static class SwordsmanAvatarAudit
    {
        public const string Folder = "Assets/Player/Editor/AvatarAudit";
        public const string Fresh = Folder + "/SwordsmanFresh.fbx";
        const string Current = "Assets/Enemy/Art/Characters/Swordsman/Swordsman_Rig.fbx";
        const string Move = "Assets/Enemy/Art/Characters/Swordsman/Swordsman_Move.fbx";

        [MenuItem("SandGuard/Animation Library/Audit Swordsman Avatars")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(Fresh)) File.Copy("Docs/model-art/bandit-minion/bandit1@Sword And Shield Run.fbx", Fresh);
            AssetDatabase.ImportAsset(Fresh, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Fresh);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var settings = importer.defaultClipAnimations;
            foreach (var setting in settings)
            {
                setting.lockRootRotation = setting.lockRootHeightY = true;
                setting.keepOriginalOrientation = setting.keepOriginalPositionY = true;
                setting.lockRootPositionXZ = false;
                setting.keepOriginalPositionXZ = true;
                setting.loopTime = setting.loopPose = true;
            }
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            string clean = Folder + "/PackedMoveClean.fbx";
            if (!File.Exists(clean)) File.Copy(Move, clean);
            AssetDatabase.ImportAsset(clean, ImportAssetOptions.ForceSynchronousImport);
            var cleanImporter = (ModelImporter)AssetImporter.GetAtPath(clean);
            cleanImporter.animationType = ModelImporterAnimationType.Human;
            cleanImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            cleanImporter.importAnimation = true;
            cleanImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            cleanImporter.animationCompression = ModelImporterAnimationCompression.Off;
            cleanImporter.SaveAndReimport();
            var cleanSettings = cleanImporter.defaultClipAnimations;
            foreach (var c in cleanSettings)
            {
                c.lockRootRotation = c.lockRootHeightY = true;
                c.keepOriginalOrientation = c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = true;
                c.loopTime = c.loopPose = true;
            }
            cleanImporter.clipAnimations = cleanSettings; cleanImporter.SaveAndReimport();
            var log = new StringBuilder("A = fresh body + fresh motion; B = current body + fresh motion; C = current body + current motion. All Foot IK off.\n");
            var preview = new PreviewRenderUtility();
            var objects = new GameObject[4];
            var graphs = new PlayableGraph[4];
            var playable = new AnimationClipPlayable[4];
            var animators = new Animator[4];
            var clips = new AnimationClip[4];
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Enemy/Art/Characters/Swordsman/Swordsman_Combat.mat");
                for (int i = 0; i < 4; i++)
                {
                    objects[i] = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(i == 0 ? Fresh : Current));
                    preview.AddSingleGO(objects[i]);
                    animators[i] = objects[i].GetComponent<Animator>();
                    animators[i].runtimeAnimatorController = null;
                    animators[i].applyRootMotion = false;
                    animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var renderer in objects[i].GetComponentsInChildren<SkinnedMeshRenderer>())
                    { renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; }
                    clips[i] = AssetDatabase.LoadAllAssetsAtPath(i == 3 ? clean : i == 2 ? Move : Fresh).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                    graphs[i] = PlayableGraph.Create("Avatar Audit " + i);
                    graphs[i].SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playable[i] = AnimationClipPlayable.Create(graphs[i], clips[i]);
                    playable[i].SetApplyFootIK(false);
                    playable[i].SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graphs[i], "Pose", animators[i]).SetSourcePlayable(playable[i]);
                    graphs[i].Play();
                    log.AppendLine($"{i}: humanScale={animators[i].humanScale}, valid={animators[i].avatar.isValid}, length={clips[i].length}");
                }
                preview.camera.fieldOfView = 32;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 100;
                preview.camera.clearFlags = CameraClearFlags.Color;
                preview.camera.backgroundColor = new Color(.16f,.18f,.21f);
                preview.ambientColor = Color.gray;
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(30,-25,0);
                preview.lights[1].intensity = .8f;
                float maxAB = 0, maxBC = 0, maxBD = 0;
                Directory.CreateDirectory("Logs/avatar-audit");
                for (int frame = 0; frame < 21; frame++)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        playable[i].SetTime(frame / 30.0);
                        graphs[i].Evaluate(0);
                        objects[i].transform.SetPositionAndRotation(new Vector3((i-1.5f)*1.4f,0,0),Quaternion.identity);
                        objects[i].transform.localScale = Vector3.one * (animators[1].humanScale / animators[i].humanScale);
                    }
                    foreach (var bone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                    {
                        Quaternion a = animators[0].GetBoneTransform(bone).rotation;
                        Quaternion b = animators[1].GetBoneTransform(bone).rotation;
                        Quaternion c = animators[2].GetBoneTransform(bone).rotation;
                        float ab = Quaternion.Angle(a,b), bc = Quaternion.Angle(b,c); float bd = Quaternion.Angle(b, animators[3].GetBoneTransform(bone).rotation); maxBD = Mathf.Max(maxBD,bd);
                        maxAB = Mathf.Max(maxAB, ab); maxBC = Mathf.Max(maxBC, bc);
                        log.AppendLine($"frame={frame} {bone} A-B={ab:F3}deg B-C={bc:F3}deg");
                    }
                    if (frame == 6 || frame == 10 || frame == 15)
                    {
                        preview.camera.transform.position = new Vector3(0,1.5f,8f);
                        preview.camera.transform.LookAt(new Vector3(0,.9f,0));
                        preview.BeginStaticPreview(new Rect(0,0,1500,750));
                        preview.Render(true);
                        var texture = preview.EndStaticPreview();
                        File.WriteAllBytes($"Logs/avatar-audit/front-{frame}.png",texture.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
                log.AppendLine($"MAX A-B={maxAB:F3}deg; MAX B-C={maxBC:F3}deg; MAX B-D(clean import)={maxBD:F3}deg");
                File.WriteAllText("Logs/avatar-audit/results.txt",log.ToString());
                Debug.Log("SWORDSMAN_AVATAR_AUDIT_COMPLETE " + log.ToString());
            }
            finally
            {
                foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy();
                foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
                preview.Cleanup();
            }
        }
    }
}

