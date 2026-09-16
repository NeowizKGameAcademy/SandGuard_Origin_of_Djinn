using UnityEngine;
using UnityEngine.Audio;

namespace SandGuard.Audio
{
    /// <summary>
    /// 소리 하나의 정의. 담당자가 인스펙터에서 편집하는 유일한 지점이다.
    /// 클립을 여기서 바꾸면 이 큐를 참조하는 모든 컴포넌트(SfxOneShot·SfxHitReaction·SfxEmitter·UI)가 함께 바뀐다.
    /// 볼륨은 "이 소리의 기본 크기", 분류 간 균형은 믹서 그룹에서 맞춘다.
    /// </summary>
    [CreateAssetMenu(menuName = "SandGuard/Audio/Sfx Cue", fileName = "NewCue")]
    public sealed class SfxCue : ScriptableObject
    {
        [Tooltip("변형 목록. 재생마다 하나를 고르며 직전 것은 피한다")]
        public AudioClip[] clips = new AudioClip[0];

        [Header("Level")]
        [Range(0f, 1f), Tooltip("기본 크기")] public float volume = 0.8f;
        [Range(0f, 12f), Tooltip("재생마다 ±이만큼 dB 랜덤")] public float volumeJitterDb = 1f;
        [Range(0.25f, 3f)] public float pitch = 1f;
        [Range(0f, 0.5f), Tooltip("재생마다 ±이만큼 피치 비율 랜덤 (0.05 = ±5%)")] public float pitchJitter = 0.04f;
        [Tooltip("믹서 그룹. 비우면 마스터로 나간다")] public AudioMixerGroup mixerGroup;
        [Range(0, 256), Tooltip("낮을수록 우선. 보이스가 부족할 때 Unity가 먼저 끊는 순서")] public int priority = 128;

        [Header("Space")]
        [Tooltip("켜면 위치가 있는 3D 소리, 끄면 UI·화면 효과 같은 2D 소리")] public bool spatial = true;
        [Min(0.1f), Tooltip("이 거리 안에서는 최대 크기")] public float minDistance = 2f;
        [Min(1f), Tooltip("이 거리 밖에서는 들리지 않는다")] public float maxDistance = 35f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        [Header("Playback")]
        [Tooltip("루프 소리(앰비언트·화염·폭풍). SfxEmitter가 켜져 있는 동안 반복한다")] public bool loop;
        [Min(1), Tooltip("같은 큐가 동시에 낼 수 있는 보이스 수. 초과하면 가장 오래된 것을 재사용한다")] public int maxVoices = 4;
        [Min(0f), Tooltip("이 시간 안의 재요청은 무시한다. 같은 프레임 중복은 항상 합친다")] public float minInterval = 0.02f;
        [Min(0f), Tooltip("루프 시작·정지 페이드(초)")] public float loopFade = 0.3f;

        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>직전 인덱스를 피해 하나 고른다. 변형이 하나면 그것.</summary>
        public AudioClip Pick(ref int lastIndex)
        {
            if (!HasClips) return null;
            int n = clips.Length;
            if (n == 1) { lastIndex = 0; return clips[0]; }
            int i = Random.Range(0, n - 1);
            if (i >= lastIndex) i++;
            lastIndex = i;
            return clips[i];
        }

        public float RandomVolume() => Mathf.Clamp01(volume * Mathf.Pow(10f, Random.Range(-volumeJitterDb, volumeJitterDb) / 20f));
        public float RandomPitch() => Mathf.Max(0.05f, pitch * (1f + Random.Range(-pitchJitter, pitchJitter)));
    }
}
