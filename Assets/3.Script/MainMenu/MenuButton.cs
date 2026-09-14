using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonSelected : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    [SerializeField] private GameObject selected;

    private void Awake()
    {
        if (selected != null)
            selected.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        selected.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        selected.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected.SetActive(false);
    }
}