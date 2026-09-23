using SandGuard.Facility;
using SandGuard.Player;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 건설 메뉴(FacilityBuildMenu) 소리를 메뉴 코드를 고치지 않고 낸다. 담당자가 건설 쪽을 통합 중이라 그 파일은 건드리지 않는다.
    ///   열림·닫힘: 공개 속성 IsOpen의 변화
    ///   수리 성공: 수리 모드에서 1을 눌러 LastRepairResult가 성공이면 그 받침대 위치에서 RepairCue(3D)
    ///   거부: 번호 키(PlayerInputReader.SlotSelected)가 눌린 프레임 끝에, 메뉴가 열린 채이고 그 시도의 결과(LastResult / LastRepairResult)가 실패면
    /// 메뉴와 입력기는 씬에서 찾는다(1초마다 다시 시도). 씬 루트에 하나 두면 된다.
    /// </summary>
    public sealed class SfxBuildMenuWatcher : MonoBehaviour
    {
        public SfxCue OpenCue, CloseCue, FailCue, RepairCue;

        public int Opens { get; private set; }
        public int Closes { get; private set; }
        public int Fails { get; private set; }
        public int Repairs { get; private set; }

        FacilityBuildMenu menu; PlayerInputReader input;
        bool wasOpen; float retryAt;
        int pressedIndex = -1; bool openAtPress;

        void OnDisable() { if (input != null) input.SlotSelected -= OnSlot; input = null; }

        void Update()
        {
            if ((menu == null || input == null) && Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 1f;
                if (menu == null) menu = FindFirstObjectByType<FacilityBuildMenu>();
                if (input == null) { input = FindFirstObjectByType<PlayerInputReader>(); if (input != null) input.SlotSelected += OnSlot; }
            }
            if (menu == null) return;
            bool open = menu.IsOpen;
            if (open && !wasOpen) { Opens++; Play(OpenCue); }
            else if (!open && wasOpen) { Closes++; Play(CloseCue); }
            wasOpen = open;
        }

        /// <summary>메뉴도 같은 이벤트로 Select를 부른다. 호출 순서에 기대지 않으려고 결과는 LateUpdate에서 본다.</summary>
        void OnSlot(int index) { pressedIndex = index; openAtPress = menu != null && menu.IsOpen; }

        void LateUpdate()
        {
            if (pressedIndex < 0) return;
            int index = pressedIndex; pressedIndex = -1;
            if (!openAtPress || menu == null || !menu.IsOpen) return; // 성공하면 메뉴가 닫힌다(닫힘 소리 + 건설 연출 소리)
            bool failed;
            if (menu.Mode == FacilityBuildMenu.MenuMode.Repair)
            {
                if (index != 0) return;
                failed = !menu.LastRepairResult.Succeeded;
                if (!failed && menu.Current != null) { Repairs++; if (RepairCue != null && Application.isPlaying) SfxPlayer.Play(RepairCue, menu.Current.transform.position); }
            }
            else
            {
                var catalog = menu.service != null ? menu.service.catalog : null;
                if (catalog == null || index >= catalog.facilities.Count) return; // 없는 번호는 메뉴가 무시한다
                failed = !menu.LastResult.Outcome.Succeeded;
            }
            if (failed) { Fails++; Play(FailCue); }
        }

        static void Play(SfxCue cue)
        {
            if (cue == null || !Application.isPlaying) return;
            SfxPlayer.Play2D(cue);
        }
    }
}
