using System;
using System.Linq;
using SandGuard.Player;
using SandGuard.Skills;
using SandGuard.Skills.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandGuard.UI.HUD
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-250)]
    public sealed class PlayerSkillTreeExecutor : MonoBehaviour, ISkillExecutor, ISkillHUDStateSource
    {
        public SkillTreeSession session;
        PlayerMotor motor;PlayerUpdraft updraft;PlayerSkillCaster caster;PlayerBasicAttack attack;PlayerInputReader input;
        CharacterController controller;PlayerHealth health;IManaWallet mana;PlayerRecall recall;PlayerPierceCharge pierce;
        SkillService service;
        public string LastResult { get; private set; }
        static readonly string[] AttackIds={"attack.burst","attack.vortex","attack.storm"};
        /// <summary>스킬 키 순서(Skill1~Skill4 = Q E R F)를 장착 칸으로 옮긴다.</summary>
        static readonly EquipSlot[] KeySlots={EquipSlot.Q,EquipSlot.E,EquipSlot.R,EquipSlot.F};

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;}
        static void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            var sessions=FindObjectsByType<SkillTreeSession>(FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
            if(sessions.Length!=1)return;
            if(sessions[0].executorSource && !(sessions[0].executorSource is PlayerSkillTreeExecutor))return;
            var window=FindObjectsByType<SkillTreeWindow>(FindObjectsSortMode.None).FirstOrDefault(w=>w.session==sessions[0]);
            var players=FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene).ToArray();
            var root=window && window.player?window.player.gameObject:players.Length==1?players[0].gameObject:null;
            if(!root)return;
            var bridge=root.GetComponent<PlayerSkillTreeExecutor>();if(!bridge)bridge=root.AddComponent<PlayerSkillTreeExecutor>();
            if(!bridge.session)bridge.session=sessions[0];
        }
        void Awake()
        {
            motor=GetComponent<PlayerMotor>();updraft=GetComponent<PlayerUpdraft>();caster=GetComponent<PlayerSkillCaster>();attack=GetComponent<PlayerBasicAttack>();
            input=GetComponent<PlayerInputReader>();controller=GetComponent<CharacterController>();health=GetComponent<PlayerHealth>();mana=GetComponent<IManaWallet>();
            // 흔적 귀환 본체. 프리팹에 없으면 기본값으로 붙인다(연출 연결은 프리팹의 PlayerRecall에서).
            recall=GetComponent<PlayerRecall>();if(!recall)recall=gameObject.AddComponent<PlayerRecall>();
            // 관통탄 충전 본체(마나·쿨다운·충전 수치는 이 컴포넌트 인스펙터).
            pierce=GetComponent<PlayerPierceCharge>();if(!pierce)pierce=gameObject.AddComponent<PlayerPierceCharge>();
            pierce.Allowed=()=>Allowed("attack.pierce");
            // Install gates before any gameplay input. Missing/disabled service fails closed.
            if(motor){motor.SkillTreeDashAllowed=()=>Allowed("move.dash");motor.SkillTreeAirJumpAllowed=()=>Allowed("move.jump");motor.SkillTreeDashInput=()=>{TryExecute("move.dash",out var reason);LastResult=reason;};}
            if(caster){caster.SkillTreeAllowed=i=>i>=0 && i<3 && Allowed(AttackIds[i]);caster.SkillTreeInput=Use;}
            if(attack)attack.SkillTreePierceAllowed=()=>Allowed("attack.pierce");
        }
        void OnEnable(){if(input){input.SkillReleased+=OnSkillReleased;input.SpellPressed+=OnRecallPressed;}}
        void Start()=>Connect();
        void OnSkillReleased(int slot)
        {
            if(!pierce || !pierce.IsHolding || pierce.HeldSlot!=slot)return;
            var result=pierce.EndHold(slot);LastResult=result.Succeeded?"관통탄":result.Failure.ToString();
        }
        public void Connect()
        {
            if(!session || session.Service==null || !motor || !caster || !attack || !input || !controller || mana==null)
            {LastResult="스킬 연결에 필요한 Session/플레이어 컴포넌트가 없습니다.";Debug.LogError(LastResult,this);return;}
            if(session.executorSource && session.executorSource!=this)
            {LastResult="기존 실행기가 있어 자동 교체하지 않았습니다.";Debug.LogError(LastResult,this);return;}
            if(service!=null)service.Changed-=Sync;
            var progression=GetComponent<PlayerProgression>();
            if(!progression){Debug.LogError("PlayerProgression이 필요합니다.",this);return;}
            var bind=session.Service.BindWallet(progression.PointWallet);
            if(!bind.Success){Debug.LogError("공유 포인트 연결 실패: 이미 구매한 시험 세션이거나 다른 트리가 지갑을 사용 중입니다. 새 게임에서 연결하세요.",this);return;}
            service=session.Service;service.Changed+=Sync;session.BindExecutor(this);
            foreach(var starter in FindObjectsByType<PlayerStarterEffects>(FindObjectsSortMode.None))
            {
                var target=starter.target;
                if(target && target.gameObject==gameObject || !target && FindFirstObjectByType<PlayerEffects>()?.gameObject==gameObject)
                    starter.SuppressForSkillTree();
            }
            foreach(var link in FindObjectsByType<SkillTreeHUDLink>(FindObjectsSortMode.None))
                if(link.session==session){link.player=gameObject;link.stateSource=this;}
            Sync();LastResult="플레이어 스킬트리 연결 완료";
        }
        void Sync()
        {
            if(!Allowed("move.recall"))recall.ClearMark();
            if(updraft)
            {
                bool unlocked=Allowed("move.updraft");
                if(updraft.unlocked!=unlocked)
                {
                    updraft.unlocked=unlocked;
                    if(!unlocked)updraft.Cancel();
                    updraft.RefreshDeferral();
                }
            }
        }
        bool Allowed(string id)=>SkillLoadoutAccess.CanUse(service,id,this && isActiveAndEnabled && session && session.isActiveAndEnabled);
        bool Ready=>isActiveAndEnabled && input && input.isActiveAndEnabled && input.AcceptsInput && Time.timeScale>0 && !SkillTreeWindow.AnyOpen && (!health || health.CurrentHealth>0);
        /// <summary>스킬 키를 눌렀다. key는 KeySlots의 번호(0 = Q … 3 = F).</summary>
        void Use(int key)
        {
            if(!Ready || service==null || key<0 || key>=KeySlots.Length)return;
            string id=SkillLoadoutAccess.AtSlot(service,KeySlots[key],Ready);if(id==null)return;
            // 관통탄은 누르고 있는 동안 충전하고 떼는 순간(OnSkillReleased) 쏜다. 나머지는 누르는 순간 실행.
            if(id=="attack.pierce"){LastResult=pierce.BeginHold(key)?"관통탄 충전":"재사용 대기";return;}
            TryExecute(id,out var reason);LastResult=reason;
        }
        /// <summary>마우스 우클릭 — 흔적 귀환 전용 칸(EquipSlot.Mouse2).</summary>
        void OnRecallPressed()
        {
            if(!Ready || service==null)return;
            if(SkillLoadoutAccess.AtSlot(service,EquipSlot.Mouse2,Ready)==null)return;
            TryExecute("move.recall",out var reason);LastResult=reason;
        }
        public bool TryExecute(string id,out string reason)
        {
            reason="스킬 잠김 또는 미장착";if(!Ready || !Allowed(id))return false;
            int index=Array.IndexOf(AttackIds,id);
            if(index>=0){var result=caster.TryCast(index);reason=result.Succeeded?"시전 완료":result.Failure.ToString();return result.Succeeded;}
            if(id=="move.dash"){var result=motor.TryDash();reason=result.Succeeded?"대시":result.Failure.ToString();return result.Succeeded;}
            if(id=="move.jump"){reason="Space 추가 점프 입력으로 사용합니다.";return false;}
            if(id=="attack.pierce")
            {
                var result=pierce.Fire(0f);
                reason=result.Succeeded?"관통탄":result.Failure==ActionFailure.Cooldown?"재사용 대기":result.Failure==ActionFailure.InsufficientMana?"마나 부족":result.Failure==ActionFailure.InvalidRequest?"공격 불가":result.Failure.ToString();
                return result.Succeeded;
            }
            if(id=="move.recall")return Recall(out reason);
            reason="지원하지 않는 스킬";return false;
        }
        bool Recall(out string reason)
        {
            bool wasMarked=recall.IsMarked;
            var result=recall.TryUse();
            if(result.Succeeded){reason=wasMarked?"흔적으로 귀환":recall.HasWindow?"흔적 생성 — "+recall.window+"초 안에 우클릭으로 귀환":"흔적 생성 — 다시 우클릭하면 귀환";return true;}
            switch(result.Failure)
            {
                case ActionFailure.InvalidPlacement:reason=wasMarked?"귀환 위치가 막혀 있습니다.":"지상에서 흔적을 남기세요.";break;
                case ActionFailure.Cooldown:reason="재사용 대기";break;
                case ActionFailure.InsufficientMana:reason="마나 부족";break;
                case ActionFailure.NotAlive:reason="사망 상태";break;
                default:reason=result.Failure.ToString();break;
            }
            return false;
        }
        void Update()
        {
            if(recall.IsMarked && !Allowed("move.recall"))recall.ClearMark();
            if(pierce.IsHolding && (!Ready || !Allowed("attack.pierce")))pierce.Cancel();
        }
        public bool TryGetHUDState(string id,out SkillHUDState state)
        {
            state=new SkillHUDState(false);if(!Allowed(id))return true;
            int index=Array.IndexOf(AttackIds,id);
            if(index>=0){float left=caster.CooldownRemaining(index);state=new SkillHUDState(Ready && caster.Unlocked(index) && mana.CurrentMana>=caster.ManaCost(index),left,caster.Cooldown(index));return true;}
            if(id=="move.dash"){state=new SkillHUDState(Ready && motor.State.DashAvailability.Succeeded,motor.DashCooldownRemaining,motor.ActiveDashCooldown);return true;}
            if(id=="move.jump"){state=new SkillHUDState(motor.IsGrounded || motor.RemainingAirJumps>0);return true;}
            if(id=="attack.pierce"){state=new SkillHUDState(Ready && pierce.CanFire,pierce.CooldownRemaining,pierce.cooldown);return true;}
            if(id=="move.recall"){state=new SkillHUDState(Ready && (recall.IsMarked || recall.CanMark),recall.CooldownRemaining,recall.TotalCooldown);return true;}
            return false;
        }
        void OnDisable(){if(input){input.SkillReleased-=OnSkillReleased;input.SpellPressed-=OnRecallPressed;}if(pierce)pierce.Cancel();if(recall)recall.ClearMark();if(updraft){updraft.unlocked=false;updraft.Cancel();updraft.RefreshDeferral();}}
        void OnDestroy(){if(service!=null)service.Changed-=Sync;if(recall)recall.ClearMark();/* Gates intentionally remain fail-closed until a replacement bridge installs them. */}
    }
}
