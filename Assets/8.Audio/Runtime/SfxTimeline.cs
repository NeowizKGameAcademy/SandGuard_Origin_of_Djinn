using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 켜진 뒤 정해진 시각에 큐를 차례로 낸다. 단계가 코드 상수로 정해진 연출(코어 파괴: 충전 0초 → 폭발 0.2초 → 파편 1.1초)용.
    /// 꺼지면 남은 항목을 버린다. 풀에서 다시 켜지면 처음부터.
    /// </summary>
    public sealed class SfxTimeline : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Min(0f)] public float Delay;
            public SfxCue Cue;
        }

        public List<Entry> Entries = new List<Entry>();
        [Tooltip("재생 위치. 비우면 이 오브젝트")] public Transform Anchor;

        public int Fired { get; private set; }
        float startedAt; int next;

        void OnEnable() { startedAt = Time.time; next = 0; Fired = 0; }

        void Update()
        {
            while (next < Entries.Count && Time.time - startedAt >= Entries[next].Delay)
            {
                var e = Entries[next++];
                Fired++;
                if (e.Cue == null) continue;
                var at = Anchor != null ? Anchor : transform;
                if (e.Cue.spatial) SfxPlayer.Play(e.Cue, at.position); else SfxPlayer.Play2D(e.Cue);
            }
        }
    }
}
