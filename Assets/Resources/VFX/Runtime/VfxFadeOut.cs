using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>CanvasGroup 알파를 잠깐 유지했다가 0으로 내리고 오브젝트를 없앤다. 화면 피격 비네트에 쓴다. 일시정지 중에도 사라진다.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class VfxFadeOut : MonoBehaviour
    {
        public float Hold = 0.05f;
        public float Duration = 0.3f;
        [Range(0f, 1f)] public float StartAlpha = 1f;
        public bool DestroyWhenDone = true;

        CanvasGroup _group;
        float _elapsed;

        void Awake() { _group = GetComponent<CanvasGroup>(); _group.alpha = StartAlpha; }

        void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float alpha = _elapsed < Hold ? StartAlpha : StartAlpha * (1f - Mathf.Clamp01((_elapsed - Hold) / Mathf.Max(Duration, 0.0001f)));
            _group.alpha = alpha;
            if (alpha <= 0f && DestroyWhenDone) Destroy(gameObject);
        }
    }
}
