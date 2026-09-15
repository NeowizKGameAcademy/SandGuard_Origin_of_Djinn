using System;
using UnityEngine;

namespace SandGuard.Skills.Unity
{
    [CreateAssetMenu(menuName="SandGuard/Skills/UI Theme")]
    public sealed class SkillUITheme : ScriptableObject
    {
        [Serializable] public sealed class IconEntry { public string skillId; public Sprite sprite; }
        public Font font;
        public Sprite panel, button, ring, ornament;
        public IconEntry[] icons=Array.Empty<IconEntry>();
        public Color gold=new Color(1f,.77f,.31f), cyan=new Color(.24f,.91f,1f), muted=new Color(.40f,.46f,.50f);
        public Sprite Icon(string id)
        { foreach(var entry in icons)if(entry!=null && entry.skillId==id)return entry.sprite;return null; }
    }
}
