using TMPro;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    /// <summary>스킬 아이콘 위의 작은 숫자가 어떤 배경에서도 읽히도록 TMP 외곽선을 적용합니다.</summary>
    public static class CooldownTextStyle
    {
        const float OutlineWidth = 0.24f;
        static readonly Color32 OutlineColor = new(8, 4, 2, 255);

        public static void Apply(TMP_Text text)
        {
            if (!text) return;
            text.fontStyle |= FontStyles.Bold;
            text.outlineColor = OutlineColor;
            text.outlineWidth = OutlineWidth;
        }
    }
}
