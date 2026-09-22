#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SandGuard.Cutscenes.Editor
{
    public static class CutsceneAudioSetup
    {
        const string Root = "Assets/Cutscenes/";

        [MenuItem("SandGuard/Cutscenes/Assign Sequence Music")]
        public static void Assign()
        {
            Assign("PrologueSequence.asset", "Audio/1.forgotten history.mp3");
            Assign("IntroSequence.asset", "Audio/2.Crystal Desert.mp3");
            Assign("EndingSequence.asset", "Audio/3.The Eternal Wait.mp3");
            AssetDatabase.SaveAssets();
            Debug.Log("CUTSCENE_AUDIO_ASSIGN_PASS");
        }

        [MenuItem("SandGuard/Cutscenes/Validate Sequence Music")]
        public static void Validate()
        {
            Validate("PrologueSequence.asset", "1.forgotten history");
            Validate("IntroSequence.asset", "2.Crystal Desert");
            Validate("EndingSequence.asset", "3.The Eternal Wait");
            Debug.Log("CUTSCENE_AUDIO_VALIDATION_PASS");
        }

        static void Assign(string sequenceName, string audioName)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(Root + sequenceName);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + audioName);
            if (!sequence || !clip) throw new System.InvalidOperationException($"Missing sequence or audio: {sequenceName}, {audioName}");
            sequence.backgroundMusic = clip;
            sequence.musicVolume = .55f;
            sequence.loopMusic = true;
            EditorUtility.SetDirty(sequence);
        }

        static void Validate(string sequenceName, string expectedClip)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(Root + sequenceName);
            if (!sequence || !sequence.backgroundMusic || sequence.backgroundMusic.name != expectedClip)
                throw new System.InvalidOperationException($"Invalid music assignment: {sequenceName}");
        }
    }
}
#endif
