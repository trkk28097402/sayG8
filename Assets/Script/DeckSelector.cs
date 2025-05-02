using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using System.Collections;

public class DeckSelector : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button confirmButton; // 確認按鈕
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject panelPop;
    [SerializeField] private TextMeshProUGUI deckNameText;
    [SerializeField] private Image deckPreviewImage;
    AudioManagerLobby audioManagerLobby;

    [Header("Input Settings")]
    [SerializeField] private float keyInputCooldown = 0.3f; // 按鍵冷卻時間
    private float lastKeyInputTime = 0f;

    [Header("Selection Visual Settings")]
    [SerializeField] private float selectedButtonScale = 1.1f; // 選中時的按鈕縮放
    [SerializeField] private Vector2 selectedButtonOffset = new Vector2(0, 3f); // 選中時的位移（模擬浮起）
    [SerializeField] private float selectionAnimationSpeed = 3f; // 選中動畫速度

    // 陰影設定
    [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.3f);
    [SerializeField] private Vector2 shadowOffset = new Vector2(2f, -2f);

    private int currentDeckIndex = 0;
    private int currentButtonIndex = 1; // 預設選中確認按鈕
    private List<Button> navigationButtons = new List<Button>(); // 按鈕導航列表
    private List<GameDeckData> availableDecks = new List<GameDeckData>();
    private Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>();
    private NetworkRunner runner;
    private bool isInitialized = false;
    private Dictionary<Button, Coroutine> selectionEffects = new Dictionary<Button, Coroutine>();

    // 儲存按鈕原始位置和縮放
    private Dictionary<Button, Vector3> originalPositions = new Dictionary<Button, Vector3>();
    private Dictionary<Button, Vector3> originalScales = new Dictionary<Button, Vector3>();
    private Dictionary<Button, Shadow> buttonShadows = new Dictionary<Button, Shadow>();

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

        // 左箭頭鍵或 A 鍵 - 直接切換到前一個卡組
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            print("------------------------------press A-------------------------");
            ChangeDeck(-1);
            lastKeyInputTime = Time.time;
        }
        // 右箭頭鍵或 D 鍵 - 直接切換到下一個卡組
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            print("------------------------------press D-------------------------");
            ChangeDeck(1);
            lastKeyInputTime = Time.time;
        }
        // Enter 鍵檢測已刪除，由其他元件處理
    }

    // 設置按鈕的視覺選中狀態（使用浮起效果）
    private void SetButtonSelected(Button button, bool isSelected)
    {
        if (button == null)
            return;

        // 確保我們已經儲存了原始位置和縮放
        if (!originalPositions.ContainsKey(button))
        {
            originalPositions[button] = button.transform.localPosition;
            originalScales[button] = button.transform.localScale;
        }

        // 確保按鈕有陰影組件
        EnsureButtonHasShadow(button);

        if (isSelected)
        {
            // 啟動浮起效果協程
            if (selectionEffects.ContainsKey(button))
            {
                StopCoroutine(selectionEffects[button]);
            }
            selectionEffects[button] = StartCoroutine(ElevationEffect(button));
        }
        else
        {
            // 停止浮起效果協程
            if (selectionEffects.ContainsKey(button))
            {
                StopCoroutine(selectionEffects[button]);
                selectionEffects.Remove(button);
            }

            // 重置按鈕位置和縮放
            if (originalPositions.ContainsKey(button))
            {
                button.transform.localPosition = originalPositions[button];
                button.transform.localScale = originalScales[button];
            }

            // 重置陰影
            if (buttonShadows.ContainsKey(button))
            {
                Shadow shadow = buttonShadows[button];
                shadow.effectDistance = Vector2.zero;
                shadow.effectColor = new Color(0, 0, 0, 0);
            }
        }
    }

    // 確保按鈕有陰影組件
    private void EnsureButtonHasShadow(Button button)
    {
        if (!buttonShadows.ContainsKey(button))
        {
            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = button.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0); // 初始透明
                shadow.effectDistance = Vector2.zero;
            }
            buttonShadows[button] = shadow;
        }
    }

    // 按鈕浮起效果協程
    private IEnumerator ElevationEffect(Button button)
    {
        Shadow shadow = buttonShadows[button];
        float time = 0;

        while (true)
        {
            // 計算脈動值 (0-1)
            float pulse = (Mathf.Sin(time * selectionAnimationSpeed) + 1f) / 4f; // 除以4使效果更微妙

            // 應用縮放效果 (原始縮放 + 額外縮放 * 脈動值)
            Vector3 targetScale = originalScales[button] * (1f + 0.05f * pulse);
            button.transform.localScale = Vector3.Lerp(button.transform.localScale, targetScale, Time.deltaTime * 10f);

            // 應用位移效果 (原始位置 + 位移 * 脈動值)
            Vector3 targetPos = originalPositions[button] + new Vector3(0, selectedButtonOffset.y * (0.8f + pulse), 0);
            button.transform.localPosition = Vector3.Lerp(button.transform.localPosition, targetPos, Time.deltaTime * 10f);

            // 應用陰影效果
            shadow.effectDistance = shadowOffset * (0.8f + pulse);
            shadow.effectColor = new Color(
                shadowColor.r,
                shadowColor.g,
                shadowColor.b,
                shadowColor.a * (0.8f + pulse)
            );

            time += Time.deltaTime;
            yield return null;
        }
    }

    public void Wait_Runner_Spawned()
    {
        Debug.Log($"[DeckSelector] Wait_Runner_Spawned 被調用，查找 NetworkRunner");

        // 尋找 NetworkRunner
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("[DeckSelector] NetworkRunner not found in scene!");
            // 啟動一個協程來持續尋找 NetworkRunner
            StartCoroutine(RetryFindNetworkRunner());
            return;
        }

        Debug.Log($"[DeckSelector] Found Runner! Runner state: {runner.State}");

        // 確保設置按鈕和加載牌組數據
        SetupButtons();
        LoadDeckData();

        isInitialized = true;

        // 確保在初始化後更新顯示
        StartCoroutine(DelayedUpdateDisplay());
    }

    private IEnumerator RetryFindNetworkRunner()
    {
        Debug.Log("[DeckSelector] Starting retry coroutine to find NetworkRunner");
        float retryTime = 0f;
        float maxRetryTime = 10f; // 最多嘗試10秒

        while (retryTime < maxRetryTime)
        {
            yield return new WaitForSeconds(0.5f);
            retryTime += 0.5f;

            runner = FindObjectOfType<NetworkRunner>();
            if (runner != null)
            {
                Debug.Log($"[DeckSelector] Found NetworkRunner after retrying! Runner state: {runner.State}");

                // 繼續初始化流程
                SetupButtons();
                LoadDeckData();
                isInitialized = true;
                StartCoroutine(DelayedUpdateDisplay());
                yield break;
            }
        }

        Debug.LogError("[DeckSelector] Failed to find NetworkRunner after multiple attempts");
    }

    // 延遲更新顯示，確保 Canvas 有足夠時間初始化
    private IEnumerator DelayedUpdateDisplay()
    {
        Debug.Log("[DeckSelector] Starting delayed update display");

        for (int attempt = 0; attempt < 3; attempt++)
        {
            yield return new WaitForSeconds(0.5f);
            UpdateDeckDisplay();

            // 再次更新，確保 UI 元素完全更新
            yield return new WaitForSeconds(0.5f);

            // 檢查 UI 是否成功更新
            if (deckPreviewImage != null && deckPreviewImage.sprite != null)
            {
                Debug.Log("[DeckSelector] UI successfully updated");
                break;
            }

            Debug.Log($"[DeckSelector] UI update attempt {attempt + 1} failed, retrying...");
        }

        // 初始化時設置當前選中的按鈕為確認按鈕
        if (navigationButtons.Count > 0)
        {
            currentButtonIndex = 1; // 確認按鈕索引
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
        // 清空並重新建立導航按鈕列表（按照畫面順序: 左鍵, 確認鍵, 右鍵）
        navigationButtons.Clear();
        originalPositions.Clear();
        originalScales.Clear();
        buttonShadows.Clear();

        if (previousButton != null)
        {
            navigationButtons.Add(previousButton);
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(() => ChangeDeck(-1));

            // 存儲按鈕原始位置和縮放
            StoreOriginalTransform(previousButton);
        }

        if (confirmButton != null)
        {
            navigationButtons.Add(confirmButton);
            confirmButton.onClick.RemoveAllListeners();
            // 確認按鈕現在會呼叫確認卡組並切換到下一頁
            confirmButton.onClick.AddListener(() => ConfirmDeckSelection());

            // 存儲按鈕原始位置和縮放
            StoreOriginalTransform(confirmButton);
        }

        if (nextButton != null)
        {
            navigationButtons.Add(nextButton);
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => ChangeDeck(1));

            // 存儲按鈕原始位置和縮放
            StoreOriginalTransform(nextButton);
        }

        // 初始化所有按鈕為非選中狀態
        foreach (var button in navigationButtons)
        {
            SetButtonSelected(button, false);
        }

        // 初始選中確認按鈕
        if (navigationButtons.Count > 1)
        {
            SetButtonSelected(navigationButtons[1], true);
        }
    }

    // 新增：存儲按鈕的原始位置和縮放
    private void StoreOriginalTransform(Button button)
    {
        if (button == null) return;

        originalPositions[button] = button.transform.localPosition;
        originalScales[button] = button.transform.localScale;

        // 確保按鈕有陰影組件
        EnsureButtonHasShadow(button);
    }

    // 確認卡組選擇的方法，切換到下一頁
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
        Debug.Log("載入卡組資料");
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
    }

    public int GetSelectedDeckId()
    {
        if (currentDeckIndex >= 0 && currentDeckIndex < availableDecks.Count)
            return availableDecks[currentDeckIndex].id;
        return -1;
    }

    public void ResetAndInitialize()
    {
        Debug.Log("[DeckSelector] 重置並初始化");

        // 重置狀態
        currentDeckIndex = 0;
        isInitialized = false;

        // 清除緩存數據
        availableDecks.Clear();
        previewSprites.Clear();

        // 重新運行初始化
        Wait_Runner_Spawned();
    }
}