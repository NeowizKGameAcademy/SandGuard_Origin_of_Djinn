using UnityEngine;
using UnityEngine.Rendering;

namespace DesertTower.VFX
{
    /// <summary>Open transparent cylinder with downward chevrons, driven by the preparation clock.</summary>
    public sealed class VfxSpawnWarning : MonoBehaviour
    {
        Mesh mesh;
        Material material;
        MeshRenderer wall;
        MaterialPropertyBlock properties;
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Fade = Shader.PropertyToID("_Fade");
        static readonly int Scroll = Shader.PropertyToID("_Scroll");
        static readonly int Pulse = Shader.PropertyToID("_Pulse");

        public void Initialize(float radius, float height)
        {
            if (wall != null) return;
            var shader = Resources.Load<Shader>("VFX/Shaders/SpawnWarningCylinder");
            if (shader == null) { Debug.LogError("Spawn warning cylinder shader is missing", this); return; }
            material = new Material(shader) { name = "Spawn Warning (Instance)" };
            properties = new MaterialPropertyBlock();
            properties.SetFloat("_Rows", Mathf.Max(3f, Mathf.Ceil(height / 6f)));
            const int segments = 96;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                var bottom = new Vector3(Mathf.Cos(t * Mathf.PI * 2f), 0f, Mathf.Sin(t * Mathf.PI * 2f)) * radius;
                vertices[i * 2] = bottom;
                vertices[i * 2 + 1] = bottom + Vector3.up * height;
                uv[i * 2] = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
                if (i == segments) continue;
                int n = i * 2, index = i * 6;
                triangles[index] = n; triangles[index + 1] = n + 1; triangles[index + 2] = n + 2;
                triangles[index + 3] = n + 2; triangles[index + 4] = n + 1; triangles[index + 5] = n + 3;
            }
            mesh = new Mesh { name = "Spawn Warning Open Cylinder" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            wall = gameObject.AddComponent<MeshRenderer>();
            wall.sharedMaterial = material;
            wall.shadowCastingMode = ShadowCastingMode.Off;
            wall.receiveShadows = false;
            SetAppearance(new Color(1f, .48f, .08f), 0f, 0f, 0f);
        }

        public void SetAppearance(Color color, float opacity, float phase, float pulse)
        {
            if (wall == null) return;
            properties.SetColor(Tint, color);
            properties.SetFloat(Fade, Mathf.Clamp01(opacity));
            properties.SetFloat(Scroll, phase * .65f);
            properties.SetFloat(Pulse, pulse);
            wall.SetPropertyBlock(properties);
        }

        void OnDestroy()
        {
            if (Application.isPlaying) { Destroy(mesh); Destroy(material); }
            else { DestroyImmediate(mesh); DestroyImmediate(material); }
        }
    }
}
