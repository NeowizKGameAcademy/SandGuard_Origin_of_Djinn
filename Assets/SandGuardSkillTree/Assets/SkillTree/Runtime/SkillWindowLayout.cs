using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.Skills.Unity
{
    /// <summary>Shared by the prefab authoring menu and runtime fallback. All text remains editable UI.</summary>
    public static class SkillWindowLayout
    {
        static readonly Color Slate=new Color(.065f,.09f,.11f,.98f), Ink=new Color(.09f,.13f,.16f,.98f);
        public static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        public static Image Pic(Transform p,string name,Sprite sprite,Color color,float x,float y,float w,float h,bool sliced=false)
        {var image=Rect(p,name,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;image.type=sliced?Image.Type.Sliced:Image.Type.Simple;return image;}
        public static Text Label(Transform p,Font font,string name,string value,int size,float x,float y,float w,float h,TextAnchor alignment=TextAnchor.MiddleLeft)
        {var t=Rect(p,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=new Color(.94f,.94f,.88f);t.alignment=alignment;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        static RectTransform Panel(Transform p,SkillUITheme t,string name,float x,float y,float w,float h)
        {var r=Rect(p,name,x,y,w,h);Pic(r,"Fill",null,Slate,2,2,w-4,h-4);Pic(r,"Frame",t.panel,t.gold,0,0,w,h,true);return r;}
        static Button Button(Transform p,SkillUITheme t,string name,string text,float x,float y,float w,float h,out Text label)
        {
            var r=Rect(p,name,x,y,w,h);Pic(r,"Fill",null,Ink,3,3,w-6,h-6);
            var image=r.gameObject.AddComponent<Image>();image.sprite=t.button;image.type=Image.Type.Sliced;image.color=t.gold;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var c=b.colors;c.highlightedColor=new Color(1,1,1,1);c.pressedColor=new Color(.65f,.85f,.9f);c.disabledColor=new Color(.48f,.48f,.48f,.65f);b.colors=c;
            label=Label(r,t.font,"Label",text,22,8,3,w-16,h-6,TextAnchor.MiddleCenter);return b;
        }
        public static void Build(SkillTreeWindow view)
        {
            var t=view.theme;if(!t)throw new System.InvalidOperationException("Skill UI Theme is required. Use the prefab creation menu.");
            var canvasObject=new GameObject("SkillTree Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasObject.transform.SetParent(view.transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var modal=Rect(canvasObject.transform,"Modal",0,0,0,0);modal.anchorMin=Vector2.zero;modal.anchorMax=Vector2.one;modal.sizeDelta=Vector2.zero;
            var backdrop=modal.gameObject.AddComponent<Image>();backdrop.color=new Color(0,0,0,.48f);view.modal=modal.gameObject;
            var win=Panel(modal,t,"Window",0,0,1480,850);win.anchorMin=win.anchorMax=win.pivot=new Vector2(.5f,.5f);win.anchoredPosition=Vector2.zero;
            Pic(win,"Scarab",t.ornament,Color.white,585,-45,310,104).preserveAspect=true;
            Label(win,t.font,"Title","스킬 구매 · 장착",38,330,35,800,55,TextAnchor.MiddleCenter);
            Label(win,t.font,"Subtitle","스킬 제단",18,540,88,400,26,TextAnchor.MiddleCenter);
            view.pointsLabel=Label(win,t.font,"Points","보유 포인트  0 SP",24,1140,45,250,48,TextAnchor.MiddleCenter);view.pointsLabel.color=t.cyan;
            Button(win,t,"Close","×",1400,30,50,50,out _);
            view.tabButtons=new Button[3];string[] tabs={"이동","공격","타워"};
            for(int i=0;i<3;i++)view.tabButtons[i]=Button(win,t,"Tab "+tabs[i],tabs[i],30+i*300,125,298,60,out _);
            var tree=Panel(win,t,"Tree",30,195,900,430);
            var viewport=Rect(tree,"Viewport",8,8,884,414);viewport.gameObject.AddComponent<RectMask2D>();
            view.treeContent=Rect(viewport,"Nodes",0,0,900,410);
            var scroll=tree.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=view.treeContent;scroll.horizontal=true;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.01f);
            Label(win,t.font,"Legend","● 해금 완료     ◇ 구매 가능     ○ 잠김    ·    드래그 / 휠로 이동",17,45,627,885,28);
            var detail=Panel(win,t,"Detail",950,125,500,530);
            Pic(detail,"IconRing",t.ring,t.cyan,192,20,116,116);
            view.detailIcon=Pic(detail,"SkillIcon",null,t.cyan,212,40,76,76);view.detailIcon.preserveAspect=true;
            view.detailTitle=Label(detail,t.font,"Name","스킬 선택",28,20,140,460,45,TextAnchor.MiddleCenter);
            view.detailBody=Label(detail,t.font,"Description","",21,32,193,436,255,TextAnchor.UpperLeft);
            view.buyButton=Button(detail,t,"Buy","구매",30,458,440,54,out view.buyLabel);
            Label(win,t.font,"LoadoutHeading","장착 편집",25,35,670,170,36);
            view.statusLabel=Label(win,t.font,"Status","",19,210,671,1225,35);
            view.slotButtons=new Button[5];view.removeButtons=new Button[5];view.slotLabels=new Text[5];view.slotIcons=new Image[5];
            string[] keys={"Q","E","R","Shift","Space"};
            for(int i=0;i<5;i++)
            {
                float x=35+i*282;view.slotButtons[i]=Button(win,t,"Slot "+keys[i],"",x,715,264,92,out _);
                var parent=view.slotButtons[i].transform;
                Label(parent,t.font,"Key",keys[i],19,8,5,80,27);
                view.slotIcons[i]=Pic(parent,"Icon",null,t.gold,15,35,44,44);view.slotIcons[i].preserveAspect=true;
                view.slotLabels[i]=Label(parent,t.font,"Skill","비어 있음",20,68,35,170,42);
                view.removeButtons[i]=Button(win,t,"Remove "+keys[i],"×",x+218,720,36,28,out _);
            }
            Label(win,t.font,"Footer","F / Esc 닫기    ·    스킬 선택 → 슬롯 클릭으로 장착    ·    × 장착 해제",17,120,811,1240,27,TextAnchor.MiddleCenter);
            var hint=Panel(canvasObject.transform,t,"Interaction Prompt",0,0,360,58);hint.anchorMin=hint.anchorMax=hint.pivot=new Vector2(.5f,.5f);hint.anchoredPosition=new Vector2(0,-220);
            view.prompt=hint.gameObject;view.promptLabel=Label(hint,t.font,"Text","F   스킬 제단",25,10,4,340,50,TextAnchor.MiddleCenter);
            hint.gameObject.SetActive(false);
        }
        public static void Line(Transform parent,Vector2 start,Vector2 end,Color color)
        {
            Vector2 delta=new Vector2(end.x-start.x,-end.y+start.y);
            var image=Pic(parent,"Prerequisite",null,color,start.x,start.y,delta.magnitude,3);
            image.rectTransform.pivot=new Vector2(0,.5f);image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        public static Button Node(Transform parent,SkillUITheme t,string name,int cost,Vector2 center,Sprite icon,Color color,bool selected)
        {
            var r=Rect(parent,"Node "+name,center.x-78,center.y-70,156,145);
            var ring=Pic(r,"Ring",t.ring,color,27,0,102,102);ring.raycastTarget=true;
            if(selected)Pic(r,"Selected",t.ring,t.cyan,21,-6,114,114);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=ring;
            Pic(r,"Icon",icon,color,48,21,60,60).preserveAspect=true;
            Label(r,t.font,"Name",name,20,0,105,156,28,TextAnchor.MiddleCenter);
            var c=Label(r,t.font,"Cost",cost+" SP",18,0,133,156,25,TextAnchor.MiddleCenter);c.color=color;
            return b;
        }
    }
}
