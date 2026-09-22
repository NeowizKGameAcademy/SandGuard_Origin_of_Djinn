using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DesertTower.VFX;
using NUnit.Framework;
using SandGuard.Enemy;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Audio.Tests
{
    public sealed class SfxEnemyTauntTests
    {
        readonly List<Object> objects = new List<Object>();
        T Track<T>(T value) where T : Object { objects.Add(value); return value; }

        SfxCue MakeCue()
        {
            Track(new GameObject("Listener")).AddComponent<AudioListener>();
            var cue = Track(ScriptableObject.CreateInstance<SfxCue>());
            cue.clips = new[] {
                Track(AudioClip.Create("Taunt A", 48000, 1, 48000, false)),
                Track(AudioClip.Create("Taunt B", 48000, 1, 48000, false))
            };
            cue.loopFade = 0f; cue.minInterval = 0f; cue.maxVoices = 1;
            SfxPlayer.Instance.muted = true;
            return cue;
        }

        SfxEnemyTaunt MakeEnemy(SfxCue cue, out Animator animator)
        {
            var go = Track(new GameObject("Enemy"));
            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyBrain>();
            var visuals = go.AddComponent<EnemyVisuals>();
            var model = new GameObject("Visual");
            model.transform.SetParent(go.transform, false);
            animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("SfxTestAnimator");
            Assert.IsNotNull(animator.runtimeAnimatorController);
            animator.Play("Idle", 0, 0); animator.Update(0f);
            typeof(EnemyVisuals).GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(visuals, animator);
            var taunt = go.AddComponent<SfxEnemyTaunt>();
            taunt.Cue = cue; taunt.LocomotionState = "Idle";
            taunt.MinDelay = taunt.MaxDelay = 0.1f;
            taunt.enabled = false; taunt.enabled = true;
            return taunt;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var o in objects) if (o != null) Object.Destroy(o);
            objects.Clear();
            if (SfxPlayer.Exists) Object.Destroy(SfxPlayer.Instance.gameObject);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator DelaysThenPlaysAndStopsWhenLeavingIdle()
        {
            var cue = MakeCue();
            var taunt = MakeEnemy(cue, out var animator);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            Assert.AreEqual(taunt.transform, SfxPlayer.Instance.ActiveVoices(cue)[0].Follow);
            animator.Play("Ground", 0, 0); animator.Update(0f);
            yield return null; yield return null;
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
        }

        [UnityTest] public IEnumerator PoolReuseWaitsAgainAndDisabledAiStaysSilent()
        {
            var cue = MakeCue();
            var taunt = MakeEnemy(cue, out _);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            taunt.gameObject.SetActive(false);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            yield return null;
            taunt.gameObject.SetActive(true);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            taunt.GetComponent<EnemyBrain>().AIEnabled = false;
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
        }

        [UnityTest] public IEnumerator CrowdedOrDistantEnemiesDoNotStealPlayingTaunt()
        {
            var cue = MakeCue();
            var first = MakeEnemy(cue, out _);
            yield return new WaitForSeconds(0.2f);
            var voice = SfxPlayer.Instance.ActiveVoices(cue)[0];
            var second = MakeEnemy(cue, out _);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            Assert.AreEqual(first.transform, voice.Follow);
            first.gameObject.SetActive(false);
            second.transform.position = Vector3.right * 1000f;
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
        }

        [UnityTest] public IEnumerator DeathStopsVoiceBeforeDespawn()
        {
            var cue = MakeCue();
            var taunt = MakeEnemy(cue, out _);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            var health = taunt.GetComponent<EnemyHealth>();
            health.TakeDamage(new DamageInfo(1000f, "Ally"));
            yield return null; yield return null;
            Assert.IsFalse(health.IsAlive);
            Assert.IsTrue(taunt.gameObject.activeInHierarchy);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
        }
    }
}
