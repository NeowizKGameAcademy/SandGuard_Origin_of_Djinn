using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SandGuard.Skills.Unity
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SkillTreeWindow : MonoBehaviour
    {
        public SkillTreeSession session;
        public SkillUITheme theme;
        public Transform player;
        [Tooltip("창이 열린 동안 끌 입력/카메라/건설 메뉴 컴포넌트. 창 자체와 세션은 넣지 마세요.")]
        public Behaviour[] suspendWhileOpen=Array.Empty<Behaviour>();
        public bool standalonePreview;
        public bool listenForInteractionKey=true;
        public UnityEvent onOpened=new UnityEvent(), onClosed=new UnityEvent();
        [HideInInspector] public GameObject modal, prompt;
        [HideInInspector] public RectTransform treeContent;
        [HideInInspector] public Text pointsLabel, detailTitle, detailBody, buyLabel, statusLabel, promptLabel;
        [HideInInspector] public Image detailIcon;
        [HideInInspector] public Button buyButton;
        [HideInInspector] public Button[] tabButtons, slotButtons, removeButtons;
        [HideInInspector] public Text[] slotLabels;
        [HideInInspector] public Image[] slotIcons;
        public bool IsOpen { get; private set; }
        public static bool AnyOpen=>owner!=null;
        static SkillTreeWindow owner;
        readonly Dictionary<Behaviour,bool> suspended=new Dictionary<Behaviour,bool>();
        SkillAltarAnchor current;
        SkillBranch branch;
        string selected, feedback="해금한 스킬을 선택한 뒤 아래 슬롯에 장착하세요.";
        bool dirty=true, previousVisible, bound;
        CursorLockMode previousLock;
        GameObject createdEventSystem;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearOwner()=>owner=null;
        void Awake()
        {
            if(!session)session=GetComponent<SkillTreeSession>();
            if(!theme){Debug.LogError("Skill UI Theme를 지정하세요. 프리팹 생성 메뉴로 만들 수 있습니다.",this);enabled=false;return;}
            if(!modal)SkillWindowLayout.Build(this);
            modal.SetActive(false);prompt.SetActive(false);
            BindButtons();
        }
        void Start()
        {
            if(!theme || !modal)return;
            if(!session || session.Service==null){Debug.LogError("SkillTreeSession 초기화와 데이터를 확인하세요.",this);enabled=false;return;}
            session.Service.Changed+=MarkDirty;bound=true;
            if(!player){var go=GameObject.FindGameObjectWithTag("Player");if(go)player=go.transform;}
            if(standalonePreview)Open();
        }
        void BindButtons()
        {
            for(int i=0;i<tabButtons.Length;i++){int index=i;tabButtons[i].onClick.AddListener(()=>SelectBranch((SkillBranch)index));}
            buyButton.onClick.AddListener(Buy);
            for(int i=0;i<slotButtons.Length;i++)
            { var slot=(EquipSlot)i;slotButtons[i].onClick.AddListener(()=>Equip(slot));removeButtons[i].onClick.AddListener(()=>Unequip(slot)); }
            modal.transform.Find("Window/Close").GetComponent<Button>().onClick.AddListener(Close);
        }
        void MarkDirty()=>dirty=true;
        void Update()
        {
            if(!session || session.Service==null)return;
            var nearest=Nearest();
            if(IsOpen && !standalonePreview && (current==null || nearest!=current))Close();
            bool available=!IsOpen && owner==null && nearest!=null && Time.timeScale>0;
            prompt.SetActive(available);
            if(available)promptLabel.text="F   "+nearest.displayName;
            if(listenForInteractionKey && Keyboard.current!=null)
            {
                if(IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame)Close();
                else if(Keyboard.current.fKey.wasPressedThisFrame)
                {if(IsOpen)Close();else if(available)Open();}
            }
            if(IsOpen && (dirty || lastEditing!=session.editingAllowed)){dirty=false;Refresh();}
        }
        SkillAltarAnchor Nearest()
        {
            if(!player)return null;SkillAltarAnchor result=null;float best=float.MaxValue;
            foreach(var a in SkillAltarAnchor.Active)
            { if(!a || !a.isActiveAndEnabled)continue;float d=(a.transform.position-player.position).sqrMagnitude;
              if(d<=a.interactionRadius*a.interactionRadius && d<best){best=d;result=a;} }
            return result;
        }
        public void Open()
        {
            if(IsOpen || !isActiveAndEnabled || !session || session.Service==null || owner!=null || Time.timeScale<=0)return;
            current=Nearest();if(!standalonePreview && current==null)return;
            owner=this;IsOpen=true;previousLock=Cursor.lockState;previousVisible=Cursor.visible;
            foreach(var b in suspendWhileOpen ?? Array.Empty<Behaviour>())
                if(b && b!=this && b!=session && !(b is Canvas) && !suspended.ContainsKey(b))
                {suspended.Add(b,b.enabled);b.enabled=false;}
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(EventSystem.current==null)
            {
                createdEventSystem=new GameObject("Skill UI EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                createdEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            modal.SetActive(true);prompt.SetActive(false);dirty=true;onOpened.Invoke();
        }
        public void Close()
        {
            if(!IsOpen)return;
            IsOpen=false;if(modal)modal.SetActive(false);
            foreach(var entry in suspended)if(entry.Key)entry.Key.enabled=entry.Value;
            suspended.Clear();Cursor.lockState=previousLock;Cursor.visible=previousVisible;
            if(createdEventSystem){createdEventSystem.SetActive(false);Destroy(createdEventSystem);createdEventSystem=null;}
            if(owner==this)owner=null;current=null;onClosed.Invoke();
        }
        void OnDisable(){Close();if(prompt)prompt.SetActive(false);}
        void OnDestroy(){if(bound && session && session.Service!=null)session.Service.Changed-=MarkDirty;}
        void SelectBranch(SkillBranch value){branch=value;selected=null;dirty=true;}
        void Buy()
        {
            if(!IsOpen)return;
            session.Service.EditingAllowed=session.editingAllowed;
            var r=session.Service.Learn(selected);feedback=r.Success?"구매 완료 — 사용할 슬롯을 선택하세요.":Reason(r.Failure);dirty=true;
        }
        void Equip(EquipSlot slot)
        {
            if(!IsOpen || selected==null)return;
            session.Service.EditingAllowed=session.editingAllowed;
            var r=session.Service.Equip(slot,selected);feedback=r.Success?slot+" 슬롯에 장착했습니다.":Reason(r.Failure);dirty=true;
        }
        void Unequip(EquipSlot slot)
        {
            if(!IsOpen)return;
            session.Service.EditingAllowed=session.editingAllowed;
            var r=session.Service.Equip(slot,null);feedback=r.Success?slot+" 슬롯에서 해제했습니다.":Reason(r.Failure);dirty=true;
        }
        bool lastEditing;
        void Refresh()
        {
            var s=session.Service;lastEditing=session.editingAllowed;s.EditingAllowed=lastEditing;
            pointsLabel.text="보유 포인트  "+s.Points+" SP";
            var nodes=s.Catalog.All.Where(d=>d.Branch==branch).ToArray();
            if(selected==null || !nodes.Any(n=>n.Id==selected))selected=nodes.FirstOrDefault()?.Id;
            for(int i=0;i<tabButtons.Length;i++)tabButtons[i].image.color=i==(int)branch?theme.cyan:theme.muted;
            RebuildTree(nodes);
            var d=s.Catalog.Find(selected);
            if(d==null){detailTitle.text="등록된 스킬 없음";detailBody.text="이 계열의 스킬 데이터를 추가하세요.";detailIcon.enabled=false;buyButton.interactable=false;buyLabel.text="선택할 스킬 없음";}
            else
            {
                detailTitle.text=d.Name;detailIcon.sprite=theme.Icon(d.Id);detailIcon.enabled=detailIcon.sprite!=null;
                detailIcon.color=s.IsLearned(d.Id)?theme.gold:theme.cyan;
                string prereq=d.Prerequisites.Count==0?"없음":string.Join("\n",d.Prerequisites.Select(p=>s.Catalog.Find(p).Name+(s.IsLearned(p)?"  ✓":"  (미해금)")));
                string desc=string.IsNullOrWhiteSpace(d.Description) || d.Description.StartsWith("테스트용")?DefaultDescription(d.Id):d.Description;
                detailBody.text=desc+"\n\n선행 스킬  "+prereq+"\n\n필요 포인트  "+d.Cost+" SP\n\n"+(d.Kind==SkillKind.Passive?"패시브 / 시설 해금 · 슬롯 불필요":"사용 슬롯  "+string.Join(" · ",d.Slots));
                var can=s.CanLearn(d.Id);buyButton.interactable=can.Success;
                buyLabel.text=s.IsLearned(d.Id)?"해금 완료":can.Success?d.Cost+" SP로 구매":Reason(can.Failure);
            }
            for(int i=0;i<slotButtons.Length;i++)
            {
                var slot=(EquipSlot)i;string id=s.Equipped(slot);var equipped=s.Catalog.Find(id);
                slotLabels[i].text=equipped?.Name??"비어 있음";slotIcons[i].sprite=theme.Icon(id);slotIcons[i].enabled=slotIcons[i].sprite!=null;
                bool compatible=d!=null && d.Kind==SkillKind.Active && d.Slots.Contains(slot) && s.IsLearned(d.Id) && lastEditing;
                slotButtons[i].interactable=compatible;slotButtons[i].image.color=compatible?theme.cyan:theme.gold;
                removeButtons[i].interactable=id!=null && lastEditing;
            }
            statusLabel.text=feedback;
        }
        void RebuildTree(SkillDefinition[] nodes)
        {
            foreach(Transform child in treeContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var s=session.Service;var ranks=new Dictionary<string,int>();
            Func<string,int> rank=null;rank=id=>{if(ranks.TryGetValue(id,out int r))return r;var d=s.Catalog.Find(id);return ranks[id]=d.Prerequisites.Count==0?0:1+d.Prerequisites.Max(rank);};
            var positions=new Dictionary<string,Vector2>();
            var groups=nodes.GroupBy(d=>rank(d.Id)).ToArray();int maxRows=groups.Length==0?1:groups.Max(g=>g.Count());
            float height=Mathf.Max(410,maxRows*180+40),width=Mathf.Max(900,groups.Length==0?900:(groups.Max(g=>g.Key)+1)*210+40);
            treeContent.sizeDelta=new Vector2(width,height);
            foreach(var group in groups){int row=0;foreach(var d in group)positions[d.Id]=new Vector2(115+group.Key*210,65+(row+++.5f)*((height-110)/group.Count()));}
            foreach(var d in nodes)foreach(var p in d.Prerequisites)
                if(positions.TryGetValue(p,out var from))SkillWindowLayout.Line(treeContent,from,positions[d.Id],s.IsLearned(p)?theme.gold:theme.muted);
            foreach(var d in nodes)
            {
                var pos=positions[d.Id];bool learned=s.IsLearned(d.Id);var color=learned?theme.gold:s.CanLearn(d.Id).Success?theme.cyan:theme.muted;
                var b=SkillWindowLayout.Node(treeContent,theme,d.Name,d.Cost,pos,theme.Icon(d.Id),color,d.Id==selected);
                string id=d.Id;b.onClick.AddListener(()=>{selected=id;dirty=true;});
            }
        }
        public static string Reason(SkillFailure f)
        {
            switch(f){case SkillFailure.Points:return "포인트 부족";case SkillFailure.Prerequisite:return "선행 스킬 필요";case SkillFailure.AlreadyLearned:return "해금 완료";case SkillFailure.EditingBlocked:return "현재 편집 불가";case SkillFailure.Locked:return "먼저 구매하세요";case SkillFailure.DuplicateSlot:return "기존 슬롯에서 먼저 해제하세요";case SkillFailure.IncompatibleSlot:return "장착할 수 없는 슬롯";case SkillFailure.None:return "완료";default:return "요청을 처리할 수 없습니다";}
        }
        static string DefaultDescription(string id)
        {
            switch(id){case "move.dash":return "바라보는 방향으로 빠르게 이동합니다.";case "move.jump":return "공중에서 한 번 더 점프합니다.";case "move.recall":return "흔적을 남기고, 제한 시간 안에 다시 사용하면 귀환합니다.";case "attack.pierce":return "전방으로 적을 관통하는 마법탄을 발사합니다.";case "attack.burst":return "모래 폭발로 범위 안의 적을 공격합니다.";case "attack.vortex":return "모래 소용돌이로 적의 움직임을 제어합니다.";case "attack.storm":return "거대한 모래 폭풍으로 넓은 범위를 공격합니다.";default:return "구매하면 해당 스킬 또는 시설을 해금합니다.";}
        }
    }
}
