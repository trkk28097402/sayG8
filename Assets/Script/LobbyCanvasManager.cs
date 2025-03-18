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
        public string nextPageName; // 下一頁面的名稱
        [HideInInspector] public CanvasGroup canvasGroup; // 使用 CanvasGroup 控制可見性
        public PageInputHandler inputHandler; // 頁面的輸入處理器
    }

    [Header("Canvas Pages")]
    [SerializeField] private List<CanvasPage> canvasPages = new List<CanvasPage>();
    [SerializeField] private string initialPageName = "DeckSelectCanvas";

    [Header("Navigation Settings")]
    [SerializeField] private bool useEnterKeyNavigation = true;
    [SerializeField] private float inputCooldown = 0.3f;

    private CanvasPage currentActivePage;
    private float lastInputTime = 0f;

    private void Awake()
    {
        // 確保所有頁面都有 CanvasGroup 組件並初始化狀態
        InitializeAllPages();
    }

    private void InitializeAllPages()
    {
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null)
            {
                // 先確保各頁面的GameObject是激活的，但通過CanvasGroup來控制可見性
                page.canvasObject.SetActive(true);

                // 確保有 CanvasGroup 組件
                page.canvasGroup = page.canvasObject.GetComponent<CanvasGroup>();
                if (page.canvasGroup == null)
                {
                    page.canvasGroup = page.canvasObject.AddComponent<CanvasGroup>();
                }

                // 檢查或添加 PageInputHandler
                page.inputHandler = page.canvasObject.GetComponent<PageInputHandler>();

                // 初始時設為不可見和不可交互
                page.canvasGroup.alpha = 0f;
                page.canvasGroup.interactable = false;
                page.canvasGroup.blocksRaycasts = false;

                // 確保輸入處理器初始化為非活動狀態
                if (page.inputHandler != null)
                {
                    page.inputHandler.SetActive(false);
                }
            }
            else
            {
                Debug.LogError($"Canvas object is null for page {page.pageName}!");
            }
        }
    }

    private void Start()
    {
        // 顯示初始頁面
        ShowPage(initialPageName);
    }

    private void Update()
    {
        // 這裡只處理頁面切換功能，具體頁面的輸入由各自的 PageInputHandler 處理
        if (currentActivePage == null || !useEnterKeyNavigation)
            return;

        // 檢查冷卻時間
        if (Time.time - lastInputTime < inputCooldown)
            return;

        // 按 Enter 鍵切換到下一頁
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!string.IsNullOrEmpty(currentActivePage.nextPageName))
            {
                ShowPage(currentActivePage.nextPageName);
                lastInputTime = Time.time;
            }
        }
    }

    /// <summary>
    /// 顯示指定頁面並隱藏其他頁面
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

        // 如果當前頁面有輸入處理器，禁用它
        if (currentActivePage != null && currentActivePage.inputHandler != null)
        {
            currentActivePage.inputHandler.SetActive(false);
        }

        // 重要修改：先禁用所有頁面的交互，然後再激活目標頁面
        // 這可以防止在頁面切換過程中按鈕被誤觸發
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null && page.canvasGroup != null)
            {
                page.canvasGroup.interactable = false;
                page.canvasGroup.blocksRaycasts = false;
            }
        }

        // 等待一幀確保禁用生效
        StartCoroutine(ActivatePageAfterDelay(pageToShow));

        Debug.Log($"Showing page: {pageName}");
    }

    // 新增：延遲激活頁面，確保先前頁面的禁用完全生效
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

                // 使用 CanvasGroup 控制可見性而不是啟用/禁用GameObject
                page.canvasGroup.alpha = shouldBeActive ? 1f : 0f;
                page.canvasGroup.interactable = shouldBeActive;
                page.canvasGroup.blocksRaycasts = shouldBeActive;

                // 控制輸入處理
                if (page.inputHandler != null)
                {
                    page.inputHandler.SetActive(shouldBeActive);
                }
            }
        }

        // 更新當前活動頁面
        currentActivePage = pageToShow;

        // 在頁面切換後強制刷新 UI
        StartCoroutine(ForceRefreshAfterPageChange());
    }

    // 頁面切換後強制刷新 UI
    private IEnumerator ForceRefreshAfterPageChange()
    {
        // 等待一幀確保 UI 組件有時間更新
        yield return null;

        // 強制更新所有 Canvas
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = false;
            canvas.enabled = true;
        }

        // 強制更新 CanvasGroup
        if (currentActivePage != null && currentActivePage.canvasGroup != null)
        {
            // 觸發重新繪製
            currentActivePage.canvasGroup.alpha = 0.99f;
            yield return null;
            currentActivePage.canvasGroup.alpha = 1f;
        }

        // 強制更新 Canvas
        Canvas.ForceUpdateCanvases();

        // 通知當前頁面已被激活
        if (currentActivePage != null && currentActivePage.canvasObject != null)
        {
            // 檢查是否有 GameReadySystem 元件並通知它頁面已激活
            GameReadySystem gameReadySystem = currentActivePage.canvasObject.GetComponentInChildren<GameReadySystem>();
            if (gameReadySystem != null)
            {
                gameReadySystem.OnPageActivated();
            }

            // 檢查是否有 DeckSelector 元件並強制刷新
            DeckSelector deckSelector = currentActivePage.canvasObject.GetComponentInChildren<DeckSelector>();
            if (deckSelector != null && deckSelector.enabled)
            {
                // 延遲一幀後再調用 ForceRefreshUI 方法
                StartCoroutine(DelayedDeckSelectorRefresh(deckSelector));
            }
        }
    }

    private IEnumerator DelayedDeckSelectorRefresh(DeckSelector deckSelector)
    {
        yield return null;
        // 反射調用私有方法 ForceRefreshUI (如果無法直接調用，你可以考慮在 DeckSelector 中添加一個公共方法)
        System.Reflection.MethodInfo method = typeof(DeckSelector).GetMethod("ForceRefreshUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
        {
            method.Invoke(deckSelector, null);
        }
    }

    /// <summary>
    /// 顯示基於當前頁面的 nextPageName 的下一頁
    /// </summary>
    public void ShowNextPage()
    {
        if (currentActivePage != null && !string.IsNullOrEmpty(currentActivePage.nextPageName))
        {
            ShowPage(currentActivePage.nextPageName);
        }
    }

    /// <summary>
    /// 在運行時添加新的 Canvas 頁面
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
            nextPageName = nextPageName
        };

        // 確保有 CanvasGroup 組件和輸入處理器
        if (canvasObject != null)
        {
            // 確保物件處於激活狀態
            canvasObject.SetActive(true);

            // 獲取或添加 CanvasGroup
            newPage.canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            if (newPage.canvasGroup == null)
            {
                newPage.canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            }

            // 獲取輸入處理器
            newPage.inputHandler = canvasObject.GetComponent<PageInputHandler>();

            // 初始化為不可見
            newPage.canvasGroup.alpha = 0f;
            newPage.canvasGroup.interactable = false;
            newPage.canvasGroup.blocksRaycasts = false;

            // 初始化禁用輸入
            if (newPage.inputHandler != null)
            {
                newPage.inputHandler.SetActive(false);
            }
        }

        canvasPages.Add(newPage);

        Debug.Log($"Added new canvas page: {pageName}");
    }

    /// <summary>
    /// 更新現有 Canvas 頁面的下一頁
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
}

// 保持 PageInputHandler 不變
public class PageInputHandler : MonoBehaviour
{
    // 包含所有需要在此頁面激活的輸入處理腳本
    [SerializeField] private List<MonoBehaviour> inputHandlerScripts = new List<MonoBehaviour>();

    // 確定此頁面當前是否應該接收輸入
    private bool _isActive = false;

    public void SetActive(bool active)
    {
        if (_isActive == active)
            return;

        _isActive = active;

        // 激活或禁用所有輸入處理腳本
        foreach (var handler in inputHandlerScripts)
        {
            if (handler != null)
            {
                handler.enabled = active;
            }
        }

        // 如果激活，確保此物件是激活的，這樣 Update 方法會被調用
        this.enabled = active;
    }

    // 當物件被禁用時自動禁用所有輸入處理
    private void OnDisable()
    {
        if (_isActive)
        {
            SetActive(false);
        }
    }
}