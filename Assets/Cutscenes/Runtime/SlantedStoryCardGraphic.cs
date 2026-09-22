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
        [SerializeField] bool preserveAspectFill;

        public Sprite Sprite
        {
            get => sprite;
            set { sprite = value; SetVerticesDirty(); SetMaterialDirty(); }
        }

        public bool PreserveAspectFill
        {
            get => preserveAspectFill;
            set { preserveAspectFill = value; SetVerticesDirty(); }
        }

        public override Texture mainTexture => sprite ? sprite.texture : Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector4 uv = sprite ? DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
            if (sprite && preserveAspectFill)
            {
                float spriteAspect = sprite.rect.width / sprite.rect.height;
                float targetAspect = r.width / r.height;
                if (spriteAspect > targetAspect)
                {
                    float visible = targetAspect / spriteAspect;
                    float center = (uv.x + uv.z) * .5f;
                    float half = (uv.z - uv.x) * visible * .5f;
                    uv.x = center - half; uv.z = center + half;
                }
                else
                {
                    float visible = spriteAspect / targetAspect;
                    float center = (uv.y + uv.w) * .5f;
                    float half = (uv.w - uv.y) * visible * .5f;
                    uv.y = center - half; uv.w = center + half;
                }
            }
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
