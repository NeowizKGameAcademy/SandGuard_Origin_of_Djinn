using System.Collections;
using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// "짠!" 등장. 연막(PoofPrefab)이 먼저 자리를 덮고, 잠깐 뒤 대상이 이미 서 있는 채로 드러나며 스케일 펀치(1.25→1)와
    /// 재질 플래시가 들어간다. 드러나는 순간 완료 이펙트(RevealPrefab)와 약한 카메라 셰이크를 낸다.
    /// 렌더러가 있는 아무 오브젝트에나 붙이는 범용 컴포넌트다. 효과 크기는 대상 바운즈에 맞춘다.
    /// </summary>
    public sealed class VfxPopIn : MonoBehaviour
    {
        [Tooltip("먼저 덮는 연막. 예: VFX_Build_Poof")]
        public GameObject PoofPrefab;
        [Tooltip("드러나는 순간의 이펙트. 예: VFX_Build_Complete")]
        public GameObject RevealPrefab;
        [Min(0f), Tooltip("연막이 덮고 나서 대상이 드러나기까지")]
        public float RevealDelay = 0.2f;
        [Min(1f)] public float PunchScale = 1.25f;
        [Min(0.01f)] public float PunchDuration = 0.18f;
        public Color FlashColor = new Color(1f, 0.95f, 0.75f);
        [Min(0.01f)] public float FlashDuration = 0.22f;
        [Min(0f)] public float Shake = 0.08f;
        [Min(0f)] public float ShakeDuration = 0.15f;
        [Tooltip("이펙트 프리팹의 기준 크기(m). 대상 바운즈 대각선 / 이 값이 이펙트 배율이 된다")]
        [Min(0.1f)] public float ReferenceSize = 2.2f;
        public bool FitEffectsToBounds = true;
        [Min(0.5f)] public float EffectLifetime = 3f;
        public bool PlayOnEnable = true;
        public bool IsPlaying { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Vector3 target;
        bool targetCaptured;
        Coroutine routine;
        MaterialPropertyBlock block;

        void OnEnable() { if (PlayOnEnable) Play(); }
        void OnDisable() { if (routine != null) StopCoroutine(routine); Restore(); }

        public void Play()
        {
            if (routine != null) StopCoroutine(routine);
            if (!targetCaptured) { target = transform.localScale; targetCaptured = true; }
            routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            var renderers = GetComponentsInChildren<Renderer>(true);
            float fit = 1f;
            if (FitEffectsToBounds && renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                fit = Mathf.Max(0.3f, bounds.size.magnitude / ReferenceSize);
            }
            Spawn(PoofPrefab, fit);
            foreach (var renderer in renderers) renderer.enabled = false;
            transform.localScale = target;
            if (RevealDelay > 0f) yield return new WaitForSeconds(RevealDelay);

            foreach (var renderer in renderers) renderer.enabled = true;
            Spawn(RevealPrefab, fit);
            if (Shake > 0f) VfxCameraShake.Shake(Shake, ShakeDuration);
            block ??= new MaterialPropertyBlock();
            var baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
            float total = Mathf.Max(PunchDuration, FlashDuration);
            for (float elapsed = 0f; elapsed < total; elapsed += Time.deltaTime)
            {
                float punch = 1f - Mathf.Clamp01(elapsed / PunchDuration);
                transform.localScale = target * Mathf.LerpUnclamped(1f, PunchScale, punch * punch);
                float flash = 1f - Mathf.Clamp01(elapsed / FlashDuration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].GetPropertyBlock(block);
                    block.SetColor(BaseColorId, Color.Lerp(baseColors[i], FlashColor, flash));
                    renderers[i].SetPropertyBlock(block);
                }
                yield return null;
            }
            Restore();
            routine = null;
        }

        void Restore()
        {
            if (targetCaptured) transform.localScale = target;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            { if (renderer != null) { renderer.enabled = true; renderer.SetPropertyBlock(null); } }
            IsPlaying = false;
        }

        void Spawn(GameObject prefab, float fit)
        {
            if (prefab == null) return;
            var instance = PrefabPool.Spawn(prefab, transform.position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * fit;
            PrefabPool.Release(instance, EffectLifetime);
        }
    }
}
