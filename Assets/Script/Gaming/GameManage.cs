using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    [Networked]
    private NetworkDictionary<PlayerRef, NetworkId> NetworkedPlayerCards { get; }

    private Dictionary<PlayerRef, CardOnHand> localPlayerCards = new Dictionary<PlayerRef, CardOnHand>();
    private GameDeckDatabase deckDatabase;

    public const int MAX_PLAYERS = 2;

    private void Awake()
    {
        InitializeDeckDatabase();
    }

    private void InitializeDeckDatabase()
    {
        if (deckDatabase == null)
        {
            deckDatabase = new GameDeckDatabase();
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }
    }

    public void RegisterPlayerCard(PlayerRef player, CardOnHand cardHand)
    {
        // 移除 HasStateAuthority 檢查
        if (!NetworkedPlayerCards.ContainsKey(player))
        {
            localPlayerCards[player] = cardHand;
            NetworkedPlayerCards.Add(player, cardHand.Object.Id);

            int deckId = GameDeckManager.Instance.GetPlayerDeck(player);
            //deckId = 0; // 測試用
            if (deckId != -1)
            {
                SetupPlayerDeck(player, deckId);
                Debug.Log($"已註冊玩家 {player} 的卡組");
            }
        }
    }

    private void SetupPlayerDeck(PlayerRef player, int deckId)
    {
        if (!localPlayerCards.TryGetValue(player, out CardOnHand cardHand))
        {
            Debug.LogError($"找不到玩家 {player} 的 CardOnHand");
            return;
        }

        GameDeckData deckData = deckDatabase.GetDeckById(deckId);
        if (deckData == null)
        {
            Debug.LogError($"找不到ID為 {deckId} 的卡組");
            return;
        }

        int initialHandSize = 5;
        NetworkedCardData[] networkCards = new NetworkedCardData[initialHandSize];
        for (int i = 0; i < initialHandSize; i++)
        {
            networkCards[i] = new NetworkedCardData
            {
                cardName = $"{deckData.deckName} Card {i + 1}",
                imagePath = $"{deckData.deck_path}/{i + 1}"  // 卡片從1開始
            };
        }

        cardHand.SetupCards(networkCards);
        Debug.Log($"設置玩家 {player} 的卡組：{deckData.deckName}");
    }

    private void InitializeGame()
    {
        // 清空現有的玩家資料
        NetworkedPlayerCards.Clear();
        localPlayerCards.Clear();
    }

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log("Game start");
    }

    // 獲取對手的CardOnHand
    public CardOnHand GetOpponentCard(PlayerRef currentPlayer)
    {
        foreach (var kvp in localPlayerCards)
        {
            if (kvp.Key != currentPlayer)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    // 處理玩家離開
    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (NetworkedPlayerCards.ContainsKey(player))
        {
            NetworkedPlayerCards.Remove(player);
            localPlayerCards.Remove(player);
            Debug.Log($"玩家 {player} 已離開遊戲");
        }
    }

}