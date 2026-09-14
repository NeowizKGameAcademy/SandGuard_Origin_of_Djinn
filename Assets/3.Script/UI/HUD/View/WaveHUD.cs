using TMPro;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class WaveHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text waveValueText;
        public void SetWave(int wave) => waveValueText.text = Mathf.Max(0, wave).ToString("00");
    }
}
