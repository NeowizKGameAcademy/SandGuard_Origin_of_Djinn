using SandGuard.Audio;
using UnityEditor;
using UnityEngine;

namespace DesertTower.VFX.Editor
{
    public static class DestructionAudioSetup
    {
        public const string ClipPath = "Assets/8.Audio/AudioResource/Tower_Destruction_Final.mp3";
        public const string CuePath = "Assets/8.Audio/Cues/Facility/Facility_Destruction_Final.asset";
        public static readonly string[] Kinds = { "Core", "Cobra", "Obelisk", "Anubis", "Coffin" };

        [MenuItem("DesertTower/VFX/Wire Final Destruction Audio")]
        public static void WireAll()
        {
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(ClipPath);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (!clip) throw new System.InvalidOperationException("Final destruction clip missing");
            var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(CuePath);
            if (!cue) { cue = ScriptableObject.CreateInstance<SfxCue>(); AssetDatabase.CreateAsset(cue, CuePath); }
            var existing = AssetDatabase.LoadAssetAtPath<SfxCue>("Assets/8.Audio/Cues/Facility/Facility_Cobra_Destroy.asset");
            cue.clips = new[] { clip }; cue.volume = 0.8f;
            cue.pitch = 1f; cue.pitchJitter = 0f; cue.volumeJitterDb = 0f;
            cue.spatial = true; cue.minDistance = 6f; cue.maxDistance = 70f;
            cue.maxVoices = 8; cue.minInterval = 0f; cue.loop = false; cue.chance = 1f;
            if (existing) cue.mixerGroup = existing.mixerGroup;
            EditorUtility.SetDirty(cue); AssetDatabase.SaveAssets();
            foreach (string kind in Kinds)
            {
                string path = "Assets/Resources/VFX/Prefabs/VFX_" + kind + "_Destruction.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"DESTRUCTION_AUDIO_READY: 5 prefabs, delay 0, clip {clip.length:F3}s, fixed pitch, no duplicate legacy timelines.");
        }

        // Called by the visual builders too, so rebuilding VFX retains the selected final mix.
        public static void Apply(GameObject root)
        {
            var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(CuePath);
            if (!cue) return;
            foreach (var timeline in root.GetComponentsInChildren<SfxTimeline>(true)) Object.DestroyImmediate(timeline);
            var holder = root.transform.Find("Sfx");
            if (!holder) { holder = new GameObject("Sfx").transform; holder.SetParent(root.transform, false); }
            foreach (var emitter in holder.GetComponents<SfxEmitter>()) Object.DestroyImmediate(emitter);
            var playback = holder.gameObject.AddComponent<SfxTimeline>();
            playback.Entries.Add(new SfxTimeline.Entry { Delay = 0f, Cue = cue });
        }
    }
}
