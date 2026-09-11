using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 카메라 셰이크 (VFX 제작계획 §5-3). <c>VfxCameraShake.Shake(강도, 시간)</c>만 부르면 되고, 컴포넌트는 Camera.main에
    /// 자동으로 붙는다. 카메라 리그가 매 LateUpdate에 위치를 다시 쓰는 경우와 고정 카메라 모두에서 동작한다:
    /// Update에서 지난 프레임의 오프셋을 걷어내고, 리그가 위치를 정한 뒤(LateUpdate 1000) 새 오프셋을 더한다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class VfxCameraShake : MonoBehaviour
    {
        public static VfxCameraShake Instance { get; private set; }
        [Tooltip("초당 흔들림 횟수")] public float Frequency = 25f;

        float _remaining, _total, _strength, _seed;
        Vector3 _applied;

        public static void Shake(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f) return;
            var instance = Instance;
            if (instance == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                instance = camera.GetComponent<VfxCameraShake>();
                if (instance == null) instance = camera.gameObject.AddComponent<VfxCameraShake>();
            }
            instance.Add(strength, duration);
        }

        void OnEnable() { Instance = this; _seed = Random.value * 100f; }
        void OnDisable() { RemoveApplied(); if (Instance == this) Instance = null; }

        void Add(float strength, float duration)
        {
            float current = _total > 0f ? _strength * Mathf.Clamp01(_remaining / _total) : 0f;
            _strength = Mathf.Max(current, strength);
            _total = _remaining = Mathf.Max(_remaining, duration);
        }

        void Update() => RemoveApplied();

        void LateUpdate()
        {
            RemoveApplied();
            if (_remaining <= 0f) return;
            _remaining -= Time.unscaledDeltaTime;
            float k = _strength * Mathf.Clamp01(_remaining / Mathf.Max(_total, 0.0001f));
            float t = Time.unscaledTime * Frequency;
            Vector3 offset = new Vector3(Mathf.PerlinNoise(_seed, t) * 2f - 1f, Mathf.PerlinNoise(_seed + 7.3f, t) * 2f - 1f, 0f) * k;
            _applied = transform.rotation * offset;
            transform.position += _applied;
        }

        void RemoveApplied()
        {
            if (_applied == Vector3.zero) return;
            transform.position -= _applied;
            _applied = Vector3.zero;
        }
    }
}
