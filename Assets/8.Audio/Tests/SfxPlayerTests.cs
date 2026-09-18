using System;
using System.Collections;
using System.Collections.Generic;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Audio.Tests
{
    /// <summary>오디오 장치 없이도 도는 검증: 보이스 대여·반납, 한도, 중복 합치기, 부착 정리, 루프, 피격 반응, 원샷 연결.</summary>
    public sealed class SfxPlayerTests
    {
        readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        T Track<T>(T value) where T : UnityEngine.Object { objects.Add(value); return value; }

        /// <summary>0.1초 무음 클립. 길이만 의미가 있다.</summary>
        SfxCue MakeCue(bool spatial = true, bool loop = false, int variants = 1, int maxVoices = 4, float minInterval = 0f)
        {
            var cue = Track(ScriptableObject.CreateInstance<SfxCue>());
            cue.clips = new AudioClip[variants];
            for (int i = 0; i < variants; i++) cue.clips[i] = Track(AudioClip.Create($"clip{i}", 4800, 1, 48000, false));
            cue.spatial = spatial; cue.loop = loop; cue.maxVoices = maxVoices; cue.minInterval = minInterval; cue.loopFade = 0f;
            return cue;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (SfxPlayer.Exists) UnityEngine.Object.Destroy(SfxPlayer.Instance.gameObject);
            if (PrefabPool.Exists) UnityEngine.Object.Destroy(PrefabPool.Instance.gameObject);
            foreach (var o in objects) if (o != null) UnityEngine.Object.Destroy(o);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator OneShotRentsVoiceAtPositionAndReturnsItAfterClipLength()
        {
            var cue = MakeCue();
            var voice = SfxPlayer.Play(cue, new Vector3(3, 0, 5));
            Assert.IsNotNull(voice);
            Assert.AreEqual(cue.clips[0], voice.Source.clip);
            Assert.AreEqual(1f, voice.Source.spatialBlend);
            Assert.AreEqual(new Vector3(3, 0, 5), voice.transform.position);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCount);
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCount, "클립 길이 뒤 스스로 반납");
            Assert.IsFalse(voice.gameObject.activeSelf, "풀에 비활성으로 보관");

            yield return null;
            SfxPlayer.Play(cue, Vector3.zero);
            Assert.AreEqual(1, PrefabPool.Instance.ReusedCount, "두 번째 재생은 풀에서 재사용");
        }

        [UnityTest] public IEnumerator SameFrameDuplicatesMergeAndMaxVoicesStealsOldest()
        {
            var cue = MakeCue(maxVoices: 2);
            var first = SfxPlayer.Play(cue, Vector3.zero);
            var dup = SfxPlayer.Play(cue, Vector3.zero);
            Assert.IsNotNull(first); Assert.IsNull(dup, "같은 프레임 중복은 한 번만");
            float firstStart = first.StartedAt;
            yield return null;
            var second = SfxPlayer.Play(cue, Vector3.zero);
            Assert.IsNotNull(second);
            yield return null;
            var third = SfxPlayer.Play(cue, Vector3.zero);
            Assert.IsNotNull(third);
            Assert.AreEqual(2, SfxPlayer.Instance.ActiveVoiceCountFor(cue), "한도 2");
            // 훔친 보이스 객체는 풀에서 곧바로 재사용되므로 first와 third가 같은 객체일 수 있다
            Assert.IsTrue(!first.Active || first.StartedAt > firstStart, "가장 오래된 보이스를 훔쳤다");
            Assert.IsTrue(second.Active && third.Active);
        }

        [UnityTest] public IEnumerator ChanceZeroNeverPlaysAndChanceOneAlwaysPlays()
        {
            var never = MakeCue(); never.chance = 0f;
            var always = MakeCue(); always.chance = 1f;
            for (int i = 0; i < 5; i++) { SfxPlayer.Play(never, Vector3.zero); SfxPlayer.Play(always, Vector3.zero); yield return null; }
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCountFor(never));
            Assert.AreEqual(4, SfxPlayer.Instance.ActiveVoiceCountFor(always), "한도 4까지 매번 재생");
        }

        [UnityTest] public IEnumerator MinIntervalDropsRapidRepeatsAcrossFrames()
        {
            var cue = MakeCue(minInterval: 0.5f);
            Assert.IsNotNull(SfxPlayer.Play(cue, Vector3.zero));
            yield return null;
            Assert.IsNull(SfxPlayer.Play(cue, Vector3.zero), "최소 간격 안의 재요청은 무시");
        }

        [UnityTest] public IEnumerator AttachedVoiceFollowsTargetAndReleasesWhenTargetDeactivates()
        {
            var cue = MakeCue(loop: true);
            var target = Track(new GameObject("Enemy"));
            target.transform.position = new Vector3(1, 2, 3);
            var voice = SfxPlayer.PlayLoop(cue, target.transform);
            Assert.AreEqual(target.transform, voice.transform.parent);
            target.transform.position = new Vector3(9, 0, 0);
            Assert.AreEqual(new Vector3(9, 0, 0), voice.transform.position, "대상을 따라간다");
            target.SetActive(false);
            yield return null;
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCount, "대상이 꺼지면 보이스도 정리");
        }

        [UnityTest] public IEnumerator EmitterLoopsWhileEnabledAndStopsWhenDisabled()
        {
            var cue = MakeCue(loop: true);
            var go = Track(new GameObject("Torch"));
            go.SetActive(false);
            var emitter = go.AddComponent<SfxEmitter>();
            emitter.Cue = cue;
            go.SetActive(true);
            yield return null;
            Assert.IsNotNull(emitter.Voice);
            Assert.IsTrue(emitter.Voice.Source.loop);
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCount);
            go.SetActive(false);
            yield return null;
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCount);
        }

        [UnityTest] public IEnumerator FlatCueUsesFixedSourcesNotThePool()
        {
            var cue = MakeCue(spatial: false, variants: 3);
            SfxPlayer.Play2D(cue);
            yield return null;
            SfxPlayer.Play2D(cue);
            Assert.AreEqual(2, SfxPlayer.Instance.FlatPlays);
            Assert.AreEqual(0, SfxPlayer.Instance.ActiveVoiceCount);
            Assert.AreEqual(0, PrefabPool.Exists ? PrefabPool.Instance.CreatedCount : 0);
        }

        [UnityTest] public IEnumerator VariantsAvoidImmediateRepeat()
        {
            var cue = MakeCue(variants: 4);
            AudioClip last = null; int repeats = 0;
            for (int i = 0; i < 12; i++)
            {
                var v = SfxPlayer.Play(cue, Vector3.zero);
                if (v.Source.clip == last) repeats++;
                last = v.Source.clip;
                yield return null;
            }
            Assert.AreEqual(0, repeats, "직전 변형은 피한다");
        }

        [UnityTest] public IEnumerator OneShotComponentFiresItsCue()
        {
            var cue = MakeCue();
            var go = Track(new GameObject("Staff"));
            var shot = go.AddComponent<SfxOneShot>();
            shot.Cue = cue;
            shot.Fire();
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(cue));
            yield return null;
        }

        sealed class FakeTarget : MonoBehaviour, IDamageEvents, ILifeState, ICombatTarget
        {
#pragma warning disable 67
            public event Action<DamageAppliedInfo> Damaged;
            public event Action<LifeStateChangedInfo> StateChanged;
            public event Action<DeathInfo> Died;
            public event Action<Guid> Revived;
            public event Action<Guid> Despawned;
#pragma warning restore 67
            public LifeState State => global::LifeState.Alive; // 아래 LifeState 속성이 열거형 이름을 가린다
            public Guid EntityId { get; } = Guid.NewGuid();
            public string FactionId => "Enemy";
            public CombatTargetKind Kind => CombatTargetKind.Enemy;
            public Vector3 HitPosition => transform.position + Vector3.up;
            public bool IsTargetable => true;
            public IDamageable DamageReceiver => null;
            public ILifeState LifeState => this;
            public void Hit(Vector3? at = null) => Damaged?.Invoke(new DamageAppliedInfo(EntityId, new DamageInfo(5f, "Player", null, "bolt", at), DamageResult.Applied(5f)));
            public void Die() => Died?.Invoke(new DeathInfo(EntityId, "enemy", FactionId, 0, new DamageInfo(5f, "Player")));
        }

        [UnityTest] public IEnumerator HitReactionPlaysHitAndDeathCues()
        {
            var hit = MakeCue(); var death = MakeCue();
            var go = Track(new GameObject("Enemy"));
            go.SetActive(false);
            var fake = go.AddComponent<FakeTarget>();
            var reaction = go.AddComponent<SfxHitReaction>();
            reaction.HitCue = hit; reaction.DeathCue = death; reaction.AttachHit = false;
            go.SetActive(true);
            go.transform.position = new Vector3(4, 0, 4);
            yield return null;

            fake.Hit(new Vector3(4, 1, 4));
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(hit));
            Assert.AreEqual(new Vector3(4, 1, 4), SfxPlayer.Instance.ActiveVoices(hit)[0].transform.position, "맞은 위치에서 난다");
            fake.Die();
            Assert.AreEqual(1, SfxPlayer.Instance.ActiveVoiceCountFor(death));
            Assert.AreEqual(1, reaction.HitCount); Assert.AreEqual(1, reaction.DeathCount);
        }
    }
}
