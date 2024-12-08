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
        // ���� HasStateAuthority �ˬd
        if (!NetworkedPlayerCards.ContainsKey(player))
        {
            localPlayerCards[player] = cardHand;
            NetworkedPlayerCards.Add(player, cardHand.Object.Id);

            int deckId = GameDeckManager.Instance.GetPlayerDeck(player);
            //deckId = 0; // ���ե�
            if (deckId != -1)
            {
                SetupPlayerDeck(player, deckId);
                Debug.Log($"�w���U���a {player} ���d��");
            }
        }
    }

    private void SetupPlayerDeck(PlayerRef player, int deckId)
    {
        if (!localPlayerCards.TryGetValue(player, out CardOnHand cardHand))
        {
            Debug.LogError($"�䤣�쪱�a {player} �� CardOnHand");
            return;
        }

        GameDeckData deckData = deckDatabase.GetDeckById(deckId);
        if (deckData == null)
        {
            Debug.LogError($"�䤣��ID�� {deckId} ���d��");
            return;
        }

        int initialHandSize = 5;
        NetworkedCardData[] networkCards = new NetworkedCardData[initialHandSize];
        for (int i = 0; i < initialHandSize; i++)
        {
            networkCards[i] = new NetworkedCardData
            {
                cardName = $"{deckData.deckName} Card {i + 1}",
                imagePath = $"{deckData.deck_path}/{i + 1}"  // �d���q1�}�l
            };
        }

        cardHand.SetupCards(networkCards);
        Debug.Log($"�]�m���a {player} ���d�աG{deckData.deckName}");
    }

    private void InitializeGame()
    {
        // �M�Ų{�������a���
        NetworkedPlayerCards.Clear();
        localPlayerCards.Clear();
    }

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log("Game start");
    }

    // �����⪺CardOnHand
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

    // �B�z���a���}
    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (NetworkedPlayerCards.ContainsKey(player))
        {
            NetworkedPlayerCards.Remove(player);
            localPlayerCards.Remove(player);
            Debug.Log($"���a {player} �w���}�C��");
        }
    }

}