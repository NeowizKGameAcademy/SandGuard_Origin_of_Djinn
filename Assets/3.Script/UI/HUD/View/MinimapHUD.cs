using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class MinimapHUD : MonoBehaviour
    {
        [SerializeField] private RawImage minimapRawImage;
        [SerializeField] private RectTransform markerRoot;
        public RectTransform MarkerRoot => markerRoot;
        public void SetTexture(Texture texture) { minimapRawImage.texture = texture; minimapRawImage.enabled = texture != null; }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
