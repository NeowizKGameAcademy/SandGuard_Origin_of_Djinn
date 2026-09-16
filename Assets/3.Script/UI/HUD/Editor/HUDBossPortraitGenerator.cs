#if UNITY_EDITOR
using System.IO;
using System.Linq;
using SandGuard.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SandGuard.UI.HUD.Editor
{
    /// <summary>
    /// 보스 초상화. 보스 프리팹을 빈 씬에 세우고 얼굴을 정면에서 찍어(투명 배경, 마름모 마스크) 스프라이트로 저장한 뒤
    /// 프리팹의 EnemyBossInfo.icon에 건다. 보스 체력바 왼쪽 마름모 칸에 들어간다.
    /// 그래픽 장치가 필요하므로 배치 실행은 -nographics 없이: <c>-executeMethod SandGuard.UI.HUD.Editor.HUDBossPortraitGenerator.GenerateChiefBatch</c>
    /// </summary>
    public static class HUDBossPortraitGenerator
    {
        public const string Folder = "Assets/4.Sprite/UI/GameScene/HUD/BossIcons";
        public const string ChiefPath = Folder + "/Boss_Chief.png";
        const string ChiefPrefab = "Assets/Enemy/Generated/Enemy_Chief.prefab";
        // 보스 체력바 마름모 안쪽(72.6×63)과 같은 비율로 찍는다.
        const int Width = 288, Height = 250;

        [MenuItem("SandGuard/HUD/Generate Boss Portrait (Chief)")]
        public static void GenerateChief() => Generate(ChiefPrefab, ChiefPath);

        public static void GenerateChiefBatch() { GenerateChief(); EditorApplication.Exit(0); }

        public static bool Generate(string prefabPath, string outputPath)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Debug.LogWarning("[HUD] Boss portrait needs a graphics device (run without -nographics)."); return false; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("[HUD] Boss prefab missing: " + prefabPath); return false; }

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .56f, .6f); RenderSettings.fog = false;
                var key = new GameObject("Key Light").AddComponent<Light>(); key.type = LightType.Directional; key.color = new Color(1f, .95f, .86f); key.intensity = 1.7f; key.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
                var fill = new GameObject("Fill Light").AddComponent<Light>(); fill.type = LightType.Directional; fill.color = new Color(.7f, .8f, 1f); fill.intensity = .5f; fill.transform.rotation = Quaternion.Euler(20f, 130f, 0f);

                var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                boss.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = boss.GetComponentInChildren<Animator>();
                if (animator != null) { try { animator.Update(0f); } catch { } } // 대기 자세 첫 프레임(안 되면 바인드 포즈)

                var head = boss.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Head"));
                var neck = boss.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Neck"));
                var renderers = boss.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                float top = bounds.max.y;
                Vector3 headPos = head != null ? head.position : new Vector3(bounds.center.x, top - bounds.size.y * .12f, bounds.center.z);
                Vector3 neckPos = neck != null ? neck.position : headPos - Vector3.up * bounds.size.y * .08f;
                float faceHeight = Mathf.Max(.12f, top - neckPos.y);
                Vector3 center = new Vector3(headPos.x, Mathf.Lerp(neckPos.y, top, .5f), headPos.z);
                float radius = faceHeight * .78f;

                var camera = new GameObject("Portrait Camera").AddComponent<Camera>();
                camera.fieldOfView = 26f; camera.nearClipPlane = .02f; camera.farClipPlane = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0);
                float distance = radius / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad);
                camera.transform.position = center + new Vector3(0f, radius * .12f, distance); // 정면(+Z)에서 살짝 위
                camera.transform.LookAt(center);

                var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                var prevActive = RenderTexture.active;
                Texture2D tex;
                try
                {
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); tex.Apply();
                }
                finally { camera.targetTexture = null; RenderTexture.active = prevActive; rt.Release(); Object.DestroyImmediate(rt); }

                ApplyDiamondMask(tex);
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                if (!Application.isBatchMode && previous.IsValid()) { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }

            AssetDatabase.ImportAsset(outputPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outputPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var info = root.GetComponent<EnemyBossInfo>();
                if (info == null) info = root.AddComponent<EnemyBossInfo>();
                info.icon = sprite;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("[HUD] Boss portrait saved: " + outputPath);
            return true;
        }

        // 마름모 밖은 투명, 안쪽에는 어두운 붉은 배경을 깔아 어두운 틀 위에서도 얼굴이 읽히게 한다.
        static void ApplyDiamondMask(Texture2D tex)
        {
            var px = tex.GetPixels();
            int w = tex.width, h = tex.height;
            var backdrop = new Color(.22f, .05f, .05f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + .5f) / w * 2f - 1f, ny = (y + .5f) / h * 2f - 1f;
                    float d = 1f - (Mathf.Abs(nx) + Mathf.Abs(ny)); // >0 안쪽
                    float edge = Mathf.Clamp01(d * w * .5f / 2f); // 2px 부드러운 가장자리
                    var c = px[y * w + x];
                    var over = Color.Lerp(backdrop, new Color(c.r, c.g, c.b, 1f), c.a); // 렌더 결과를 배경 위에 얹는다
                    over.a = edge;
                    px[y * w + x] = over;
                }
            tex.SetPixels(px); tex.Apply();
        }
    }
}
#endif
