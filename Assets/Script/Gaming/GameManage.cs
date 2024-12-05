using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private CardonHand cardHand;
    private GameDeckDatabase deckDatabase;

    private void Awake()
    {
        deckDatabase = new GameDeckDatabase();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }
    }

    private void InitializeGame()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            int deckId = GameDeckManager.Instance.GetPlayerDeck(player);
            deckId = 0;
            if (deckId != -1)
            {
                SetupPlayerDeck(player, deckId);
            }
        }
    }

    private void SetupPlayerDeck(PlayerRef player, int deckId)
    {
        // 從您的數據庫獲取卡組資料
        GameDeckData deckData = deckDatabase.GetDeckById(deckId);
        if (deckData == null)
        {
            Debug.LogError($"找不到ID為 {deckId} 的卡組");
            return;
        }

        // 根據卡組資料創建測試卡片
        // 這裡假設我們先創建5張牌作為起始手牌
        int initialHandSize = Mathf.Min(5, deckData.cardCount);
        CardData[] cards = new CardData[initialHandSize];

        for (int i = 0; i < initialHandSize; i++)
        {
            cards[i] = new CardData
            {
                cardName = $"{deckData.deckName} Card {i + 1}",
                cardImage = Resources.Load<Sprite>($"{GameDeckDatabase.DECK_PATH_PREFIX}{deckData.deckName}/card_{i}")
                // 如果您有其他卡片屬性，可以在這裡設置
            };
        }

        // 如果這是本地玩家，設置他的手牌
        if (player == Runner.LocalPlayer)
        {
            cardHand.SetupCard(cards);
            Debug.Log($"設置玩家 {player} 的卡組：{deckData.deckName}");
        }
    }

    // 用於測試的方法
    public void SetupTestDeck(int deckId)
    {
        GameDeckData testDeck = deckDatabase.GetDeckById(deckId);
        if (testDeck == null) return;

        int testHandSize = Mathf.Min(5, testDeck.cardCount);
        CardData[] testCards = new CardData[testHandSize];

        for (int i = 0; i < testHandSize; i++)
        {
            testCards[i] = new CardData
            {
                cardName = $"{testDeck.deckName} Test Card {i + 1}",
                cardImage = null  // 或載入預設圖片
            };
        }

        cardHand.SetupCard(testCards);
    }
}