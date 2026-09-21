using UnityEngine;
using UnityEngine.UI;

public class TowerHPBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, 0);

    private LegacyTowerStatus target;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;

        if (slider == null)
            slider = GetComponent<Slider>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetTarget(LegacyTowerStatus tower)
    {
        target = tower;

        slider.maxValue = target.maxHP;
        slider.value = target.curHP;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 worldPosition = target.transform.position + offset;
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

        bool isVisible =
            screenPosition.z > 0 &&
            screenPosition.x >= 0 &&
            screenPosition.x <= Screen.width &&
            screenPosition.y >= 0 &&
            screenPosition.y <= Screen.height;

        canvasGroup.alpha = isVisible ? 1f : 0f;

        if (!isVisible)
            return;

        transform.position = screenPosition;
        slider.value = target.curHP;
    }

    public void ClearTarget()
    {
        target = null;
        canvasGroup.alpha = 0f;
    }
}