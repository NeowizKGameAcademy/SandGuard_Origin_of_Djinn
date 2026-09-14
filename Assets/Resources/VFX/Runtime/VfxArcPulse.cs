using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>World-space stroke flash or looping electrical flicker. No UI or timer.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class VfxArcPulse : MonoBehaviour
    {
        public bool Loop;
        public float Duration = 0.3f;
        public Color Tint = Color.white;
        public float Phase;
        LineRenderer line;
        float age;

        void OnEnable() => Restart();
        void Update() => Tick(Time.deltaTime);
        public void Restart() { age = 0f; line = GetComponent<LineRenderer>(); Tick(0f); }
        public void Tick(float dt)
        {
            age += Mathf.Max(0f, dt);
            if (line == null) line = GetComponent<LineRenderer>();
            float alpha = Loop
                ? (Mathf.Sin(age * 27f + Phase) > -0.15f ? 0.8f : 0.08f)
                : Mathf.Clamp01(1f - age / Mathf.Max(0.01f, Duration));
            var color = Tint; color.a *= alpha;
            line.startColor = color; line.endColor = color;
            line.enabled = alpha > 0f;
        }
    }
}
