using System;
using System.Linq;
using UnityEngine;

namespace SandGuard.Skills.Unity
{
    /// <summary>Game 뷰 단독 기능 테스트 패널. 최종 아트/UI 및 게임 입력과 분리되어 있습니다.</summary>
    public sealed class SkillTreeTestPanel : MonoBehaviour
    {
        public SkillTreeSession session;
        Vector2 scroll;string selected;
        void Awake(){if(!session)session=GetComponent<SkillTreeSession>();}
        void OnGUI()
        {
            if(GetComponent<SkillTreeWindow>())return;
            if(!session || session.Service==null){GUI.Label(new Rect(20,20,1000,60),"SkillTreeSession 설정을 확인하세요.");return;}
            var service=session.Service;
            GUILayout.BeginArea(new Rect(12,12,Mathf.Max(300,Screen.width-24),Mathf.Max(200,Screen.height-24)),GUI.skin.box);
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("SANDGUARD — 독립 스킬 구매 / 장착 테스트",GUI.skin.box);
            GUILayout.Label("남은 포인트: "+service.Points+" | "+(session.DemoMode?"시전은 기록만 하는 데모입니다.":"외부 스킬 실행기 연결됨"));
            GUILayout.BeginHorizontal();
            if(session.DemoMode){if(GUILayout.Button("테스트 포인트 +10"))session.GrantPoints(10);if(GUILayout.Button("테스트 초기화"))session.ResetRun();}
            session.editingAllowed=GUILayout.Toggle(session.editingAllowed,"구매·장착 편집 허용");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach(SkillBranch branch in Enum.GetValues(typeof(SkillBranch)))
            {
                GUILayout.BeginVertical(GUI.skin.box,GUILayout.MinWidth(200));GUILayout.Label(branch.ToString());
                foreach(var d in service.Catalog.All.Where(d=>d.Branch==branch))
                {
                    bool learned=service.IsLearned(d.Id);
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(d.Name+" — "+d.Cost+" SP "+(learned?"[해금]":""));
                    GUILayout.Label("선행: "+(d.Prerequisites.Count==0?"없음":string.Join(", ",d.Prerequisites.Select(p=>service.Catalog.Find(p).Name))));
                    GUILayout.Label(d.Kind==SkillKind.Passive?"패시브 / 시설 해금":"장착: "+string.Join(", ",d.Slots));
                    if(GUILayout.Button(selected==d.Id?"선택됨":"상세 / 장착 선택"))selected=d.Id;
                    var availability=service.CanLearn(d.Id);
                    GUI.enabled=availability.Success;
                    if(GUILayout.Button(learned?"구매 완료":"구매 ("+availability.Failure+")"))session.Learn(d.Id);
                    GUI.enabled=true;GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            var chosen=service.Catalog.Find(selected);
            if(chosen!=null)GUILayout.Label("선택: "+chosen.Name+" — "+chosen.Description);
            GUILayout.Label("선택한 스킬을 아래 슬롯에 장착하세요. 동일 스킬 중복 장착은 금지됩니다.");
            foreach(EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                string id=service.Equipped(slot);
                GUILayout.Label(slot+": "+(id==null?"비어 있음":service.Catalog.Find(id).Name),GUILayout.Width(240));
                GUI.enabled=chosen!=null && service.IsLearned(selected) && chosen.Slots.Contains(slot) && session.editingAllowed;
                if(GUILayout.Button("선택 스킬 넣기"))session.Equip(slot,selected);
                GUI.enabled=session.editingAllowed && id!=null;
                if(GUILayout.Button("빼기"))session.Equip(slot,null);
                GUI.enabled=id!=null;
                if(GUILayout.Button("사용 시험"))session.Use(slot);
                GUI.enabled=true;GUILayout.EndHorizontal();
            }
            GUILayout.Label("결과: "+session.LastMessage,GUI.skin.box);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
