using SandGuard.Skills;

namespace SandGuard.UI.HUD
{
    public static class SkillLoadoutAccess
    {
        public static bool CanUse(SkillService service,string id,bool connected)
        {
            if(!connected || service==null || id==null || !service.IsLearned(id))return false;
            // Motor and Updraft consume their normal movement inputs, not a loadout action.
            if(id=="move.dash" || id=="move.jump" || id=="move.updraft")return true;
            for(int i=0;i<5;i++)if(service.Equipped((EquipSlot)i)==id)return true;
            return false;
        }
        public static string AtSlot(SkillService service,EquipSlot slot,bool connected)
        {
            var id=service?.Equipped(slot);return CanUse(service,id,connected)?id:null;
        }
    }
}
