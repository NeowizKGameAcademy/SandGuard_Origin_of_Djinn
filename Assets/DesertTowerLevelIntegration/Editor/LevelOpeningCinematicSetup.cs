using System;
using System.Linq;
using DesertTower.Levels;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DesertTower.LevelIntegration.Editor
{
    public static class LevelOpeningCinematicSetup
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";

        [MenuItem("SandGuard/Build Level Opening Cinematic")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var level = Object.FindFirstObjectByType<LevelRoot>();
            if (!level) throw new InvalidOperationException("LevelRoot is missing.");
            var markers = level.Markers;
            var core = markers.First(m => m.kind == MarkerKind.Core).transform.position;
            var entrances = markers.Where(m => m.kind == MarkerKind.EnemySpawn)
                .OrderBy(m => m.transform.position.x + m.transform.position.z).ToArray();
            if (entrances.Length < 2) throw new InvalidOperationException("Two entrance markers are required.");
            var opening = Object.FindFirstObjectByType<LevelOpeningCinematic>();
            if (!opening) opening = new GameObject("Level Opening Cinematic").AddComponent<LevelOpeningCinematic>();
            opening.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/9.Font/Pretendard-Bold SDF.asset");
            opening.playOnStart = true;
            opening.returnSeconds = 1.2f;
            opening.skipReturnSeconds = .45f;
            var center = new Vector3(core.x, 18f, core.z);
            opening.shots = new[]
            {
                new LevelOpeningCinematic.Shot
                {
                    title = "모래에 잠긴 신전", description = "신전의 길을 살피고 코어를 지켜내세요",
                    from = center + new Vector3(138, 105, -163), to = center + new Vector3(123, 98, -151),
                    lookAt = center, fieldOfView = 57f, seconds = 1.7f
                },
                Entrance(entrances.First(), core, "서남쪽 진입로", "적은 신전 바깥에서 계단을 따라 올라옵니다"),
                Entrance(entrances.Last(), core, "동북쪽 진입로", "반대편 길목도 놓치지 마세요"),
                new LevelOpeningCinematic.Shot
                {
                    title = "최후의 방어선", description = "정상에 있는 코어를 끝까지 보호하세요",
                    from = core + new Vector3(28, 19, -34), to = core + new Vector3(20, 16, -29),
                    lookAt = core + Vector3.up * 2f, fieldOfView = 55f, seconds = 1.3f
                }
            };
            EditorUtility.SetDirty(opening);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"LEVEL_OPENING_READY: {opening.shots.Length} shots, 6.6 seconds; core={core}; "
                + string.Join("; ", entrances.Select(m => m.label + "=" + m.transform.position)));
        }

        static LevelOpeningCinematic.Shot Entrance(LevelMarker entrance, Vector3 core, string title, string description)
        {
            Vector3 point = entrance.transform.position;
            Vector3 outward = Vector3.ProjectOnPlane(point - core, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            return new LevelOpeningCinematic.Shot
            {
                title = title, description = description,
                // Spawn markers sit inside the opaque storm. Film the approach from inside
                // the courtyard, looking toward where the enemies emerge from the sand.
                from = point - outward * 38f + side * 20f + Vector3.up * 24f,
                to = point - outward * 34f + side * 14f + Vector3.up * 22f,
                lookAt = point - outward * 20f + Vector3.up * 4f, fieldOfView = 59f, seconds = 1.2f
            };
        }
    }
}
