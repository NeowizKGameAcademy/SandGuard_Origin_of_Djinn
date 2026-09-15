using System;

namespace SandGuard.Skills
{
    /// <summary>Single-threaded shared balance. Commit callbacks complete state before notifications.</summary>
    public sealed class SkillPointWallet
    {
        public int Balance { get; private set; }
        public int Generation { get; private set; }
        public event Action Changed;
        bool busy;object treeOwner;
        public bool IsBusy=>busy;
        public SkillPointWallet(int initial=0){if(initial<0)throw new ArgumentOutOfRangeException();Balance=initial;}
        public bool Claim(object owner){if(treeOwner!=null && treeOwner!=owner)return false;treeOwner=owner;return true;}
        public void Release(object owner){if(treeOwner==owner)treeOwner=null;}
        public bool TryChange(int delta)=>TryChange(delta,null);
        internal bool TryChange(int delta,Action commit)
        {
            long next=(long)Balance+delta;if(busy || next<0 || next>int.MaxValue)return false;
            busy=true;
            try{Balance=(int)next;commit?.Invoke();Notify();return true;}
            finally{busy=false;}
        }
        public bool Reset(int value)
        {
            if(busy || value<0)return false;
            busy=true;try{Balance=value;Generation++;Notify();return true;}finally{busy=false;}
        }
        void Notify(){if(Changed!=null)foreach(Action a in Changed.GetInvocationList())try{a();}catch{/* committed balance remains authoritative */}}
    }
}
