using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Skills.Unity
{
    [Serializable] public sealed class SkillRow
    {
        public string id, displayName;
        [TextArea] public string description;
        public SkillBranch branch;
        public SkillKind kind;
        [Min(0)] public int cost=1;
        public List<string> prerequisites=new List<string>();
        public List<EquipSlot> allowedSlots=new List<EquipSlot>();
    }
    [CreateAssetMenu(menuName="SandGuard/Skills/Skill Tree")]
    public sealed class SkillTreeAsset : ScriptableObject
    {
        public List<SkillRow> nodes=new List<SkillRow>();
        public SkillCatalog Build()
        {
            var definitions=new List<SkillDefinition>();
            foreach(var row in nodes)
            {
                if(row==null)throw new ArgumentException("Null skill row");
                definitions.Add(new SkillDefinition(row.id,row.displayName,row.description,row.cost,row.kind,row.branch,row.prerequisites,row.allowedSlots));
            }
            return new SkillCatalog(definitions);
        }
        public static SkillTreeAsset CreateDemo()
        {
            var asset=CreateInstance<SkillTreeAsset>();
            asset.Add("move.dash","대시",SkillBranch.Movement,1,SkillKind.Active,"",EquipSlot.Shift);
            asset.Add("move.jump","더블 점프",SkillBranch.Movement,1,SkillKind.Active,"",EquipSlot.Space);
            asset.Add("move.recall","흔적 귀환",SkillBranch.Movement,3,SkillKind.Active,"move.dash",EquipSlot.Shift,EquipSlot.Q,EquipSlot.E,EquipSlot.R);
            asset.Add("attack.pierce","관통탄",SkillBranch.Attack,1,SkillKind.Active,"",EquipSlot.Q,EquipSlot.E,EquipSlot.R);
            asset.Add("attack.burst","모래 폭발",SkillBranch.Attack,2,SkillKind.Active,"attack.pierce",EquipSlot.Q,EquipSlot.E,EquipSlot.R);
            asset.Add("attack.vortex","모래 소용돌이",SkillBranch.Attack,3,SkillKind.Active,"attack.burst",EquipSlot.Q,EquipSlot.E,EquipSlot.R);
            asset.Add("attack.storm","사막 폭풍",SkillBranch.Attack,3,SkillKind.Active,"attack.vortex",EquipSlot.Q,EquipSlot.E,EquipSlot.R);
            asset.Add("tower.cobra","화염 코브라 해금",SkillBranch.Tower,1,SkillKind.Passive,"");
            asset.Add("tower.obelisk","모래시계 오벨리스크 해금",SkillBranch.Tower,2,SkillKind.Passive,"tower.cobra");
            asset.Add("tower.skeleton","해골 소환진 해금",SkillBranch.Tower,2,SkillKind.Passive,"tower.cobra");
            asset.Add("tower.anubis","아누비스 해금",SkillBranch.Tower,3,SkillKind.Passive,"tower.skeleton");
            return asset;
        }
        void Add(string id,string label,SkillBranch branch,int cost,SkillKind kind,string previous,params EquipSlot[] slots)
        {
            nodes.Add(new SkillRow {id=id,displayName=label,description="테스트용 해금 데이터 — 효과 실행은 외부 연결부가 담당합니다.",branch=branch,kind=kind,cost=cost,
                prerequisites=string.IsNullOrEmpty(previous)?new List<string>():new List<string>{previous},allowedSlots=new List<EquipSlot>(slots)});
        }
    }
}
