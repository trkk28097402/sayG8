using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using System.Collections;

// 修改 DeckSelector 以配合新的輸入控制系統
public class DeckSelector : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button deckDescriptOpenButton;
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

    private int currentDeckIndex = 0;
    private List<GameDeckData> availableDecks = new List<GameDeckData>();
    private Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>();
    private NetworkRunner runner;
    private bool isInitialized = false;

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

        // 左箭頭鍵或 A 鍵
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            ChangeDeck(-1);
            lastKeyInputTime = Time.time;
        }
        // 右箭頭鍵或 D 鍵
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            ChangeDeck(1);
            lastKeyInputTime = Time.time;
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
        if (previousButton != null)
        {
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(() => ChangeDeck(-1));
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => ChangeDeck(1));
        }

        if (deckDescriptOpenButton != null)
        {
            deckDescriptOpenButton.onClick.RemoveAllListeners();
            deckDescriptOpenButton.onClick.AddListener(() => OpenDescriptPop());
        }

        if (deckDescriptCloseButton != null)
        {
            deckDescriptCloseButton.onClick.RemoveAllListeners();
            deckDescriptCloseButton.onClick.AddListener(() => CloseDescriptPop());
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

        // 確保文字在 Pop 顯示後能正確刷新
        StartCoroutine(ForceRefreshAfterDelay());
    }

    private void CloseDescriptPop()
    {
        Debug.Log("CloseDescriptPop----------------------------");
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        panelPop.SetActive(true);
        deckDescriptPop.SetActive(false);
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
        if (previousButton != null)
            previousButton.onClick.RemoveAllListeners();

        if (nextButton != null)
            nextButton.onClick.RemoveAllListeners();

        if (deckDescriptOpenButton != null)
            deckDescriptOpenButton.onClick.RemoveAllListeners();

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