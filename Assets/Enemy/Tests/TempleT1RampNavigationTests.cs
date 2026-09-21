#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Tests
{
    public sealed class TempleT1RampNavigationTests
    {
        const string Report = "Docs/LevelArt/TempleArchitecture/V2/courtyard-ramps/enemy-traversal.txt";
        readonly List<string> lines = new List<string>();
        GameObject actor;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if(actor)Object.Destroy(actor);
            File.WriteAllLines(Report,lines);
            var empty=SceneManager.CreateScene("Ramp verification cleanup");SceneManager.SetActiveScene(empty);
            for(int i=SceneManager.sceneCount-1;i>=0;i--){var scene=SceneManager.GetSceneAt(i);if(scene!=empty&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        }

        static Vector3 Map(Vector3 point, int deck)
        {
            if(deck>=2){point.x=-point.x;point.z=-point.z;}
            point.z+=deck==1?128:deck==3?-128:0;return point;
        }

        [UnityTest, Timeout(300000)] public IEnumerator EnemiesWalkSavedLevelRampBothWays()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/1.Scene/Level.unity",new LoadSceneParameters(LoadSceneMode.Single));
            // Isolate locomotion from waves, player attacks, and UI; do not save these runtime changes.
            foreach(var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(behaviour && !(behaviour is NavMeshSurface))behaviour.enabled=false;
            Time.timeScale=3;
            yield return null;
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Enemy/Generated"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<EnemyMotor>()).OrderBy(p=>p).ToArray();
            Assert.GreaterOrEqual(paths.Length,5);
            lines.Add("Actual enemy prefabs on saved Level NavMesh. EnemyBrain.Steer -> EnemyMotor -> NavMeshAgent.");
            lines.Add("Combat target selection disabled to isolate navigation; existing scene colliders and NavMesh obstacles retained.");
            for(int deck=0;deck<4;deck++) foreach(var path in paths)foreach(float z in new[]{-65.3f,-64f,-62.7f})foreach(bool up in new[]{true,false})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var config=prefab.GetComponent<NavMeshAgent>();
                var filter=new NavMeshQueryFilter{agentTypeID=config.agentTypeID,areaMask=config.areaMask};
                var requestedStart=new Vector3(up?-54.1f:-60.2f,up?1.13f:2f,z);
                var requestedEnd=new Vector3(up?-60.2f:-54.1f,up?2f:1.13f,z);
                requestedStart=Map(requestedStart,deck);requestedEnd=Map(requestedEnd,deck);
                Assert.True(NavMesh.SamplePosition(requestedStart,out var start,.3f,filter),path+" start");
                Assert.True(NavMesh.SamplePosition(requestedEnd,out var end,.3f,filter),path+" end");
                actor=Object.Instantiate(prefab,start.position,Quaternion.identity);
                var brain=actor.GetComponent<EnemyBrain>();var motor=actor.GetComponent<EnemyMotor>();
                Assert.NotNull(brain);brain.selector=null;brain.despawnOnArrival=false;brain.Steer(end.position);
                yield return null;
                Assert.True(motor.IsOnNavMesh,path+" attached");
                var route=new NavMeshPath();Assert.True(motor.TryCalculatePath(end.position,route));Assert.AreEqual(NavMeshPathStatus.PathComplete,route.status);
                float length=0;for(int i=1;i<route.corners.Length;i++)length+=Vector3.Distance(route.corners[i-1],route.corners[i]);
                Assert.Less(length,8f,path+" must take new ramp, not a detour");
                float deadline=Time.time+12;bool crossed=false;float maxZDeviation=0;
                while(Time.time<deadline && !motor.HasArrived)
                {
                    var p=actor.transform.position;
                    crossed |= Mathf.Abs(p.x)<58.7f && Mathf.Abs(p.x)>55.3f && p.y>1.2f && p.y<2.4f;
                    maxZDeviation=Mathf.Max(maxZDeviation,Mathf.Abs(p.z-requestedStart.z));
                    Assert.True(motor.IsOnNavMesh,path+" stayed on mesh");
                    Assert.AreNotEqual(EnemyBrainState.Blocked,brain.State,path+" blocked");
                    yield return null;
                }
                float error=Vector3.Distance(actor.transform.position,end.position);
                bool pass=motor.HasArrived && crossed && error<.65f && maxZDeviation<1.2f;
                lines.Add($"{(pass?"PASS":"FAIL")} T{deck+1} {prefab.name} {(up?"up":"down")} z={z} arrived={motor.HasArrived} crossedRamp={crossed} endError={error:F3} deviation={maxZDeviation:F3} radius={motor.Agent.radius:F2} height={motor.Agent.height:F2}");
                File.WriteAllLines(Report,lines);
                Assert.True(pass,lines.Last());
                Object.Destroy(actor);yield return null;
            }
            lines.Add($"PASS: {paths.Length} enemy prefabs, {paths.Length*24} actual movement checks across T1-T4.");
        }
    }
}
#endif
