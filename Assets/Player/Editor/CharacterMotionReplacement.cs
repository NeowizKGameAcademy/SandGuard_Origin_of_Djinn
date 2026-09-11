using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SandGuard.Player.Editor
{
    public static class CharacterMotionReplacement
    {
        const string Library = "Assets/MotionLibrary/CharacterDownloads";
        [InitializeOnLoadMethod]
        static void PendingHammerUpdate()
        {
            const string request = "Library/HammerBrute.update-request";
            if (!File.Exists(request)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                { EditorApplication.delayCall += PendingHammerUpdate; return; }
                string action = File.ReadAllText(request).Trim();
                File.Delete(request);
                try { if (action == "walk-range") ValidateHammerWalkRange(); else UpdateHammerBruteSwing(); }
                catch (Exception ex) { File.WriteAllText("Logs/hammerbrute-motion-update-error.txt", ex.ToString()); Debug.LogException(ex); }
            };
        }
        sealed class Job
        {
            public string character, folder, name, target, body, source, library;
            public float previousLength;
        }
        static readonly string[] Enemies = { "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" };
        static readonly string[] Folders = { "bandit-minion", "bandit-ninja", "bandit-shielder", "bandit-hammerer", "bandit-leader" };
        static readonly string[][] Names = {
            new[] { "Sword And Shield Idle", "Sword And Shield Run", "Sword And Shield Slash", "Sword And Shield Death" },
            new[] { "Knife Idle", "Run", "Standing Torch Melee Attack Stab", "Standing React Death Left" },
            new[] { "Sword And Shield Block Idle", "Sword And Shield Walk", "Sword And Shield Slash", "Sword And Shield Death" },
            new[] { "Great Sword Idle", "Great Sword Walk", "Heavy Weapon Swing", "Two Handed Sword Death" },
            new[] { "Sword And Shield Idle", "Sword And Shield Walk", "Sword And Shield Slash", "Standing React Death Backward" }
        };

        public static string FindSource(string folder, string motion)
        {
            return Directory.GetFiles("Docs/model-art/" + folder, "*.fbx", SearchOption.TopDirectoryOnly)
                .Single(p => Path.GetFileNameWithoutExtension(p).Split('@').Last().Equals(motion, StringComparison.OrdinalIgnoreCase));
        }

        static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__"));

        [MenuItem("SandGuard/Animation Library/Update Player Running Advanced")]
        public static void UpdatePlayerRunningAdvanced()
        {
            string source = FindSource("protagonist", "Running advanced");
            const string staged = Library + "/Player/Running advanced.fbx";
            string target = ProtagonistArtBuilder.Art + "/Run.fbx";
            File.Copy(source, staged, true);
            AssetDatabase.ImportAsset(staged, ImportAssetOptions.ForceSynchronousImport);
            var fresh = (ModelImporter)AssetImporter.GetAtPath(staged);
            fresh.animationType = ModelImporterAnimationType.Human;
            fresh.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            fresh.materialImportMode = ModelImporterMaterialImportMode.None;
            fresh.animationCompression = ModelImporterAnimationCompression.Off;
            fresh.SaveAndReimport();
            var sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(staged).OfType<Avatar>().First();
            if (!sourceAvatar.isValid || !sourceAvatar.isHuman || !Clip(staged).humanMotion)
                throw new InvalidOperationException("Invalid Running advanced humanoid source");
            var importer = (ModelImporter)AssetImporter.GetAtPath(target);
            var settings = importer.clipAnimations[0];
            string guid = AssetDatabase.AssetPathToGUID(target);
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            importer = (ModelImporter)AssetImporter.GetAtPath(target);
            // Keep the established player reference rotations and the new source's bone lengths.
            var description = fresh.humanDescription;
            var common = ((ModelImporter)AssetImporter.GetAtPath(Library + "/Player/Standing Run Forward.fbx"))
                .humanDescription.skeleton.ToDictionary(b => b.name, b => b.rotation);
            var skeleton = description.skeleton;
            for (int i = 0; i < skeleton.Length; i++)
                if (common.TryGetValue(skeleton[i].name, out var rotation)) skeleton[i].rotation = rotation;
            description.skeleton = skeleton;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.humanDescription = description;
            var range = fresh.defaultClipAnimations[0];
            settings.takeName = range.takeName;
            settings.firstFrame = range.firstFrame; settings.lastFrame = range.lastFrame;
            settings.loopTime = settings.loopPose = true;
            settings.lockRootPositionXZ = false;
            importer.clipAnimations = new[] { settings };
            importer.SaveAndReimport();
            var clip = Clip(target);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProtagonistArtBuilder.Art + "/Protagonist.controller");
            var tree = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>()
                .Single(t => t.name == "Player Run Directions");
            if (AssetDatabase.AssetPathToGUID(target) != guid || !clip.humanMotion || !clip.isLooping
                || tree.children.First(c => c.position == Vector2.up).motion != clip
                || Mathf.Abs(clip.length - Clip(staged).length) > .001f)
                throw new InvalidOperationException("Running advanced import or locomotion link failed");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/player-running-advanced.txt", $"PASS: {source} -> {target}\nGUID preserved: {guid}\nHumanoid/loop/forward blend link valid. Full clip: {clip.length:F3}s ({range.firstFrame}..{range.lastFrame})\n");
            Debug.Log("PLAYER_RUNNING_ADVANCED_COMPLETE");
        }

        [MenuItem("SandGuard/Animation Library/Verify HammerBrute Walk Range")]
        public static void ValidateHammerWalkRange()
        {
            const string path = "Assets/Enemy/Art/Characters/HammerBrute/HammerBrute_Move.fbx";
            const string source = Library + "/HammerBrute/Great Sword Walk.fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            float length = Clip(path).length, expected = Clip(source).length;
            if (Mathf.Abs(length-expected) > .001f || importer.clipAnimations[0].lastFrame != 41)
                throw new InvalidOperationException($"Walk range mismatch: {length} vs source {expected}");
            float gap = ValidatePose(new Job { character = "HammerBrute", body = "Assets/Enemy/Art/Characters/HammerBrute/HammerBrute_Rig.fbx", target = path, library = source });
            File.WriteAllText("Logs/hammer-walk-range-verification.txt", $"PASS: destination={length:F6}s source={expected:F6}s, frames=0..41, 21 full-cycle pose samples, toe difference={gap:F3}deg\n");
            Debug.Log("HAMMER_WALK_RANGE_VERIFIED");
        }

        [MenuItem("SandGuard/Animation Library/Update HammerBrute Swing")]
        public static void UpdateHammerBruteSwing()
        {
            const string art = "Assets/Enemy/Art/Characters/HammerBrute/HammerBrute";
            var move = new Job { character = "HammerBrute", body = art + "_Rig.fbx", target = art + "_Move.fbx",
                library = Library + "/HammerBrute/Great Sword Walk.fbx" };
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(art + ".controller");
            var tree = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state.motion as BlendTree;
            var moveChild = tree.children.First(c => c.motion == Clip(move.target));
            bool identical = File.ReadAllBytes(FindSource("bandit-hammerer", "Great Sword Walk"))
                .SequenceEqual(File.ReadAllBytes(move.target));
            float footDifference = ValidatePose(move);
            var report = new StringBuilder($"Move source bytes identical: {identical}\nMove clip length: {Clip(move.target).length:F3}s\nController move rate: {moveChild.timeScale:F6}\nFull-speed cycle: {Clip(move.target).length / moveChild.timeScale:F3}s\nSource-body vs game-body toe direction max difference: {footDifference:F3}deg (21 samples, IK off)\n");
            if (!identical) throw new InvalidOperationException("Move differs from the supplied Great Sword Walk source");
            // Complete the requested movement comparison before replacing the attack.
            File.WriteAllText("Logs/hammerbrute-motion-update.txt", report.ToString());
            string source = FindSource("bandit-hammerer", "Heavy Weapon Swing");
            string staged = Library + "/HammerBrute/Heavy Weapon Swing.fbx";
            File.Copy(source, staged, true);
            AssetDatabase.ImportAsset(staged, ImportAssetOptions.ForceSynchronousImport);
            var fresh = (ModelImporter)AssetImporter.GetAtPath(staged);
            fresh.animationType = ModelImporterAnimationType.Human;
            fresh.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            fresh.importAnimation = true;
            fresh.materialImportMode = ModelImporterMaterialImportMode.None;
            fresh.animationCompression = ModelImporterAnimationCompression.Off;
            fresh.SaveAndReimport();
            string path = art + "_Attack.fbx";
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Attack").state;
            float oldDuration = Clip(path).length / state.speed;
            string guid = AssetDatabase.AssetPathToGUID(path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var settings = importer.clipAnimations[0];
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.humanDescription = fresh.humanDescription;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
            var range = fresh.defaultClipAnimations[0];
            if (Mathf.Abs(Clip(staged).length - 5.2f) > .01f || Mathf.Abs(range.lastFrame-range.firstFrame-156f) > .01f)
                throw new InvalidOperationException("Heavy Weapon Swing source range is not the verified full 156 frames");
            settings.takeName = range.takeName; settings.firstFrame = range.firstFrame; settings.lastFrame = range.lastFrame;
            settings.loopTime = settings.loopPose = false;
            importer.clipAnimations = new[] { settings }; importer.SaveAndReimport();
            state.motion = Clip(path); state.speed = Clip(path).length / oldDuration;
            foreach (var transition in state.transitions)
            {
                transition.exitTime = 1f; transition.hasExitTime = true;
                transition.duration = .05f;
                EditorUtility.SetDirty(transition);
            }
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            if (AssetDatabase.AssetPathToGUID(path) != guid || !Clip(path).humanMotion)
                throw new InvalidOperationException("Attack linkage invalid");
            report.AppendLine($"Attack source: {source}\nAttack clip: {Clip(path).length:F3}s\nAttack state rate: {state.speed:F6}\nAttack state duration preserved: {oldDuration:F3}s");
            report.AppendLine("Attack toe direction difference: " + ValidatePose(new Job { character = "HammerBrute", body = move.body, target = path, library = staged }).ToString("F3"));
            report.AppendLine("Attack max hand-to-handle gap: " + ValidateHammerGrip().ToString("F5") + "m (41 samples)");
            File.WriteAllText("Logs/hammerbrute-motion-update.txt", report.ToString());
            Debug.Log("HAMMERBRUTE_MOTION_UPDATE_COMPLETE\n" + report);
        }

        static float ValidateHammerGrip()
        {
            const string art = "Assets/Enemy/Art/Characters/HammerBrute/HammerBrute";
            var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(art + "_CombatVisual.prefab"));
            var graph = PlayableGraph.Create("Hammer Swing Grip Check");
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(obj);
                var animator = obj.GetComponentInChildren<Animator>();
                var grip = obj.GetComponentsInChildren<MonoBehaviour>().First(c => c.GetType().Name == "EnemyEquipmentGrip");
                var type = grip.GetType();
                type.GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(grip, null);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var clip = Clip(art + "_Attack.fbx");
                var motion = AnimationClipPlayable.Create(graph, clip);
                motion.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(motion); graph.Play();
                float gap = 0;
                for (int f=0; f<=40; f++)
                {
                    motion.SetTime(clip.length*f/40.0); graph.Evaluate(0);
                    type.GetMethod("ApplyPose").Invoke(grip,null);
                    foreach (var pair in new[] { ("rightPalm", "primaryGrip"), ("leftPalm", "supportGrip") })
                    {
                        var palm = (Transform)type.GetField(pair.Item1).GetValue(grip);
                        var handle = (Transform)type.GetField(pair.Item2).GetValue(grip);
                        if (palm == null || handle == null) throw new InvalidOperationException("Missing hammer grip");
                        gap = Mathf.Max(gap,Vector3.Distance(palm.position,handle.position));
                    }
                }
                if (gap > .035f) throw new InvalidOperationException("Hammer grip gap " + gap);
                return gap;
            }
            finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(obj); preview.Cleanup(); }
        }

        static bool validateOnly; public static void ValidateInstalled() { validateOnly = true; try { Run(); } finally { validateOnly = false; } }
        [MenuItem("SandGuard/Animation Library/Unify Player and Assassin Avatars")]
        public static void UnifyAvatars()
        {
            UnifyAvatarSources();
            ValidateInstalled();
        }

        static void UnifyAvatarSources()
        {
            var report = new StringBuilder();
            foreach (var pair in new[] {
                (ProtagonistArtBuilder.Art + "/Protagonist.fbx", Library + "/Player/Standing Run Forward.fbx"),
                ("Assets/Enemy/Art/Characters/Assassin/Assassin_Rig.fbx", Library + "/Assassin/Run.fbx") })
            {
                var target = (ModelImporter)AssetImporter.GetAtPath(pair.Item1);
                var source = (ModelImporter)AssetImporter.GetAtPath(pair.Item2);
                string bodyReference = "Assets/Player/Editor/AvatarAudit/Reference_" + Path.GetFileName(pair.Item1);
                if (!File.Exists(bodyReference)) File.Copy(pair.Item1, bodyReference);
                AssetDatabase.ImportAsset(bodyReference, ImportAssetOptions.ForceSynchronousImport);
                var referenceImporter = (ModelImporter)AssetImporter.GetAtPath(bodyReference);
                referenceImporter.animationType = ModelImporterAnimationType.Human;
                referenceImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                referenceImporter.importAnimation = false;
                referenceImporter.SaveAndReimport();
                var bodyDescription = referenceImporter.humanDescription;
                var commonRotations = source.humanDescription.skeleton.ToDictionary(b => b.name, b => b.rotation);
                var bodySkeleton = bodyDescription.skeleton;
                for (int i = 0; i < bodySkeleton.Length; i++)
                    if (commonRotations.TryGetValue(bodySkeleton[i].name, out var rotation)) bodySkeleton[i].rotation = rotation;
                bodyDescription.skeleton = bodySkeleton;
                target.humanDescription = bodyDescription;
                target.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(pair.Item1).OfType<Avatar>().First();
                if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Invalid shared avatar " + pair.Item1);
                var paths = Directory.GetFiles(Path.GetDirectoryName(pair.Item1), "*.fbx", SearchOption.AllDirectories)
                    .Select(p => p.Replace('\\', '/')).Where(p => p != pair.Item1 && !p.EndsWith("_Body.fbx"));
                foreach (string path in paths)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    string sourceFolder = Path.GetDirectoryName(pair.Item2);
                    string matched = Directory.GetFiles(sourceFolder, "*.fbx")
                        .First(p => File.ReadAllBytes(p).SequenceEqual(File.ReadAllBytes(path)));
                    var original = ((ModelImporter)AssetImporter.GetAtPath(matched)).humanDescription;
                    var rotations = source.humanDescription.skeleton.ToDictionary(b => b.name, b => b.rotation);
                    var skeleton = original.skeleton;
                    for (int i = 0; i < skeleton.Length; i++)
                        if (rotations.TryGetValue(skeleton[i].name, out var rotation)) skeleton[i].rotation = rotation;
                    original.skeleton = skeleton;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.sourceAvatar = null;
                    importer.humanDescription = original;
                    importer.SaveAndReimport();
                    if (!Clip(path).humanMotion)
                        throw new InvalidOperationException("Shared avatar failed: " + path);
                    report.AppendLine(path + " -> common reference rotations: " + pair.Item2 + " (own bone lengths)");
                }
            }
            File.WriteAllText("Logs/character-shared-avatars.txt", report.ToString());
            Debug.Log("CHARACTER_SHARED_AVATARS_COMPLETE");
        }
        [MenuItem("SandGuard/Animation Library/Apply Character Downloads")]
        public static void Run()
        {
            var jobs = new List<Job>();
            string player = ProtagonistArtBuilder.Art;
            void Add(string who, string folder, string motion, string target, string body)
            {
                jobs.Add(new Job { character = who, folder = folder, name = motion, target = target, body = body,
                    source = FindSource(folder, motion), library = Library + "/" + who + "/" + motion + ".fbx" });
            }
            foreach (var pair in new[] { ("Idle", "Protagonist"), ("Walk", "Walk"), ("Running advanced", "Run"),
                ("Running Forward Flip", "Jump"), ("Jumping", "CastingJump") })
                Add("Player", "protagonist", pair.Item1, player + "/" + pair.Item2 + ".fbx", player + "/Protagonist.fbx");
            foreach (string gait in new[] { "Walk", "Run" })
                foreach (string direction in new[] { "Forward", "Back", "Left", "Right" })
                    Add("Player", "protagonist", "Standing " + gait + " " + direction,
                        player + "/CombatAnimations/" + gait + direction + ".fbx", player + "/Protagonist.fbx");
            foreach (var pair in new[] { ("Standing 1H Magic Attack 01", "CastMagic"), ("Spell Cast", "CastGreatSword"),
                ("Sword And Shield Casting", "CastSwordShield"), ("Standing React Small From Front", "Hit"), ("Standing Death Backward 01", "Death") })
                Add("Player", "protagonist", pair.Item1, player + "/CombatAnimations/" + pair.Item2 + ".fbx", player + "/Protagonist.fbx");
            string[] roles = { "Idle", "Move", "Attack", "Death" };
            for (int i = 0; i < Enemies.Length; i++)
                for (int role = 0; role < roles.Length; role++)
                {
                    string art = "Assets/Enemy/Art/Characters/" + Enemies[i] + "/" + Enemies[i];
                    Add(Enemies[i], Folders[i], Names[i][role], art + "_" + roles[role] + ".fbx", art + "_Rig.fbx");
                }
            if (validateOnly)
            {
                File.WriteAllLines("Logs/character-motion-directions.txt", jobs.Select(j => j.character + "/" + Path.GetFileNameWithoutExtension(j.target) + ": " + ValidatePose(j).ToString("F2")));
                return;
            }
            // Resolve and validate the complete input set before changing live motion files.
            foreach (var job in jobs)
            {
                if (!File.Exists(job.target)) throw new InvalidOperationException("Missing destination " + job.target);
                job.previousLength = Clip(job.target).length;
                Directory.CreateDirectory(Path.GetDirectoryName(job.library));
                File.Copy(job.source, job.library, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var job in jobs)
            {
                var fresh = (ModelImporter)AssetImporter.GetAtPath(job.library);
                fresh.animationType = ModelImporterAnimationType.Human;
                fresh.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                fresh.sourceAvatar = null;
                fresh.importAnimation = true;
                fresh.materialImportMode = ModelImporterMaterialImportMode.None;
                fresh.animationCompression = ModelImporterAnimationCompression.Off;
                fresh.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(job.library).OfType<Avatar>().First();
                if (!avatar.isValid || !avatar.isHuman || !Clip(job.library).humanMotion)
                    throw new InvalidOperationException("Invalid downloaded motion " + job.source);
            }
            var report = new StringBuilder("# Character motion replacement\n\nAll sources are character-specific downloads. Existing FBX GUIDs and clip names preserved.\n\n");
            foreach (var job in jobs)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(job.target);
                var previous = importer.clipAnimations[0];
                string originalGuid = AssetDatabase.AssetPathToGUID(job.target);
                File.Copy(job.source, job.target, true);
                AssetDatabase.ImportAsset(job.target, ImportAssetOptions.ForceSynchronousImport);
                importer = (ModelImporter)AssetImporter.GetAtPath(job.target);
                var fresh = (ModelImporter)AssetImporter.GetAtPath(job.library);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                // Rebuild from the downloaded skeleton, not the old pack's serialized reference pose.
                importer.humanDescription = fresh.humanDescription;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
                // Existing destinations can still report the previous take's cached range.
                // Read the range from the independently imported new source instead.
                var sourceClip = fresh.defaultClipAnimations[0];
                previous.takeName = sourceClip.takeName;
                previous.firstFrame = sourceClip.firstFrame;
                previous.lastFrame = sourceClip.lastFrame;
                importer.clipAnimations = new[] { previous };
                importer.SaveAndReimport();
                if (AssetDatabase.AssetPathToGUID(job.target) != originalGuid || !Clip(job.target).humanMotion)
                    throw new InvalidOperationException("Motion link invalid " + job.target);
                report.AppendLine($"- {job.character} / {Path.GetFileNameWithoutExtension(job.target)} ← {job.source} ({job.previousLength:F2}s → {Clip(job.target).length:F2}s)");
            }
            // Keep the previous state duration for replacement attacks/reactions. Runtime damage timing is unchanged.
            foreach (var controllerPath in Enemies.Select(n => "Assets/Enemy/Art/Characters/" + n + "/" + n + ".controller")
                .Concat(new[] { player + "/Protagonist.controller" }))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                foreach (var layer in controller.layers)
                    foreach (var child in layer.stateMachine.states)
                    {
                        var state = child.state;
                        if (!(state.motion is AnimationClip clip) || state.timeParameterActive) continue;
                        var job = jobs.FirstOrDefault(j => j.target == AssetDatabase.GetAssetPath(clip));
                        if (job != null && job.previousLength > 0 && state.name != "Dash")
                        { state.speed *= clip.length / job.previousLength; EditorUtility.SetDirty(state); }
                    }
                EditorUtility.SetDirty(controller);
            }
            UnifyAvatarSources();
            AssetDatabase.SaveAssets();
            report.AppendLine("\n## Pose validation: maximum ankle-to-toe direction difference against source's own avatar (21 samples, IK off)\n");
            foreach (var job in jobs) report.AppendLine($"- {job.character} / {Path.GetFileNameWithoutExtension(job.target)}: {ValidatePose(job):F2} degrees");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/character-motion-replacement.md", report.ToString());
            Debug.Log("CHARACTER_MOTION_REPLACEMENT_COMPLETE " + jobs.Count);
        }

        static float ValidatePose(Job job)
        {
            var objects = new GameObject[2]; var graphs = new PlayableGraph[2];
            var animators = new Animator[2]; var playables = new AnimationClipPlayable[2];
            float max = 0; var preview = new PreviewRenderUtility();
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    objects[i] = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(i == 0 ? job.library : job.body));
                    objects[i].hideFlags = HideFlags.HideAndDontSave;
                    preview.AddSingleGO(objects[i]);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(job.character == "Player" ? ProtagonistArtBuilder.Art + "/Protagonist.mat" : "Assets/Enemy/Art/Characters/" + job.character + "/" + job.character + "_Combat.mat");
                    foreach (var renderer in objects[i].GetComponentsInChildren<SkinnedMeshRenderer>()) { renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; }
                    animators[i] = objects[i].GetComponent<Animator>();
                    animators[i].runtimeAnimatorController = null;
                    animators[i].applyRootMotion = false;
                    animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    graphs[i] = PlayableGraph.Create("Character Download Verification");
                    graphs[i].SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    // Same imported clip on its source body and the production body isolates retargeting.
                    playables[i] = AnimationClipPlayable.Create(graphs[i], Clip(job.target));
                    playables[i].SetApplyFootIK(false); playables[i].SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graphs[i], "Pose", animators[i]).SetSourcePlayable(playables[i]);
                    graphs[i].Play();
                }
                for (int f = 0; f < 21; f++)
                {
                    for (int i = 0; i < 2; i++)
                    { playables[i].SetTime(Clip(job.target).length * f / 21.0); graphs[i].Evaluate(0); objects[i].transform.rotation = Quaternion.identity; }
                    foreach (var bone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                    {
                        var a = animators[0].GetBoneTransform(bone); var b = animators[1].GetBoneTransform(bone);
                        if (a == null || b == null || !float.IsFinite(b.position.y)) throw new InvalidOperationException("Invalid foot " + job.target);
                        var toe = bone == HumanBodyBones.LeftFoot ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes; var ta = animators[0].GetBoneTransform(toe); var tb = animators[1].GetBoneTransform(toe); if (ta == null || tb == null) throw new InvalidOperationException("Missing toes"); max = Mathf.Max(max, Vector3.Angle(ta.position-a.position, tb.position-b.position));
                    }
                }
                if (job.target.EndsWith("_Move.fbx") || job.target.EndsWith("RunForward.fbx"))
                {
                    for (int i=0;i<2;i++) { playables[i].SetTime(Clip(job.target).length * .45); graphs[i].Evaluate(0); objects[i].transform.SetPositionAndRotation(new Vector3(i==0 ? .75f : -.75f,0,0),Quaternion.identity); }
                    objects[0].transform.localScale = Vector3.one * (animators[1].humanScale/animators[0].humanScale);
                    preview.camera.fieldOfView=32; preview.camera.nearClipPlane=.01f; preview.camera.farClipPlane=100;
                    preview.camera.transform.position=new Vector3(0,1.3f,5.8f); preview.camera.transform.LookAt(new Vector3(0,.9f,0));
                    preview.camera.clearFlags=CameraClearFlags.Color; preview.camera.backgroundColor=new Color(.16f,.18f,.21f);
                    preview.ambientColor=Color.gray; preview.lights[0].intensity=1.3f; preview.lights[0].transform.rotation=Quaternion.Euler(30,-25,0); preview.lights[1].intensity=.8f;
                    preview.BeginStaticPreview(new Rect(0,0,1000,700)); preview.Render(true); var texture=preview.EndStaticPreview();
                    File.WriteAllBytes("Logs/" + job.character + "-replacement.png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                }
                return max;
            }
            finally
            {
                foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy();
                foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj); preview.Cleanup();
            }
        }
    }
}



