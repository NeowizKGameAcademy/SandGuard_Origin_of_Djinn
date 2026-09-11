using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 피해·사망 알림을 이펙트로 바꾼다 (VFX 제작계획 #8 피격 플래시, #23 사망). IDamageEvents가 있는 오브젝트에
    /// 붙이면 Damaged마다 피격 프리팹·흰색 플래시·움찔·화면 효과·카메라 셰이크를, ILifeState.Died에 사망 프리팹을 낸다.
    /// 적, 플레이어, 벽, 코어 모두 같은 컴포넌트를 쓰고 프리팹만 다르게 넣는다. 게임 코드에 대한 의존은 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxHitReaction : MonoBehaviour
    {
        [Header("피격")]
        [Tooltip("적중 위치에 스폰. 루트의 +Z가 맞은 표면의 바깥쪽을 향한다")]
        public GameObject HitPrefab;
        public float HitLifetime = 2f;
        public bool OrientToHit = true;

        [Header("흰색 플래시 (URP _BaseColor를 잠깐 밝힌다)")]
        [Tooltip("비우면 자식 MeshRenderer/SkinnedMeshRenderer를 전부 쓴다")]
        public Renderer[] FlashRenderers;
        public Color FlashColor = Color.white;
        public float FlashIntensity = 2.5f;
        public float FlashDuration = 0.1f;

        [Header("움찔 (스케일 펀치)")]
        public Transform FlinchTarget;
        [Range(0.5f, 1f)] public float FlinchScale = 0.9f;
        public float FlinchDuration = 0.12f;

        [Header("화면·카메라 (플레이어용)")]
        [Tooltip("위치와 무관하게 그대로 스폰한다 (오버레이 캔버스 등)")]
        public GameObject ScreenPrefab;
        public float ScreenLifetime = 1f;
        [Tooltip("0이면 흔들지 않는다. 0.05 약함, 0.15 강함")]
        public float CameraShake = 0f;
        public float CameraShakeDuration = 0.2f;

        [Header("사망")]
        [Tooltip("발밑(경계 상자의 바닥 중심)에 스폰")]
        public GameObject DeathPrefab;
        public float DeathLifetime = 4f;
        [Tooltip("사망 프리팹에 VfxVolume이 있으면 이 오브젝트의 크기에 맞춘다 (벽 파괴)")]
        public bool FitDeathToBounds;
        [Tooltip("사망 프리팹에 VfxTint가 있으면 첫 렌더러의 색으로 물들인다")]
        public bool TintDeathWithRenderer = true;
        [Tooltip("사망 프리팹의 VfxParticleAttractor가 향할 대상 (경험치 흡수)")]
        public Transform AttractTarget;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        IDamageEvents _events;
        ILifeState _life;
        ICombatTarget _target;
        MaterialPropertyBlock _block;
        Coroutine _flash, _flinch;
        Vector3 _flinchBase;

        void Awake()
        {
            _events = GetComponent<IDamageEvents>();
            _life = GetComponent<ILifeState>();
            _target = GetComponent<ICombatTarget>();
            if (FlashRenderers == null || FlashRenderers.Length == 0) FlashRenderers = CollectRenderers();
            if (FlinchTarget != null) _flinchBase = FlinchTarget.localScale;
        }

        void OnEnable()
        {
            if (_events != null) _events.Damaged += OnDamaged;
            if (_life != null) _life.Died += OnDied;
        }

        void OnDisable()
        {
            if (_events != null) _events.Damaged -= OnDamaged;
            if (_life != null) _life.Died -= OnDied;
        }

        void OnDamaged(DamageAppliedInfo info)
        {
            Vector3 fallback = _target != null ? _target.HitPosition : transform.position + Vector3.up;
            Vector3 point = info.Damage.HitPosition ?? fallback;
            Vector3 direction = info.Damage.HitDirection ?? (point - transform.position);
            PlayHit(point, direction);
        }

        /// <summary>피격 연출을 직접 재생한다. direction은 공격이 날아온 방향이다.</summary>
        public void PlayHit(Vector3 point, Vector3 direction)
        {
            if (HitPrefab != null)
            {
                Quaternion rotation = OrientToHit && direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-direction.normalized) : Quaternion.identity;
                PrefabPool.Release(PrefabPool.Spawn(HitPrefab, point, rotation), HitLifetime);
            }
            if (FlashDuration > 0f && FlashRenderers != null && FlashRenderers.Length > 0)
            {
                if (_flash != null) StopCoroutine(_flash);
                _flash = StartCoroutine(Flash());
            }
            if (FlinchTarget != null && FlinchDuration > 0f)
            {
                if (_flinch != null) { StopCoroutine(_flinch); FlinchTarget.localScale = _flinchBase; }
                _flinch = StartCoroutine(Flinch());
            }
            if (ScreenPrefab != null)
                PrefabPool.Release(PrefabPool.Spawn(ScreenPrefab, ScreenPrefab.transform.position, ScreenPrefab.transform.rotation), ScreenLifetime);
            if (CameraShake > 0f) VfxCameraShake.Shake(CameraShake, CameraShakeDuration);
        }

        void OnDied(DeathInfo info) => PlayDeath();

        /// <summary>사망 연출을 직접 재생한다.</summary>
        public void PlayDeath()
        {
            if (DeathPrefab == null) return;
            Bounds bounds = ComputeBounds();
            Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            GameObject instance = PrefabPool.Spawn(DeathPrefab, feet, Quaternion.identity);
            if (FitDeathToBounds)
            {
                var volume = instance.GetComponent<VfxVolume>();
                if (volume != null) volume.Fit(bounds);
            }
            if (TintDeathWithRenderer)
            {
                var tint = instance.GetComponent<VfxTint>();
                if (tint != null && TryGetRendererColor(out Color color)) tint.Apply(color);
            }
            // 재사용된 개체가 지난 대상을 물고 있지 않도록 비어 있어도 덮어쓴다.
            foreach (var attractor in instance.GetComponentsInChildren<VfxParticleAttractor>()) attractor.Target = AttractTarget;
            PrefabPool.Release(instance, DeathLifetime);
        }

        IEnumerator Flash()
        {
            _block ??= new MaterialPropertyBlock();
            Color color = FlashColor * FlashIntensity;
            color.a = 1f;
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            foreach (var renderer in FlashRenderers) if (renderer != null) renderer.SetPropertyBlock(_block);
            yield return new WaitForSeconds(FlashDuration);
            _block.Clear();
            foreach (var renderer in FlashRenderers) if (renderer != null) renderer.SetPropertyBlock(_block);
            _flash = null;
        }

        IEnumerator Flinch()
        {
            float half = FlinchDuration * 0.5f;
            for (float t = 0f; t < FlinchDuration && FlinchTarget != null; t += Time.deltaTime)
            {
                float k = t < half ? t / half : 1f - (t - half) / half;
                FlinchTarget.localScale = _flinchBase * Mathf.Lerp(1f, FlinchScale, k);
                yield return null;
            }
            if (FlinchTarget != null) FlinchTarget.localScale = _flinchBase;
            _flinch = null;
        }

        Renderer[] CollectRenderers()
        {
            var list = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) list.Add(renderer);
            return list.ToArray();
        }

        bool TryGetRendererColor(out Color color)
        {
            color = Color.white;
            if (FlashRenderers == null) return false;
            foreach (var renderer in FlashRenderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var material = renderer.sharedMaterial;
                if (material.HasProperty(BaseColorId)) { color = material.GetColor(BaseColorId); return true; }
                if (material.HasProperty(ColorId)) { color = material.GetColor(ColorId); return true; }
            }
            return false;
        }

        /// <summary>비활성 상태에서도 쓸 수 있도록 메시와 변환으로 월드 경계를 계산한다.</summary>
        Bounds ComputeBounds()
        {
            bool any = false;
            Bounds result = new Bounds(transform.position, Vector3.zero);
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<ParticleSystem>() != null) continue;
                Encapsulate(ref result, ref any, filter.sharedMesh.bounds, filter.transform);
            }
            foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Encapsulate(ref result, ref any, skinned.localBounds, skinned.transform);
            if (!any)
            {
                foreach (var collider in GetComponentsInChildren<Collider>(true))
                {
                    if (!any) { result = collider.bounds; any = true; }
                    else result.Encapsulate(collider.bounds);
                }
            }
            if (!any) result = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
            return result;
        }

        static void Encapsulate(ref Bounds result, ref bool any, Bounds local, Transform space)
        {
            Vector3 min = local.min, max = local.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = space.TransformPoint(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
                if (!any) { result = new Bounds(corner, Vector3.zero); any = true; }
                else result.Encapsulate(corner);
            }
        }
    }
}
