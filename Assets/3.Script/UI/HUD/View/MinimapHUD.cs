using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class MinimapHUD : MonoBehaviour
    {
        [SerializeField] private RawImage minimapRawImage;
        [SerializeField] private RectTransform markerRoot;
        private Color placeholderColor = new Color(.25f, .18f, .1f, 1f);
        private bool placeholderCached;

        public RectTransform MarkerRoot => markerRoot;

        /// <summary>지도 텍스처를 건다. 텍스처가 없으면 프리팹의 갈색 빈 판으로 돌아간다.</summary>
        public void SetTexture(Texture texture)
        {
            if (!placeholderCached) { placeholderColor = minimapRawImage.color; placeholderCached = true; }
            minimapRawImage.texture = texture;
            minimapRawImage.color = texture != null ? Color.white : placeholderColor;
            minimapRawImage.enabled = true;
        }

        /// <summary>마커 이미지를 지도 위에 만든다. 위치는 호출한 쪽이 anchoredPosition으로 옮긴다(지도 중심이 0,0).</summary>
        public RectTransform AddMarker(string name, Sprite sprite, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(markerRoot, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; image.preserveAspect = true;
            return rect;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
