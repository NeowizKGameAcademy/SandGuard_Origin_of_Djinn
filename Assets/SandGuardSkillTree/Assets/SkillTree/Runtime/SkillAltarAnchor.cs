using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Skills.Unity
{
    /// <summary>Attach to the tower/altar interaction point. One window can serve many anchors.</summary>
    public sealed class SkillAltarAnchor : MonoBehaviour
    {
        internal static readonly List<SkillAltarAnchor> Active=new List<SkillAltarAnchor>();
        public string displayName="스킬 제단";
        [Min(.1f)] public float interactionRadius=4f;
        void OnEnable(){if(!Active.Contains(this))Active.Add(this);}
        void OnDisable()=>Active.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()=>Active.Clear();
        void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,interactionRadius);}
    }
}
