using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    [Networked]
    private NetworkDictionary<PlayerRef, NetworkId> NetworkedPlayerCards { get; }
    private Dictionary<PlayerRef, CardOnHand> localPlayerCards = new Dictionary<PlayerRef, CardOnHand>();
    private Dictionary<PlayerRef, PlayerStatus> playerStatuses = new Dictionary<PlayerRef, PlayerStatus>();

    public const int MAX_PLAYERS = 2;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }
    }

    public void RegisterPlayerCard(PlayerRef player, CardOnHand cardHand)
    {
        if (!NetworkedPlayerCards.ContainsKey(player))
        {
            localPlayerCards[player] = cardHand;
            NetworkedPlayerCards.Add(player, cardHand.Object.Id);
            Debug.Log($"已註冊玩家 {player} 的卡牌管理器");
        }
    }

    public void RegisterPlayerStatus(PlayerRef player, PlayerStatus status)
    {
        if (!playerStatuses.ContainsKey(player))
        {
            playerStatuses[player] = status;
            Debug.Log($"已註冊玩家 {player} 的狀態管理器");

            // 如果兩個玩家都已準備好，開始遊戲
            //if (playerStatuses.Count == MAX_PLAYERS)
            {
                StartGame();
            }
        }
    }

    private void InitializeGame()
    {
        NetworkedPlayerCards.Clear();
        localPlayerCards.Clear();
        playerStatuses.Clear();
    }

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        // 初始化每個玩家的牌組
        foreach (var playerStatus in playerStatuses.Values)
        {
            playerStatus.Initialized_Cards();
        }

        Debug.Log("Game started - Players initialized");
    }

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

    public PlayerStatus GetOpponentStatus(PlayerRef currentPlayer)
    {
        foreach (var kvp in playerStatuses)
        {
            if (kvp.Key != currentPlayer)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (NetworkedPlayerCards.ContainsKey(player))
        {
            NetworkedPlayerCards.Remove(player);
            localPlayerCards.Remove(player);
            playerStatuses.Remove(player);
            Debug.Log($"玩家 {player} 已離開遊戲");
        }
    }
}