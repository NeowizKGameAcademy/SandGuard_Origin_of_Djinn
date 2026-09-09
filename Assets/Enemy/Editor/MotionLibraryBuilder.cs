using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Enemy.Editor
{
    /// <summary>Imports the local character motion collection without changing gameplay controllers.</summary>
    public static class MotionLibraryBuilder
    {
        public const string Folder = "Assets/MotionLibrary";
        const string Source = "Docs/model-art";
        const string Request = "Library/MotionLibrary.import-request";
        [InitializeOnLoadMethod]
        static void CheckPendingImport()
        {
            if (!File.Exists(Request)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                { EditorApplication.delayCall += CheckPendingImport; return; }
                File.Delete(Request);
                try { Build(); }
                catch (Exception error) { File.WriteAllText("Logs/motion-library-status.txt", "FAILED\n" + error); Debug.LogException(error); }
            };
        }
        [Serializable] public sealed class Entry
        {
            public string source, asset, sha256;
            public string[] aliases;
            public string[] clips;
            public float[] seconds;
        }
        [Serializable] sealed class Manifest { public List<Entry> motions = new List<Entry>(); public List<string> skipped = new List<string>(); }

        [MenuItem("SandGuard/Animation Library/Import All Local Motions")]
        public static void Build()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/motion-library-status.txt", "IMPORTING");
            Directory.CreateDirectory(Folder);
            var sources = new List<string>();
            foreach (string pack in new[] { "Pro Magic Pack", "Pro Sword and Shield Pack", "Great Sword Pack", "Pro Melee Axe Pack" })
                sources.AddRange(Directory.GetFiles(Source + "/" + pack, "*.fbx", SearchOption.AllDirectories)
                    .Where(p => !Path.GetFileName(p).Equals("bandit1.fbx", StringComparison.OrdinalIgnoreCase)));
            sources.AddRange(Directory.GetFiles(Source, "*.fbx", SearchOption.TopDirectoryOnly));
            foreach (string character in new[] { "protagonist", "bandit-minion", "bandit-ninja", "bandit-shielder", "bandit-hammerer", "bandit-leader" })
                sources.AddRange(Directory.GetFiles(Source + "/" + character, "*@*.fbx", SearchOption.AllDirectories));
            // Include every selected variant too; byte-identical source copies are listed as aliases.
            sources.AddRange(Directory.GetFiles(Source + "/animation-selection-v1", "*.fbx", SearchOption.AllDirectories));
            var manifest = new Manifest();
            var byHash = new Dictionary<string, Entry>();
            try
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    string source = sources[i].Replace('\\', '/');
                    string relative = source.Substring(Source.Length + 1);
                    EditorUtility.DisplayProgressBar("Import motion library", relative, (float)i / sources.Count);
                    string hash = Hash(source);
                    if (byHash.TryGetValue(hash, out Entry duplicate))
                    {
                        duplicate.aliases = duplicate.aliases.Concat(new[] { relative }).ToArray();
                        continue;
                    }
                    string destination = Folder + "/" + (relative.Contains("/") ? relative : "Standalone/" + relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    bool fresh = !File.Exists(destination);
                    if (fresh || Hash(destination) != hash) { File.Copy(source, destination, true); fresh = true; }
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(destination);
                    if (fresh || importer.animationType != ModelImporterAnimationType.Human || !importer.importAnimation)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        importer.globalScale = 1f; importer.useFileScale = true;
                        importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
                        importer.materialImportMode = ModelImporterMaterialImportMode.None;
                        importer.animationCompression = ModelImporterAnimationCompression.Off;
                        importer.SaveAndReimport();
                        var definitions = importer.defaultClipAnimations;
                        if (definitions.Length == 0) { manifest.skipped.Add(relative + " (no animation)"); continue; }
                        foreach (var definition in definitions)
                        {
                            definition.name = Path.GetFileNameWithoutExtension(source) + (definitions.Length > 1 ? " - " + definition.name : "");
                            definition.loopTime = false; definition.loopPose = false;
                            definition.lockRootRotation = true; definition.keepOriginalOrientation = true;
                            definition.lockRootHeightY = true; definition.keepOriginalPositionY = true;
                            definition.lockRootPositionXZ = false; definition.keepOriginalPositionXZ = true;
                        }
                        importer.clipAnimations = definitions;
                        importer.SaveAndReimport();
                    }
                    var assets = AssetDatabase.LoadAllAssetsAtPath(destination);
                    var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                    if (clips.Length == 0) { manifest.skipped.Add(relative + " (no animation)"); continue; }
                    var avatar = assets.OfType<Avatar>().FirstOrDefault();
                    if (avatar == null || !avatar.isValid || !avatar.isHuman || clips.Any(c => !c.humanMotion || c.length <= 0))
                        throw new InvalidOperationException("Motion is not a valid Humanoid animation: " + relative);
                    var entry = new Entry { source = relative, asset = destination, sha256 = hash, aliases = new string[0],
                        clips = clips.Select(c => c.name).ToArray(), seconds = clips.Select(c => c.length).ToArray() };
                    byHash.Add(hash, entry); manifest.motions.Add(entry);
                    Debug.Log("MOTION_IMPORTED " + manifest.motions.Count + " " + relative);
                    File.WriteAllText("Logs/motion-library-status.txt", "IMPORTING " + manifest.motions.Count + " " + relative);
                }
                File.WriteAllText(Folder + "/manifest.json", JsonUtility.ToJson(manifest, true));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                Debug.Log("MOTION_LIBRARY_COMPLETE files=" + manifest.motions.Count + " clips=" + manifest.motions.Sum(e => e.clips.Length)
                    + " aliases=" + manifest.motions.Sum(e => e.aliases.Length) + " skipped=" + manifest.skipped.Count);
                File.WriteAllText("Logs/motion-library-status.txt", "COMPLETE files=" + manifest.motions.Count + " clips=" + manifest.motions.Sum(e => e.clips.Length)
                    + " aliases=" + manifest.motions.Sum(e => e.aliases.Length) + " skipped=" + manifest.skipped.Count);
                if (!Application.isBatchMode) Open();
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [MenuItem("SandGuard/Animation Library/Open Library")]
        public static void Open()
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Folder);
            Selection.activeObject = folder; EditorGUIUtility.PingObject(folder);
            EditorUtility.FocusProjectWindow();
        }
    }
}
