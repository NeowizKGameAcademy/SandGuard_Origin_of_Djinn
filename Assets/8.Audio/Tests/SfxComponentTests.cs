using System.Collections;
using System.Collections.Generic;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Audio.Tests
{
    /// <summary>연결 컴포넌트: 루프 토글, 발소리, 파티클 추종 이미터, 배경음 전환.</summary>
    public sealed class SfxComponentTests
    {
        readonly List<Object> objects = new List<Object>();
        T Track<T>(T value) where T : Object { objects.Add(value); return value; }

        SfxCue MakeCue(bool spatial = true, bool loop = false, float length = 0.1f)
        {
            var cue = Track(ScriptableObject.CreateInstance<SfxCue>());
            cue.clips = new[] { Track(AudioClip.Create("clip", Mathf.Max(48, (int)(48000 * length)), 1, 48000, false)) };
            cue.spatial = spatial; cue.loop = loop; cue.loopFade = 0f; cue.minInterval = 0f;
            return cue;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (SfxPlayer.Exists) Object.Destroy(SfxPlayer.Instance.gameObject);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            foreach (var o in objects) if (o != null) Object.Destroy(o);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator LoopToggleBeginsRaisesPitchAndEnds()
        {
            var loop = MakeCue(loop: true); var end = MakeCue();
            var go = Track(new GameObject("Charge"));
            var toggle = go.AddComponent<SfxLoopToggle>();
            toggle.LoopCue = loop; toggle.EndCue = end;
            toggle.Begin();
            Assert.IsTrue(toggle.Running);
            toggle.SetIntensity(1f);
            Assert.AreEqual(loop.pitch * 1.5f, toggle.Voice.Source.pitch, 0.001f, "세기 1이면 피치 +50%");
            toggle.End();
            Assert.IsFalse(toggle.Running);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(end), "끝날 때 원샷");
            yield return null;
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(loop));
        }

        [UnityTest] public IEnumerator FootstepsCountStrides()
        {
            var cue = MakeCue();
            var go = Track(new GameObject("Walker"));
            var steps = go.AddComponent<SfxFootsteps>();
            steps.Cue = cue; steps.Stride = 0.5f; steps.RequireGround = false; steps.MinSpeed = 0.1f;
            yield return null;
            for (int i = 0; i < 20; i++) { go.transform.position += new Vector3(0.15f, 0, 0); yield return null; }
            // 3m 이동 / 0.5m = 6걸음 (첫 프레임 오차 허용)
            Assert.GreaterOrEqual(steps.Steps, 5);
            Assert.LessOrEqual(steps.Steps, 6);
        }

        [UnityTest] public IEnumerator FootstepsIgnoreSlowMovement()
        {
            var cue = MakeCue();
            var go = Track(new GameObject("Idle"));
            var steps = go.AddComponent<SfxFootsteps>();
            steps.Cue = cue; steps.Stride = 0.3f; steps.RequireGround = false; steps.MinSpeed = 2f;
            // 프레임 시간에 맞춰 MinSpeed의 절반 속도로만 움직인다 (배치 모드는 프레임이 매우 짧다)
            for (int i = 0; i < 10; i++) { go.transform.position += new Vector3(steps.MinSpeed * 0.5f * Time.deltaTime, 0, 0); yield return null; }
            Assert.AreEqual(0, steps.Steps);
        }

        [UnityTest] public IEnumerator EmitterFollowsParticleEmission()
        {
            var cue = MakeCue(loop: true);
            var vfx = Track(new GameObject("Flame"));
            var ps = vfx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // AddComponent 직후 재생 중이라 먼저 멈춘다
            var main = ps.main; main.playOnAwake = false; main.loop = true;
            var holder = new GameObject("Sfx"); holder.transform.SetParent(vfx.transform, false);
            holder.SetActive(false);
            var emitter = holder.AddComponent<SfxEmitter>();
            emitter.Cue = cue; emitter.Trigger = SfxEmitterTrigger.WhileParticlesEmit;
            holder.SetActive(true);
            yield return null;
            Assert.IsNull(emitter.Voice, "방출 전에는 조용");
            ps.Play(true);
            yield return null; yield return null;
            Assert.IsNotNull(emitter.Voice, "방출 시작 → 루프 시작");
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return null; yield return null;
            Assert.IsNull(emitter.Voice, "방출 정지 → 루프 정지");
        }

        [UnityTest] public IEnumerator MusicDirectorPlaysMenuThenPreparationWithoutDirector()
        {
            var menu = MakeCue(spatial: false, loop: true, length: 1f);
            var prep = MakeCue(spatial: false, loop: true, length: 1f);
            var go = Track(new GameObject("Music"));
            go.SetActive(false);
            var m = go.AddComponent<MusicDirector>();
            m.Menu = menu; m.Preparation = prep; m.MenuMode = true; m.Crossfade = 0.1f;
            go.SetActive(true);
            yield return null;
            Assert.AreEqual(menu, m.Current);
            Assert.AreEqual(menu.clips[0], m.Front.clip);
            Assert.IsTrue(m.Front.loop);
            m.MenuMode = false;
            yield return null;
            Assert.AreEqual(prep, m.Current, "WaveDirector가 없으면 준비 테마");
            Assert.AreEqual(prep.clips[0], m.Front.clip);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(prep.volume, m.Front.volume, 0.01f, "크로스페이드 완료");
        }
    }
}
