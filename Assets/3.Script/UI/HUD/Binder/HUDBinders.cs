using System;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class PlayerHUDBinder : MonoBehaviour { [SerializeField] private PlayerStatusHUD view; public void SetLevel(int v)=>view.SetLevel(v); public void SetHealth(float c,float m)=>view.SetHealth(c,m); public void SetExperience(float c,float m)=>view.SetExperience(c,m); public void SetMana(float c,float m)=>view.SetMana(c,m); }
    public sealed class CoreHUDBinder : MonoBehaviour { [SerializeField] private CoreStatusHUD view; public void SetStability(float c,float m)=>view.SetStability(c,m); public void SetVisible(bool v)=>view.SetVisible(v); }
    public sealed class BossHUDBinder : MonoBehaviour { [SerializeField] private BossStatusHUD view; public void ShowBoss(string n,Sprite i,float m)=>view.ShowBoss(n,i,m); public void SetHealth(float c,float m)=>view.SetHealth(c,m); public void HideBoss()=>view.HideBoss(); }
    public sealed class WaveHUDBinder : MonoBehaviour { [SerializeField] private WaveHUD view; public void SetWave(int v)=>view.SetWave(v); }
    public sealed class SkillHUDBinder : MonoBehaviour { [SerializeField] private CombatSkillHUD combat; [SerializeField] private MovementSkillHUD movement; public CombatSkillHUD Combat=>combat; public MovementSkillHUD Movement=>movement; }
}
