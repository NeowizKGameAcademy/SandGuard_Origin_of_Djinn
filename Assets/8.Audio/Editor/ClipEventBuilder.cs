using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Audio.Editor
{
    /// <summary>
    /// FBX 클립 임포터에 애니메이션 이벤트를 심는다. 우리가 쓰는 함수 이름(Swing·Impact·BodyFall·Footstep)만 갈아 끼우고
    /// 다른 이벤트(ReleaseChiefBomb 등)는 보존한다. 이벤트 시각은 임포터 규격대로 0~1 정규화다.
    /// 클립을 다시 임포트하는 빌더(EnemyCombatArtBuilder, PlayerAnimationPackBuilder)가 돌면 이벤트가 사라지므로 그 뒤에 다시 실행한다.
    /// </summary>
    public static class ClipEventBuilder
    {
        public static readonly string[] OurFunctions = { "Swing", "Impact", "BodyFall", "Footstep" };

        /// <summary>FBX의 첫 (미리보기 아닌) 클립 에셋.</summary>
        public static AnimationClip LoadClip(string fbxPath)
            => AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

        /// <summary>events: (초, 함수 이름). 초는 잘라낸 클립 기준. 변경이 있으면 재임포트하고 true.</summary>
        public static bool SetEvents(string fbxPath, params (float seconds, string function)[] events)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            var clipAsset = LoadClip(fbxPath);
            if (importer == null || clipAsset == null) { Debug.LogWarning($"[Audio] 클립을 찾지 못했다: {fbxPath}"); return false; }
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips.Length == 0) return false;
            var clip = clips[0];
            float length = Mathf.Max(0.001f, clipAsset.length);

            var kept = (clip.events ?? new AnimationEvent[0]).Where(e => !OurFunctions.Contains(e.functionName)).ToList();
            var wanted = events.Where(e => !string.IsNullOrEmpty(e.function))
                .Select(e => new AnimationEvent { time = Mathf.Clamp01(e.seconds / length), functionName = e.function }).ToList();
            var merged = kept.Concat(wanted).OrderBy(e => e.time).ToArray();

            bool same = clip.events != null && clip.events.Length == merged.Length
                        && clip.events.Zip(merged, (a, b) => a.functionName == b.functionName && Mathf.Abs(a.time - b.time) < 0.0005f).All(x => x);
            if (same) return false;
            clip.events = merged;
            clips[0] = clip;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// 쓰러지는 클립에서 몸(엉덩이 뼈)이 바닥에 닿는 시각(초)을 찾는다. 서 있을 때 높이와 최저 높이 사이 15% 지점을 처음 지나는 시각.
        /// 낙차가 0.2m 미만이면 쓰러지는 클립이 아니라고 보고 null.
        /// </summary>
        public static float? DetectBodyFallSeconds(GameObject rigPrefab, AnimationClip clip)
        {
            if (rigPrefab == null || clip == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            try
            {
                go.hideFlags = HideFlags.HideAndDontSave;
                var animator = go.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman) return null;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips == null) return null;
                var target = animator.gameObject;
                var samples = new List<(float t, float h)>();
                AnimationMode.StartAnimationMode();
                try
                {
                    for (float t = 0f; t <= clip.length; t += 1f / 30f)
                    {
                        AnimationMode.SampleAnimationClip(target, clip, t);
                        samples.Add((t, hips.position.y - target.transform.position.y));
                    }
                }
                finally { AnimationMode.StopAnimationMode(); }
                if (samples.Count < 3) return null;
                float rest = samples[0].h, min = samples.Min(s => s.h);
                if (rest - min < 0.2f) return null;
                float threshold = min + 0.15f * (rest - min);
                foreach (var s in samples) if (s.h <= threshold) return s.t;
                return null;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
