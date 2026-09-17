using UnityEngine;
using UnityEngine.UI;

public sealed class HowToPlayPopup : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    private int pageIndex;

    private void Awake()
    {
        previousButton.onClick.AddListener(PreviousPage);
        nextButton.onClick.AddListener(NextPage);
        closeButton.onClick.AddListener(Close);
        ShowPage(0);
    }

    private void OnDestroy()
    {
        previousButton.onClick.RemoveListener(PreviousPage);
        nextButton.onClick.RemoveListener(NextPage);
        closeButton.onClick.RemoveListener(Close);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousPage();
        else if (Input.GetKeyDown(KeyCode.RightArrow)) NextPage();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowPage(0);
    }

    public void Close() => gameObject.SetActive(false);
    public void PreviousPage() => ShowPage(pageIndex - 1);
    public void NextPage() => ShowPage(pageIndex + 1);

    private void ShowPage(int index)
    {
        if (pages == null || pages.Length == 0) return;
        pageIndex = Mathf.Clamp(index, 0, pages.Length - 1);
        for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == pageIndex);
        previousButton.interactable = pageIndex > 0;
        nextButton.interactable = pageIndex < pages.Length - 1;
    }
}
