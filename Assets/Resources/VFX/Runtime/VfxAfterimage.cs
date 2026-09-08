using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 대시 잔상 (VFX 제작계획 #5): 캐릭터 메시를 복제해 틸 실루엣으로 남기고 서서히 지운다.
    /// 대시가 시작될 때 <c>Play(대시 시간)</c>을 부르면 Interval마다 스냅샷을 찍는다. 스키닝 메시는 현재 포즈로 굽는다.
    /// </summary>
    public sealed class VfxAfterimage : MonoBehaviour
    {
        [Tooltip("잔상 머티리얼. 비우면 M_VFX_Mesh_Additive 같은 가산 머티리얼을 넣는다")]
        public Material Material;
        public Color Color = new Color(0.18f, 0.9f, 0.84f, 0.7f);
        public float Interval = 0.05f;
        public float GhostLifetime = 0.35f;
        [Tooltip("비우면 자식 MeshRenderer/SkinnedMeshRenderer 전부")]
        public Renderer[] Sources;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Coroutine _run;

        void Awake() => CollectSources();

        // 외형이 나중에 교체·생성될 수 있으므로 비어 있으면 스냅샷 때 다시 모은다.
        void CollectSources()
        {
            if (Sources != null && Sources.Length > 0) return;
            var list = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) list.Add(renderer);
            Sources = list.ToArray();
        }

        public void Play(float duration)
        {
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Run(duration));
        }

        IEnumerator Run(float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                Snapshot();
                yield return new WaitForSeconds(Interval);
            }
            _run = null;
        }

        /// <summary>지금 포즈로 잔상 하나를 남긴다.</summary>
        public void Snapshot()
        {
            CollectSources();
            if (Material == null || Sources == null) return;
            foreach (var source in Sources)
            {
                if (source == null) continue;
                Mesh mesh = null;
                bool baked = false;
                if (source is SkinnedMeshRenderer skinned) { mesh = new Mesh(); skinned.BakeMesh(mesh); baked = true; }
                else if (source is MeshRenderer) { var filter = source.GetComponent<MeshFilter>(); mesh = filter != null ? filter.sharedMesh : null; }
                if (mesh == null) continue;
                var ghost = new GameObject("Afterimage");
                ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                ghost.transform.localScale = baked ? Vector3.one : source.transform.lossyScale;
                ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ghost.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var fade = ghost.AddComponent<Ghost>();
                fade.Renderer = renderer; fade.Color = Color; fade.Lifetime = GhostLifetime; fade.OwnsMesh = baked;
            }
        }

        sealed class Ghost : MonoBehaviour
        {
            public MeshRenderer Renderer; public Color Color; public float Lifetime; public bool OwnsMesh;
            MaterialPropertyBlock _block; float _age;
            void Update()
            {
                _age += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(_age / Mathf.Max(Lifetime, 0.0001f));
                _block ??= new MaterialPropertyBlock();
                Color color = Color; color.a *= k;
                _block.SetColor(BaseColorId, color);
                Renderer.SetPropertyBlock(_block);
                if (k <= 0f) Destroy(gameObject);
            }
            void OnDestroy() { if (OwnsMesh) { var filter = GetComponent<MeshFilter>(); if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh); } }
        }
    }
}
