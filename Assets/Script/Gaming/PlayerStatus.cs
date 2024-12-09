using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct InGameDeck
{
    public int In_Hand_Count; // 手牌數目
    public int Deck_Left_Count; // 牌組剩餘數目
    public int id; // 牌組id

    private int[] CardOrder; // 改用普通數組
    public int CurrentIndex { get; set; }

    // 初始化牌組順序的方法
    public void InitializeCardOrder(List<int> initialOrder)
    {
        Debug.Log("初始化洗牌!");
        CardOrder = new int[40]; // 固定大小為40
        for (int i = 0; i < initialOrder.Count && i < CardOrder.Length; i++)
        {
            CardOrder[i] = initialOrder[i];
        }
        CurrentIndex = 0;
    }

    public int DrawNextCard()
    {
        Debug.Log("抽一張牌!");
        if (CurrentIndex >= CardOrder.Length || Deck_Left_Count <= 0)
        {
            return -1; // 牌組已空
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

        // 將洗過的牌放回陣列
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

    public bool IsInitialized { get; set; }

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

    private GameManager gameManager;

    // OnNetworkSpawned 是在網路物件完全準備好後才呼叫的
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

        // 通知所有玩家有新玩家加入
        Rpc_NotifyHost(runner.LocalPlayer, Object.Id);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_NotifyHost(PlayerRef player, NetworkId statusId)
    {
        // 這個方法只會在房主端執行
        Debug.Log($"Host received notification from player {player}");
        GameManager.Instance.Rpc_RegisterPlayerStatus(player, statusId);
    }

    public void Initialized_Cards()
    {

        if (IsInitialized) return;  // 防止重複初始化

        Debug.Log($"正在為玩家 {Runner.LocalPlayer} 初始化卡牌");
        try
        {
            gameDeckDatabase = new GameDeckDatabase();
            currentdeck = new InGameDeck();
            currentdeck.id = GameDeckManager.Instance.GetPlayerDeck(Runner.LocalPlayer);
            currentdeck.In_Hand_Count = 5;
            currentdeck.Deck_Left_Count = 35;

            List<int> initialOrder = new List<int>();
            for (int i = 0; i < totalcard; i++)
            {
                initialOrder.Add(i);
            }

            Debug.Log("開始初始化卡牌順序");
            currentdeck.InitializeCardOrder(initialOrder);
            currentdeck.Shuffle();
            Debug.Log("完成洗牌");

            IsInitialized = true;
            DrawInitialHand();
            Debug.Log($"玩家 {Runner.LocalPlayer} 完成初始手牌");
        }
        catch (Exception e)
        {
            Debug.LogError($"初始化卡牌時發生錯誤: {e}");
        }
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