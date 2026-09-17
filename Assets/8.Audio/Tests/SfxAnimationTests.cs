using System.Collections;
using System.Collections.Generic;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Audio.Tests
{
    /// <summary>애니메이션 동기 컴포넌트: 접지 감지기, 애니메이터 루프 핑, 상태 진입/이탈, 클립 이벤트 수신, 타임라인, 이미터 시작·끝 큐.</summary>
    public sealed class SfxAnimationTests
    {
        readonly List<Object> objects = new List<Object>();
        T Track<T>(T value) where T : Object { objects.Add(value); return value; }

        SfxCue MakeCue(bool spatial = true, bool loop = false)
        {
            var cue = Track(ScriptableObject.CreateInstance<SfxCue>());
            cue.clips = new[] { Track(AudioClip.Create("clip", 4800, 1, 48000, false)) };
            cue.spatial = spatial; cue.loop = loop; cue.loopFade = 0f; cue.minInterval = 0f;
            return cue;
        }

        /// <summary>테스트 컨트롤러(Setup Everything이 만든 Resources 에셋)를 단 Animator. 상태: Ground 0.2초 / Idle 0.1초 / Falling 1초 / Attack 0.3초(Swing@0.1).</summary>
        Animator MakeAnimator(string stateName, System.Action<GameObject> before = null)
        {
            var assets = Resources.Load<SfxTestAssets>("SfxTestAssets");
            Assert.IsNotNull(assets, "Assets/8.Audio/Tests/Resources/SfxTestAssets.asset이 없다. SandGuard > Audio > Create Test Animator Assets");
            var go = Track(new GameObject("Animated"));
            before?.Invoke(go); // 이벤트 수신 컴포넌트는 Animator가 초기화되기 전에 있어야 한다 (프리팹에서는 항상 그렇다)
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.controller;
            animator.Play(stateName, 0, 0f);
            return animator;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (SfxPlayer.Exists) Object.Destroy(SfxPlayer.Instance.gameObject);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            foreach (var o in objects) if (o != null) Object.Destroy(o);
            objects.Clear();
            yield return null;
        }

        [Test] public void FootPlantDetectorFiresOncePerLiftAndPlant()
        {
            var d = new FootPlantDetector { LiftHeight = 0.08f, PlantHeight = 0.03f };
            int steps = 0;
            foreach (var h in new[] { 0.10f, 0.10f, 0.15f, 0.25f, 0.20f, 0.12f, 0.11f, 0.10f, 0.22f, 0.11f })
                if (d.Update(h)) steps++;
            Assert.AreEqual(2, steps);
        }

        [Test] public void FootPlantDetectorIgnoresSmallWobble()
        {
            var d = new FootPlantDetector { LiftHeight = 0.08f, PlantHeight = 0.03f };
            int steps = 0;
            foreach (var h in new[] { 0.10f, 0.13f, 0.11f, 0.14f, 0.10f, 0.15f, 0.10f }) if (d.Update(h)) steps++;
            Assert.AreEqual(0, steps, "들림 임계값보다 작은 흔들림은 걸음이 아니다");
        }

        [UnityTest] public IEnumerator AnimatorLoopPingsOncePerCycle()
        {
            var cue = MakeCue(spatial: false);
            var animator = MakeAnimator("Ground");
            var loop = animator.gameObject.AddComponent<SfxAnimatorLoop>();
            loop.Cue = cue; loop.StateName = "Ground";
            yield return new WaitForSeconds(0.72f);
            // 0.2초 루프: 시작 1회 + 감김 3회 = 4 (프레임 경계 오차 ±1)
            Assert.GreaterOrEqual(loop.Fired, 3);
            Assert.LessOrEqual(loop.Fired, 5);
            Assert.AreEqual(loop.Fired, SfxPlayer.Instance.FlatPlays);
        }

        [UnityTest] public IEnumerator AnimatorLoopIgnoresOtherStates()
        {
            var cue = MakeCue(spatial: false);
            var animator = MakeAnimator("Idle");
            var loop = animator.gameObject.AddComponent<SfxAnimatorLoop>();
            loop.Cue = cue; loop.StateName = "Ground";
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(0, loop.Fired);
        }

        [UnityTest] public IEnumerator AnimatorStateStartsLoopOnEnterAndStopsOnDisable()
        {
            var loopCue = MakeCue(spatial: false, loop: true);
            var animator = MakeAnimator("Falling");
            var state = animator.gameObject.AddComponent<SfxAnimatorState>();
            state.StateName = "Falling"; state.LoopCue = loopCue; state.MinDwell = 0f;
            yield return null; yield return null;
            Assert.IsTrue(state.Active);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(loopCue));
            animator.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse(state.Active);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(loopCue));
        }

        /// <summary>주의: Unity는 -nographics 배치 모드에서 애니메이션 클립 이벤트를 보내지 않는다. 이 테스트는 -batchmode만으로(그래픽 장치 포함) 돌린다.</summary>
        [UnityTest] public IEnumerator AnimationEventsComponentReceivesClipEvent()
        {
            var swing = MakeCue();
            SfxAnimationEvents events = null;
            var animator = MakeAnimator("Attack", go => { events = go.AddComponent<SfxAnimationEvents>(); events.SwingCue = swing; });
            var trace = new System.Text.StringBuilder();
            foreach (var c in animator.runtimeAnimatorController.animationClips) trace.Append($"clip {c.name} len {c.length:0.00} events {c.events.Length}; ");
            float until = Time.time + 0.25f; int frames = 0;
            while (Time.time < until)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (frames++ % 50 == 0) trace.Append($"[f{frames} {(info.IsName("Attack") ? "Attack" : "other")} t={info.normalizedTime:0.00} swing={events.SwingCount}] ");
                yield return null;
            }
            Assert.AreEqual(1, events.SwingCount, "Attack 클립 0.1초의 Swing 이벤트. " + trace);
            // 0.1초 클립은 0.25초 안에 이미 끝나 반납되므로 '보이스를 빌렸다'는 사실만 확인한다
            Assert.IsTrue(PrefabPool.Exists && PrefabPool.Instance.CreatedCount >= 1, "휘두름 큐가 보이스로 재생됐다");
        }

        [UnityTest] public IEnumerator TimelineFiresEntriesInOrder()
        {
            var a = MakeCue(spatial: false); var b = MakeCue(spatial: false);
            var go = Track(new GameObject("Destruction"));
            go.SetActive(false);
            var tl = go.AddComponent<SfxTimeline>();
            tl.Entries.Add(new SfxTimeline.Entry { Delay = 0f, Cue = a });
            tl.Entries.Add(new SfxTimeline.Entry { Delay = 0.15f, Cue = b });
            go.SetActive(true);
            yield return null;
            Assert.AreEqual(1, tl.Fired);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(2, tl.Fired);
            Assert.AreEqual(2, SfxPlayer.Instance.FlatPlays);
        }

        [UnityTest] public IEnumerator EmitterPlaysBeginAndEndCuesWithParticleEmission()
        {
            var begin = MakeCue(spatial: false); var end = MakeCue(spatial: false);
            var vfx = Track(new GameObject("Flame"));
            var ps = vfx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true;
            var holder = new GameObject("Sfx"); holder.transform.SetParent(vfx.transform, false);
            holder.SetActive(false);
            var emitter = holder.AddComponent<SfxEmitter>();
            emitter.BeginCue = begin; emitter.EndCue = end; emitter.Trigger = SfxEmitterTrigger.WhileParticlesEmit;
            holder.SetActive(true);
            yield return null;
            ps.Play(true);
            yield return null; yield return null;
            Assert.AreEqual(1, SfxPlayer.Instance.FlatPlays, "점화");
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return null; yield return null;
            Assert.AreEqual(2, SfxPlayer.Instance.FlatPlays, "소화");
        }
    }
}
