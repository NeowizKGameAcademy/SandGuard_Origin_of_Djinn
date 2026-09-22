#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.UI.HUD.Editor
{
    public static class HUDSceneBuilder
    {
        private const string ScenePath = "Assets/1.Scene/HUD.unity";
        private const string PrefabRoot = "Assets/Prefabs/HUD";
        private const string SpriteRoot = "Assets/4.Sprite/UI/GameScene/HUD";
        private static readonly Color Cream = new(1f, .94f, .78f, 1f);
        private static readonly Color Gold = new(1f, .72f, .18f, 1f);
        private static readonly Color Dark = new(.035f, .025f, .05f, .82f);

        [MenuItem("Tools/SandGuard/Build HUD Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Prefabs", "HUD");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "GameHUDCanvas") UnityEngine.Object.DestroyImmediate(root);

            GameObject player = BuildPlayer(); Save(player, "PlayerStatusHUD");
            GameObject core = BuildCore(); Save(core, "CoreStatusHUD");
            GameObject boss = BuildBoss(); Save(boss, "BossStatusHUD");
            GameObject wave = BuildWave(); Save(wave, "WaveHUD");
            GameObject combatSlot = BuildCombatSlot("CombatSkillSlot", Sprite("SkillContaioer/SkillContainer_Q.png"), "Q"); Save(combatSlot, "CombatSkillSlot");
            GameObject combat = BuildCombat(); Save(combat, "CombatSkillHUD");
            GameObject movementSlot = BuildMovementSlot("MovementSkillSlot", "대시"); Save(movementSlot, "MovementSkillSlot");
            GameObject movement = BuildMovement(); Save(movement, "MovementSkillHUD");
            GameObject minimap = BuildMinimap(); Save(minimap, "MinimapHUD");

            DestroyAll(player, core, boss, wave, combatSlot, combat, movementSlot, movement, minimap);

            GameObject canvasGo = new("GameHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            Stretch(canvasGo.GetComponent<RectTransform>());
            GameObject safe = Rect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            GameObject gameHUD = Rect("GameHUD", safe.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);

            GameObject minimapI = Instance("MinimapHUD", gameHUD.transform); Anchor(minimapI, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-30,-115), new Vector2(280,280));
            GameObject top = Rect("TopHUD", gameHUD.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            GameObject coreI = Instance("CoreStatusHUD", top.transform); Anchor(coreI, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(-390,-25), new Vector2(330,74));
            GameObject bossI = Instance("BossStatusHUD", top.transform); Anchor(bossI, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-22), new Vector2(610,82));
            GameObject waveI = Instance("WaveHUD", top.transform); Anchor(waveI, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-30,-25), new Vector2(250,72));
            GameObject playerI = Instance("PlayerStatusHUD", gameHUD.transform); Anchor(playerI, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(26,26), new Vector2(625,170));
            GameObject combatI = Instance("CombatSkillHUD", gameHUD.transform); Anchor(combatI, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,28), new Vector2(450,112));
            GameObject movementI = Instance("MovementSkillHUD", gameHUD.transform); Anchor(movementI, new Vector2(1,0), new Vector2(1,0), new Vector2(1,0), new Vector2(-30,28), new Vector2(360,130));
            Rect("Overlay", gameHUD.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);

            GameHUDController controller = gameHUD.AddComponent<GameHUDController>();
            Set(controller,"playerStatus",playerI.GetComponent<PlayerStatusHUD>()); Set(controller,"coreStatus",coreI.GetComponent<CoreStatusHUD>()); Set(controller,"bossStatus",bossI.GetComponent<BossStatusHUD>()); Set(controller,"wave",waveI.GetComponent<WaveHUD>()); Set(controller,"combatSkills",combatI.GetComponent<CombatSkillHUD>()); Set(controller,"movementSkills",movementI.GetComponent<MovementSkillHUD>()); Set(controller,"minimap",minimapI.GetComponent<MinimapHUD>());
            HUDDebugController debug = gameHUD.AddComponent<HUDDebugController>(); Set(debug,"hud",controller);
            PrefabUtility.SaveAsPrefabAsset(gameHUD, $"{PrefabRoot}/GameHUD.prefab");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Selection.activeGameObject = canvasGo;
            Debug.Log("[HUD] HUD scene and prefabs built successfully.");
        }

        public static void BuildBatch() { Build(); EditorApplication.Exit(0); }

        private static GameObject BuildPlayer()
        {
            GameObject root=Root("PlayerStatusHUD",new Vector2(625,170)); var view=root.AddComponent<PlayerStatusHUD>();
            GameObject badge=Rect("LevelBadge",root.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(0,12),new Vector2(145,145)); Image("Background",badge.transform,Sprite("PlayerStatus/LevelPannel.png"),Color.white,true);
            Text("LevelLabel_TMP",badge.transform,"LEVEL",18,Gold,new Vector2(0,28),new Vector2(100,26)); var lvl=Text("LevelValue_TMP",badge.transform,"12",44,Cream,new Vector2(0,-8),new Vector2(100,55));
            GameObject panel=Rect("StatusPanel",root.transform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(120,0),new Vector2(-120,0)); Image("Background",panel.transform,Sprite("PlayerStatus/Container.png"),Color.white,false);
            var hp=StatusRow("HP",panel.transform,95,Sprite("PlayerStatus/HP_ICON.png"),Sprite("PlayerStatus/HP.png"),"체력",new Color(.92f,.18f,.13f,1),out var hpText);
            var ex=StatusRow("EXP",panel.transform,52,Sprite("PlayerStatus/EXP_ICON.png"),Sprite("PlayerStatus/EXP.png"),"경험치",new Color(1f,.75f,.12f,1),out var exText);
            var mp=StatusRow("Mana",panel.transform,9,Sprite("PlayerStatus/MP_ICON.png"),Sprite("PlayerStatus/MP.png"),"마나",new Color(.12f,.84f,1f,1),out var mpText);
            Set(view,"levelValueText",lvl); Set(view,"hpFill",hp); Set(view,"hpValueText",hpText); Set(view,"expFill",ex); Set(view,"expValueText",exText); Set(view,"manaFill",mp); Set(view,"manaValueText",mpText); return root;
        }
        private static Image StatusRow(string name,Transform p,float y,Sprite icon,Sprite fillSprite,string label,Color color,out TMP_Text value)
        {
            GameObject row=Rect(name,p,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(25,y),new Vector2(455,38)); Image("Icon",row.transform,icon,Color.white,true,new Vector2(16,19),new Vector2(34,34)); Text("Label_TMP",row.transform,label,17,Cream,new Vector2(70,19),new Vector2(70,28),TextAlignmentOptions.Left);
            Image("BarBackground",row.transform,null,new Color(.02f,.02f,.03f,.8f),false,new Vector2(267,19),new Vector2(300,22)); Image fill=Image("Fill",row.transform,fillSprite,color,false,new Vector2(267,19),new Vector2(294,16)); fill.type=UnityEngine.UI.Image.Type.Filled; fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin=0;
            value=Text("Value_TMP",row.transform,"0 / 0",16,Cream,new Vector2(267,19),new Vector2(270,25)); return fill;
        }
        private static GameObject BuildCore(){ GameObject r=Root("CoreStatusHUD",new Vector2(330,74)); Image("Background",r.transform,Sprite("CoreHP.png"),Color.white,false); Text("Label_TMP",r.transform,"코어 안정도",19,Cream,new Vector2(0,16),new Vector2(200,26)); GameObject b=Rect("Bar",r.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Vector2.zero); Image("BarBackground",b.transform,null,new Color(.03f,.02f,.02f,.8f),false,new Vector2(0,-17),new Vector2(260,16)); Image f=Image("Fill",b.transform,Sprite("CoreHP.png"),new Color(1f,.56f,.1f,1),false,new Vector2(0,-17),new Vector2(256,12)); f.type=UnityEngine.UI.Image.Type.Filled; f.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; TMP_Text v=Text("Value_TMP",b.transform,"70 / 100",13,Cream,new Vector2(0,-17),new Vector2(180,20)); var c=r.AddComponent<CoreStatusHUD>(); Set(c,"fill",f); Set(c,"valueText",v); return r; }
        private static GameObject BuildBoss(){ GameObject r=Root("BossStatusHUD",new Vector2(610,82)); Image("Background",r.transform,Sprite("BossHP.png"),Color.white,false); Image icon=Image("BossIcon",r.transform,null,Color.white,true,new Vector2(-260,0),new Vector2(58,58)); TMP_Text n=Text("BossName_TMP",r.transform,"사막의 수호자",21,Gold,new Vector2(0,22),new Vector2(300,28)); GameObject hp=Rect("HP",r.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Vector2.zero); Image("BarBackground",hp.transform,null,new Color(.03f,.01f,.01f,.85f),false,new Vector2(10,-17),new Vector2(480,18)); Image f=Image("Fill",hp.transform,Sprite("BossHP.png"),new Color(.83f,.08f,.08f,1),false,new Vector2(10,-17),new Vector2(474,13)); f.type=UnityEngine.UI.Image.Type.Filled; f.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; TMP_Text v=Text("Value_TMP",hp.transform,"7,500 / 10,000",13,Cream,new Vector2(10,-17),new Vector2(240,20)); var c=r.AddComponent<BossStatusHUD>(); Set(c,"bossIcon",icon);Set(c,"bossNameText",n);Set(c,"hpFill",f);Set(c,"hpValueText",v); return r; }
        private static GameObject BuildWave(){ GameObject r=Root("WaveHUD",new Vector2(250,72)); Image("Background",r.transform,Sprite("WaveContainer.png"),Color.white,false); Text("WaveLabel_TMP",r.transform,"웨이브",20,Cream,new Vector2(-38,0),new Vector2(90,30)); TMP_Text v=Text("WaveValue_TMP",r.transform,"03",30,Gold,new Vector2(55,0),new Vector2(70,38)); var c=r.AddComponent<WaveHUD>();Set(c,"waveValueText",v);return r; }
        private static GameObject BuildCombatSlot(string name,Sprite frame,string key){ GameObject r=Root(name,new Vector2(100,108)); Image("Frame",r.transform,frame,Color.white,true); Image icon=Image("Icon",r.transform,null,Color.white,true,new Vector2(0,3),new Vector2(58,58)); Image cd=Image("CooldownOverlay",r.transform,null,new Color(0,0,0,.68f),false); cd.type=UnityEngine.UI.Image.Type.Filled;cd.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;cd.fillOrigin=2; TMP_Text ct=Text("CooldownText_TMP",r.transform,"",22,Cream,Vector2.zero,new Vector2(80,34)); CooldownTextStyle.Apply(ct); TMP_Text kt=Text("KeyText_TMP",r.transform,key,17,Gold,new Vector2(-32,-38),new Vector2(28,22)); GameObject locked=Image("LockedOverlay",r.transform,null,new Color(.05f,.05f,.05f,.72f),false).gameObject;locked.SetActive(false); var c=r.AddComponent<CombatSkillSlotHUD>();Set(c,"icon",icon);Set(c,"cooldownOverlay",cd);Set(c,"cooldownText",ct);Set(c,"keyText",kt);Set(c,"lockedOverlay",locked);return r; }
        private static GameObject BuildCombat(){ GameObject r=Root("CombatSkillHUD",new Vector2(450,112)); string[] keys={"Q","E","R","F"}; string[] files={"Q","E","R","F"}; CombatSkillSlotHUD[] slots=new CombatSkillSlotHUD[4]; for(int i=0;i<4;i++){ GameObject s=BuildCombatSlot("Skill_"+keys[i],Sprite("SkillContaioer/SkillContainer_"+files[i]+".png"),keys[i]);s.transform.SetParent(r.transform,false);Anchor(s,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2((i-1.5f)*108f,0),new Vector2(100,108));slots[i]=s.GetComponent<CombatSkillSlotHUD>(); } var c=r.AddComponent<CombatSkillHUD>();Set(c,"q",slots[0]);Set(c,"e",slots[1]);Set(c,"r",slots[2]);Set(c,"f",slots[3]);return r; }
        private static GameObject BuildMovementSlot(string name,string title){ GameObject r=Root(name,new Vector2(112,130)); TMP_Text t=Text("Name_TMP",r.transform,title,17,Cream,new Vector2(0,50),new Vector2(110,24)); Image("Frame",r.transform,Sprite("SkillContaioer/SkillContainer_Movement.png"),Color.white,true,new Vector2(0,-8),new Vector2(94,94)); Image icon=Image("Icon",r.transform,null,Color.white,true,new Vector2(0,-8),new Vector2(52,52)); Image cd=Image("CooldownRing",r.transform,null,new Color(0,0,0,.66f),false,new Vector2(0,-8),new Vector2(82,82));cd.type=UnityEngine.UI.Image.Type.Filled;cd.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;cd.fillOrigin=2;TMP_Text ct=Text("CooldownText_TMP",r.transform,"",21,Cream,new Vector2(0,-8),new Vector2(70,30));CooldownTextStyle.Apply(ct);var c=r.AddComponent<MovementSkillSlotHUD>();Set(c,"titleText",t);Set(c,"icon",icon);Set(c,"cooldownRing",cd);Set(c,"cooldownText",ct);return r; }
        private static GameObject BuildMovement(){ GameObject r=Root("MovementSkillHUD",new Vector2(360,130)); GameObject d=BuildMovementSlot("DashSlot","대시");d.transform.SetParent(r.transform,false);Anchor(d,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-116,0),new Vector2(112,130));GameObject j=BuildMovementSlot("DoubleJumpSlot","더블 점프");j.transform.SetParent(r.transform,false);Anchor(j,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,0),new Vector2(112,130));GameObject k=BuildMovementSlot("RecallSlot","흔적 귀환");k.transform.SetParent(r.transform,false);Anchor(k,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(116,0),new Vector2(112,130));var c=r.AddComponent<MovementSkillHUD>();Set(c,"dash",d.GetComponent<MovementSkillSlotHUD>());Set(c,"doubleJump",j.GetComponent<MovementSkillSlotHUD>());Set(c,"recall",k.GetComponent<MovementSkillSlotHUD>());return r; }
        private static GameObject BuildMinimap(){ GameObject r=Root("MinimapHUD",new Vector2(280,280)); GameObject vp=Rect("MapViewport",r.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(226,226)); Image maskImg=vp.AddComponent<Image>();maskImg.color=new Color(.05f,.04f,.06f,1);maskImg.raycastTarget=false;vp.AddComponent<Mask>().showMaskGraphic=true;RawImage raw=Rect("MinimapRawImage",vp.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Vector2.zero).AddComponent<RawImage>();raw.raycastTarget=false;raw.color=new Color(.25f,.18f,.1f,1);GameObject markers=Rect("MarkerRoot",vp.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Vector2.zero);Image("Frame",r.transform,Sprite("MinimapContainer.png"),Color.white,true);var c=r.AddComponent<MinimapHUD>();Set(c,"minimapRawImage",raw);Set(c,"markerRoot",markers.GetComponent<RectTransform>());return r; }

        private static GameObject Root(string n,Vector2 size){ GameObject g=new(n,typeof(RectTransform));g.GetComponent<RectTransform>().sizeDelta=size;return g; }
        private static GameObject Rect(string n,Transform p,Vector2 amin,Vector2 amax,Vector2 pivot,Vector2 pos,Vector2 size){GameObject g=new(n,typeof(RectTransform));g.transform.SetParent(p,false);RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=amin;r.anchorMax=amax;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;return g;}
        private static Image Image(string n,Transform p,Sprite s,Color c,bool aspect,Vector2? pos=null,Vector2? size=null){GameObject g=Rect(n,p,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos??Vector2.zero,size??((RectTransform)p).rect.size);Image i=g.AddComponent<Image>();i.sprite=s;i.color=c;i.preserveAspect=aspect;i.raycastTarget=false;return i;}
        private static TMP_Text Text(string n,Transform p,string text,float size,Color color,Vector2 pos,Vector2 area,TextAlignmentOptions align=TextAlignmentOptions.Center){GameObject g=Rect(n,p,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,area);var t=g.AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.enableWordWrapping=false;return t;}
        private static void Anchor(GameObject g,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size){RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;}
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
        private static Sprite Sprite(string relative)=>AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}/{relative}");
        private static void Set(UnityEngine.Object o,string field,UnityEngine.Object value){SerializedObject so=new(o);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Save(GameObject g,string n){PrefabUtility.SaveAsPrefabAsset(g,$"{PrefabRoot}/{n}.prefab");}
        private static GameObject Instance(string n,Transform p){GameObject g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{n}.prefab"),p);g.name=n;return g;}
        private static void DestroyAll(params GameObject[] gs){foreach(var g in gs)UnityEngine.Object.DestroyImmediate(g);}
        private static void EnsureFolder(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
    }
}
#endif
