using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>A short discharge travels along a baked path. Unoccupied path sections are never drawn.</summary>
    public sealed class VfxLightningPath : MonoBehaviour
    {
        public Vector3[] Points;
        public LineRenderer Glow, Core;
        public ParticleSystem Sparks;
        public float Period = 0.95f, Phase, Delay, TravelTime = 0.32f;
        [Range(0.05f, 0.5f)] public float Tail = 0.25f;
        float age, lastSpark;
        float[] distances;
        Vector3[] visible;
        float length;
        public Vector3 HeadPosition { get; private set; }
        public bool IsDischarging { get; private set; }

        void OnEnable() => Restart();
        void OnDisable() { Hide(); if (Sparks != null) Sparks.Clear(); }
        void Update() => Tick(Time.deltaTime);

        public void Restart()
        {
            age = 0f; lastSpark = -1f;
            if (Points == null || Points.Length < 2) { Hide(); return; }
            distances = new float[Points.Length]; visible = new Vector3[Points.Length + 2];
            for (int i = 1; i < Points.Length; i++) distances[i] = distances[i - 1] + Vector3.Distance(Points[i - 1], Points[i]);
            length = distances[distances.Length - 1];
            if (Sparks != null) Sparks.Clear();
            Tick(0f);
        }

        public void Tick(float dt)
        {
            age += Mathf.Max(0f, dt);
            if (length < 0.0001f || Glow == null || Core == null) { Hide(); return; }
            float cycle = Mathf.Repeat(age + Phase, Mathf.Max(0.01f, Period));
            float progress = (cycle - Delay) / Mathf.Max(0.01f, TravelTime);
            if (progress <= 0f || progress >= 1f + Tail) { Hide(); return; }
            float start = Mathf.Max(0f, progress - Tail) * length;
            float end = Mathf.Min(1f, progress) * length;
            int count = 0;
            visible[count++] = Sample(start);
            for (int i = 1; i < Points.Length - 1; i++)
                if (distances[i] > start && distances[i] < end) visible[count++] = Points[i];
            HeadPosition = Sample(end);
            visible[count++] = HeadPosition;
            IsDischarging = true;
            float fade = progress > 1f ? (1f + Tail - progress) / Tail : 1f;
            float crackle = 0.8f + 0.2f * Mathf.Abs(Mathf.Sin(age * 137f + Phase * 43f));
            Draw(Glow, count, new Color(1f, 0.12f, 0.025f, fade * crackle));
            Draw(Core, count, new Color(1f, 0.94f, 0.72f, fade * crackle));
            if (Sparks != null && progress < 1f && dt > 0f && age - lastSpark >= 0.045f)
            {
                var emit = new ParticleSystem.EmitParams { position = HeadPosition };
                Sparks.Emit(emit, 1); lastSpark = age;
            }
        }

        void Draw(LineRenderer line, int count, Color color)
        {
            line.enabled = true; line.positionCount = count;
            for (int i = 0; i < count; i++) line.SetPosition(i, visible[i]);
            var tailColor = color; tailColor.a = 0f;
            line.startColor = tailColor; line.endColor = color;
        }

        Vector3 Sample(float distance)
        {
            for (int i = 1; i < distances.Length; i++)
                if (distance <= distances[i]) return Vector3.Lerp(Points[i - 1], Points[i],
                    (distance - distances[i - 1]) / Mathf.Max(0.0001f, distances[i] - distances[i - 1]));
            return Points[Points.Length - 1];
        }

        void Hide()
        {
            IsDischarging = false;
            if (Glow != null) Glow.enabled = false;
            if (Core != null) Core.enabled = false;
        }
    }
}
