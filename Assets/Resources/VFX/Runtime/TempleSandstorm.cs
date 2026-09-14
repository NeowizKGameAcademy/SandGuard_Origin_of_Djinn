using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Environment-only seal: no damage, collision or changes to navigation.</summary>
    [ExecuteAlways]
    public sealed class TempleSandstorm : MonoBehaviour
    {
        [Min(0)] public float animationSpeed = 1;
        public bool animate = true;
        [Min(0)] public float previewTime = 12;
        public Renderer[] sandLayers;
        MaterialPropertyBlock properties;
        static readonly int StormTime = Shader.PropertyToID("_StormTime");

        void OnEnable() { ApplyTime(previewTime); }
        void Update()
        {
            ApplyTime(animate ? Time.realtimeSinceStartup * animationSpeed : previewTime);
        }
        public void ApplyTime(float time)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            if (sandLayers == null) return;
            foreach (var layer in sandLayers)
            {
                if (!layer) continue;
                layer.GetPropertyBlock(properties);
                properties.SetFloat(StormTime, time);
                layer.SetPropertyBlock(properties);
            }
        }
    }
}
