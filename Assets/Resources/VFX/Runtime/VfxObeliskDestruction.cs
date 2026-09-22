using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Blue chain detonations followed by textured obelisk rubble; gameplay owns health.</summary>
    public sealed class VfxObeliskDestruction : MonoBehaviour, IVfxTargetBinding
    {
        public GameObject Proxy;
        public Transform[] Pieces;
        public Vector3[] Starts, Landings;
        public Quaternion[] Rotations;
        public float BurstTime = 0.72f;
        float age;
        Renderer[] sourceRenderers;
        bool[] sourceEnabled;
        Light[] sourceLights;
        bool[] lightsEnabled;
        ParticleSystem[] sourceParticles;
        bool[] particlesPlaying;
        MaterialPropertyBlock block;
        public float Age => age;
        void OnEnable() => Restart();
        void Update() => Tick(Time.deltaTime);

        public void Restart() { age = 0f; block ??= new MaterialPropertyBlock(); Tick(0f); }
        public void BindTarget(Transform tower)
        {
            if (tower == null) return;
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var r in tower.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(transform)) renderers.Add(r);
            sourceRenderers = renderers.ToArray(); sourceEnabled = new bool[sourceRenderers.Length];
            for (int i = 0; i < sourceRenderers.Length; i++) { sourceEnabled[i] = sourceRenderers[i].enabled; sourceRenderers[i].enabled = false; }
            var lights = new System.Collections.Generic.List<Light>();
            foreach (var l in tower.GetComponentsInChildren<Light>(true)) if (!l.transform.IsChildOf(transform)) lights.Add(l);
            sourceLights = lights.ToArray(); lightsEnabled = new bool[sourceLights.Length];
            for (int i = 0; i < sourceLights.Length; i++) { lightsEnabled[i] = sourceLights[i].enabled; sourceLights[i].enabled = false; }
            var particles = new System.Collections.Generic.List<ParticleSystem>();
            foreach (var ps in tower.GetComponentsInChildren<ParticleSystem>(true)) if (!ps.transform.IsChildOf(transform)) particles.Add(ps);
            sourceParticles = particles.ToArray(); particlesPlaying = new bool[sourceParticles.Length];
            for (int i = 0; i < sourceParticles.Length; i++)
            { particlesPlaying[i] = sourceParticles[i].isPlaying; sourceParticles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }

        // Only an explicit respawn/showcase reset restores the original.
        public void RestoreTarget()
        {
            if (sourceRenderers != null) for (int i = 0; i < sourceRenderers.Length; i++) if (sourceRenderers[i] != null) sourceRenderers[i].enabled = sourceEnabled[i];
            if (sourceLights != null) for (int i = 0; i < sourceLights.Length; i++) if (sourceLights[i] != null) sourceLights[i].enabled = lightsEnabled[i];
            if (sourceParticles != null) for (int i = 0; i < sourceParticles.Length; i++) if (sourceParticles[i] != null && particlesPlaying[i]) sourceParticles[i].Play(false);
            sourceRenderers = null; sourceLights = null; sourceParticles = null;
        }

        public void Tick(float dt)
        {
            age += Mathf.Max(0f, dt);
            if (Proxy != null)
            {
                Proxy.SetActive(age < BurstTime);
                float shake = age < BurstTime ? Mathf.Sin(age * 95f) * 0.025f * Mathf.Clamp01(age / BurstTime) : 0f;
                Proxy.transform.localPosition = new Vector3(shake, 0f, shake * 0.6f);
                Proxy.transform.localRotation = Quaternion.Euler(shake * 35f, 0f, shake * 50f);
            }
            if (Pieces == null) return;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < Pieces.Length; i++)
            {
                var piece = Pieces[i]; piece.gameObject.SetActive(age >= BurstTime);
                if (age < BurstTime) continue;
                float t = Mathf.Clamp01((age - BurstTime) / (0.75f + (i % 4) * 0.08f));
                piece.localPosition = Vector3.Lerp(Starts[i], Landings[i], t) + Vector3.up * (1.5f * t * (1f - t));
                piece.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.8f, 3.8f, age)));
                piece.localRotation = Quaternion.Slerp(Quaternion.identity, Rotations[i], t);
                float brightness = Mathf.Lerp(1.2f, 0.65f, Mathf.InverseLerp(0.2f, 1.5f, age));
                var color = new Color(brightness, brightness, brightness, 1f);
                block.Clear(); block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
                piece.GetComponent<Renderer>().SetPropertyBlock(block);
            }
        }
    }
}
