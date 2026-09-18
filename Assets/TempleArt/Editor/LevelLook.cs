using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Level.unity 조명·후처리 룩: 컨셉아트의 해 질 녘 톤(낮게 뜬 주황 해, 푸른 그림자, 강한 대비, 발광 번짐).
/// 씬의 해/보조광/앰비언트/안개, 전역 볼륨, PC URP 에셋의 그림자·색보정 설정, 플레이어 카메라 후처리를 한 번에 맞춘다.
/// 다시 실행해도 같은 결과가 나오도록 값은 모두 여기 상수에서만 정한다.</summary>
public static class LevelLook
{
    const string ScenePath = "Assets/1.Scene/Level.unity";
    const string ProfileDir = "Assets/TempleArt/Lighting";
    const string ProfilePath = ProfileDir + "/LevelLook_Profile.asset";
    const string PipelinePath = "Assets/2.Model/PIpeLine/Settings/PC_RPAsset.asset";
    const string RendererPath = "Assets/2.Model/PIpeLine/Settings/PC_Renderer.asset";
    const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";
    const string VolumeName = "Level Look - Global Volume";

    // ── 해: 고도 25°. 기본 시선(+z)의 옆·뒤에서 비춰 계단·벽 그림자가 화면 쪽으로 길게 떨어진다 ──
    static readonly Vector3 SunEuler = new(25, 215, 0);
    static readonly Color SunColor = new(1f, .76f, .54f);
    const float SunIntensity = 2.4f, SunShadowStrength = .92f;
    // 그림자 없는 보조광은 그림자를 평평하게 만드니 약하게, 대신 푸르게.
    static readonly Color FillColor = new(.55f, .68f, 1f);
    const float FillIntensity = .2f;
    // 그림자 쪽 색은 앰비언트가 정한다: 하늘은 푸르게, 지면은 모래색 반사.
    static readonly Color AmbientSky = new(.42f, .55f, .80f), AmbientEquator = new(.46f, .40f, .33f), AmbientGround = new(.26f, .20f, .14f);
    // 지수제곱 안개: 50m 4%, 100m 15% → 폭풍 벽과 먼 성벽이 따뜻한 먼지에 묻힌다.
    static readonly Color FogColor = new(.80f, .70f, .58f);
    const float FogDensity = .004f;

    // ── URP(PC): 탑다운에 가까운 카메라라 그림자 거리 50m 로는 성벽 그림자가 잘린다 ──
    const float ShadowDistance = 180;
    static readonly Vector3 Cascade4Split = new(.05f, .15f, .4f);
    const int ShadowResolution = 4096;
    const float SsaoIntensity = .8f, SsaoRadius = .6f, SsaoDirect = .35f;

    [MenuItem("Tools/Desert Tower/Look/Apply sunset lighting + volume (Level.unity)")]
    public static void Apply()
    {
        try
        {
            ApplyPipeline();
            ApplyPlayerCamera();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("LEVEL_LOOK_APPLIED");
        }
        catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    /// <summary>배치 전용: 전(before) 촬영 → 적용 → 후(after) 촬영. 출력 폴더는 환경변수 LOOK_OUT.</summary>
    public static void ApplyWithCaptures()
    {
        try
        {
            string root = Environment.GetEnvironmentVariable("LOOK_OUT") ?? "Docs/LevelArt/LevelLook";
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CaptureAll(root + "/before");
            Apply();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CaptureAll(root + "/after");
            Debug.Log("LEVEL_LOOK_DONE"); if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    static void ApplyScene()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sun = lights.FirstOrDefault(l => l.name == "Directional Light") ?? throw new Exception("Level.unity 에 'Directional Light' 가 없습니다.");
        sun.transform.rotation = Quaternion.Euler(SunEuler);
        sun.color = SunColor; sun.intensity = SunIntensity; sun.useColorTemperature = false;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = SunShadowStrength;
        var fill = lights.FirstOrDefault(l => l.name == "Dunes - Sky Fill");
        if (fill) { fill.color = FillColor; fill.intensity = FillIntensity; }

        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky; RenderSettings.ambientEquatorColor = AmbientEquator; RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor; RenderSettings.fogDensity = FogDensity;
        DynamicGI.UpdateEnvironment();

        var go = GameObject.Find(VolumeName) ?? new GameObject(VolumeName);
        go.transform.SetParent(null); go.layer = 0;
        if (!go.TryGetComponent(out Volume volume)) volume = go.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 10; volume.weight = 1; volume.sharedProfile = BuildProfile();

        // 씬의 편집용 Main Camera 도 게임 뷰에서 같은 룩으로 보이게.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.name == "Main Camera"))
            EnablePost(cam);
    }

    static VolumeProfile BuildProfile()
    {
        Directory.CreateDirectory(ProfileDir);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        // 매번 비우고 다시 채워서 상수만 고치면 결과가 그대로 따라오게 한다.
        foreach (var c in profile.components.ToArray()) { profile.Remove(c.GetType()); Object.DestroyImmediate(c, true); }

        var tone = Add<Tonemapping>(profile); tone.mode.Override(TonemappingMode.ACES);
        var bloom = Add<Bloom>(profile);
        bloom.threshold.Override(1.1f); bloom.intensity.Override(.55f); bloom.scatter.Override(.7f);
        bloom.tint.Override(new Color(1f, .86f, .72f)); bloom.highQualityFiltering.Override(true);
        var adjust = Add<ColorAdjustments>(profile);
        adjust.postExposure.Override(.1f); adjust.contrast.Override(20f); adjust.saturation.Override(12f);
        // 밝은 곳은 주황, 어두운 곳은 청록: 컨셉아트의 따뜻한 빛/차가운 그림자 대비.
        var split = Add<SplitToning>(profile);
        split.shadows.Override(new Color(.34f, .50f, .70f)); split.highlights.Override(new Color(.68f, .52f, .38f)); split.balance.Override(-15f);
        var white = Add<WhiteBalance>(profile); white.temperature.Override(4f);
        var vignette = Add<Vignette>(profile); vignette.intensity.Override(.25f); vignette.smoothness.Override(.45f);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var c = profile.Add<T>(false); c.name = typeof(T).Name; c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, profile); return c;
    }

    static void ApplyPipeline()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) ?? throw new Exception(PipelinePath + " 없음");
        var so = new SerializedObject(pipeline);
        so.FindProperty("m_ShadowDistance").floatValue = ShadowDistance;
        so.FindProperty("m_ShadowCascadeCount").intValue = 4;
        so.FindProperty("m_Cascade4Split").vector3Value = Cascade4Split;
        so.FindProperty("m_MainLightShadowmapResolution").intValue = ShadowResolution;
        so.FindProperty("m_ColorGradingMode").intValue = (int)ColorGradingMode.HighDynamicRange;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline);

        var ssao = AssetDatabase.LoadAllAssetsAtPath(RendererPath).OfType<ScriptableRendererFeature>().FirstOrDefault(f => f.GetType().Name == "ScreenSpaceAmbientOcclusion");
        if (!ssao) { Debug.LogWarning("PC_Renderer 에 SSAO 가 없어 건너뜀"); return; }
        var sso = new SerializedObject(ssao);
        sso.FindProperty("m_Settings.Intensity").floatValue = SsaoIntensity;
        sso.FindProperty("m_Settings.Radius").floatValue = SsaoRadius;
        sso.FindProperty("m_Settings.DirectLightingStrength").floatValue = SsaoDirect;
        sso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ssao);
    }

    static void ApplyPlayerCamera()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var cam = root.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.name == "PlayerCamera") ?? throw new Exception("Player.prefab 에 PlayerCamera 없음");
            EnablePost(cam);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void EnablePost(Camera cam)
    {
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        EditorUtility.SetDirty(data);
    }

    // ── 촬영 ──
    static void CaptureAll(string outDir)
    {
        Directory.CreateDirectory(outDir);
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        var main = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Main Camera");
        if (main) Shot(outDir, "main-camera", main.transform.position, main.transform.position + main.transform.forward, main.fieldOfView);
        var player = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "PlayerCamera");
        if (player)
        {
            var p = player.transform.root; var f = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
            if (f == Vector3.zero) f = Vector3.forward;
            // 컨셉아트처럼 플레이어 뒤 높은 곳에서 내려다보는 시점.
            Shot(outDir, "player-high", p.position - f * 28 + Vector3.up * 34, p.position + f * 18, 50);
        }
        Shot(outDir, "overview", new Vector3(0, 150, -150), new Vector3(0, 20, 0), 45);
    }

    static void Shot(string outDir, string name, Vector3 pos, Vector3 target, float fov)
    {
        const int w = 1600, h = 900;
        var go = new GameObject("look capture"); var c = go.AddComponent<Camera>();
        c.transform.position = pos; c.transform.LookAt(target);
        c.fieldOfView = fov; c.nearClipPlane = .3f; c.farClipPlane = 2000; c.clearFlags = CameraClearFlags.Skybox; c.useOcclusionCulling = false;
        var d = c.GetUniversalAdditionalCameraData(); d.requiresDepthTexture = true; d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create(); c.targetTexture = rt; c.aspect = w / (float)h;
        c.Render(); c.Render(); // 첫 프레임은 볼륨/그림자 캐스케이드가 아직 안 잡힐 수 있다
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(outDir + "/" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = prev; c.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
