using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace SandGuard.Cutscenes
{
    /// <summary>스토리 선택 화면에서 사용하는 비스듬한 사각 카드입니다.</summary>
    public sealed class SlantedStoryCardGraphic : MaskableGraphic
    {
        [SerializeField] Sprite sprite;
        [SerializeField, Range(-120f, 120f)] float slant = 42f;

        public Sprite Sprite
        {
            get => sprite;
            set { sprite = value; SetVerticesDirty(); SetMaterialDirty(); }
        }

        public override Texture mainTexture => sprite ? sprite.texture : Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector4 uv = sprite ? DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
            float s = Mathf.Clamp(slant, -r.width * 0.3f, r.width * 0.3f);

            Add(vh, new Vector2(r.xMin + s, r.yMax), color, new Vector2(uv.x, uv.w));
            Add(vh, new Vector2(r.xMax + s, r.yMax), color, new Vector2(uv.z, uv.w));
            Add(vh, new Vector2(r.xMax - s, r.yMin), color, new Vector2(uv.z, uv.y));
            Add(vh, new Vector2(r.xMin - s, r.yMin), color, new Vector2(uv.x, uv.y));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        static void Add(VertexHelper vh, Vector2 position, Color color, Vector2 uv)
        {
            vh.AddVert(position, color, uv);
        }
    }
}
