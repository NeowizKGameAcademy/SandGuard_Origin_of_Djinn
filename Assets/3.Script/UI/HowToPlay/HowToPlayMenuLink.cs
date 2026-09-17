using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class HowToPlayMenuLink : MonoBehaviour
{
    [SerializeField] private HowToPlayPopup popup;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Open);
    }

    private void OnDestroy()
    {
        if (button) button.onClick.RemoveListener(Open);
    }

    private void Open()
    {
        if (popup) popup.Open();
    }
}
