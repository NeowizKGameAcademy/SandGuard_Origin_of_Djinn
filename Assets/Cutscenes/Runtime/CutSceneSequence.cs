using System;
using UnityEngine;

namespace SandGuard.Cutscenes
{
    [CreateAssetMenu(menuName = "SandGuard/Cutscene Sequence", fileName = "CutSceneSequence")]
    public sealed class CutSceneSequence : ScriptableObject
    {
        [Serializable]
        public sealed class Cut
        {
            public string title;
            [TextArea(6, 16)] public string body;
            public Sprite image;
            [Min(1f)] public float duration = 14f;
            [Range(0.15f, 0.85f)] public float panelX = 0.72f;
            [Range(0.15f, 0.85f)] public float panelY = 0.5f;
        }

        public Cut[] cuts = Array.Empty<Cut>();
        public string storyId = "Prologue";
        public bool playOnceAutomatically = true;
        public string nextScene = "MainScene";
        [Min(0f)] public float fadeDuration = 0.6f;
    }
}
