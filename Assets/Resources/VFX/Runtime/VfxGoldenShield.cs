using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Persistent summoned shield visuals. Damage blocking remains the combat controller's job.</summary>
    public sealed class VfxGoldenShield : MonoBehaviour
    {
        [Min(.01f)] public float summonTime=.35f;
        [Min(.01f)] public float dismissTime=.3f;
        public Renderer[] shieldRenderers;
        public ParticleSystem motes;
        float fade, hit;
        bool dismissing;
        MaterialPropertyBlock properties;
        static readonly int Fade=Shader.PropertyToID("_EffectFade"), Hit=Shader.PropertyToID("_Hit");
        public bool IsVisible => fade>0;
        void OnEnable()=>Restart();
        void Update()=>Tick(Time.deltaTime);
        public void Restart()
        {
            fade=0;hit=0;dismissing=false;
            if(motes){motes.Clear(true);if(Application.isPlaying)motes.Play(true);}
            Apply();
        }
        public void Dismiss(){dismissing=true;if(motes)motes.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        public void PulseHit(){if(!dismissing)hit=1;Apply();}
        public void Tick(float dt)
        {
            dt=Mathf.Max(0,dt);
            fade=Mathf.MoveTowards(fade,dismissing?0:1,dt/Mathf.Max(.01f,dismissing?dismissTime:summonTime));
            hit=Mathf.MoveTowards(hit,0,dt/.22f);Apply();
        }
        void Apply()
        {
            properties??=new MaterialPropertyBlock();
            properties.SetFloat(Fade,fade);properties.SetFloat(Hit,hit);
            if(shieldRenderers==null)return;
            foreach(var r in shieldRenderers)if(r){r.SetPropertyBlock(properties);r.enabled=fade>0;}
        }
        void OnDisable(){if(motes)motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    }
}
