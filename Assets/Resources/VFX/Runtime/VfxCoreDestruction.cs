using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Staged core destruction, with animated Level-model pose capture and legacy crystal support.</summary>
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
        public bool WholeModel;
        public string[] MeshPaths;
        public Transform[] ProxyMeshes;
        public Matrix4x4[] ReferenceMatrices;
        public int[] ShardMeshIndices;
        Vector3[] poseStarts, poseScales;
        Quaternion[] poseRotations;
        Vector3[] intactStarts, intactScales, intactLandings;
        Quaternion[] intactRotations, intactLandingRotations;
        Renderer[] modelRenderers;
        bool[] rendererStates;
        Light[] modelLights;
        bool[] lightStates;
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
            poseStarts = null; poseRotations = null; poseScales = null;
            if (WholeModel && ProxyMeshes != null)
            {
                for (int i = 0; i < ProxyMeshes.Length; i++)
                {
                    ProxyMeshes[i].localPosition = ReferenceMatrices[i].GetColumn(3);
                    ProxyMeshes[i].localRotation = ReferenceMatrices[i].rotation;
                    ProxyMeshes[i].localScale = ReferenceMatrices[i].lossyScale;
                }
                CaptureIntactParts();
            }
            block ??= new MaterialPropertyBlock();
            if (Overload != null && overloadScale == Vector3.zero) overloadScale = Overload.localScale;
            Tick(0f);
        }

        /// <summary>Bind the source model root. Destruction remains after this VFX is released.</summary>
        public void BindTarget(Transform coreBase)
        {
            if (WholeModel)
            {
                if (!coreBase) return;
                // Preserve the current animated pose, not just the prefab's rest pose.
                var deltas = new Matrix4x4[MeshPaths.Length];
                for (int i = 0; i < MeshPaths.Length; i++)
                {
                    var source = coreBase.Find(MeshPaths[i]);
                    var current = source ? coreBase.worldToLocalMatrix * source.localToWorldMatrix : ReferenceMatrices[i];
                    deltas[i] = current * ReferenceMatrices[i].inverse;
                    ProxyMeshes[i].localPosition = current.GetColumn(3);
                    ProxyMeshes[i].localRotation = current.rotation;
                    ProxyMeshes[i].localScale = current.lossyScale;
                }
                CaptureIntactParts();
                poseStarts = new Vector3[Shards.Length]; poseRotations = new Quaternion[Shards.Length];
                poseScales = new Vector3[Shards.Length];
                for (int i = 0; i < Shards.Length; i++)
                {
                    var delta = deltas[ShardMeshIndices[i]];
                    poseStarts[i] = delta.MultiplyPoint3x4(Starts[i]);
                    poseRotations[i] = delta.rotation; poseScales[i] = delta.lossyScale;
                }
                modelRenderers = coreBase.GetComponentsInChildren<Renderer>(true);
                rendererStates = new bool[modelRenderers.Length];
                for (int i = 0; i < modelRenderers.Length; i++)
                { rendererStates[i] = modelRenderers[i].enabled; modelRenderers[i].enabled = false; }
                modelLights = coreBase.GetComponentsInChildren<Light>(true);
                lightStates = new bool[modelLights.Length];
                for (int i = 0; i < modelLights.Length; i++)
                { lightStates[i] = modelLights[i].enabled; modelLights[i].enabled = false; }
                return;
            }
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
            if (modelRenderers != null) for (int i = 0; i < modelRenderers.Length; i++)
                if (modelRenderers[i]) modelRenderers[i].enabled = rendererStates[i];
            if (modelLights != null) for (int i = 0; i < modelLights.Length; i++)
                if (modelLights[i]) modelLights[i].enabled = lightStates[i];
            modelRenderers = null; modelLights = null;
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
                Overload.gameObject.SetActive(charging || WholeModel);
                if (charging)
                {
                    float t = Mathf.Clamp01(age / BurstTime);
                    Overload.localScale = WholeModel ? overloadScale : overloadScale * Mathf.Lerp(1f, 0.92f, t * t);
                    Colorize(OverloadRenderer, Color.Lerp(new Color(0.1f, 0.85f, 0.8f), Color.white * 3f, t * t));
                }
            }
            if (WholeModel && intactStarts != null) TickIntactParts(charging);
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
                var shard = Shards[i];
                bool crystal = !WholeModel || IsCrystal(ShardMeshIndices[i]);
                shard.gameObject.SetActive(!charging && crystal);
                if (charging) continue;
                float t = Mathf.Clamp01((age - BurstTime) / (FlightTime + (WholeModel ? i % 4 : i) * 0.035f));
                shard.localPosition = Vector3.Lerp(poseStarts != null ? poseStarts[i] : Starts[i], Landings[i], t) + Vector3.up * (4f * t * (1f - t) * Lift);
                shard.localRotation = Quaternion.Slerp(poseRotations != null ? poseRotations[i] : Quaternion.identity, LandingRotations[i], t);
                if (WholeModel)
                {
                    // Fast radial impulse first, then gravity pulls the fragments down.
                    float flight = Mathf.Clamp01((age - BurstTime) / (1.05f + i % 4 * 0.055f));
                    float spread = 1f - Mathf.Pow(1f - flight, 3f);
                    var start = poseStarts != null ? poseStarts[i] : Starts[i];
                    var position = Vector3.Lerp(start, Landings[i], spread);
                    position.y = Mathf.Lerp(start.y, Landings[i].y, flight * flight)
                        + 4f * flight * (1f - flight) * Lift * (1.4f + i % 5 * 0.45f);
                    shard.localPosition = position;
                    shard.localRotation = Quaternion.Slerp(poseRotations != null ? poseRotations[i] : Quaternion.identity,
                        LandingRotations[i], spread) * Quaternion.AngleAxis(360f * flight, new Vector3(1f, i % 3 + 1f, 0.5f).normalized);
                    shard.localScale = (poseScales != null ? poseScales[i] : Vector3.one)
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.8f, 3.8f, age)));
                    continue;
                }
                float fade = Mathf.InverseLerp(0.45f, 1.8f, age);
                Colorize(shard.GetComponent<Renderer>(), Color.Lerp(new Color(0.1f, 1f, 0.9f) * 2f,
                    new Color(0.045f, 0.19f, 0.20f), fade));
            }
        }

        bool IsCrystal(int index) => MeshPaths[index] == "Core Crystal/Mesh";

        void CaptureIntactParts()
        {
            int count = ProxyMeshes.Length;
            intactStarts = new Vector3[count]; intactScales = new Vector3[count];
            intactLandings = new Vector3[count]; intactRotations = new Quaternion[count];
            intactLandingRotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                var part = ProxyMeshes[i];
                intactStarts[i] = part.localPosition; intactScales[i] = part.localScale;
                intactRotations[i] = part.localRotation;
                var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
                bool ring = MeshPaths[i].StartsWith("Ring ");
                var size = bounds.size;
                var normal = size.x < size.y && size.x < size.z ? Vector3.right
                    : size.z < size.y ? Vector3.forward : Vector3.up;
                var rotation = ring
                    ? Quaternion.FromToRotation(part.localRotation * normal, Vector3.up) * part.localRotation
                    : part.localRotation;
                intactLandingRotations[i] = rotation;
                float bottom = float.PositiveInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    bottom = Mathf.Min(bottom, (rotation * Vector3.Scale(point, part.localScale)).y);
                }
                intactLandings[i] = new Vector3(part.localPosition.x + Mathf.Cos(i * 2.4f) * 0.35f,
                    -bottom + 0.04f + (ring ? (i % 3) * 0.08f : 0f),
                    part.localPosition.z + Mathf.Sin(i * 2.4f) * 0.35f);
            }
        }

        void TickIntactParts(bool charging)
        {
            for (int i = 0; i < ProxyMeshes.Length; i++)
            {
                var part = ProxyMeshes[i];
                part.gameObject.SetActive(charging || !IsCrystal(i));
                float t = charging ? 0f : Mathf.Clamp01((age - BurstTime) / (FlightTime + i * 0.035f));
                // Accelerate downwards, then settle flat without an explosive outward launch.
                part.localPosition = Vector3.Lerp(intactStarts[i], intactLandings[i], t * t);
                part.localRotation = Quaternion.Slerp(intactRotations[i], intactLandingRotations[i], t * t);
                part.localScale = intactScales[i] * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(2.8f, 3.8f, age)));
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
