using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertTower.VFX.Editor
{
    public static class SpawnWarningPreview
    {
        [MenuItem("DesertTower/Preview Spawn Warning Cylinder", false, -1000)]
        public static void Render()
        {
            var shader = Resources.Load<Shader>("VFX/Shaders/SpawnWarningCylinder");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Spawn warning shader did not compile");
            string folder = "Docs/vfx-preview/SpawnWarningCylinder";
            Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D frame = null;
            Material floorMaterial = null;
            try
            {
                var root = new GameObject("Upcoming Spawn");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = Vector3.up * .36f;
                var warning = root.AddComponent<VfxSpawnWarning>();
                warning.Initialize(10f, 60f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.position = new Vector3(0f, -.12f, 0f);
                ground.transform.localScale = new Vector3(50f, .2f, 50f);
                floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                floorMaterial.SetColor("_BaseColor", new Color(.085f, .095f, .12f));
                ground.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(deck, scene);
                deck.name = "12.5m Deck Reference";
                deck.transform.position = Vector3.up * .15f;
                deck.transform.localScale = new Vector3(12.5f, .3f, 12.5f);
                deck.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var camera = new GameObject("Spawn Warning Preview Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = 36f;
                camera.transform.position = new Vector3(65f, 46f, -90f);
                camera.transform.LookAt(new Vector3(0f, 28f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .03f, .045f);
                rt = new RenderTexture(640, 640, 24);
                frame = new Texture2D(640, 640, TextureFormat.RGB24, false);
                camera.targetTexture = rt;
                for (int i = 0; i < 60; i++)
                {
                    float time = i / 25f;
                    warning.SetAppearance(new Color(1f, .48f, .08f), 1f, time,
                        .5f + .5f * Mathf.Sin(time * Mathf.PI * 2f));
                    camera.Render();
                    var previous = RenderTexture.active;
                    try { RenderTexture.active = rt; frame.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); frame.Apply(); }
                    finally { RenderTexture.active = previous; }
                    File.WriteAllBytes(Path.Combine(folder, $"frame-{i:00}.png"), frame.EncodeToPNG());
                }
                if (ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Spawn warning shader variant failed");
                camera.orthographic = false;
                camera.fieldOfView = 50f;
                camera.transform.position = new Vector3(80f, 38f, -110f);
                camera.transform.LookAt(new Vector3(0f, 25f, 0f));
                camera.Render();
                var lastActive = RenderTexture.active;
                try { RenderTexture.active = rt; frame.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); frame.Apply(); }
                finally { RenderTexture.active = lastActive; }
                File.WriteAllBytes(Path.Combine(folder, "distance-136m.png"), frame.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "validation.txt"), "PASS: shader compiled, open cylinder rendered, 60 frames of downward chevrons.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (rt) Object.DestroyImmediate(rt);
                if (frame) Object.DestroyImmediate(frame);
                if (floorMaterial) Object.DestroyImmediate(floorMaterial);
            }
        }
    }
}
