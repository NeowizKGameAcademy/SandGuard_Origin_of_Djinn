using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SandGuard.Audio.Tests
{
    /// <summary>
    /// 실제 Level 씬에서 코어 핑이 도는지 확인한다. -nographics에서는 Level 재생이 죽으므로 -batchmode만으로 돌린다 (약 4분).
    /// 실패 메시지에 진단(루프 훅 목록, 상태, 거리, 보이스 수)을 담는다.
    /// </summary>
    public sealed class LevelAudioSmokeTests
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";

        [UnityTest] public IEnumerator CorePingFiresInLevelScene()
        {
            LogAssert.ignoreFailingMessages = true; // 레벨 자체의 검증 오류("코어 마커 위치…")는 이 테스트의 관심사가 아니다
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(4.5f);

            var sb = new StringBuilder();
            var listener = Object.FindFirstObjectByType<AudioListener>();
            sb.Append($"listener={(listener ? listener.transform.position.ToString("0.0") : "none")} ");
            var loops = Object.FindObjectsByType<SfxAnimatorLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.Append($"loops={loops.Length} ");
            int fired = 0;
            foreach (var l in loops)
            {
                var a = l.Animator != null ? l.Animator : l.GetComponent<Animator>();
                string state = "?"; float nt = -1;
                if (a != null && a.isActiveAndEnabled && a.runtimeAnimatorController != null)
                {
                    var info = a.GetCurrentAnimatorStateInfo(l.Layer);
                    state = info.IsName(l.StateName) ? l.StateName : "other"; nt = info.normalizedTime;
                }
                float dist = listener ? Vector3.Distance(listener.transform.position, l.transform.position) : -1;
                sb.Append($"[{l.transform.root.name}/{l.name} active={l.isActiveAndEnabled} anim={(a ? a.runtimeAnimatorController?.name : "none")} state={state} nt={nt:0.00} fired={l.Fired} cue={(l.Cue ? l.Cue.name : "none")} clips={(l.Cue ? l.Cue.clips.Length : 0)} dist={dist:0.0}] ");
                fired += l.Fired;
            }
            if (SfxPlayer.Exists) sb.Append($"player voices={SfxPlayer.Instance.ActiveVoiceCount} flat={SfxPlayer.Instance.FlatPlays} ");
            if (DesertTower.VFX.PrefabPool.Exists) sb.Append($"pool created={DesertTower.VFX.PrefabPool.Instance.CreatedCount} ");
            Debug.Log("LEVEL_AUDIO_SMOKE " + sb);
            Assert.GreaterOrEqual(fired, 2, sb.ToString());
        }

        /// <summary>웨이브를 시작하고 준비 시간을 건너뛰면(Preparing → Running) 웨이브 시작 신호가 한 번 난다.</summary>
        [UnityTest] public IEnumerator WaveStartCuePlaysWhenWaveBegins()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(1.5f); // MusicDirector가 WaveDirector를 찾는 1초 주기
            var music = Object.FindFirstObjectByType<MusicDirector>();
            var director = Object.FindFirstObjectByType<DesertTower.LevelIntegration.WaveDirector>();
            Assert.IsNotNull(music, "Level에 MusicDirector가 없다");
            Assert.IsNotNull(director, "Level에 WaveDirector가 없다");
            Assert.IsNotNull(music.WaveStart, "MusicDirector.WaveStart 큐가 비어 있다");
            Assert.IsTrue(music.WaveStart.HasClips, "Wave_Start 큐에 클립이 없다");
            int before = SfxPlayer.Exists ? SfxPlayer.Instance.FlatPlays : 0;
            director.SkipPreparation(); // 시작 전이면 Begin 후 준비 시간 0
            for (int i = 0; i < 10 && director.State != DesertTower.LevelIntegration.RunState.Running; i++) yield return null;
            yield return null; yield return null;
            Debug.Log($"LEVEL_WAVE_START state={director.State} waveStarts={music.WaveStarts} flatPlays={(SfxPlayer.Exists ? SfxPlayer.Instance.FlatPlays : -1)} error={director.LastError}");
            Assert.AreEqual(DesertTower.LevelIntegration.RunState.Running, director.State, "웨이브가 시작되지 않았다: " + director.LastError);
            Assert.AreEqual(1, music.WaveStarts, "웨이브 시작 신호 횟수");
            Assert.Greater(SfxPlayer.Instance.FlatPlays, before, "웨이브 시작 소리가 재생되지 않았다");
        }
    }
}
