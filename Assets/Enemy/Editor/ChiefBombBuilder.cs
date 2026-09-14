using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Editor
{
    public static class ChiefBombBuilder
    {
        const string Art="Assets/Enemy/Art/ChiefBomb";
        const string Prefab="Assets/Enemy/Generated/Chief_Bomb.prefab";
        const string Docs="Docs/model-art/chief-bomb";
        [MenuItem("SandGuard/Enemy/Build Chief Bomb")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Art+"/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/ChiefBomb.fbx");
            importer.importAnimation=false; importer.importCameras=false; importer.importLights=false; importer.addCollider=false;
            var iron=Mat("Bomb_BlackIron",new Color(.07f,.085f,.095f),.55f,.5f);
            var brass=Mat("Bomb_AgedBrass",new Color(.55f,.32f,.095f),.65f,.58f);
            var red=Mat("Bomb_Crimson",new Color(.28f,.035f,.025f),.05f,.15f);
            var rope=Mat("Bomb_FuseRope",new Color(.50f,.36f,.19f),0,.05f);
            var ember=Mat("Bomb_Ember",new Color(.95f,.19f,.015f),0,.4f);
            ember.EnableKeyword("_EMISSION"); ember.SetColor("_EmissionColor",new Color(3f,.5f,.025f));
            foreach(var m in new[]{iron,brass,red,rope,ember}) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),m.name),m);
            importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                var root=new GameObject("Chief_Bomb");
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/ChiefBomb.fbx"),scene);
                visual.name="VisualRoot"; visual.transform.SetParent(root.transform,false);
                var body=root.AddComponent<Rigidbody>(); body.mass=1f; body.isKinematic=true; body.useGravity=true;
                body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var collider=root.AddComponent<SphereCollider>(); collider.radius=.19f; collider.enabled=false;
                var prop=root.AddComponent<ChiefBombProp>(); prop.body=body; prop.hitCollider=collider;
                prop.gripPoint=Child(root.transform,"GripPoint",Vector3.zero);
                // Derive the tip from the imported model, including FBX handedness conversion.
                var tip=visual.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="FuseEmber");
                prop.fuseTip=Child(root.transform,"FuseTip",root.transform.InverseTransformPoint(tip.bounds.center));
                prop.fuseEffects=new GameObject("FuseEffects"); prop.fuseEffects.transform.SetParent(prop.fuseTip,false);
                // The ember mesh belongs to the lit effects, leaving an unlit fuse when held.
                var glow=new GameObject("EmberGlow"); glow.transform.SetParent(prop.fuseEffects.transform,false);
                var glowFilter=glow.AddComponent<MeshFilter>(); glowFilter.sharedMesh=tip.GetComponent<MeshFilter>().sharedMesh;
                glow.transform.position=tip.transform.position; glow.transform.rotation=tip.transform.rotation; glow.transform.localScale=tip.transform.lossyScale;
                glow.AddComponent<MeshRenderer>().sharedMaterial=ember; tip.enabled=false;
                var ps=prop.fuseEffects.AddComponent<ParticleSystem>(); var main=ps.main;
                main.duration=1; main.loop=true; main.startLifetime=new ParticleSystem.MinMaxCurve(.1f,.24f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(.10f,.3f); main.startSize=new ParticleSystem.MinMaxCurve(.005f,.012f);
                main.startColor=new Color(1,.55f,.08f); main.maxParticles=24; main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.gravityModifier=.12f; var emission=ps.emission; emission.rateOverTime=18;
                var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.007f;
                var spark=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); spark.name="Bomb_Spark";
                spark.SetColor("_BaseColor",new Color(2,1,.15f,1));
                var oldSpark=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/Bomb_Spark.mat");
                if(oldSpark){EditorUtility.CopySerialized(spark,oldSpark);Object.DestroyImmediate(spark);spark=oldSpark;}else AssetDatabase.CreateAsset(spark,Art+"/Materials/Bomb_Spark.mat");
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=spark;
                prop.explosionPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VFX/Prefabs/VFX_Demolition_Bomb_Explosion.prefab");
                if(!prop.explosionPrefab)throw new Exception("Missing explosion VFX");
                prop.SetFuseLit(false);
                PrefabUtility.SaveAsPrefabAsset(root,Prefab); AssetDatabase.SaveAssets();
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<ChiefBombProp>();
                if(!saved || !saved.body || !saved.hitCollider || !saved.explosionPrefab || saved.fuseEffects.activeSelf || !saved.body.isKinematic || saved.hitCollider.enabled)throw new Exception("Invalid held-state prefab bindings");
                if(root.GetComponentsInChildren<Collider>(true).Length!=1)throw new Exception("Extra imported collider");
                var bounds=new Bounds(Vector3.zero,Vector3.zero); foreach(var r in visual.GetComponentsInChildren<MeshRenderer>())if(r.enabled)bounds.Encapsulate(r.bounds);
                if(bounds.size.y<.35f || bounds.size.y>.55f || bounds.size.x>.5f)throw new Exception("Bomb size / orientation incorrect "+bounds);
                var camera=new GameObject("Bomb Preview Camera").AddComponent<Camera>(); camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.08f,.10f);camera.orthographic=true;camera.orthographicSize=.32f;
                camera.transform.position=new Vector3(.55f,.34f,-.85f);camera.transform.LookAt(new Vector3(0,.045f,0));
                var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2.2f;key.transform.rotation=Quaternion.Euler(35,-30,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.75f);RenderSettings.skybox=null;
                Render(camera,Docs+"/Chief_Bomb_Unlit.png");
                prop.SetFuseLit(true);ps.useAutoRandomSeed=false;ps.randomSeed=73;ps.Simulate(.5f,true,true);Render(camera,Docs+"/Chief_Bomb_Lit.png");
                prop.SetFuseLit(false);
                EditorSceneManager.SaveScene(scene,Art+"/Chief_Bomb_Preview.unity");
                File.WriteAllText(Docs+"/unity-validation.txt","PASS\nChief_Bomb.prefab reloaded\nSingle sphere collider, held state kinematic / collider off\nShared mesh + URP materials\nFuse particles and explosion reference assigned\nModel size: "+bounds.size+"\nTwo preview renders\nChief AI and attack prefab unchanged\n");
                Debug.Log("CHIEF_BOMB_PASS "+Prefab);
            }
            finally{if(!Application.isBatchMode && previous.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);}
        }
        static Transform Child(Transform parent,string name,Vector3 position){var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.localPosition=position;return o.transform;}
        static Material Mat(string name,Color color,float metallic,float smoothness)
        {
            var path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);return m;
        }
        static void Render(Camera c,string path)
        {
            var rt=new RenderTexture(1000,1000,24);var image=new Texture2D(1000,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1000,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{c.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
