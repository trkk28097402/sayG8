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
            Debug.Log($"�w���U���a {player} ���d�P�޲z��");
        }
    }

    public void RegisterPlayerStatus(PlayerRef player, PlayerStatus status)
    {
        if (!playerStatuses.ContainsKey(player))
        {
            playerStatuses[player] = status;
            Debug.Log($"�w���U���a {player} �����A�޲z��");

            // �p�G��Ӫ��a���w�ǳƦn�A�}�l�C��
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

        // ��l�ƨC�Ӫ��a���P��
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
            Debug.Log($"���a {player} �w���}�C��");
        }
    }
}