using System;
using SandGuard.Player;
using SandGuard.Skills;
using SandGuard.Skills.Unity;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    /// <summary>Optional executor contract for future skills. Query only; must never cast or spend resources.</summary>
    public interface ISkillHUDStateSource
    {
        bool TryGetHUDState(string skillId,out SkillHUDState state);
    }
    public struct SkillHUDState
    {
        public bool available;
        public float remaining,total;
        public SkillHUDState(bool available,float remaining=0,float total=0)
        {this.available=available;this.remaining=Mathf.Max(0,remaining);this.total=Mathf.Max(total,this.remaining);}
    }

    /// <summary>Read-only projection of one SkillTreeSession onto GameHUDCanvas. Does not unlock effects or route input.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class SkillTreeHUDLink : MonoBehaviour
    {
        public GameHUDController hud;
        public SkillTreeSession session;
        public SkillUITheme theme;
        public GameObject player;
        [Tooltip("선택: ISkillHUDStateSource를 구현한 실제 스킬 실행 상태 공급자")]
        public MonoBehaviour stateSource;
        public bool autoFindSession=true;
        public bool OwnsSlots=>isActiveAndEnabled && acquired;
        /// <summary>EquipSlot 값으로 인덱싱한다(표시 순서는 EquipSlotInfo.Order).</summary>
        public string[] SlotStatus { get; }=new string[EquipSlotInfo.Order.Length];
        SkillService observed;
        PlayerMotor motor;PlayerSkillCaster caster;
        GameObject boundPlayer;
        bool acquired,debugEnabled;
        HUDDebugController debug;
        float nextScan;
        bool warned;
        void Awake(){if(!hud)hud=GetComponent<GameHUDController>();}
        void OnEnable(){nextScan=0;}
        void LateUpdate()
        {
            if(!hud)return;
            if(Time.unscaledTime>=nextScan){Resolve();nextScan=Time.unscaledTime+1f;}
            var service=session && session.isActiveAndEnabled?session.Service:null;
            if(service!=observed)
            {
                
                observed=service;
            }
            if(observed!=null && !acquired)Acquire();
            if(!acquired)return;
            if(player && session && session.Service!=null && (!session.executorSource || session.executorSource is PlayerSkillTreeExecutor))
            {
                var executor=player.GetComponent<PlayerSkillTreeExecutor>();
                if(!executor){executor=player.AddComponent<PlayerSkillTreeExecutor>();executor.session=session;executor.Connect();}
                if(executor.session==session)stateSource=executor;
            }
            if(player!=boundPlayer){boundPlayer=player;motor=player?player.GetComponent<PlayerMotor>():null;caster=player?player.GetComponent<PlayerSkillCaster>():null;}
            // After losing a bound session, fail closed instead of returning to fixed unlocked icons.
            RenderCombat(hud.CombatSkills?hud.CombatSkills.Q:null,EquipSlot.Q);
            RenderCombat(hud.CombatSkills?hud.CombatSkills.E:null,EquipSlot.E);
            RenderCombat(hud.CombatSkills?hud.CombatSkills.R:null,EquipSlot.R);
            RenderCombat(hud.CombatSkills?hud.CombatSkills.F:null,EquipSlot.F);
            RenderMovement(hud.MovementSkills?hud.MovementSkills.Dash:null,EquipSlot.Shift);
            RenderMovement(hud.MovementSkills?hud.MovementSkills.DoubleJump:null,EquipSlot.Space);
            RenderMovement(hud.MovementSkills?hud.MovementSkills.Recall:null,EquipSlot.Mouse2);
        }
        void Resolve()
        {
            if(!session && autoFindSession)
            {
                SkillTreeSession only=null;int count=0;
                foreach(var s in FindObjectsByType<SkillTreeSession>(FindObjectsSortMode.None))
                    if(s.isActiveAndEnabled && s.gameObject.scene==gameObject.scene){only=s;count++;}
                if(count==1){session=only;warned=false;}
                else if(count>1 && !warned){Debug.LogWarning("스킬 세션이 여러 개입니다. SkillTreeHUDLink의 Session을 직접 지정하세요.",this);warned=true;}
            }
            if(session && !theme)
            {
                foreach(var window in FindObjectsByType<SkillTreeWindow>(FindObjectsSortMode.None))
                    if(window.session==session && window.theme){theme=window.theme;break;}
            }
            if(!player)
            {
                var presenter=GetComponent<GameHUDPresenter>();if(presenter && presenter.player)player=presenter.player;
                if(!player && session)
                    foreach(var window in FindObjectsByType<SkillTreeWindow>(FindObjectsSortMode.None))
                        if(window.session==session && window.player){player=window.player.gameObject;break;}
            }
        }
        void Acquire()
        {
            acquired=true;debug=GetComponent<HUDDebugController>();
            if(debug){debugEnabled=debug.enabled;debug.enabled=false;}
            // F는 이제 네 번째 공격 슬롯이다(예전에는 상호작용 자리라 숨겼다).
            if(hud.CombatSkills && hud.CombatSkills.F)hud.CombatSkills.F.gameObject.SetActive(true);
        }
        string Id(EquipSlot slot)
        {var id=observed?.Equipped(slot);return id!=null && observed.IsLearned(id)?id:null;}
        SkillHUDState State(string id,EquipSlot slot)
        {
            if(id==null)return new SkillHUDState(false);
            var provider=stateSource as ISkillHUDStateSource;
            if(provider==null && session && !session.DemoMode)provider=session.executorSource as ISkillHUDStateSource;
            if(provider!=null && provider.TryGetHUDState(id,out var state))return state;
            // A demo executor never represents actual player ability availability.
            if(!session || session.DemoMode)return new SkillHUDState(false);
            int index=id=="attack.burst"?PlayerSkillCaster.Burst:id=="attack.vortex"?PlayerSkillCaster.Vortex:id=="attack.storm"?PlayerSkillCaster.Storm:-1;
            if(index>=0 && caster)return new SkillHUDState(caster.Unlocked(index),caster.CooldownRemaining(index),caster.Cooldown(index));
            if(id=="move.dash" && motor)return new SkillHUDState(motor.State.DashAvailability.Succeeded,motor.DashCooldownRemaining,motor.ActiveDashCooldown);
            if(id=="move.jump" && motor)return new SkillHUDState(motor.ExtraAirJumps>0 && (motor.IsGrounded || motor.RemainingAirJumps>0));
            return new SkillHUDState(false);
        }
        void RenderCombat(CombatSkillSlotHUD view,EquipSlot slot)
        {
            if(!view)return;var id=Id(slot);var state=State(id,slot);
            // Update icons each frame too: theme assets can change without a purchase event.
            view.SetIcon(id!=null && theme?theme.Icon(id):null);view.SetKey(EquipSlotInfo.Key(slot));
            view.SetLocked(id==null);
            view.SetCooldown(id==null?0:state.remaining,state.total);
            if(id!=null && !state.available && state.remaining<=0)view.SetUnavailable(session && session.DemoMode?"미연결":"불가");
            SlotStatus[(int)slot]=id==null?"미장착":state.remaining>0?"재사용 대기":state.available?"사용 가능":"실행 연결 / 해금 확인 필요";
        }
        void RenderMovement(MovementSkillSlotHUD view,EquipSlot slot)
        {
            if(!view)return;var id=Id(slot);var state=State(id,slot);var definition=observed?.Catalog.Find(id);
            view.SetIcon(id!=null && theme?theme.Icon(id):null);
            // 위쪽 판자는 80px밖에 안 돼 키까지 넣으면 번진다. 이름만 판자에, 키는 고리 아래에.
            view.SetTitle((definition?.Name??"미장착")+(id!=null && session && session.DemoMode?" (미연결)":""));
            view.SetKey(EquipSlotInfo.Key(slot));
            if(id==null || !state.available && state.remaining<=0)view.SetUnavailable();else view.SetCooldown(state.remaining,state.total);
            SlotStatus[(int)slot]=id==null?"미장착":state.remaining>0?"재사용 대기":state.available?"사용 가능":"실행 연결 / 사용 조건 확인 필요";
        }
        void OnDisable()
        {
            observed=null;
            if(acquired && debug)debug.enabled=debugEnabled;
            acquired=false;
        }
    }
}
