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
        [Min(0)] public int pierceManaCost=8,recallManaCost=12;
        [Min(.1f)] public float pierceCooldown=2f,recallCooldown=10f,recallWindow=6f;
        public LayerMask recallObstacles=~0;
        PlayerMotor motor;PlayerSkillCaster caster;PlayerBasicAttack attack;PlayerInputReader input;
        CharacterController controller;PlayerHealth health;IManaWallet mana;
        SkillService service;
        float pierceReady,recallReady,markExpiry;
        Vector3 mark;bool marked;GameObject marker;
        public string LastResult { get; private set; }
        static readonly string[] AttackIds={"attack.burst","attack.vortex","attack.storm"};

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
            motor=GetComponent<PlayerMotor>();caster=GetComponent<PlayerSkillCaster>();attack=GetComponent<PlayerBasicAttack>();
            input=GetComponent<PlayerInputReader>();controller=GetComponent<CharacterController>();health=GetComponent<PlayerHealth>();mana=GetComponent<IManaWallet>();
            // Install gates before any gameplay input. Missing/disabled service fails closed.
            if(motor){motor.SkillTreeDashAllowed=()=>Allowed("move.dash");motor.SkillTreeAirJumpAllowed=()=>Allowed("move.jump");motor.SkillTreeDashInput=()=>Use(EquipSlot.Shift);}
            if(caster){caster.SkillTreeAllowed=i=>i>=0 && i<3 && Allowed(AttackIds[i]);caster.SkillTreeInput=i=>{if(i>=0 && i<3)Use((EquipSlot)i);};}
            if(attack)attack.SkillTreePierceAllowed=()=>Allowed("attack.pierce");
        }
        void Start()=>Connect();
        public void Connect()
        {
            if(!session || session.Service==null || !motor || !caster || !attack || !input || !controller || mana==null)
            {LastResult="스킬 연결에 필요한 Session/플레이어 컴포넌트가 없습니다.";Debug.LogError(LastResult,this);return;}
            if(session.executorSource && session.executorSource!=this)
            {LastResult="기존 실행기가 있어 자동 교체하지 않았습니다.";Debug.LogError(LastResult,this);return;}
            if(service!=null)service.Changed-=Sync;
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
            if(!Allowed("move.recall"))ClearMark();
            if(service!=null && service.Snapshot().Learned.Count==0){pierceReady=recallReady=0;caster.ResetCooldowns();motor.ResetDashCooldown();}
        }
        bool Allowed(string id)=>SkillLoadoutAccess.CanUse(service,id,this && isActiveAndEnabled && session && session.isActiveAndEnabled);
        bool Ready=>isActiveAndEnabled && input && input.isActiveAndEnabled && input.AcceptsInput && Time.timeScale>0 && !SkillTreeWindow.AnyOpen && (!health || health.CurrentHealth>0);
        void Use(EquipSlot slot)
        {
            if(!Ready || service==null)return;
            string id=SkillLoadoutAccess.AtSlot(service,slot,Ready);if(id==null)return;
            TryExecute(id,out var reason);LastResult=reason;
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
                if(Time.time<pierceReady){reason="재사용 대기";return false;}
                if(!attack.CanFire){reason="공격 불가";return false;}
                if(!mana.TrySpend(pierceManaCost)){reason="마나 부족";return false;}
                if(!attack.TrySkillPierce()){mana.Gain(pierceManaCost);reason="발사 실패";return false;}
                pierceReady=Time.time+pierceCooldown;reason="관통탄";return true;
            }
            if(id=="move.recall")return Recall(out reason);
            reason="지원하지 않는 스킬";return false;
        }
        bool Recall(out string reason)
        {
            if(marked && Time.time<=markExpiry)
            {
                if(!DestinationClear()){reason="귀환 위치가 막혀 있습니다.";return false;}
                motor.Teleport(mark);ClearMark();reason="흔적으로 귀환";return true;
            }
            ClearMark();
            if(Time.time<recallReady){reason="재사용 대기";return false;}
            if(!motor.IsGrounded || motor.IsDashing){reason="지상에서 흔적을 남기세요.";return false;}
            if(!mana.TrySpend(recallManaCost)){reason="마나 부족";return false;}
            mark=transform.position;marked=true;markExpiry=Time.time+recallWindow;recallReady=markExpiry+recallCooldown;
            marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Recall Mark";
            var col=marker.GetComponent<Collider>();col.enabled=false;Destroy(col);
            marker.transform.position=mark+Vector3.up*.12f;marker.transform.localScale=new Vector3(.65f,.12f,.65f);
            reason="흔적 생성 — "+recallWindow+"초 안에 같은 키로 귀환";return true;
        }
        bool DestinationClear()
        {
            if(!controller)return false;
            float scale=Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
            float radius=Mathf.Max(.05f,controller.radius*scale-controller.skinWidth);
            float half=Mathf.Max(radius,controller.height*Mathf.Abs(transform.lossyScale.y)*.5f);
            Vector3 center=mark+transform.TransformVector(controller.center)+Vector3.up*.08f;
            foreach(var c in Physics.OverlapCapsule(center+Vector3.up*(half-radius),center-Vector3.up*(half-radius),radius,recallObstacles,QueryTriggerInteraction.Ignore))
                if(!c.transform.IsChildOf(transform))return false;
            // A removed platform must not leave a valid-looking floating return point.
            return Physics.Raycast(mark+Vector3.up*.25f,Vector3.down,.8f,recallObstacles,QueryTriggerInteraction.Ignore);
        }
        void Update(){if(marked && (Time.time>markExpiry || !Allowed("move.recall") || health && health.CurrentHealth<=0))ClearMark();}
        void ClearMark(){marked=false;if(marker)Destroy(marker);marker=null;}
        public bool TryGetHUDState(string id,out SkillHUDState state)
        {
            state=new SkillHUDState(false);if(!Allowed(id))return true;
            int index=Array.IndexOf(AttackIds,id);
            if(index>=0){float left=caster.CooldownRemaining(index);state=new SkillHUDState(Ready && caster.Unlocked(index) && mana.CurrentMana>=caster.ManaCost(index),left,caster.Cooldown(index));return true;}
            if(id=="move.dash"){state=new SkillHUDState(Ready && motor.State.DashAvailability.Succeeded,motor.DashCooldownRemaining,motor.ActiveDashCooldown);return true;}
            if(id=="move.jump"){state=new SkillHUDState(motor.IsGrounded || motor.RemainingAirJumps>0);return true;}
            if(id=="attack.pierce"){state=new SkillHUDState(Ready && attack.CanFire && mana.CurrentMana>=pierceManaCost,Mathf.Max(0,pierceReady-Time.time),pierceCooldown);return true;}
            if(id=="move.recall"){state=new SkillHUDState(Ready && (marked || motor.IsGrounded && mana.CurrentMana>=recallManaCost),marked?0:Mathf.Max(0,recallReady-Time.time),recallWindow+recallCooldown);return true;}
            return false;
        }
        void OnDisable(){ClearMark();}
        void OnDestroy(){if(service!=null)service.Changed-=Sync;ClearMark();/* Gates intentionally remain fail-closed until a replacement bridge installs them. */}
    }
}
