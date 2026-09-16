using UnityEngine;
using UnityEngine.EventSystems;

namespace SandGuard.Audio
{
    /// <summary>uGUI 버튼에 붙이는 호버·클릭 소리. 다른 스크립트에 의존하지 않아 어느 씬의 Button에도 붙는다.</summary>
    public sealed class SfxUiButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISelectHandler, ISubmitHandler
    {
        public SfxCue HoverCue;
        public SfxCue ClickCue;

        public void OnPointerEnter(PointerEventData e) { if (HoverCue != null) SfxPlayer.Play2D(HoverCue); }
        public void OnSelect(BaseEventData e) { if (HoverCue != null) SfxPlayer.Play2D(HoverCue); }
        public void OnPointerClick(PointerEventData e) { if (ClickCue != null) SfxPlayer.Play2D(ClickCue); }
        public void OnSubmit(BaseEventData e) { if (ClickCue != null) SfxPlayer.Play2D(ClickCue); }
    }
}
