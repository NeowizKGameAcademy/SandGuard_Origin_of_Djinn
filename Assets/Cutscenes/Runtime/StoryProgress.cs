using UnityEngine;

namespace SandGuard.Cutscenes
{
    /// <summary>스토리 최초 재생 기록과 메뉴에서 요청한 재감상을 관리합니다.</summary>
    public static class StoryProgress
    {
        const string SeenPrefix = "SandGuard.Story.Seen.";
        static string replayStoryId;

        public static bool IsSeen(string storyId)
        {
            return !string.IsNullOrWhiteSpace(storyId) && PlayerPrefs.GetInt(SeenPrefix + storyId, 0) == 1;
        }

        public static void MarkSeen(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId)) return;
            PlayerPrefs.SetInt(SeenPrefix + storyId, 1);
            PlayerPrefs.Save();
        }

        public static void RequestReplay(string storyId)
        {
            replayStoryId = storyId;
        }

        public static bool ConsumeReplayRequest(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId) || replayStoryId != storyId) return false;
            replayStoryId = null;
            return true;
        }

        public static void ResetSeen(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId)) return;
            PlayerPrefs.DeleteKey(SeenPrefix + storyId);
            PlayerPrefs.Save();
        }

        public static void ResetAllSeen()
        {
            ResetSeen("Prologue");
            ResetSeen("Intro");
            ResetSeen("Ending");
            replayStoryId = null;
        }
    }
}
