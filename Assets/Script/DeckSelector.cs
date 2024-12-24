using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;


public class DeckSelector : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button deckDescriptOpenButton; //yu
    [SerializeField] private Button deckDescriptCloseButton; //yu
    [SerializeField] private GameObject deckDescriptPop; //yu
    [SerializeField] private GameObject panelPop; //yu
    [SerializeField] private TextMeshProUGUI deckNameText;
    [SerializeField] private TextMeshProUGUI deckDescriptionText;
    [SerializeField] private Image deckPreviewImage;
    AudioManagerLobby audioManagerLobby;//yu

    private int currentDeckIndex = 0;
    private List<GameDeckData> availableDecks = new List<GameDeckData>();
    private Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>();
    private NetworkRunner runner;
    
    //yu
    private void Awake()
    {
        audioManagerLobby = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManagerLobby>();
    }
    //

    private void Start()
    {
        
    }

    public void Wait_Runner_Spawned() {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
        }
        Debug.Log($"Runner state: {runner.State}");

        SetupButtons();
        LoadDeckData();
        UpdateDeckDisplay();
    }

    private void OnEnable()
    {
        
        if (availableDecks.Count > 0)
        {
            UpdateDeckDisplay();
        }
    }

    private void SetupButtons()
    {
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(() => ChangeDeck(-1));
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(() => ChangeDeck(1));
        }

        //yu
        if (deckDescriptOpenButton != null)
        {
            deckDescriptOpenButton.onClick.AddListener(() => OpenDescriptPop());
        }

        if (deckDescriptCloseButton != null)
        {
            deckDescriptCloseButton.onClick.AddListener(() => CloseDescriptPop());
        }
        //
    }

    private void LoadDeckData()
    {
        // 載入所有卡組數據
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
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);//yu
        currentDeckIndex += direction;

        if (currentDeckIndex >= availableDecks.Count)
        {
            currentDeckIndex = 0;
        }
        else if (currentDeckIndex < 0)
        {
            currentDeckIndex = availableDecks.Count - 1;
        }

        UpdateDeckDisplay();

        if (currentDeckIndex >= 0 && currentDeckIndex < availableDecks.Count)
        {
            if (!runner.IsRunning)
            {
                Debug.LogError("NetworkRunner is not running!");
                return;
            }

            if (runner.LocalPlayer == PlayerRef.None)
            {
                Debug.LogError("LocalPlayer is invalid! Ensure the player has joined a session.");
                return;
            }
            int selectedDeckId = availableDecks[currentDeckIndex].id;
            // 使用 GameDeckManager 來儲存選擇
            GameDeckManager.Instance.SetPlayerDeck(runner.LocalPlayer, selectedDeckId);
        }
    }

    //yu
    private void OpenDescriptPop()
    {
        Debug.Log("OpenDescriptPop---------------------------------");
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);//yu
        panelPop.SetActive(false);
        deckDescriptPop.SetActive(true);
        
    }

    private void CloseDescriptPop()
    {
        Debug.Log("CloseDescriptPop----------------------------");
        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);//yu
        panelPop.SetActive(true);
        deckDescriptPop.SetActive(false);
        
    }
    //

    private void UpdateDeckDisplay()
    {
        if (!gameObject.activeInHierarchy || availableDecks.Count == 0)
            return;

        if (currentDeckIndex < 0 || currentDeckIndex >= availableDecks.Count)
            return;

        var currentDeck = availableDecks[currentDeckIndex];

        if (deckNameText != null)
            deckNameText.text = currentDeck.deckName;

        if (deckDescriptionText != null)
            deckDescriptionText.text = currentDeck.description;

        if (deckPreviewImage != null && previewSprites.ContainsKey(currentDeck.preview_imgae_path))
        {
            deckPreviewImage.sprite = previewSprites[currentDeck.preview_imgae_path];
            // force update
            deckPreviewImage.enabled = false;
            deckPreviewImage.enabled = true;
        }

        if (previousButton != null)
            previousButton.interactable = availableDecks.Count > 1;

        if (nextButton != null)
            nextButton.interactable = availableDecks.Count > 1;

        if (deckPreviewImage != null)
        {
            Canvas.ForceUpdateCanvases();
            deckPreviewImage.transform.parent.GetComponent<CanvasRenderer>()?.SetAlpha(1);
        }
    }

    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveAllListeners();

        if (nextButton != null)
            nextButton.onClick.RemoveAllListeners();
    }

    // 獲取當前選擇的卡組ID
    public int GetSelectedDeckId()
    {
        if (currentDeckIndex >= 0 && currentDeckIndex < availableDecks.Count)
            return availableDecks[currentDeckIndex].id;
        return -1;
    }

}