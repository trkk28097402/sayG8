using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using System.Collections;

// 修改 DeckSelector 以支援按鈕導航和 Enter 鍵確認
public class DeckSelector : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button confirmButton; // 確認按鈕
    [SerializeField] private Button deckDescriptOpenButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button deckDescriptCloseButton;
    [SerializeField] private GameObject deckDescriptPop;
    [SerializeField] private GameObject panelPop;
    [SerializeField] private TextMeshProUGUI deckNameText;
    [SerializeField] private TextMeshProUGUI deckDescriptionText;
    [SerializeField] private Image deckPreviewImage;
    AudioManagerLobby audioManagerLobby;

    [Header("Input Settings")]
    [SerializeField] private float keyInputCooldown = 0.3f; // 按鍵冷卻時間
    private float lastKeyInputTime = 0f;

    [Header("Selection Visual Settings")]
    [SerializeField] private Color selectedTextColor = new Color(0.1f, 0.6f, 1f, 1f); // 選中時的文字顏色（藍色）
    [SerializeField] private Color normalTextColor = Color.black; // 正常狀態的文字顏色
    [SerializeField] private Color selectedButtonColor = new Color(1f, 0.92f, 0.4f, 1f); // 選中時的按鈕顏色

    private int currentDeckIndex = 0;
    private int currentButtonIndex = 0; // 目前選中的按鈕索引
    private List<Button> navigationButtons = new List<Button>(); // 按鈕導航列表
    private List<GameDeckData> availableDecks = new List<GameDeckData>();
    private Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>();
    private NetworkRunner runner;
    private bool isInitialized = false;
    private bool isDescriptionOpen = false; // 追蹤描述視窗是否開啟
    private Dictionary<Button, Coroutine> selectionEffects = new Dictionary<Button, Coroutine>();
    // 儲存按鈕原始文字顏色
    private Dictionary<Button, Color> originalTextColors = new Dictionary<Button, Color>();

    private void Awake()
    {
        audioManagerLobby = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManagerLobby>();
    }

    // 當此頁面啟用時，檢查輸入
    private void Update()
    {
        // 只有在初始化後才處理輸入
        if (!isInitialized)
            return;

        // 檢查冷卻時間
        if (Time.time - lastKeyInputTime < keyInputCooldown)
            return;

        // 在描述窗口開啟時，只處理關閉描述窗口的輸入
        /*
        if (isDescriptionOpen)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseDescriptPop();
                lastKeyInputTime = Time.time;
            }
            return;
        }
        */

        // 左箭頭鍵或 A 鍵 - 移動到前一個按鈕
        if ((Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) && (isDescriptionOpen == false))
        {
            print("------------------------------press A-------------------------");
            NavigateButtons(-1);
            lastKeyInputTime = Time.time;
        }
        // 右箭頭鍵或 D 鍵 - 移動到下一個按鈕
        else if ((Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) && (isDescriptionOpen == false))
        {
            print("------------------------------press D-------------------------");
            NavigateButtons(1);
            lastKeyInputTime = Time.time;
        }
        // Enter 鍵 - 按下當前選中的按鈕
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            print("------------------------------press ENTER-------------------------");
            PressSelectedButton();
            lastKeyInputTime = Time.time;
        }
        print("-----------------------------------"+currentButtonIndex+"--------------------------------------");
    }

    // 在按鈕間導航
    private void NavigateButtons(int direction)
    {
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);

        // 取消當前按鈕的視覺選中效果
        SetButtonSelected(navigationButtons[currentButtonIndex], false);

        // 計算新的按鈕索引
        currentButtonIndex += direction;

        // 確保索引在有效範圍內
        if (currentButtonIndex >= navigationButtons.Count - 1)
        {
            currentButtonIndex = 0;
        }
        else if (currentButtonIndex < 0)
        {
            currentButtonIndex = navigationButtons.Count - 2;
        }

        // 設置新按鈕的視覺選中效果
        SetButtonSelected(navigationButtons[currentButtonIndex], true);
    }

    // 按下當前選中的按鈕
    private void PressSelectedButton()
    {
        if (currentButtonIndex >= 0 && currentButtonIndex < navigationButtons.Count)
        {
            Button selectedButton = navigationButtons[currentButtonIndex];

            // 模擬按鈕點擊
            if (selectedButton.interactable)
            {
                selectedButton.onClick.Invoke();
            }
        }
    }

    // 設置按鈕的視覺選中狀態（增加文字變色效果）
    private void SetButtonSelected(Button button, bool isSelected)
    {
        if (button == null)
            return;

        // 1. 按鈕脈動效果
        if (isSelected)
        {
            // 啟動閃爍效果
            if (!selectionEffects.ContainsKey(button))
            {
                selectionEffects[button] = StartCoroutine(PulseEffect(button));
            }
        }
        else
        {
            // 停止閃爍效果
            if (selectionEffects.ContainsKey(button))
            {
                StopCoroutine(selectionEffects[button]);
                selectionEffects.Remove(button);

                // 重置按鈕顏色
                Image img = button.GetComponent<Image>();
                if (img != null)
                {
                    img.color = Color.white;
                }
            }
        }

        // 2. 文字顏色變化
        // 尋找按鈕中的所有文字元件
        Text[] texts = button.GetComponentsInChildren<Text>(true);
        TextMeshProUGUI[] tmpTexts = button.GetComponentsInChildren<TextMeshProUGUI>(true);

        // 設置 Unity UI Text 顏色
        foreach (Text text in texts)
        {
            if (!originalTextColors.ContainsKey(button) && text != null)
            {
                originalTextColors[button] = text.color;
            }

            if (isSelected)
            {
                text.color = selectedTextColor;
                // 可選：設置粗體
                text.fontStyle = FontStyle.Bold;
            }
            else
            {
                text.color = originalTextColors.ContainsKey(button) ? originalTextColors[button] : normalTextColor;
                text.fontStyle = FontStyle.Normal;
            }
        }

        // 設置 TextMeshPro 文字顏色
        foreach (TextMeshProUGUI tmpText in tmpTexts)
        {
            if (!originalTextColors.ContainsKey(button) && tmpText != null)
            {
                originalTextColors[button] = tmpText.color;
            }

            if (isSelected)
            {
                tmpText.color = selectedTextColor;
                // 可選：設置粗體
                tmpText.fontStyle = TMPro.FontStyles.Bold;
            }
            else
            {
                tmpText.color = originalTextColors.ContainsKey(button) ? originalTextColors[button] : normalTextColor;
                tmpText.fontStyle = TMPro.FontStyles.Normal;
            }
        }
    }

    private IEnumerator PulseEffect(Button button)
    {
        Image img = button.GetComponent<Image>();
        if (img == null) yield break;

        float time = 0;

        while (true)
        {
            // 在白色和亮黃色之間脈動
            float pulse = (Mathf.Sin(time * 3f) + 1f) / 2f;
            img.color = Color.Lerp(Color.white, selectedButtonColor, pulse);

            time += Time.deltaTime;
            yield return null;
        }
    }

    public void Wait_Runner_Spawned()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
            return;
        }
        Debug.Log($"Runner state: {runner.State}");

        SetupButtons();
        LoadDeckData();

        isInitialized = true;

        // 確保在初始化後更新顯示
        StartCoroutine(DelayedUpdateDisplay());
    }

    // 延遲更新顯示，確保 Canvas 有足夠時間初始化
    private IEnumerator DelayedUpdateDisplay()
    {
        yield return new WaitForEndOfFrame();
        UpdateDeckDisplay();

        // 再次更新一次，確保 UI 元素完全更新
        yield return new WaitForEndOfFrame();
        ForceRefreshUI();

        // 初始化時選中第一個按鈕
        if (navigationButtons.Count > 0)
        {
            currentButtonIndex = 0;
            SetButtonSelected(navigationButtons[currentButtonIndex], true);
        }
    }

    private void OnEnable()
    {
        // 如果已經初始化，則在啟用時更新顯示
        if (isInitialized && availableDecks.Count > 0)
        {
            StartCoroutine(DelayedUpdateDisplay());
        }
    }

    private void SetupButtons()
    {
        // 清空並重新建立導航按鈕列表（按照畫面順序: 左鍵, 確認鍵, 卡組描述, 右鍵）
        navigationButtons.Clear();
        originalTextColors.Clear(); // 清空原始顏色緩存

        if (previousButton != null)
        {
            navigationButtons.Add(previousButton);
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(() => ChangeDeck(-1));

            // 存儲按鈕文字的原始顏色
            StoreOriginalTextColors(previousButton);
        }

        if (confirmButton != null)
        {
            navigationButtons.Add(confirmButton);
            confirmButton.onClick.RemoveAllListeners();
            // 確認按鈕現在會呼叫確認卡組並切換到下一頁
            confirmButton.onClick.AddListener(() => ConfirmDeckSelection());

            // 存儲按鈕文字的原始顏色
            StoreOriginalTextColors(confirmButton);
        }

        if (deckDescriptOpenButton != null)
        {
            navigationButtons.Add(deckDescriptOpenButton);
            deckDescriptOpenButton.onClick.RemoveAllListeners();
            deckDescriptOpenButton.onClick.AddListener(() => OpenDescriptPop());

            // 存儲按鈕文字的原始顏色
            StoreOriginalTextColors(deckDescriptOpenButton);
        }

        if (nextButton != null)
        {
            navigationButtons.Add(nextButton);
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => ChangeDeck(1));

            // 存儲按鈕文字的原始顏色
            StoreOriginalTextColors(nextButton);
        }

        if (deckDescriptCloseButton != null)
        {
            navigationButtons.Add(deckDescriptCloseButton);
            deckDescriptCloseButton.onClick.RemoveAllListeners();
            deckDescriptCloseButton.onClick.AddListener(() => CloseDescriptPop());

            // 存儲按鈕文字的原始顏色
            StoreOriginalTextColors(deckDescriptCloseButton);
        }

        // 初始化所有按鈕為非選中狀態
        foreach (var button in navigationButtons)
        {
            SetButtonSelected(button, false);
        }
    }

    // 新增：存儲按鈕文字的原始顏色
    private void StoreOriginalTextColors(Button button)
    {
        if (button == null) return;

        // 檢查 Unity UI Text
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            originalTextColors[button] = text.color;
            return;
        }

        // 檢查 TextMeshPro
        TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpText != null)
        {
            originalTextColors[button] = tmpText.color;
        }
    }

    // 確認卡組選擇的方法，現在會切換到下一頁
    private void ConfirmDeckSelection()
    {
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);

        // 獲取當前選中的卡組ID
        int selectedDeckId = GetSelectedDeckId();

        if (selectedDeckId != -1)
        {
            // 這裡新增確認選擇卡組的邏輯
            Debug.Log($"Confirmed deck selection: {selectedDeckId}");

            // 如果有網絡運行器，則設置玩家卡組
            if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None)
            {
                GameDeckManager.Instance.SetPlayerDeck(runner.LocalPlayer, selectedDeckId);

                // 保存選擇的卡組ID
                DeckSelector.selectedDeckId = GetSelectedDeckId();

                // 找到 CanvasManager 並切換到下一頁
                CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
                if (canvasManager != null)
                {
                    canvasManager.ShowNextPage();
                }
                else
                {
                    Debug.LogWarning("CanvasManager not found when trying to go to next page!");
                }
            }
        }
    }

    private void LoadDeckData()
    {
        // 清空舊資料
        availableDecks.Clear();
        previewSprites.Clear();

        // 載入所有卡組資料
        availableDecks.AddRange(GameDeckDatabase.Decks);

        // 預先載入所有預覽圖
        foreach (var deck in availableDecks)
        {
            Sprite preview = Resources.Load<Sprite>(deck.preview_imgae_path);
            if (preview == null)
            {
                Debug.LogError($"Failed to load preview image: {deck.preview_imgae_path}");
                continue;
            }
            previewSprites[deck.preview_imgae_path] = preview;
        }

        if (availableDecks.Count == 0)
        {
            Debug.LogWarning("No decks were loaded!");
        }
    }

    private static int selectedDeckId = 0;
    public static int GetLastSelectedDeckId()
    {
        return selectedDeckId;
    }

    private void ChangeDeck(int direction)
    {
        if (!isInitialized || availableDecks.Count == 0)
            return;

        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        currentDeckIndex += direction;

        if (currentDeckIndex >= availableDecks.Count)
        {
            currentDeckIndex = 0;
        }
        else if (currentDeckIndex < 0)
        {
            currentDeckIndex = availableDecks.Count - 1;
        }

        // 更新顯示並強制刷新
        UpdateDeckDisplay();
        StartCoroutine(ForceRefreshAfterDelay());

        if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None)
        {
            if (currentDeckIndex >= 0 && currentDeckIndex < availableDecks.Count)
            {
                int selectedDeckId = availableDecks[currentDeckIndex].id;
                GameDeckManager.Instance.SetPlayerDeck(runner.LocalPlayer, selectedDeckId);
            }
        }
        else
        {
            Debug.LogWarning("NetworkRunner is not in a valid state for setting player deck.");
        }
    }

    // 延遲刷新 UI，確保在頁面切換後能正確顯示
    private IEnumerator ForceRefreshAfterDelay()
    {
        yield return new WaitForEndOfFrame();
        ForceRefreshUI();
    }

    // 強制刷新 UI 元素
    private void ForceRefreshUI()
    {
        if (deckPreviewImage != null)
        {
            // 強制刷新圖片
            deckPreviewImage.enabled = false;
            deckPreviewImage.enabled = true;

            // 強制更新 Canvas
            Canvas.ForceUpdateCanvases();

            // 如果有父 Canvas，強制重繪
            Canvas parentCanvas = deckPreviewImage.transform.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                CanvasGroup canvasGroup = parentCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    // 微調 alpha 值以觸發重繪
                    float originalAlpha = canvasGroup.alpha;
                    canvasGroup.alpha = originalAlpha * 0.99f;
                    canvasGroup.alpha = originalAlpha;
                }
            }
        }

        // 強制更新文字元素
        if (deckNameText != null)
        {
            deckNameText.enabled = false;
            deckNameText.enabled = true;
            deckNameText.ForceMeshUpdate();
        }

        if (deckDescriptionText != null)
        {
            deckDescriptionText.enabled = false;
            deckDescriptionText.enabled = true;
            deckDescriptionText.ForceMeshUpdate();
        }
    }

    private void OpenDescriptPop()
    {
        Debug.Log("OpenDescriptPop---------------------------------");
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        panelPop.SetActive(false);
        deckDescriptPop.SetActive(true);
        isDescriptionOpen = true;
        currentButtonIndex = 4;
        //SetButtonSelected(navigationButtons[currentButtonIndex], false);
        
        // 確保文字在 Pop 顯示後能正確刷新
        //StartCoroutine(ForceRefreshAfterDelay());
    }

    private void CloseDescriptPop()
    {
        Debug.Log("CloseDescriptPop----------------------------");
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        panelPop.SetActive(true);
        deckDescriptPop.SetActive(false);
        isDescriptionOpen = false;
        currentButtonIndex = 2;
    }

    private void UpdateDeckDisplay()
    {
        if (!gameObject.activeInHierarchy || availableDecks.Count == 0)
            return;

        if (currentDeckIndex < 0 || currentDeckIndex >= availableDecks.Count)
            return;

        var currentDeck = availableDecks[currentDeckIndex];

        // 更新文字
        if (deckNameText != null)
        {
            deckNameText.text = currentDeck.deckName;
            // 強制更新，確保文字顯示
            deckNameText.ForceMeshUpdate();
        }

        if (deckDescriptionText != null)
        {
            deckDescriptionText.text = currentDeck.description;
            // 強制更新，確保文字顯示
            deckDescriptionText.ForceMeshUpdate();
        }

        // 更新圖片
        if (deckPreviewImage != null)
        {
            // 檢查圖片路徑是否有效
            if (previewSprites.ContainsKey(currentDeck.preview_imgae_path))
            {
                deckPreviewImage.sprite = previewSprites[currentDeck.preview_imgae_path];
            }
            else
            {
                // 嘗試重新載入圖片
                Sprite preview = Resources.Load<Sprite>(currentDeck.preview_imgae_path);
                if (preview != null)
                {
                    previewSprites[currentDeck.preview_imgae_path] = preview;
                    deckPreviewImage.sprite = preview;
                }
                else
                {
                    Debug.LogError($"Failed to load preview image: {currentDeck.preview_imgae_path}");
                }
            }

            // 強制更新圖片
            deckPreviewImage.enabled = false;
            deckPreviewImage.enabled = true;
        }

        // 更新按鈕狀態
        if (previousButton != null)
            previousButton.interactable = availableDecks.Count > 1;

        if (nextButton != null)
            nextButton.interactable = availableDecks.Count > 1;

        // 強制更新 Canvas
        Canvas.ForceUpdateCanvases();
    }

    private void OnDestroy()
    {
        // 停止所有正在運行的協程
        foreach (var effect in selectionEffects.Values)
        {
            if (effect != null)
                StopCoroutine(effect);
        }
        selectionEffects.Clear();

        // 清除所有按鈕的監聽器
        foreach (var button in navigationButtons)
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        if (deckDescriptCloseButton != null)
            deckDescriptCloseButton.onClick.RemoveAllListeners();
    }

    public int GetSelectedDeckId()
    {
        if (currentDeckIndex >= 0 && currentDeckIndex < availableDecks.Count)
            return availableDecks[currentDeckIndex].id;
        return -1;
    }
}