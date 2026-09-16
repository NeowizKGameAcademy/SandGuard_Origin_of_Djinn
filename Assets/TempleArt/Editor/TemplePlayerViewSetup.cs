using System;
using System.Linq;
using SandGuard.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TemplePlayerViewSetup
{
    const string ScenePath = "Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity";

    [MenuItem("Tools/Temple Art/Enable Courtyard Player View")]
    public static void Setup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open Level_TempleCourtyard before running player setup.");
        var player = Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(p => p.gameObject.scene == scene);
        if (!player)
        {
            Physics.SyncTransforms();
            var origin = new Vector3(0, 80, -82);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 160, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No ground at the south entrance.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            if (!prefab) throw new InvalidOperationException("Player prefab missing.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "Player";
            go.transform.SetPositionAndRotation(hit.point + Vector3.up * .15f, Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            player = go.GetComponent<PlayerMotor>();
        }
        player.gameObject.SetActive(true);
        var rig = player.GetComponentInChildren<PlayerCameraRig>(true);
        if (!rig || !rig.target || !rig.input || !rig.owner)
            throw new InvalidOperationException("Player camera bindings are incomplete.");
        var camera = rig.GetComponent<Camera>();
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(c => c.gameObject.scene == scene))
        {
            c.enabled = c == camera;
            c.tag = c == camera ? "MainCamera" : "Untagged";
            EditorUtility.SetDirty(c);
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            PrefabUtility.RecordPrefabInstancePropertyModifications(c.gameObject);
        }
        foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(l => l.gameObject.scene == scene))
        {
            listener.enabled = listener.gameObject == camera.gameObject;
            PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
        }
        camera.gameObject.SetActive(true);
        camera.orthographic = false;
        var rotation = Quaternion.Euler(rig.pitch, player.transform.eulerAngles.y, 0);
        camera.transform.SetPositionAndRotation(rig.target.position + rotation * new Vector3(rig.shoulderOffset, 0, -rig.distance), rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TEMPLE_PLAYER_VIEW_READY position=" + player.transform.position + " camera=" + camera.name);
    }

    public static void SetupBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Setup();
        var player = Object.FindFirstObjectByType<PlayerMotor>();
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled).ToArray();
        if (cameras.Length != 1 || !cameras[0].GetComponent<PlayerCameraRig>() || Camera.main != cameras[0])
            throw new InvalidOperationException("Expected exactly one active player camera.");
        if (Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) != 1)
            throw new InvalidOperationException("Expected exactly one active listener.");
        if (!player.input || !player.input.actions || player.view != cameras[0].transform)
            throw new InvalidOperationException("Player input/movement camera binding missing.");
        Physics.SyncTransforms();
        var controller = player.GetComponent<CharacterController>();
        var center = player.transform.TransformPoint(controller.center);
        var half = Mathf.Max(0, controller.height * .5f - controller.radius);
        if (Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half, controller.radius * .95f, ~0, QueryTriggerInteraction.Ignore)
            .Any(c => !c.transform.IsChildOf(player.transform)))
            throw new InvalidOperationException("Player spawn overlaps level collision.");
        Debug.Log("TEMPLE_PLAYER_VIEW_VALIDATED: camera, listener, input, movement binding, spawn clearance");
    }
}

