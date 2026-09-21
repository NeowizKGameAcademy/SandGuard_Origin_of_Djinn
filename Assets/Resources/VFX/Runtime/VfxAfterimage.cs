using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 대시 잔상 (VFX 제작계획 #5): 캐릭터 메시를 복제해 틸 실루엣으로 남기고 서서히 지운다.
    /// 대시가 시작될 때 <c>Play(대시 시간)</c>을 부르면 Interval마다 스냅샷을 찍는다. 스키닝 메시는 현재 포즈로 굽는다.
    /// </summary>
    /// <remarks>
    /// 잔상은 프리팹이 아니라 코드로 만든 오브젝트라 PrefabPool을 쓸 수 없어 여기서 직접 돌려 쓴다.
    /// 다 사라진 잔상은 이 오브젝트 아래에 비활성으로 보관하고, 구운 메시도 그대로 두고 다음 스냅샷에 덮어 굽는다.
    /// 활성 잔상은 부모 없이 월드에 두어 주인이 사라져도 자연스럽게 지워진다.
    /// </remarks>
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
        readonly Stack<Ghost> idle = new Stack<Ghost>();
        Coroutine _run;

        /// <summary>보관 중인 잔상 수. 재사용이 도는지 확인할 때 쓴다.</summary>
        public int IdleGhostCount => idle.Count;

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
                var skinned = source as SkinnedMeshRenderer;
                Mesh shared = null;
                if (skinned == null)
                {
                    if (!(source is MeshRenderer)) continue;
                    var filter = source.GetComponent<MeshFilter>();
                    shared = filter != null ? filter.sharedMesh : null;
                    if (shared == null) continue;
                }
                Ghost ghost = Rent();
                if (skinned != null) ghost.Bake(skinned); else ghost.Use(shared);
                ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                ghost.transform.localScale = skinned != null ? Vector3.one : source.transform.lossyScale;
                ghost.Begin(Material, Color, GhostLifetime);
            }
        }

        Ghost Rent()
        {
            Ghost ghost = null;
            while (idle.Count > 0 && ghost == null) ghost = idle.Pop(); // 씬과 함께 지워진 잔상은 건너뛴다
            if (ghost == null)
            {
                var go = new GameObject("Afterimage");
                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                ghost = go.AddComponent<Ghost>();
                ghost.Filter = filter; ghost.Renderer = renderer; ghost.Pool = this;
            }
            ghost.transform.SetParent(null, false);
            ghost.gameObject.SetActive(true);
            return ghost;
        }

        void Return(Ghost ghost)
        {
            ghost.gameObject.SetActive(false);
            ghost.transform.SetParent(transform, false);
            idle.Push(ghost);
        }

        sealed class Ghost : MonoBehaviour
        {
            public MeshFilter Filter; public MeshRenderer Renderer; public VfxAfterimage Pool;
            Mesh _baked; // 구운 포즈를 담아 두는 전용 메시. 매번 새로 만들지 않고 덮어 굽는다
            Color _color; float _lifetime, _age;
            MaterialPropertyBlock _block;

            /// <summary>지금 포즈를 전용 메시에 덮어 굽는다.</summary>
            public void Bake(SkinnedMeshRenderer source)
            {
                if (_baked == null) _baked = new Mesh { name = "Afterimage Baked" };
                source.BakeMesh(_baked);
                Filter.sharedMesh = _baked;
            }

            public void Use(Mesh shared) => Filter.sharedMesh = shared;

            public void Begin(Material material, Color color, float lifetime)
            {
                Renderer.sharedMaterial = material;
                _color = color; _lifetime = lifetime; _age = 0f;
                Apply(1f);
            }

            void Apply(float k)
            {
                _block ??= new MaterialPropertyBlock();
                Color color = _color; color.a *= k;
                _block.SetColor(BaseColorId, color);
                Renderer.SetPropertyBlock(_block);
            }

            void Update()
            {
                _age += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(_age / Mathf.Max(_lifetime, 0.0001f));
                Apply(k);
                if (k > 0f) return;
                if (Pool != null) Pool.Return(this); else Destroy(gameObject);
            }

            void OnDestroy() { if (_baked != null) Destroy(_baked); }
        }
    }
}
