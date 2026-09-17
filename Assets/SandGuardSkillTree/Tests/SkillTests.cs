using System;
using System.Collections.Generic;
using System.Linq;
using SandGuard.Skills;

static class SkillTests
{
    static int passed;
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
    static SkillDefinition D(string id,int cost=2,string[] prereq=null,EquipSlot[] slots=null,SkillKind kind=SkillKind.Active)
        =>new SkillDefinition(id,id,"",cost,kind,SkillBranch.Movement,prereq,slots??(kind==SkillKind.Active?new[]{EquipSlot.Q,EquipSlot.E}:Array.Empty<EquipSlot>()));
    static void Throws(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
    static string State(SkillService s){var p=s.Snapshot();return p.Points+":"+string.Join(",",p.Learned.OrderBy(x=>x))+":"+string.Join(",",p.Loadout.OrderBy(x=>x.Key).Select(x=>x.Key+"="+x.Value));}
    static void Main()
    {
        var catalog=new SkillCatalog(new[]{D("a"),D("b",4,new[]{"a"}),D("c",8,new[]{"a","b"}),D("passive",1,kind:SkillKind.Passive),D("jump",1,slots:new[]{EquipSlot.Space})});
        var s=new SkillService(catalog,6);int events=0;s.Changed+=()=>events++;
        string before=State(s);
        Check(s.Learn("b").Failure==SkillFailure.Prerequisite && State(s)==before && events==0,"prerequisite rejection is atomic");
        Check(s.Learn("missing").Failure==SkillFailure.NotFound && State(s)==before,"unknown skill rejection");
        Check(s.Learn("a").Success && s.Points==4 && events==1,"purchase deducts exact cost and notifies once");
        Check(s.Equipped(EquipSlot.Q)==null,"purchase does not auto equip");
        Check(s.Learn("a").Failure==SkillFailure.AlreadyLearned && s.Points==4,"duplicate purchase does not charge");
        Check(s.Learn("b").Success && s.Points==0,"advanced purchase without level requirement");
        before=State(s);Check(s.Learn("c").Failure==SkillFailure.Points && State(s)==before,"insufficient points is atomic");
        Check(s.Equip(EquipSlot.Q,"c").Failure==SkillFailure.Locked,"locked skill cannot equip");
        Check(s.Equip(EquipSlot.Shift,"a").Failure==SkillFailure.IncompatibleSlot,"incompatible slot rejected");
        Check(s.Equip(EquipSlot.Q,"a").Success,"equip learned skill");
        before=State(s);Check(s.Equip(EquipSlot.E,"a").Failure==SkillFailure.DuplicateSlot && State(s)==before,"duplicate slot rejected atomically");
        int n=events;Check(s.Equip(EquipSlot.Q,"a").Success && events==n,"unchanged equip has no event");
        s.Grant(20);s.Learn("passive");Check(s.Equip(EquipSlot.R,"passive").Failure==SkillFailure.IncompatibleSlot,"passive cannot occupy active slot");
        s.EditingAllowed=false;before=State(s);
        Check(s.Learn("c").Failure==SkillFailure.EditingBlocked && s.Equip(EquipSlot.Q,null).Failure==SkillFailure.EditingBlocked && State(s)==before,"edit lock blocks purchase and removal");
        Check(s.Grant(1).Success,"rewards allowed during editing lock");s.EditingAllowed=true;
        var snapshot=s.Snapshot();s.Equip(EquipSlot.Q,"b");Check(snapshot.Loadout[EquipSlot.Q]=="a","snapshot detached from mutations");
        Check(s.Restore(snapshot).Success && s.Equipped(EquipSlot.Q)=="a","valid snapshot restores loadout");
        before=State(s);Check(s.Restore(new SkillSnapshot(50,new[]{"b"},new Dictionary<EquipSlot,string>())).Failure==SkillFailure.Invalid && State(s)==before,"restore rejects missing prerequisite atomically");
        Check(s.Restore(new SkillSnapshot(0,new[]{"a"},new Dictionary<EquipSlot,string>{{EquipSlot.Q,"a"},{EquipSlot.E,"a"}})).Failure==SkillFailure.Invalid && State(s)==before,"restore rejects duplicate loadout");
        Check(s.Grant(-1).Failure==SkillFailure.Invalid && State(s)==before,"negative reward rejected");
        var max=new SkillService(catalog,int.MaxValue);Check(max.Grant(1).Failure==SkillFailure.Invalid && max.Points==int.MaxValue,"point overflow rejected");
        var reentrant=new SkillService(catalog,5);SkillFailure nested=SkillFailure.None;bool sawComplete=false;int errors=0;bool last=false;
        reentrant.ObserverError+=e=>errors++;
        reentrant.Changed+=()=>{sawComplete=reentrant.IsLearned("a") && reentrant.Points==3;nested=reentrant.Grant(1).Failure;};
        reentrant.Changed+=()=>throw new Exception("observer");reentrant.Changed+=()=>last=true;
        Check(reentrant.Learn("a").Success && sawComplete && nested==SkillFailure.Busy,"notifications see committed state and reject reentrancy");
        Check(errors==1 && last && reentrant.Points==3,"observer failure does not rollback or skip later observers");
        double time=0;var executor=new DemoSkillExecutor(()=>time);var dispatch=new SkillDispatch(s,executor);
        Check(dispatch.TryUse(EquipSlot.Q,out _),"dispatch equipped skill");s.Equip(EquipSlot.Q,null);s.Equip(EquipSlot.E,"a");
        Check(!dispatch.TryUse(EquipSlot.E,out var reason) && reason=="Cooldown","slot change cannot bypass cooldown");
        time=3;Check(dispatch.TryUse(EquipSlot.E,out _),"cooldown expires using injected clock");
        Check(!dispatch.TryUse(EquipSlot.Q,out _),"empty slot cannot execute");
        s.Reset(6);Check(s.Points==6 && s.Snapshot().Learned.Count==0 && s.Snapshot().Loadout.Count==0,"new run clears unlock and loadout");
        Throws(()=>new SkillCatalog(new[]{D("a"),D("a")}),"duplicate definition rejected");
        Throws(()=>new SkillCatalog(new[]{D("a",prereq:new[]{"missing"})}),"dangling prerequisite rejected");
        Throws(()=>new SkillCatalog(new[]{D("a",prereq:new[]{"b"}),D("b",prereq:new[]{"a"})}),"cycle rejected");
        Throws(()=>new SkillCatalog(new[]{D("a",slots:new[]{(EquipSlot)99})}),"invalid slot definition rejected");
        var multi=new SkillService(catalog,50);multi.Learn("a");Check(multi.Learn("c").Failure==SkillFailure.Prerequisite,"all prerequisites required");
        multi.Learn("b");Check(multi.Learn("c").Success,"all prerequisites satisfied");
        var movement=new SkillService(new SkillCatalog(new[]{
            new SkillDefinition("move.dash","대시","",1,SkillKind.Active,SkillBranch.Movement,Array.Empty<string>(),new[]{EquipSlot.Shift}),
            new SkillDefinition("move.jump","더블 점프","",1,SkillKind.Active,SkillBranch.Movement,Array.Empty<string>(),new[]{EquipSlot.Space}),
            new SkillDefinition("move.updraft","차지 점프","",2,SkillKind.Passive,SkillBranch.Movement,new[]{"move.jump"},Array.Empty<EquipSlot>()),
            new SkillDefinition("move.recall","흔적 귀환","",3,SkillKind.Active,SkillBranch.Movement,new[]{"move.dash"},new[]{EquipSlot.Shift,EquipSlot.Q,EquipSlot.E,EquipSlot.R})}),10);
        Check(movement.Learn("move.dash").Success && movement.Equipped(EquipSlot.Shift)=="move.dash","dash auto equips on purchase");
        Check(movement.Learn("move.jump").Success && movement.Equipped(EquipSlot.Space)=="move.jump","double jump auto equips on purchase");
        Check(movement.Learn("move.updraft").Success && movement.IsLearned("move.updraft"),"charge jump unlocks without a slot");
        Check(movement.Learn("move.recall").Success && movement.Equipped(EquipSlot.Q)=="move.recall","recall auto equips to a free key");
        Check(movement.Restore(new SkillSnapshot(3,new[]{"move.dash","move.jump","move.updraft","move.recall"},
            new Dictionary<EquipSlot,string>{{EquipSlot.Shift,"move.recall"}})).Success
            && movement.Equipped(EquipSlot.Shift)=="move.dash"
            && movement.Equipped(EquipSlot.Space)=="move.jump"
            && movement.Equipped(EquipSlot.Q)=="move.recall","legacy movement loadout migrates to automatic keys");
        Check(movement.Respec().Success && movement.Equipped(EquipSlot.Shift)==null && !movement.IsLearned("move.updraft"),"respec removes automatic movement unlocks");
        Console.WriteLine("TOTAL: "+passed+" passed");
    }
}
