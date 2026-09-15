using SandGuard.Skills;

namespace SandGuard.Facility
{
    public static class TowerUnlockPolicy
    {
        public static bool IsUnlocked(SkillService skills,string facilityId,string requiredSkillId)
        {
            if(skills==null || string.IsNullOrWhiteSpace(facilityId))return false;
            string id=string.IsNullOrWhiteSpace(requiredSkillId)?"tower."+facilityId:requiredSkillId;
            return skills.IsLearned(id);
        }
    }
}
