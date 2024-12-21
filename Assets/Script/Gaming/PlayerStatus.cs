using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct InGameDeck
{
    public int In_Hand_Count; 
    public int Deck_Left_Count; 
    public int id; 

    private int[] CardOrder; 
    public int CurrentIndex { get; set; }

    public void InitializeCardOrder(List<int> initialOrder)
    {
        CardOrder = new int[40]; 
        for (int i = 0; i < initialOrder.Count && i < CardOrder.Length; i++)
        {
            CardOrder[i] = initialOrder[i];
        }
        CurrentIndex = 0;
    }

    public int DrawNextCard()
    {
        if (CurrentIndex >= CardOrder.Length || Deck_Left_Count <= 0)
        {
            return -1; 
        }

        int nextCard = CardOrder[CurrentIndex];
        CurrentIndex++;
        Deck_Left_Count--;
        return nextCard;
    }

    public void Shuffle()
    {
        List<int> tempList = new List<int>();
        for (int i = CurrentIndex; i < CardOrder.Length; i++)
        {
            tempList.Add(CardOrder[i]);
        }

        // Fisher-Yates shuffle
        for (int i = tempList.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = tempList[i];
            tempList[i] = tempList[j];
            tempList[j] = temp;
        }

        // �N�~�L���P��^�}�C
        for (int i = 0; i < tempList.Count; i++)
        {
            CardOrder[CurrentIndex + i] = tempList[i];
        }
    }
}

public class PlayerStatus : NetworkBehaviour
{
    private NetworkRunner runner;
    public int totalcard = 40;
    public InGameDeck currentdeck;
    public static PlayerStatus Instance { get; private set; }

    GameDeckDatabase gameDeckDatabase;
    private bool isWaitingForDeckId = false;

    public bool IsInitialized { get; set; }

    public event Action<NetworkedCardData[]> OnInitialHandDrawn;
    public event Action<NetworkedCardData> OnCardDrawn;
    public event Action<int> OnCardRemoved;
    public event Action OnDeckShuffled;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private GameManager gameManager;

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("PlayerStatus.Spawned called");
        runner = Object.Runner;
        StartCoroutine(InitializeAfterSpawn());
    }

    private IEnumerator InitializeAfterSpawn()
    {
        while (runner == null)
        {
            runner = Object.Runner;
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"Runner found, LocalPlayer: {runner.LocalPlayer}");

        while (GameManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log("GameManager found");

        while (GameDeckManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log("GameDeckManager found");

        Rpc_NotifyHost(runner.LocalPlayer, Object.Id);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_NotifyHost(PlayerRef player, NetworkId statusId)
    {
        Debug.Log($"Host received notification from player {player}");
        GameManager.Instance.Rpc_RegisterPlayerStatus(player, statusId);
    }

    public void Initialized_Cards()
    {
        if (IsInitialized) return;

        Debug.Log($"Initializing cards for player {Runner.LocalPlayer}");
        try
        {
            StartCoroutine(InitializeCardsWithRetry());
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in Initialized_Cards: {e}");
        }
    }

    private IEnumerator InitializeCardsWithRetry()
    {
        int maxRetries = 5;
        int currentRetry = 0;

        while (currentRetry < maxRetries)
        {
            if (GameDeckManager.Instance != null)
            {
                int? deckId = GameDeckManager.Instance.GetPlayerDeck(Runner.LocalPlayer);

                if (deckId.HasValue)
                {
                    InitializeCardsWithDeckId(deckId.Value);
                    yield break;
                }
            }

            Debug.Log($"Retry {currentRetry + 1}/{maxRetries} to get deck ID");
            currentRetry++;
            yield return new WaitForSeconds(1f);
        }

        Debug.LogError("Failed to initialize cards after maximum retries");
    }

    private void InitializeCardsWithDeckId(int deckId)
    {
        try
        {
            gameDeckDatabase = new GameDeckDatabase();
            currentdeck = new InGameDeck();
            currentdeck.id = deckId;
            currentdeck.In_Hand_Count = 5;
            currentdeck.Deck_Left_Count = 35;

            List<int> initialOrder = new List<int>();
            for (int i = 0; i < totalcard; i++)
            {
                initialOrder.Add(i);
            }

            Debug.Log("Initializing card order");
            currentdeck.InitializeCardOrder(initialOrder);
            currentdeck.Shuffle();
            Debug.Log("Deck shuffled");

            IsInitialized = true;
            DrawInitialHand();
            Debug.Log($"Player {Runner.LocalPlayer} initial hand drawn");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize cards with deck ID {deckId}: {e}");
            IsInitialized = false;
        }
    }

    public void DrawInitialHand()
    {
        if (!IsInitialized)
        {
            Debug.LogError("Attempting to draw initial hand before initialization");
            return;
        }

        if (OnInitialHandDrawn == null)
        {
            Debug.Log("Waiting for event subscribers...");
            StartCoroutine(WaitForSubscriberAndDraw());
            return;
        }

        try
        {
            NetworkedCardData[] initialHand = new NetworkedCardData[5];
            for (int i = 0; i < 5; i++)
            {
                int cardId = currentdeck.DrawNextCard();
                if (cardId == -1)
                {
                    Debug.LogError("Failed to draw card: deck is empty");
                    return;
                }

                bool success = TryGetCardData(cardId, out NetworkedCardData cardData);
                if (!success)
                {
                    Debug.LogError($"Failed to get card data for card ID {cardId}");
                    return;
                }
                initialHand[i] = cardData;
            }
            OnInitialHandDrawn?.Invoke(initialHand);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error drawing initial hand: {e}");
        }
    }

    private bool TryGetCardData(int cardId, out NetworkedCardData cardData)
    {
        cardData = new NetworkedCardData
        {
            cardId = cardId,
            cardName = "Invalid Card",
            imagePath = "invalid_path"
        };

        if (currentdeck.id < 0)
        {
            Debug.LogError($"Invalid deck ID: {currentdeck.id}");
            return false;
        }

        try
        {
            GameDeckData deckData = gameDeckDatabase.GetDeckById(currentdeck.id);
            if (deckData == null)
            {
                Debug.LogError($"No deck data found for ID: {currentdeck.id}");
                return false;
            }

            cardData = new NetworkedCardData
            {
                cardId = cardId,
                cardName = $"Card {cardId}",
                imagePath = $"{deckData.deck_path}/{cardId + 1}"
            };
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in GetCardData: {e}");
            return false;
        }
    }

    public void DrawCard()
    {
        if (!IsInitialized || currentdeck.Deck_Left_Count <= 0)
        {
            Debug.LogWarning("Cannot draw card: deck not initialized or empty");
            return;
        }

        int cardId = currentdeck.DrawNextCard();
        if (cardId == -1)
        {
            Debug.LogError("Failed to draw card");
            return;
        }

        if (TryGetCardData(cardId, out NetworkedCardData cardData))
        {
            OnCardDrawn?.Invoke(cardData);
            currentdeck.In_Hand_Count++;
        }
    }

    private IEnumerator WaitForSubscriberAndDraw()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (OnInitialHandDrawn == null && elapsed < timeout)
        {
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (OnInitialHandDrawn != null)
        {
            DrawInitialHand();
        }
        else
        {
            Debug.LogError("Timed out waiting for event subscribers");
        }
    }
}