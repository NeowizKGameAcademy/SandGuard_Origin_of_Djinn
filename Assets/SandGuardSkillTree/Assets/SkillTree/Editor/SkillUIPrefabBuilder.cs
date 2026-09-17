using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SandGuard.Skills.Unity;

namespace SandGuard.Skills.Editor
{
    public static class SkillUIPrefabBuilder
    {
        public static string Root
        {
            get
            {
                var guid=AssetDatabase.FindAssets("SkillUIPrefabBuilder t:MonoScript").First();
                return Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid))).Replace('\\','/');
            }
        }
        [MenuItem("SandGuard/Skills/Create Styled UI Prefabs")]
        public static void CreatePrefabs()
        {
            string dir=Root+"/Generated";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(Root+"/Art","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                if(path.Contains("Frame"))importer.spriteBorder=new Vector4(24,24,24,24);
                importer.SaveAndReimport();
            }
            var theme=AssetDatabase.LoadAssetAtPath<SkillUITheme>(dir+"/SkillUITheme.asset");
            if(!theme)
            {
                theme=ScriptableObject.CreateInstance<SkillUITheme>();
                theme.font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Art/SkillUIFont.ttf");
                if(!theme.font)theme.font=AssetDatabase.FindAssets("Pretendard-Medium t:Font").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Font>).FirstOrDefault(f=>f);
                if(!theme.font)theme.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                theme.panel=Sprite("PanelFrame");theme.button=Sprite("ButtonFrame");theme.ring=Sprite("NodeRing");theme.ornament=Sprite("HeaderScarab");
                string[] ids={"move.dash","move.jump","move.recall","attack.pierce","attack.burst","attack.vortex","attack.storm","tower.cobra","tower.obelisk","tower.skeleton","tower.anubis"};
                theme.icons=ids.Select(id=>new SkillUITheme.IconEntry{skillId=id,sprite=Sprite(id)}).ToArray();
                AssetDatabase.CreateAsset(theme,dir+"/SkillUITheme.asset");
            }
            var data=AssetDatabase.LoadAssetAtPath<SkillTreeAsset>(dir+"/SkillTreeData.asset");
            if(!data){data=SkillTreeAsset.CreateDemo();AssetDatabase.CreateAsset(data,dir+"/SkillTreeData.asset");}
            UpdateObelisk(theme,data);
            // Existing edited prefabs are kept. Subsequent runs create a numbered sibling.
            var root=new GameObject("SkillTree UI");
            try
            {
                var session=root.AddComponent<SkillTreeSession>();session.definition=data;
                var window=root.AddComponent<SkillTreeWindow>();window.session=session;window.theme=theme;
                SkillWindowLayout.Build(window);window.modal.SetActive(false);
                string path=AssetDatabase.GenerateUniqueAssetPath(dir+"/SkillTreeUI.prefab");
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Selection.activeObject=prefab;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var anchor=new GameObject("Skill Altar Anchor");
            try{anchor.AddComponent<SkillAltarAnchor>();PrefabUtility.SaveAsPrefabAsset(anchor,AssetDatabase.GenerateUniqueAssetPath(dir+"/SkillAltarAnchor.prefab"));}
            finally{UnityEngine.Object.DestroyImmediate(anchor);}
            AssetDatabase.SaveAssets();Debug.Log("스킬 UI / 제단 프리팹 생성 완료: "+dir);
        }
        static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/"+name+".png");
        [MenuItem("SandGuard/Skills/Apply Obelisk Name And Icon")]
        public static void ApplyObelisk()
        {
            string iconPath=Root+"/Art/tower.obelisk.png";
            AssetDatabase.ImportAsset(iconPath,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(iconPath);
            if(importer.textureType!=TextureImporterType.Sprite)
            {
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            string dir=Root+"/Generated";
            var theme=AssetDatabase.LoadAssetAtPath<SkillUITheme>(dir+"/SkillUITheme.asset");
            var data=AssetDatabase.LoadAssetAtPath<SkillTreeAsset>(dir+"/SkillTreeData.asset");
            if(!theme || !data)throw new InvalidOperationException("Skill tree theme or data is missing.");
            UpdateObelisk(theme,data);
            AssetDatabase.SaveAssets();
            Debug.Log("[Skills] Obelisk name and simplified icon applied.");
        }
        static void UpdateObelisk(SkillUITheme theme,SkillTreeAsset data)
        {
            var icon=Sprite("tower.obelisk");
            if(!icon)throw new InvalidOperationException("Obelisk icon is missing.");
            var entry=theme.icons.FirstOrDefault(x=>x!=null && x.skillId=="tower.obelisk");
            if(entry==null)
            {
                entry=new SkillUITheme.IconEntry{skillId="tower.obelisk"};
                theme.icons=theme.icons.Concat(new[]{entry}).ToArray();
            }
            entry.sprite=icon;
            var row=data.nodes.FirstOrDefault(x=>x!=null && x.id=="tower.obelisk");
            if(row==null)throw new InvalidOperationException("Obelisk skill is missing.");
            row.displayName="오벨리스크 해금";
            row.description="구매하면 오벨리스크를 설치할 수 있습니다.";
            EditorUtility.SetDirty(theme);
            EditorUtility.SetDirty(data);
        }
        [MenuItem("SandGuard/Skills/Create Styled Preview Scene")]
        public static void CreatePreview()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Generated/SkillTreeUI.prefab"))CreatePrefabs();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Generated/SkillTreeUI.prefab");
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);obj.GetComponent<SkillTreeWindow>().standalonePreview=true;
            obj.GetComponent<SkillTreeSession>().startingPoints=20;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.15f,.18f);
            Selection.activeGameObject=obj;EditorSceneManager.MarkSceneDirty(scene);
        }
        [MenuItem("SandGuard/Skills/Connect Selected UI To Scene Player")]
        public static void ConnectPlayer()
        {
            var window=Selection.activeGameObject?Selection.activeGameObject.GetComponent<SkillTreeWindow>():null;
            if(!window){Debug.LogError("씬의 SkillTree UI를 선택하세요.");return;}
            var all=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var input=all.FirstOrDefault(b=>b.GetType().Name=="PlayerInputReader");
            if(!input){Debug.LogError("PlayerInputReader를 찾지 못했습니다. Player를 직접 지정하세요.");return;}
            Undo.RecordObject(window,"Connect skill UI player");window.player=input.transform;
            window.suspendWhileOpen=all.Where(b=>b==input || b.GetType().Name=="FacilityBuildMenu").Cast<Behaviour>().ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(window);EditorUtility.SetDirty(window);
        }
        [MenuItem("SandGuard/Skills/Upgrade Selected Test Panel")]
        public static void UpgradeSelected()
        {
            var go=Selection.activeGameObject;
            var session=go?go.GetComponent<SkillTreeSession>():null;
            if(!session){Debug.LogError("기존 SkillTreeSession 오브젝트를 선택하세요.");return;}
            if(!AssetDatabase.LoadAssetAtPath<SkillUITheme>(Root+"/Generated/SkillUITheme.asset"))CreatePrefabs();
            var old=go.GetComponent<SkillTreeTestPanel>();if(old)Undo.DestroyObjectImmediate(old);
            var window=go.GetComponent<SkillTreeWindow>();if(window)return;
            window=Undo.AddComponent<SkillTreeWindow>(go);window.session=session;
            window.theme=AssetDatabase.LoadAssetAtPath<SkillUITheme>(Root+"/Generated/SkillUITheme.asset");
            SkillWindowLayout.Build(window);window.modal.SetActive(false);
            Undo.RegisterCreatedObjectUndo(window.modal.transform.parent.gameObject,"Create skill canvas");
            EditorUtility.SetDirty(window);EditorSceneManager.MarkSceneDirty(go.scene);Selection.activeGameObject=go;
        }
    }
}
