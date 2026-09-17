using TMPro;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class WaveHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text waveValueText;
        [SerializeField] private GameObject remainingEnemyPanel;
        [SerializeField] private TMP_Text remainingEnemyText;
        public void SetWave(int wave) => waveValueText.text = Mathf.Max(0, wave).ToString("00");
        public void SetRemainingEnemies(int remaining)
        {
            if (remainingEnemyText != null) remainingEnemyText.text = Mathf.Max(0, remaining).ToString();
        }
        public void SetRemainingEnemiesVisible(bool visible)
        {
            if (remainingEnemyPanel != null && remainingEnemyPanel.activeSelf != visible)
                remainingEnemyPanel.SetActive(visible);
        }
    }
}
