using System;
using System.Collections.Generic;
using System.Linq;

namespace SandGuard.Skills
{
    public enum SkillKind { Active, Passive }
    public enum SkillBranch { Movement, Attack, Tower }
    public enum EquipSlot { Q, E, R, Shift, Space }
    public enum SkillFailure { None, NotFound, AlreadyLearned, Prerequisite, Points, Locked, IncompatibleSlot, DuplicateSlot, EditingBlocked, Busy, Invalid }
    public readonly struct SkillResult
    {
        public readonly SkillFailure Failure;
        public bool Success => Failure == SkillFailure.None;
        public SkillResult(SkillFailure failure) { Failure = failure; }
    }
    public sealed class SkillDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public SkillKind Kind { get; }
        public SkillBranch Branch { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public IReadOnlyList<EquipSlot> Slots { get; }
        public SkillDefinition(string id,string name,string description,int cost,SkillKind kind,SkillBranch branch,IEnumerable<string> prerequisites,IEnumerable<EquipSlot> slots)
        {
            Id=id; Name=name; Description=description; Cost=cost; Kind=kind; Branch=branch;
            Prerequisites=Array.AsReadOnly((prerequisites ?? Array.Empty<string>()).ToArray());
            Slots=Array.AsReadOnly((slots ?? Array.Empty<EquipSlot>()).ToArray());
        }
    }
    public sealed class SkillCatalog
    {
        readonly Dictionary<string,SkillDefinition> lookup = new Dictionary<string,SkillDefinition>(StringComparer.Ordinal);
        public IReadOnlyList<SkillDefinition> All { get; }
        public SkillCatalog(IEnumerable<SkillDefinition> definitions)
        {
            if(definitions==null)throw new ArgumentNullException(nameof(definitions));
            var list=definitions.ToArray();
            foreach(var d in list)
            {
                if(d==null || string.IsNullOrWhiteSpace(d.Id) || d.Cost<0 || lookup.ContainsKey(d.Id)) throw new ArgumentException("Invalid or duplicate skill ID/cost");
                if(!Enum.IsDefined(typeof(SkillKind),d.Kind) || !Enum.IsDefined(typeof(SkillBranch),d.Branch)) throw new ArgumentException("Invalid skill category");
                if(d.Slots.Distinct().Count()!=d.Slots.Count || d.Slots.Any(s=>!Enum.IsDefined(typeof(EquipSlot),s))) throw new ArgumentException("Invalid slots");
                if(d.Kind==SkillKind.Active && d.Slots.Count==0 || d.Kind==SkillKind.Passive && d.Slots.Count!=0) throw new ArgumentException("Active/passive slot mismatch");
                lookup.Add(d.Id,d);
            }
            foreach(var d in list)
                if(d.Prerequisites.Distinct().Count()!=d.Prerequisites.Count || d.Prerequisites.Any(p=>p==null || p==d.Id || !lookup.ContainsKey(p))) throw new ArgumentException("Invalid prerequisite");
            var visiting=new HashSet<string>(); var done=new HashSet<string>();
            foreach(var d in list) Visit(d.Id,visiting,done);
            All=Array.AsReadOnly(list);
        }
        void Visit(string id,HashSet<string> visiting,HashSet<string> done)
        {
            if(done.Contains(id)) return;
            if(!visiting.Add(id)) throw new ArgumentException("Prerequisite cycle");
            foreach(var p in lookup[id].Prerequisites) Visit(p,visiting,done);
            visiting.Remove(id);done.Add(id);
        }
        public SkillDefinition Find(string id) => id!=null && lookup.TryGetValue(id,out var d)?d:null;
    }
    public sealed class SkillSnapshot
    {
        public int Points { get; }
        public IReadOnlyList<string> Learned { get; }
        public IReadOnlyDictionary<EquipSlot,string> Loadout { get; }
        public SkillSnapshot(int points,IEnumerable<string> learned,IDictionary<EquipSlot,string> loadout)
        {
            Points=points;Learned=Array.AsReadOnly(learned.ToArray());
            Loadout=new System.Collections.ObjectModel.ReadOnlyDictionary<EquipSlot,string>(new Dictionary<EquipSlot,string>(loadout));
        }
    }
    /// <summary>Points and unlock state have one owner. No Unity, casting, mana or HUD dependency.</summary>
    public sealed class SkillService : IDisposable
    {
        public SkillCatalog Catalog { get; }
        public int Points => wallet.Balance;
        SkillPointWallet wallet;bool shared,changing,disposed;int generation;
        public int RefundPoints { get; private set; }
        public bool EditingAllowed { get; set; }=true;
        readonly HashSet<string> learned=new HashSet<string>();
        readonly Dictionary<EquipSlot,string> loadout=new Dictionary<EquipSlot,string>();
        bool notifying;
        public event Action Changed;
        public event Action<Exception> ObserverError;
        public SkillService(SkillCatalog catalog,int initialPoints=0)
        { Catalog=catalog??throw new ArgumentNullException(nameof(catalog));if(initialPoints<0)throw new ArgumentOutOfRangeException();wallet=new SkillPointWallet(initialPoints);wallet.Claim(this);wallet.Changed+=WalletChanged; }
        public SkillSnapshot Snapshot()=>new SkillSnapshot(Points,learned,loadout);
        public bool IsLearned(string id)=>id!=null && learned.Contains(id);
        public string Equipped(EquipSlot slot)=>loadout.TryGetValue(slot,out var id)?id:null;
        public SkillResult CanLearn(string id)
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(!EditingAllowed)return Fail(SkillFailure.EditingBlocked);
            var d=Catalog.Find(id);if(d==null)return Fail(SkillFailure.NotFound);
            if(learned.Contains(id))return Fail(SkillFailure.AlreadyLearned);
            if(d.Prerequisites.Any(p=>!learned.Contains(p)))return Fail(SkillFailure.Prerequisite);
            return Fail(Points<d.Cost?SkillFailure.Points:SkillFailure.None);
        }
        public SkillResult Learn(string id)
        {
            var r=CanLearn(id);if(!r.Success)return r;
            int cost=Catalog.Find(id).Cost;
            if(RefundPoints>int.MaxValue-cost)return Fail(SkillFailure.Invalid);
            changing=true;bool ok;
            try{ok=wallet.TryChange(-cost,()=>{learned.Add(id);RefundPoints+=cost;AutoEquipMovement(id);});}finally{changing=false;}
            if(!ok)return Fail(SkillFailure.Points);
            Notify();return r;
        }
        void AutoEquipMovement(string id)
        {
            var d=Catalog.Find(id);
            if(d==null || d.Branch!=SkillBranch.Movement || d.Kind!=SkillKind.Active)return;
            // Movement has dedicated keys. Recall takes the first free combat key so buying it
            // is immediately useful without stealing an already equipped attack.
            if(id=="move.dash" && d.Slots.Contains(EquipSlot.Shift)){loadout[EquipSlot.Shift]=id;return;}
            if(id=="move.jump" && d.Slots.Contains(EquipSlot.Space)){loadout[EquipSlot.Space]=id;return;}
            if(id=="move.recall")
            {
                foreach(var slot in new[]{EquipSlot.Q,EquipSlot.E,EquipSlot.R})
                    if(d.Slots.Contains(slot) && !loadout.ContainsKey(slot)){loadout[slot]=id;return;}
                // Keep the newly purchased skill usable even when every combat slot is full.
                foreach(var slot in new[]{EquipSlot.Q,EquipSlot.E,EquipSlot.R})
                    if(d.Slots.Contains(slot)){loadout[slot]=id;return;}
            }
        }
        public SkillResult Equip(EquipSlot slot,string id)
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(!EditingAllowed)return Fail(SkillFailure.EditingBlocked);
            if(!Enum.IsDefined(typeof(EquipSlot),slot))return Fail(SkillFailure.Invalid);
            if(id==null){if(loadout.Remove(slot))Notify();return Fail(SkillFailure.None);}
            var d=Catalog.Find(id);if(d==null)return Fail(SkillFailure.NotFound);
            if(!learned.Contains(id))return Fail(SkillFailure.Locked);
            if(d.Kind!=SkillKind.Active || !d.Slots.Contains(slot))return Fail(SkillFailure.IncompatibleSlot);
            if(loadout.Any(p=>p.Key!=slot && p.Value==id))return Fail(SkillFailure.DuplicateSlot);
            if(Equipped(slot)==id)return Fail(SkillFailure.None);
            loadout[slot]=id;Notify();return Fail(SkillFailure.None);
        }
        public SkillResult Grant(int amount)
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(amount<=0 || Points>int.MaxValue-amount)return Fail(SkillFailure.Invalid);
            if(shared)return Fail(SkillFailure.Invalid);
            return Fail(wallet.TryChange(amount)?SkillFailure.None:SkillFailure.Busy);
        }
        // New run, not a refund. Caller chooses authoritative starting points.
        public SkillResult Reset(int startingPoints)
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(startingPoints<0)return Fail(SkillFailure.Invalid);
            if(shared)return Fail(SkillFailure.Invalid);
            return Fail(wallet.Reset(startingPoints)?SkillFailure.None:SkillFailure.Busy);
        }
        public SkillResult Restore(SkillSnapshot state)
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(shared)return Fail(SkillFailure.Invalid);
            if(state==null || state.Points<0)return Fail(SkillFailure.Invalid);
            var set=new HashSet<string>(state.Learned);
            if(set.Count!=state.Learned.Count || set.Any(id=>Catalog.Find(id)==null || Catalog.Find(id).Prerequisites.Any(p=>!set.Contains(p))))return Fail(SkillFailure.Invalid);
            var used=new HashSet<string>();
            foreach(var p in state.Loadout)
                if(!Enum.IsDefined(typeof(EquipSlot),p.Key) || p.Value==null || !set.Contains(p.Value) || !used.Add(p.Value) || !Catalog.Find(p.Value).Slots.Contains(p.Key))return Fail(SkillFailure.Invalid);
            // Legacy snapshots did not record actual purchase payments: imported unlocks receive no refund credit.
            changing=true;
            try{if(!wallet.Reset(state.Points))return Fail(SkillFailure.Busy);generation=wallet.Generation;
                learned.Clear();foreach(var id in set)learned.Add(id);RefundPoints=0;
                loadout.Clear();foreach(var p in state.Loadout)loadout.Add(p.Key,p.Value);
                // Older saves can contain purchased movement skills without their automatic slot.
                if(set.Contains("move.dash"))AutoEquipMovement("move.dash");
                if(set.Contains("move.jump"))AutoEquipMovement("move.jump");
                if(set.Contains("move.recall") && !loadout.ContainsValue("move.recall"))AutoEquipMovement("move.recall");}
            finally{changing=false;}
            Notify();return Fail(SkillFailure.None);
        }
        public SkillResult BindWallet(SkillPointWallet source)
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(source==wallet)return Fail(SkillFailure.None);
            if(source==null || learned.Count>0 || !source.Claim(this))return Fail(SkillFailure.Invalid);
            wallet.Changed-=WalletChanged;wallet.Release(this);wallet=source;shared=true;generation=source.Generation;
            wallet.Changed+=WalletChanged;Notify();return Fail(SkillFailure.None);
        }
        public SkillResult Respec()
        {
            if(disposed)return Fail(SkillFailure.Invalid);
            if(notifying || changing)return Fail(SkillFailure.Busy);
            if(!EditingAllowed)return Fail(SkillFailure.EditingBlocked);
            if(learned.Count==0 && loadout.Count==0)return Fail(SkillFailure.None);
            changing=true;bool ok;
            try{ok=wallet.TryChange(RefundPoints,()=>{learned.Clear();loadout.Clear();RefundPoints=0;});}
            finally{changing=false;}
            if(!ok)return Fail(SkillFailure.Invalid);Notify();return Fail(SkillFailure.None);
        }
        void WalletChanged()
        {
            if(changing)return;
            if(generation!=wallet.Generation){generation=wallet.Generation;learned.Clear();loadout.Clear();RefundPoints=0;}
            Notify();
        }
        public void Dispose(){disposed=true;wallet.Changed-=WalletChanged;wallet.Release(this);}
        static SkillResult Fail(SkillFailure f)=>new SkillResult(f);
        void Notify()
        {
            notifying=true;
            try{if(Changed!=null)foreach(Action observer in Changed.GetInvocationList())try{observer();}catch(Exception e){try{ObserverError?.Invoke(e);}catch{}}}
            finally{notifying=false;}
        }
    }
}
