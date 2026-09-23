using TMPro;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class WaveHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text waveValueText;
        [SerializeField] private GameObject remainingEnemyPanel;
        [SerializeField] private TMP_Text remainingEnemyText;
        [SerializeField] private TMP_Text remainingEnemyLabel;
        bool showingPreparation;
        int preparationSeconds = -1;
        public void SetWave(int wave) => waveValueText.text = Mathf.Max(0, wave).ToString("00");
        public void SetRemainingEnemies(int remaining)
        {
            showingPreparation = false;
            if (remainingEnemyLabel != null) remainingEnemyLabel.text = "남은 적";
            if (remainingEnemyText != null) remainingEnemyText.text = Mathf.Max(0, remaining).ToString();
        }
        public void SetPreparationTime(float? remaining)
        {
            int seconds = remaining.HasValue ? Mathf.CeilToInt(Mathf.Max(0f, remaining.Value)) : -1;
            SetRemainingEnemiesVisible(true);
            if (showingPreparation && preparationSeconds == seconds) return;
            showingPreparation = true;
            preparationSeconds = seconds;
            if (remainingEnemyLabel != null) remainingEnemyLabel.text = "다음 웨이브";
            if (remainingEnemyText != null)
                remainingEnemyText.text = seconds < 0 ? "대기" : $"{seconds / 60:00}:{seconds % 60:00}";
        }
        public void SetRemainingEnemiesVisible(bool visible)
        {
            if (remainingEnemyPanel != null && remainingEnemyPanel.activeSelf != visible)
                remainingEnemyPanel.SetActive(visible);
        }
    }
}
