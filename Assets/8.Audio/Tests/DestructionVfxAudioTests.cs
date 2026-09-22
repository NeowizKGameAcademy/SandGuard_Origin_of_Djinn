using System.Collections;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Audio.Tests
{
    public sealed class DestructionVfxAudioTests
    {
        GameObject effect;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (effect) Object.Destroy(effect);
            if (SfxPlayer.Exists) Object.Destroy(SfxPlayer.Instance.gameObject);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator AllDestructionEffectsPlayFinalClipOnceAndReplayAfterPooling()
        {
            foreach (var kind in new[] { "Core", "Cobra", "Obelisk", "Anubis", "Coffin" })
            {
                var prefab = Resources.Load<GameObject>("VFX/Prefabs/VFX_" + kind + "_Destruction");
                Assert.IsNotNull(prefab, kind);
                effect = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
                var timelines = effect.GetComponentsInChildren<SfxTimeline>(true);
                Assert.AreEqual(1, timelines.Length, kind);
                var timeline = timelines[0];
                Assert.AreEqual(1, timeline.Entries.Count, kind);
                Assert.AreEqual(0f, timeline.Entries[0].Delay);
                var cue = timeline.Entries[0].Cue;
                Assert.AreEqual(1, cue.clips.Length);
                Assert.AreEqual("Tower_Destruction_Final", cue.clips[0].name);
                Assert.AreEqual(0f, cue.pitchJitter);
                yield return null;
                yield return null;
                Assert.AreEqual(1, timeline.Fired, kind);
                Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(cue), kind);
                Assert.AreSame(cue.clips[0], SfxPlayer.Instance.ActiveVoices(cue)[0].Source.clip);
                PrefabPool.Release(effect);
                yield return null;
                effect = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
                Assert.AreEqual(0, effect.GetComponentInChildren<SfxTimeline>().Fired);
                yield return null;
                yield return null;
                Assert.AreEqual(1, effect.GetComponentInChildren<SfxTimeline>().Fired);
                Assert.AreEqual(2, SfxPlayer.Instance.ActiveVoiceCountFor(cue), kind + " replay and previous tail");
                PrefabPool.Release(effect);
                effect = null;
                Object.Destroy(SfxPlayer.Instance.gameObject);
                yield return null;
            }
        }
    }
}
