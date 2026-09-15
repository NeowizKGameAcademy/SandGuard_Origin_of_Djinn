using System;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Skills.Unity
{
    public sealed class SkillTreeSession : MonoBehaviour
    {
        public SkillTreeAsset definition;
        [Min(0)] public int startingPoints=6;
        [Tooltip("지정하면 단독 시험 실행기 대신 사용합니다. ISkillExecutor 구현 필요")]
        public MonoBehaviour executorSource;
        public bool editingAllowed=true;
        public UnityEvent onChanged=new UnityEvent();
        public SkillService Service { get; private set; }
        public string LastMessage { get; private set; }="";
        SkillDispatch dispatch;DemoSkillExecutor demo;SkillTreeAsset temporary;
        public bool DemoMode=>demo!=null;
        void Awake()
        {
            try
            {
                if(!definition){temporary=SkillTreeAsset.CreateDemo();definition=temporary;}
                Service=new SkillService(definition.Build(),startingPoints);
                ISkillExecutor executor;
                if(executorSource)
                {
                    executor=executorSource as ISkillExecutor;
                    if(executor==null)throw new ArgumentException("Executor must implement ISkillExecutor");
                }
                else{demo=new DemoSkillExecutor(()=>Time.timeAsDouble);executor=demo;}
                dispatch=new SkillDispatch(Service,executor);
                Service.Changed+=OnChanged;
                Service.ObserverError+=OnError;
                Service.EditingAllowed=editingAllowed;
            }
            catch(Exception e){LastMessage=e.Message;Debug.LogException(e,this);enabled=false;}
        }
        void Update(){if(Service!=null)Service.EditingAllowed=editingAllowed;}
        void OnChanged()=>onChanged.Invoke();
        void OnError(Exception e)=>Debug.LogException(e,this);
        bool Ready(){if(Service!=null && dispatch!=null)return true;LastMessage="세션 초기화 실패: 설정을 확인하세요";return false;}
        public void Learn(string id){if(!Ready())return;Service.EditingAllowed=editingAllowed;Report(Service.Learn(id));}
        public void Equip(EquipSlot slot,string id){if(!Ready())return;Service.EditingAllowed=editingAllowed;Report(Service.Equip(slot,id));}
        public void GrantPoints(int amount){if(Ready())Report(Service.Grant(amount));}
        public void ResetRun(){if(Ready() && Service.Reset(startingPoints).Success){demo?.Reset();LastMessage="새 테스트 시작";}}
        // Connect existing input actions to these methods; F remains interaction.
        public void UseQ()=>Use(EquipSlot.Q);
        public void UseE()=>Use(EquipSlot.E);
        public void UseR()=>Use(EquipSlot.R);
        public void UseShift()=>Use(EquipSlot.Shift);
        public void UseSpace()=>Use(EquipSlot.Space);
        public void Use(EquipSlot slot)
        {
            if(!Ready())return;
            if(!isActiveAndEnabled || Time.timeScale<=0){LastMessage="실행 불가";return;}
            dispatch.TryUse(slot,out var reason);LastMessage=reason;
        }
        void Report(SkillResult r)=>LastMessage=r.Success?"완료":r.Failure.ToString();
        void OnDestroy()
        {
            if(Service!=null){Service.Changed-=OnChanged;Service.ObserverError-=OnError;}
            if(temporary)Destroy(temporary);
        }
    }
}
