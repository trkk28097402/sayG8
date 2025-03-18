using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasManager : MonoBehaviour
{
    [System.Serializable]
    public class CanvasPage
    {
        public string pageName;
        public GameObject canvasObject;
        public string nextPageName; // 下一頁名稱
        [Tooltip("勾選此項，使此頁面可以使用 Enter 鍵跳轉到下一頁")]
        public bool handleEnterKey = false; // 新增：是否處理 Enter 鍵
        [HideInInspector] public CanvasGroup canvasGroup; // 使用 CanvasGroup 控制可見性
    }

    [Header("Canvas Pages")]
    [SerializeField] private List<CanvasPage> canvasPages = new List<CanvasPage>();
    [SerializeField] private string initialPageName = "DeckSelectCanvas";

    [Header("Enter Key Settings")]
    [Tooltip("按鍵冷卻時間，避免連續觸發")]
    [SerializeField] private float inputCooldown = 0.3f;

    private CanvasPage currentActivePage;
    private float lastInputTime = 0f;

    private void Awake()
    {
        // 初始化所有頁面
        InitializeAllPages();
    }

    private void Update()
    {
        // 檢查當前頁面是否需要處理 Enter 鍵
        if (currentActivePage != null && currentActivePage.handleEnterKey)
        {
            // 檢查冷卻時間
            if (Time.time - lastInputTime < inputCooldown)
                return;

            // 檢查 DeckSelector 是否存在且活動
            DeckSelector deckSelector = currentActivePage.canvasObject.GetComponentInChildren<DeckSelector>();
            if (deckSelector != null && deckSelector.gameObject.activeInHierarchy)
                return;

            // 處理 Enter 鍵
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Debug.Log($"頁面 '{currentActivePage.pageName}' 的 Enter 鍵輸入已處理");
                ShowNextPage();
                lastInputTime = Time.time;
            }
        }
    }

    private void InitializeAllPages()
    {
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null)
            {
                // 確保每個頁面的 GameObject 是活動的，以便我們可以設置 CanvasGroup
                page.canvasObject.SetActive(true);

                // 確保有 CanvasGroup 組件
                page.canvasGroup = page.canvasObject.GetComponent<CanvasGroup>();
                if (page.canvasGroup == null)
                {
                    page.canvasGroup = page.canvasObject.AddComponent<CanvasGroup>();
                }

                // 設置每個頁面的初始值（不可見和不可交互）
                page.canvasGroup.alpha = 0f;
                page.canvasGroup.interactable = false;
                page.canvasGroup.blocksRaycasts = false;

                // 尋找所有導航按鈕並設置監聽器
                SetupButtonListeners(page);
            }
            else
            {
                Debug.LogError($"Canvas object is null for page {page.pageName}!");
            }
        }
    }

    // 新方法，設置按鈕點擊監聽器
    private void SetupButtonListeners(CanvasPage page)
    {
        // 在此頁面中查找所有按鈕
        Button[] buttons = page.canvasObject.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            // 檢查此按鈕是否有 NavigationButton 組件
            NavigationButton navButton = button.GetComponent<NavigationButton>();
            if (navButton != null && !string.IsNullOrEmpty(navButton.targetPageName))
            {
                // 添加監聽器到按鈕
                button.onClick.AddListener(() => {
                    ShowPage(navButton.targetPageName);
                });
            }
        }
    }

    private void Start()
    {
        // 顯示初始頁面
        ShowPage(initialPageName);
    }

    /// <summary>
    /// 顯示指定的頁面並隱藏其他頁面
    /// </summary>
    /// <param name="pageName">要顯示的頁面名稱</param>
    public void ShowPage(string pageName)
    {
        if (string.IsNullOrEmpty(pageName))
            return;

        bool foundPage = false;

        // 尋找要顯示的頁面
        CanvasPage pageToShow = null;
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                pageToShow = page;
                foundPage = true;
                break;
            }
        }

        if (!foundPage)
        {
            Debug.LogWarning($"Page '{pageName}' not found in canvas pages list!");
            return;
        }

        // 首先禁用所有頁面的交互
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null && page.canvasGroup != null)
            {
                page.canvasGroup.interactable = false;
                page.canvasGroup.blocksRaycasts = false;
            }
        }

        // 使用協程來激活頁面
        StartCoroutine(ActivatePageAfterDelay(pageToShow));

        Debug.Log($"Showing page: {pageName}");
    }

    private IEnumerator ActivatePageAfterDelay(CanvasPage pageToShow)
    {
        // 等待一幀
        yield return null;

        // 設置所有頁面的可見性
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null && page.canvasGroup != null)
            {
                bool shouldBeActive = (page == pageToShow);

                // 使用 CanvasGroup 控制可見性
                page.canvasGroup.alpha = shouldBeActive ? 1f : 0f;
                page.canvasGroup.interactable = shouldBeActive;
                page.canvasGroup.blocksRaycasts = shouldBeActive;
            }
        }

        // 更新當前活動頁面
        currentActivePage = pageToShow;

        // 頁面更改後刷新UI
        StartCoroutine(ForceRefreshAfterPageChange());
    }

    private IEnumerator ForceRefreshAfterPageChange()
    {
        // 等待一幀，確保UI有時間更新
        yield return null;

        // 刷新所有Canvas
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = false;
            canvas.enabled = true;
        }

        // 刷新CanvasGroup
        if (currentActivePage != null && currentActivePage.canvasGroup != null)
        {
            // 觸發刷新
            currentActivePage.canvasGroup.alpha = 0.99f;
            yield return null;
            currentActivePage.canvasGroup.alpha = 1f;
        }

        // 強制Canvas更新
        Canvas.ForceUpdateCanvases();

        // 通知系統，頁面現在已激活
        if (currentActivePage != null && currentActivePage.canvasObject != null)
        {
            // 檢查是否有GameReadySystem並通知它
            GameReadySystem gameReadySystem = currentActivePage.canvasObject.GetComponentInChildren<GameReadySystem>();
            if (gameReadySystem != null)
            {
                gameReadySystem.OnPageActivated();
            }

            // 檢查是否有DeckSelector並刷新它
            DeckSelector deckSelector = currentActivePage.canvasObject.GetComponentInChildren<DeckSelector>();
            if (deckSelector != null && deckSelector.enabled)
            {
                StartCoroutine(DelayedDeckSelectorRefresh(deckSelector));
            }
        }
    }

    private IEnumerator DelayedDeckSelectorRefresh(DeckSelector deckSelector)
    {
        yield return null;
        // 使用反射調用ForceRefreshUI方法
        System.Reflection.MethodInfo method = typeof(DeckSelector).GetMethod("ForceRefreshUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
        {
            method.Invoke(deckSelector, null);
        }
    }

    /// <summary>
    /// 顯示當前頁面中定義的下一頁
    /// </summary>
    public void ShowNextPage()
    {
        if (currentActivePage != null && !string.IsNullOrEmpty(currentActivePage.nextPageName))
        {
            Debug.Log($"CanvasManager: 從 '{currentActivePage.pageName}' 切換到 '{currentActivePage.nextPageName}'");
            ShowPage(currentActivePage.nextPageName);
        }
        else
        {
            Debug.LogWarning($"CanvasManager: 無法切換到下一頁。當前頁面: '{(currentActivePage?.pageName ?? "null")}', 下一頁名稱: '{(currentActivePage?.nextPageName ?? "null")}'");
        }
    }

    /// <summary>
    /// 在運行時添加新的Canvas頁面
    /// </summary>
    public void AddCanvasPage(string pageName, GameObject canvasObject, string nextPageName = "")
    {
        // 檢查頁面是否已存在
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                Debug.LogWarning($"Canvas page '{pageName}' already exists!");
                return;
            }
        }

        // 創建並添加新頁面
        CanvasPage newPage = new CanvasPage
        {
            pageName = pageName,
            canvasObject = canvasObject,
            nextPageName = nextPageName,
            handleEnterKey = false // 默認不處理Enter鍵
        };

        // 設置CanvasGroup
        if (canvasObject != null)
        {
            // 確保對象處於活動狀態
            canvasObject.SetActive(true);

            // 獲取或添加CanvasGroup
            newPage.canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            if (newPage.canvasGroup == null)
            {
                newPage.canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            }

            // 初始化為不可見
            newPage.canvasGroup.alpha = 0f;
            newPage.canvasGroup.interactable = false;
            newPage.canvasGroup.blocksRaycasts = false;

            // 為新頁面設置按鈕監聽器
            SetupButtonListeners(newPage);
        }

        canvasPages.Add(newPage);

        Debug.Log($"Added new canvas page: {pageName}");
    }

    /// <summary>
    /// 更新Canvas頁面的下一頁
    /// </summary>
    public void SetNextPage(string pageName, string nextPageName)
    {
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                page.nextPageName = nextPageName;
                return;
            }
        }

        Debug.LogWarning($"Canvas page '{pageName}' not found!");
    }

    /// <summary>
    /// 啟用或禁用頁面的Enter鍵處理
    /// </summary>
    public void SetEnterKeyHandling(string pageName, bool enableEnterKey)
    {
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                page.handleEnterKey = enableEnterKey;
                Debug.Log($"Set Enter key handling for page '{pageName}' to {enableEnterKey}");
                return;
            }
        }

        Debug.LogWarning($"Canvas page '{pageName}' not found for Enter key setting!");
    }
}