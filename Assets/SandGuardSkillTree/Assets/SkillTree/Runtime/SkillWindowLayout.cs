using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.Skills.Unity
{
    /// <summary>Shared by the prefab authoring menu and runtime fallback. All text remains editable UI.</summary>
    public static class SkillWindowLayout
    {
        static readonly Color Slate=new Color(.065f,.055f,.05f,.98f), Ink=new Color(.035f,.065f,.09f,.98f);
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
        {var r=Rect(p,name,x,y,w,h);Pic(r,"Fill",null,Slate,2,2,w-4,h-4);Pic(r,"Frame",SkillLampSkin.Available?SkillLampSkin.Panel:t.panel,SkillLampSkin.Available?Color.white:t.gold,0,0,w,h,true);return r;}
        static Button Button(Transform p,SkillUITheme t,string name,string text,float x,float y,float w,float h,out Text label)
        {
            var r=Rect(p,name,x,y,w,h);Pic(r,"Fill",null,Ink,3,3,w-6,h-6);
            var image=r.gameObject.AddComponent<Image>();image.sprite=SkillLampSkin.Available?SkillLampSkin.Button:t.button;image.type=Image.Type.Sliced;image.color=SkillLampSkin.Available?Color.white:t.gold;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var c=b.colors;c.highlightedColor=new Color(1,1,1,1);c.pressedColor=new Color(.65f,.85f,.9f);c.disabledColor=new Color(.48f,.48f,.48f,.65f);b.colors=c;
            if(SkillLampSkin.Available)SkillLampSkin.StyleButton(b);
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
            Pic(win,"Lamp",SkillLampSkin.Available?SkillLampSkin.Lamp:t.ornament,Color.white,585,-83,310,104).preserveAspect=true;
            Label(win,t.font,"Title","스킬 구매 · 장착",38,330,35,800,55,TextAnchor.MiddleCenter);
            Label(win,t.font,"Subtitle","모래가 흐르듯, 새로운 힘을 손에 넣으세요.",18,420,88,640,26,TextAnchor.MiddleCenter);
            Pic(win,"Point Badge",SkillLampSkin.Available?SkillLampSkin.Button:t.button,Color.white,1115,43,275,51,true);
            view.pointsLabel=Label(win,t.font,"Points","보유 포인트  0 SP",24,1140,45,250,48,TextAnchor.MiddleCenter);view.pointsLabel.color=t.cyan;
            Button(win,t,"Close","X",1400,30,50,50,out var closeLabel);closeLabel.fontSize=28;closeLabel.fontStyle=FontStyle.Bold;closeLabel.color=t.gold;
            view.tabButtons=new Button[3];string[] tabs={"이동","공격","타워"};
            for(int i=0;i<3;i++)view.tabButtons[i]=Button(win,t,"Tab "+tabs[i],tabs[i],30+i*300,125,298,60,out _);
            var tree=Panel(win,t,"Tree",30,195,900,430);
            if(SkillLampSkin.Available)Pic(tree,"Desert Background",SkillLampSkin.Background,new Color(1,1,1,.75f),17,17,866,396);
            var viewport=Rect(tree,"Viewport",8,8,884,414);viewport.gameObject.AddComponent<RectMask2D>();
            view.treeContent=Rect(viewport,"Nodes",0,0,900,410);
            var scroll=tree.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=view.treeContent;scroll.horizontal=true;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.01f);
            Label(win,t.font,"Legend","드래그 / 휠로 이동",17,45,627,885,28);
            var detail=Panel(win,t,"Detail",950,125,500,530);
            if(SkillLampSkin.Available)
            {
                Pic(detail,"IconRing",SkillLampSkin.Node,Color.white,172,15,156,156);
                SkillLampSkin.Disc(detail,195,38,110,new Color(.025f,.06f,.07f));
            }
            else Pic(detail,"IconRing",t.ring,t.cyan,192,20,116,116);
            view.detailIcon=Pic(detail,"SkillIcon",null,t.cyan,212,54,76,76);view.detailIcon.preserveAspect=true;
            view.detailTitle=Label(detail,t.font,"Name","스킬 선택",28,20,174,460,45,TextAnchor.MiddleCenter);
            Pic(detail,"Divider",null,t.gold,30,214,440,1);
            view.detailBody=Label(detail,t.font,"Description","",21,32,226,436,214,TextAnchor.UpperLeft);
            view.buyButton=Button(detail,t,"Buy","구매",30,458,440,54,out view.buyLabel);
            Label(win,t.font,"LoadoutHeading","장착 편집",25,35,670,170,36);
            view.statusLabel=Label(win,t.font,"Status","",19,210,671,1225,35);
            var slots=EquipSlotInfo.Order;int count=slots.Length;
            view.slotButtons=new Button[count];view.removeButtons=new Button[count];view.slotLabels=new Text[count];view.slotIcons=new Image[count];
            // 한 줄에 일곱 칸(Q E R F Shift Space 우클릭)이 들어가도록 너비 190 / 간격 201로 줄였다.
            for(int i=0;i<count;i++)
            {
                string key=EquipSlotInfo.Key(slots[i]);
                float x=35+i*201;view.slotButtons[i]=Button(win,t,"Slot "+slots[i],"",x,715,190,92,out _);
                var parent=view.slotButtons[i].transform;
                Label(parent,t.font,"Empty Marker","⊕",34,15,36,42,42,TextAnchor.MiddleCenter).color=t.muted;
                Label(parent,t.font,"Key",key,17,8,5,110,25);
                view.slotIcons[i]=Pic(parent,"Icon",null,t.gold,13,36,40,40);view.slotIcons[i].preserveAspect=true;
                view.slotLabels[i]=Label(parent,t.font,"Skill","비어 있음",17,58,36,108,42);
                view.removeButtons[i]=Button(win,t,"Remove "+slots[i],"X",x+148,720,32,26,out var removeLabel);
                removeLabel.fontSize=16;removeLabel.fontStyle=FontStyle.Bold;removeLabel.color=t.gold;
            }
            Label(win,t.font,"Footer","Tab / Esc 닫기    ·    이동은 구매 시 자동 장착(Shift·Space·우클릭)    ·    공격은 Q/E/R/F 슬롯 클릭으로 장착",17,120,811,1240,27,TextAnchor.MiddleCenter);
            RectTransform hint;
            if(SkillLampSkin.Prompt)
            {
                hint=Rect(canvasObject.transform,"Interaction Prompt",0,0,360,58);
                Pic(hint,"Rounded Frame",SkillLampSkin.Prompt,Color.white,0,0,360,58,true);
            }
            else hint=Panel(canvasObject.transform,t,"Interaction Prompt",0,0,360,58);
            hint.anchorMin=hint.anchorMax=hint.pivot=new Vector2(.5f,.5f);hint.anchoredPosition=new Vector2(0,-220);
            view.prompt=hint.gameObject;view.promptLabel=Label(hint,t.font,"Text","Tab   스킬 제단",25,10,4,340,50,TextAnchor.MiddleCenter);
            hint.gameObject.SetActive(false);
        }
        public static void Line(Transform parent,Vector2 start,Vector2 end,Color color)
        {
            Vector2 previous=start;
            for(int i=1;i<=20;i++)
            {
                float t=i/20f;Vector2 next=Vector2.Lerp(start,end,t);next.y+=Mathf.Sin(t*Mathf.PI)*24f;
                Vector2 delta=new Vector2(next.x-previous.x,previous.y-next.y);
                var glow=Pic(parent,"Link Glow",null,new Color(color.r,color.g,color.b,.15f),previous.x,previous.y,delta.magnitude+1,8);
                glow.rectTransform.pivot=new Vector2(0,.5f);glow.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                var line=Pic(parent,"Link",null,color,previous.x,previous.y,delta.magnitude+1,2);
                line.rectTransform.pivot=new Vector2(0,.5f);line.rectTransform.localRotation=glow.rectTransform.localRotation;previous=next;
            }
        }
        public static Button Node(Transform parent,SkillUITheme t,string name,int cost,Vector2 center,Sprite icon,Color color,bool selected)
        {
            // 166 px includes the ring, skill name and the complete SP label.
            var r=Rect(parent,"Node "+name,center.x-78,center.y-70,156,166);
            var ring=Pic(r,"Ring",SkillLampSkin.Available?SkillLampSkin.Node:t.ring,SkillLampSkin.Available?(color==t.muted?new Color(.4f,.4f,.4f):Color.white):color,18,-8,120,120);ring.raycastTarget=true;
            if(SkillLampSkin.Available)SkillLampSkin.Disc(r,36,10,84,new Color(.025f,.05f,.065f));
            if(selected)Pic(r,"Selected",t.ring,t.cyan,11,-15,134,134);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=ring;
            Pic(r,"Icon",icon,color,48,21,60,60).preserveAspect=true;
            Label(r,t.font,"Name",name,20,0,105,156,28,TextAnchor.MiddleCenter);
            var c=Label(r,t.font,"Cost",cost+" SP",18,0,133,156,25,TextAnchor.MiddleCenter);c.color=color;
            if(color==t.muted)Label(r,t.font,"Locked","잠김",14,47,79,62,22,TextAnchor.MiddleCenter).color=t.gold;
            return b;
        }
    }
}
