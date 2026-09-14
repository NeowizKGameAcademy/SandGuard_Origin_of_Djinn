using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Visual-only staged destruction. Bind a Core Base; its pedestal survives.</summary>
    public sealed class VfxCoreDestruction : MonoBehaviour, IVfxTargetBinding
    {
        public Transform Overload;
        public Renderer OverloadRenderer;
        public LineRenderer[] Cracks;
        public Transform[] Shards;
        public Vector3[] Starts, Landings;
        public Quaternion[] LandingRotations;
        public Vector3 Center;
        public float BurstTime = 0.2f;
        public float FlightTime = 0.9f;
        public float Lift = 1f;
        float age;
        Vector3 overloadScale;
        MaterialPropertyBlock block;
        GameObject sourceCore, sourceCircle;
        bool coreWasActive, circleWasActive;
        public float Age => age;

        void OnEnable() => Restart();
        void Update() => Tick(Time.deltaTime);

        public void Restart()
        {
            age = 0f;
            block ??= new MaterialPropertyBlock();
            if (Overload != null && overloadScale == Vector3.zero) overloadScale = Overload.localScale;
            Tick(0f);
        }

        /// <summary>Root and scale must match Core Base. Destruction remains after this VFX is released.</summary>
        public void BindTarget(Transform coreBase)
        {
            sourceCore = coreBase != null ? coreBase.Find("Core")?.gameObject : null;
            sourceCircle = coreBase != null ? coreBase.Find("Circle Effect")?.gameObject : null;
            coreWasActive = sourceCore != null && sourceCore.activeSelf;
            circleWasActive = sourceCircle != null && sourceCircle.activeSelf;
            if (sourceCore != null) sourceCore.SetActive(false); // proxy replaces crystal including its light
            if (age >= BurstTime && sourceCircle != null) sourceCircle.SetActive(false);
        }

        /// <summary>Explicit reset for showcase/respawn only. Never called automatically by cleanup.</summary>
        public void RestoreTarget()
        {
            if (sourceCore != null) sourceCore.SetActive(coreWasActive);
            if (sourceCircle != null) sourceCircle.SetActive(circleWasActive);
            sourceCore = null; sourceCircle = null;
        }

        public void Tick(float dt)
        {
            age += Mathf.Max(0f, dt);
            block ??= new MaterialPropertyBlock();
            bool charging = age < BurstTime;
            if (Overload != null)
            {
                Overload.gameObject.SetActive(charging);
                if (charging)
                {
                    float t = Mathf.Clamp01(age / BurstTime);
                    Overload.localScale = overloadScale * Mathf.Lerp(1f, 0.92f, t * t);
                    Colorize(OverloadRenderer, Color.Lerp(new Color(0.1f, 0.85f, 0.8f), Color.white * 3f, t * t));
                }
            }
            if (!charging && sourceCircle != null) sourceCircle.SetActive(false);
            if (Cracks != null) foreach (var line in Cracks)
            {
                line.enabled = charging;
                var color = new Color(0.65f, 1f, 1f, Mathf.Clamp01(age / BurstTime));
                line.startColor = color; line.endColor = color;
            }
            if (Shards == null) return;
            for (int i = 0; i < Shards.Length; i++)
            {
                var shard = Shards[i]; shard.gameObject.SetActive(!charging);
                if (charging) continue;
                float t = Mathf.Clamp01((age - BurstTime) / (FlightTime + i * 0.035f));
                shard.localPosition = Vector3.Lerp(Starts[i], Landings[i], t) + Vector3.up * (4f * t * (1f - t) * Lift);
                shard.localRotation = Quaternion.Slerp(Quaternion.identity, LandingRotations[i], t);
                float fade = Mathf.InverseLerp(0.45f, 1.8f, age);
                Colorize(shard.GetComponent<Renderer>(), Color.Lerp(new Color(0.1f, 1f, 0.9f) * 2f,
                    new Color(0.045f, 0.19f, 0.20f), fade));
            }
        }

        void Colorize(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            color.a = 1f; block.Clear(); block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
