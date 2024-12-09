using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct InGameDeck : INetworkStruct
{
    public int In_Hand_Count; // 手牌數目
    public int Deck_Left_Count; // 牌組剩餘數目
    public int id; // 牌組id

    // 使用固定大小的數組來存儲牌組順序
    [Networked, Capacity(40)]
    public NetworkArray<int> CardOrder { get; }

    // 當前抽牌位置的索引
    public int CurrentIndex { get; set; }

    // 初始化牌組順序的方法
    public void InitializeCardOrder(List<int> initialOrder)
    {
        Debug.Log("初始化洗牌!");
        for (int i = 0; i < initialOrder.Count && i < CardOrder.Length; i++)
        {
            CardOrder.Set(i, initialOrder[i]);
        }
        CurrentIndex = 0;
    }

    // 獲取下一張牌
    public int DrawNextCard()
    {
        Debug.Log("抽一張牌!");
        if (CurrentIndex >= CardOrder.Length || Deck_Left_Count <= 0)
        {
            return -1; // 牌組已空
        }

        int nextCard = CardOrder.Get(CurrentIndex);
        CurrentIndex++;
        Deck_Left_Count--;
        return nextCard;
    }

    // 洗牌方法
    public void Shuffle()
    {
        List<int> tempList = new List<int>();
        for (int i = CurrentIndex; i < CardOrder.Length; i++)
        {
            tempList.Add(CardOrder.Get(i));
        }

        // Fisher-Yates shuffle
        for (int i = tempList.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = tempList[i];
            tempList[i] = tempList[j];
            tempList[j] = temp;
        }

        // 將洗過的牌放回陣列
        for (int i = 0; i < tempList.Count; i++)
        {
            CardOrder.Set(CurrentIndex + i, tempList[i]);
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

    // 定義事件
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

    public override void Spawned()
    {
        base.Spawned();

        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
            return;
        }

        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.RegisterPlayerStatus(runner.LocalPlayer, this);
            Debug.Log($"已註冊玩家狀態到 GameManager: Player {runner.LocalPlayer}");
        }
        else
        {
            Debug.LogError("GameManager not found in scene!");
        }

        Debug.Log($"Runner state: {runner.State}");
    }

    public void Initialized_Cards()
    {
        gameDeckDatabase = new GameDeckDatabase();
        currentdeck = new InGameDeck();
        currentdeck.id = GameDeckManager.Instance.GetPlayerDeck(runner.LocalPlayer);
        currentdeck.In_Hand_Count = 5;
        currentdeck.Deck_Left_Count = 35;

        // 初始化並抽取起始手牌
        List<int> initialOrder = new List<int>();
        for (int i = 0; i < totalcard; i++)
        {
            initialOrder.Add(i);
        }
        currentdeck.InitializeCardOrder(initialOrder);
        currentdeck.Shuffle();

        DrawInitialHand();
    }

    public void DrawInitialHand()
    {
        // 如果沒有訂閱者，等待一下再試
        if (OnInitialHandDrawn == null)
        {
            Debug.Log("等待事件訂閱者...");
            StartCoroutine(WaitForSubscriberAndDraw());
            return;
        }

        NetworkedCardData[] initialHand = new NetworkedCardData[5];
        for (int i = 0; i < 5; i++)
        {
            int cardId = currentdeck.DrawNextCard();
            initialHand[i] = GetCardData(cardId);
        }
        OnInitialHandDrawn?.Invoke(initialHand);
    }

    private IEnumerator WaitForSubscriberAndDraw()
    {
        // 等待直到有訂閱者
        while (OnInitialHandDrawn == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // 有訂閱者了，執行抽牌
        DrawInitialHand();
    }

    public void DrawCard()
    {
        if (currentdeck.Deck_Left_Count > 0)
        {
            int cardId = currentdeck.DrawNextCard();
            NetworkedCardData cardData = GetCardData(cardId);
            OnCardDrawn?.Invoke(cardData);
            currentdeck.In_Hand_Count++;
        }
    }

    private NetworkedCardData GetCardData(int cardid)
    {
        GameDeckData deckData = gameDeckDatabase.GetDeckById(currentdeck.id);
        return new NetworkedCardData
        {
            cardId = cardid,
            cardName = $"Card {cardid}",  // 使用 NetworkString
            imagePath = $"{deckData.deck_path}/{cardid + 1}"  // 使用 NetworkString
        };
    }

}