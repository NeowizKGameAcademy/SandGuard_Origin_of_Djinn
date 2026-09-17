using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>Attach only in the preview scene. Toggle Show Failure in the Inspector.</summary>
    [ExecuteAlways]
    public sealed class GameResultPreview : MonoBehaviour
    {
        [SerializeField] GameResultScreen screen;
        [SerializeField] bool showFailure;
        bool? appliedFailure;

        void OnEnable() { appliedFailure = null; }

        void Update()
        {
            if (appliedFailure == showFailure || !screen) return;
            screen.ShowPreview(showFailure);
            appliedFailure = showFailure;
        }
    }
}
