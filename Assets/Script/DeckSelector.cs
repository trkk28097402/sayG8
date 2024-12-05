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
    [SerializeField] private TextMeshProUGUI deckNameText;
    [SerializeField] private TextMeshProUGUI deckDescriptionText;
    [SerializeField] private Image deckPreviewImage;

    private int currentDeckIndex = 0;
    private List<GameDeckData> availableDecks = new List<GameDeckData>();
    private Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>();
    private NetworkRunner runner;

    private void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
        }

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
    }

    private void LoadDeckData()
    {
        // ���J�Ҧ��d�ռƾ�
        availableDecks.AddRange(GameDeckDatabase.Decks);

        // �w�����J�Ҧ��w����
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
            // �ϥ� GameDeckManager ���x�s���
            GameDeckManager.Instance.SetPlayerDeck(runner.LocalPlayer, selectedDeckId);
        }
    }

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

    // �����e��ܪ��d��ID
    public int GetSelectedDeckId()
    {
        if (currentDeckIndex >= 0 && currentDeckIndex < availableDecks.Count)
            return availableDecks[currentDeckIndex].id;
        return -1;
    }

}