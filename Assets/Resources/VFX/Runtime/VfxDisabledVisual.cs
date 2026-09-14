using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Optional target desaturation. The caller owns the actual disable state and five-second lifetime.</summary>
    public sealed class VfxDisabledVisual : MonoBehaviour
    {
        sealed class Entry { public Renderer Renderer; public Material[] Original, Copies; }
        readonly List<Entry> entries = new List<Entry>();

        /// <summary>Call after spawning under the facility. Never changes attack logic or UI.</summary>
        public void BindTarget(Transform target)
        {
            Restore();
            if (target == null) return;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || renderer.transform.IsChildOf(transform)) continue;
                var original = renderer.sharedMaterials;
                var copies = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    var mat = copies[i] = new Material(original[i]);
                    foreach (string property in new[] { "_BaseColor", "_Color" })
                    {
                        if (!mat.HasProperty(property)) continue;
                        var c = mat.GetColor(property);
                        float gray = c.grayscale * 0.55f;
                        mat.SetColor(property, new Color(gray, gray, gray, c.a));
                    }
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                }
                entries.Add(new Entry { Renderer = renderer, Original = original, Copies = copies });
                renderer.sharedMaterials = copies;
            }
        }

        void OnDisable() => Restore();
        void OnDestroy() => Restore();
        public void Restore()
        {
            foreach (var entry in entries)
            {
                if (entry.Renderer != null)
                {
                    var current = entry.Renderer.sharedMaterials;
                    for (int i = 0; i < current.Length && i < entry.Copies.Length; i++)
                        if (current[i] == entry.Copies[i]) current[i] = entry.Original[i];
                    entry.Renderer.sharedMaterials = current;
                }
                foreach (var mat in entry.Copies)
                    if (mat != null) { if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat); }
            }
            entries.Clear();
        }
    }
}
