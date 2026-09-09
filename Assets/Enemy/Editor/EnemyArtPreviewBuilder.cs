using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Editor
{
    public static class EnemyArtPreviewBuilder
    {
        public const string ScenePath = "Assets/Enemy/Generated/EnemyArtPreview.unity";
        const string Art = "Assets/Enemy/Art/Characters";
        const string Equipment = "Assets/Enemy/Art/Equipment";
        const string Docs = "Docs/model-art/enemy-preview-v1";
        static readonly string[] Names = { "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" };
        static readonly string[] Labels = { "검병", "암살자", "방패병", "망치병", "우두머리" };
        static readonly float[] Heights = { 1.75f, 1.68f, 1.84f, 1.94f, 2.04f };
        // Palm-region centers measured from the source GLB vertices, mapped to Unity +Z front.
        static readonly Vector3[] Hand = { new Vector3(.259f,.416f,.070f), new Vector3(.259f,.418f,.076f), new Vector3(.268f,.415f,.090f), new Vector3(.283f,.413f,.099f), new Vector3(.268f,.417f,.047f) };

        [MenuItem("SandGuard/Enemy/Build Art Preview")]
        public static void Build()
        {
            Directory.CreateDirectory(Docs);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var report = new List<string>();
            for (int i = 0; i < Names.Length; ++i) BuildCharacter(i, report);
            BuildScene();
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Docs + "/validation.txt", report);
            Debug.Log("ENEMY_ART_PREVIEW_COMPLETE\n" + string.Join("\n", report));
        }

        static void BuildCharacter(int index, List<string> report)
        {
            string name = Names[index], folder = Art + "/" + name;
            string texturePath = folder + "/" + name + "_BaseColor.png";
            var texture = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            if (texture == null) throw new FileNotFoundException("Run export_preview_bodies.py first", texturePath);
            texture.textureType = TextureImporterType.Default; texture.sRGBTexture = true;
            texture.maxTextureSize = 2048; texture.mipmapEnabled = true; texture.filterMode = FilterMode.Bilinear;
            texture.textureCompression = TextureImporterCompression.CompressedHQ; texture.SaveAndReimport();
            var mat = MaterialAt(folder + "/" + name + ".mat", Color.white);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(mat);
            string modelPath = folder + "/" + name + "_Body.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false; importer.importLights = false;
            importer.globalScale = 1; importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name + "_Body"), mat);
            importer.SaveAndReimport();

            var root = new GameObject(name + "_PreviewVisual");
            try
            {
                var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.transform.SetParent(body, false); model.name = "StaticBody_Apose";
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                Bounds bounds = BoundsOf(body.gameObject);
                body.localScale = Vector3.one * Heights[index] / bounds.size.y;
                bounds = BoundsOf(body.gameObject);
                body.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = BoundsOf(body.gameObject);
                if (Mathf.Abs(bounds.min.y) > .001f || Mathf.Abs(bounds.size.y - Heights[index]) > .001f)
                    throw new InvalidOperationException(name + " normalization failed");
                var attachments = new GameObject("PreviewAttachments").transform; attachments.SetParent(root.transform, false);
                // World-space placeholders under an identity root; replace their parents with hand bones later.
                Vector3 rh = Hand[index] * Heights[index], lh = rh; lh.x = -lh.x;
                var right = Socket("RightHand_PreviewSocket", attachments, rh);
                var left = Socket("LeftHand_PreviewSocket", attachments, lh);
                switch (index)
                {
                    case 0:
                        Attach("ShortSword", right, .65f, new Vector3(8,0,-12));
                        Attach("RoundShield", left, .9f, new Vector3(0,-15,0));
                        break;
                    case 1:
                        Attach("AssassinDagger", right, .63f, new Vector3(12,0,-25));
                        Attach("AssassinDagger", left, .63f, new Vector3(12,180,25));
                        break;
                    case 2:
                        Attach("ShortSword", right, .72f, new Vector3(8,0,-10));
                        Attach("TowerShield", left, 1.0f, new Vector3(0,-10,0));
                        break;
                    case 3:
                        Attach("Warhammer", right, 1.0f, new Vector3(5,0,-12));
                        break;
                    case 4:
                        Attach("ChiefScimitar", right, 1.0f, new Vector3(5,0,-10));
                        var back = Socket("Back_PreviewSocket", attachments, new Vector3(0,1.45f,-.13f) * Heights[index] / 1.8f);
                        Attach("ChiefCape", back, Heights[index] / 1.8f, new Vector3(12,0,0), "ShoulderAttach");
                        break;
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (r.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name.Contains("Error"))) throw new InvalidOperationException("Missing material on " + r.name);
                // Distinct roots make later body replacement independent of attachment calibration.
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + "_PreviewVisual.prefab");
                report.Add(name + ": height=" + bounds.size.y.ToString("F3") + "m floor=" + bounds.min.y.ToString("F5") + "m, +Z forward, static body, " + attachments.GetComponentsInChildren<Renderer>().Length + " equipment renderers");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform Socket(string name, Transform parent, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }

        static void Attach(string name, Transform socket, float scale, Vector3 euler, string anchor = "HandGrip")
        {
            var wrapper = new GameObject(name + "_Placement").transform; wrapper.SetParent(socket, false);
            wrapper.localScale = Vector3.one * scale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/" + name + ".prefab"));
            if (model == null) throw new FileNotFoundException(name + " equipment prefab is missing");
            model.transform.SetParent(wrapper, false); // Preserve FBX axis conversion.
            var grip = model.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith(anchor, StringComparison.Ordinal));
            model.transform.position += socket.position - grip.position;
            wrapper.localRotation = Quaternion.Euler(euler);
            if (Vector3.Distance(grip.position, socket.position) > .001f) throw new InvalidOperationException(name + " grip offset");
        }

        static Material MaterialAt(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", .15f); EditorUtility.SetDirty(mat); return mat;
        }

        public static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No renderer: " + root.name);
            var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
        }

        static void BuildScene()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.62f,.64f,.68f); RenderSettings.fog = false;
                var sand = MaterialAt(Art + "/PreviewGround.mat", new Color(.34f,.28f,.20f));
                var trim = MaterialAt(Art + "/PreviewTrim.mat", new Color(.14f,.13f,.115f));
                Cube("Ground", new Vector3(0,-.075f,0), new Vector3(30,.1f,26),sand);
                for (int x=-7;x<=7;x++) Cube("Grid_X_"+x, new Vector3(x,-.023f,0),new Vector3(.012f,.004f,8),trim);
                for (int z=-4;z<=4;z++) Cube("Grid_Z_"+z, new Vector3(0,-.023f,z),new Vector3(14,.004f,.012f),trim);
                var light = new GameObject("Key Light").AddComponent<Light>(); light.type=LightType.Directional;
                light.color=new Color(1,.96f,.89f); light.intensity=1.8f; light.shadows=LightShadows.Soft;
                light.transform.rotation=Quaternion.Euler(45,145,0);
                var fill = new GameObject("Fill Light").AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.45f;
                fill.color=new Color(.80f,.87f,1); fill.transform.rotation=Quaternion.Euler(28,-35,0);
                var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.tag="MainCamera";
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.075f,.09f,.12f);
                camera.nearClipPlane=.05f; camera.farClipPlane=80; camera.gameObject.AddComponent<AudioListener>();
                var controls = new GameObject("Preview Controls").AddComponent<EnemyArtPreviewControls>();
                controls.previewCamera=camera; controls.displayNames=Labels; controls.heights=Heights;
                // Read saved prefab overrides, not the camera script's defaults.
                var playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
                if(playerPrefab!=null)
                {
                    var rig=playerPrefab.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null && c.GetType().Name=="PlayerCameraRig");
                    if(rig!=null)
                    {
                        var settings=new SerializedObject(rig);
                        controls.gameDistance=settings.FindProperty("distance").floatValue;
                        controls.gamePitch=settings.FindProperty("pitch").floatValue;
                        controls.gameShoulderOffset=settings.FindProperty("shoulderOffset").floatValue;
                        controls.gamePitchLift=settings.FindProperty("pitchLift").floatValue;
                        controls.gameMaxPitch=settings.FindProperty("pitchLimits").vector2Value.y;
                        var target=settings.FindProperty("target").objectReferenceValue as Transform;
                        if(target!=null) controls.gamePivotHeight=target.position.y-playerPrefab.transform.position.y;
                    }
                    var playerCamera=playerPrefab.GetComponentInChildren<Camera>(true);
                    if(playerCamera!=null) controls.gameFieldOfView=playerCamera.fieldOfView;
                }
                controls.displays=new Transform[5]; controls.lineupPositions=new Vector3[5]; controls.equipmentGroups=new GameObject[5];
                controls.stationLabels=new Transform[5];
                var animators = new List<Animator>();
                for (int i=0;i<5;i++)
                {
                    var station=new GameObject(Names[i]+" Display").transform;
                    station.position=new Vector3((2-i)*1.85f,0,0);
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+Names[i]+"/"+Names[i]+"_PreviewVisual.prefab"));
                    visual.transform.SetParent(station,false);
                    var baseplate=Cube(Names[i]+" Base",new Vector3(0,-.014f,0),new Vector3(1.5f,.028f,1.3f),trim);
                    baseplate.transform.SetParent(station,false);
                    var text = new GameObject("Name and Height").AddComponent<TextMesh>(); text.transform.SetParent(station,false);
                    text.transform.localPosition=new Vector3(0,.02f,.77f); text.transform.localRotation=Quaternion.Euler(60,180,0);
                    text.text=Names[i].ToUpperInvariant()+"\n"+Heights[i].ToString("F2")+"m";
                    text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center; text.fontSize=64; text.characterSize=.013f;
                    text.color=new Color(.92f,.79f,.52f);
                    controls.displays[i]=station; controls.lineupPositions[i]=station.position;
                    controls.stationLabels[i]=text.transform;
                    controls.equipmentGroups[i]=visual.transform.Find("PreviewAttachments").gameObject;
                    animators.AddRange(visual.GetComponentsInChildren<Animator>());
                }
                controls.capeAnimators=animators.ToArray(); controls.ApplyView();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            finally
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static GameObject Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.position=position; obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=material; Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }

        [MenuItem("SandGuard/Enemy/Open Art Preview")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Capture()
        {
            Directory.CreateDirectory(Docs);
            var scene = EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var controls = Object.FindFirstObjectByType<EnemyArtPreviewControls>();
            var cam=controls.previewCamera;
            cam.aspect=16f/9;
            controls.showInterface=false;
            for(int mode=0;mode<3;mode++)
            {
                controls.SelectCharacter(-1); controls.SetView(mode);
                SaveImage(cam,Docs+"/Lineup_"+new[]{"Front","Side","Rear"}[mode]+".png");
            }
            for(int i=0;i<5;i++)
            {
                controls.SelectCharacter(i); controls.SetView(0); SaveImage(cam,Docs+"/"+Names[i]+"_Front.png");
                controls.SetView(3); SaveImage(cam,Docs+"/"+Names[i]+"_GameDistance.png");
                if(controls.displays.Count(t=>t.gameObject.activeSelf)!=1 || cam.orthographic)
                    throw new InvalidOperationException("Solo camera mode failed");
            }
            controls.SetEquipmentVisible(false);
            if(controls.equipmentGroups.Any(g=>g.activeSelf)) throw new InvalidOperationException("Equipment toggle failed");
            controls.SetEquipmentVisible(true);
            controls.SelectCharacter(4); controls.SetView(2); SaveImage(cam,Docs+"/Chief_Rear.png");
            controls.SelectCharacter(-1); controls.SetView(0);
            if(controls.displays.Count(t=>t.gameObject.activeSelf)!=5 || !cam.orthographic)
                throw new InvalidOperationException("Lineup camera mode failed");
            File.WriteAllText(Docs+"/viewer-validation.txt","5-character selection, front/side/rear views, equipment toggle, and solo perspective passed.\nGame camera prefab settings: distance="+controls.gameDistance+"m, pitch="+controls.gamePitch+", FOV="+controls.gameFieldOfView+".\nScreenshots rendered in Unity; combat animation is not connected.\n");
            Debug.Log("ENEMY_ART_PREVIEW_CAPTURED");
        }

        public static void BuildAndCapture() { Build(); Capture(); }

        static void SaveImage(Camera camera,string path)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active; var previousTarget=camera.targetTexture;
            try
            {
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1600,900),0,0); tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            }
            finally { camera.targetTexture=previousTarget; RenderTexture.active=previous; rt.Release(); Object.DestroyImmediate(rt); }
        }
    }
}
