using System;
using System.Collections.Generic;

namespace SandGuard.Skills
{
    /// <summary>Executor owns mana, cooldown by skill ID and actual game effects.</summary>
    public interface ISkillExecutor { bool TryExecute(string skillId,out string reason); }
    public sealed class SkillDispatch
    {
        readonly SkillService skills;readonly ISkillExecutor executor;
        public SkillDispatch(SkillService skills,ISkillExecutor executor){this.skills=skills??throw new ArgumentNullException(nameof(skills));this.executor=executor??throw new ArgumentNullException(nameof(executor));}
        public bool TryUse(EquipSlot slot,out string reason)
        {
            var id=skills.Equipped(slot);
            if(id==null || !skills.IsLearned(id)){reason="No equipped skill";return false;}
            return executor.TryExecute(id,out reason);
        }
    }
    // Standalone demo executor. Production executors retain cooldown on skill ID, not slot.
    public sealed class DemoSkillExecutor : ISkillExecutor
    {
        readonly Func<double> clock;
        readonly Dictionary<string,double> ready=new Dictionary<string,double>();
        public string LastSkill { get; private set; }
        public DemoSkillExecutor(Func<double> clock){this.clock=clock;}
        public bool TryExecute(string id,out string reason)
        {
            if(ready.TryGetValue(id,out var time) && clock()<time){reason="Cooldown";return false;}
            ready[id]=clock()+3;LastSkill=id;reason="Demo cast: "+id;return true;
        }
        public void Reset(){ready.Clear();LastSkill=null;}
    }
}
