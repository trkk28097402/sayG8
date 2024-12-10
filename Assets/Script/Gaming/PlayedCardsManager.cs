using Fusion;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using System;
using System.Collections;

public struct PlayedCardInfo : INetworkStruct
{
    public PlayerRef PlayerRef;
    public int CardId;
    public int DeckId;
}

public class PlayedCardsManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject playedCardPrefab;
    [SerializeField] private Image playAreaImage;

    [Header("Settings")]
    [SerializeField] private float cardSpacing = 150f;
    [SerializeField] private float playAnimationDuration = 0.5f;
    [SerializeField] private Vector2 centerPosition = new Vector2(0, 0);
    [SerializeField] private Vector2 restPosition = new Vector2(-400, 0);

    [Networked, Capacity(40)]
    private NetworkArray<PlayedCardInfo> PlayedCards { get; }
    [Networked]
    private int CurrentPlayedCardCount { get; set; }

    private List<RectTransform> playedCardObjects = new List<RectTransform>();
    private NetworkRunner runner;
    private GameManager gameManager;
    private bool isInitialized = false;
    private GameDeckDatabase gameDeckDatabase;

    private RectTransform PlayArea => playAreaImage.rectTransform;

    public override void Spawned()
    {
        base.Spawned();
        StartCoroutine(InitializeAfterSpawn());
        gameDeckDatabase = new GameDeckDatabase();
    }

    private IEnumerator InitializeAfterSpawn()
    {
        while (TurnManager.Instance == null || !TurnManager.Instance.IsFullyInitialized())
        {
            Debug.Log("Waiting for TurnManager to initialize...");
            yield return new WaitForSeconds(0.1f);
        }

        while (runner == null)
        {
            runner = Object.Runner;
            if (runner == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

        isInitialized = true;
        Debug.Log("PlayedCardsManager initialized");
    }

    public void PlayCard(NetworkedCardData cardData, int handIndex)
    {
        Debug.Log($"Attempting to play card with index {handIndex}");

        if (runner == null)
        {
            Debug.LogError("NetworkRunner is null");
            return;
        }

        if (!TurnManager.Instance.IsPlayerTurn(runner.LocalPlayer))
        {
            Debug.Log("Not your turn!");
            return;
        }

        Rpc_RequestPlayCard(cardData.cardId, GameDeckManager.Instance.GetPlayerDeck(runner.LocalPlayer),
            handIndex, runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestPlayCard(int cardId, int deckId, int handIndex, PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (!TurnManager.Instance.IsPlayerTurn(player))
        {
            Debug.LogWarning($"Received play card request from {player} but it's not their turn!");
            return;
        }

        PlayedCardInfo newCard = new PlayedCardInfo
        {
            PlayerRef = player,
            CardId = cardId,
            DeckId = deckId
        };

        if (CurrentPlayedCardCount < PlayedCards.Length)
        {
            PlayedCards.Set(CurrentPlayedCardCount, newCard);
            CurrentPlayedCardCount++;

            // Notify all clients about the new card
            Rpc_NotifyCardPlayed(handIndex, player, cardId, deckId);

            // Switch turn after card is played
            TurnManager.Instance.SwitchToNextPlayer();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyCardPlayed(int handIndex, PlayerRef player, int cardId, int deckId)
    {
        Debug.Log($"Card played notification received: Player {player}, Card {cardId}");

        // Remove card from hand if it's the local player
        if (player == runner.LocalPlayer)
        {
            if (GameManager.Instance.localPlayerCards.TryGetValue(player, out var playerCard))
            {
                Debug.Log($"Removing card {handIndex} from player {player}'s hand");
                playerCard.HandleCardRemoved(handIndex);
            }
            else
            {
                Debug.LogError($"Player {player} not found in GameManager's dictionary");
            }
        }

        // Create the played card for all players
        PlayedCardInfo cardInfo = new PlayedCardInfo
        {
            PlayerRef = player,
            CardId = cardId,
            DeckId = deckId
        };

        CreatePlayedCard(cardInfo);
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        // Make sure all clients have all played cards
        while (playedCardObjects.Count < CurrentPlayedCardCount)
        {
            PlayedCardInfo cardInfo = PlayedCards.Get(playedCardObjects.Count);
            CreatePlayedCard(cardInfo);
        }
    }

    private void CreatePlayedCard(PlayedCardInfo cardInfo)
    {
        GameObject cardObj = Instantiate(playedCardPrefab, PlayArea);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();

        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);

        // Get deck data and create card data
        GameDeckData deckData = gameDeckDatabase.GetDeckById(cardInfo.DeckId);
        NetworkedCardData cardData = new NetworkedCardData
        {
            cardId = cardInfo.CardId,
            cardName = $"Card {cardInfo.CardId}",
            imagePath = $"{deckData.deck_path}/{cardInfo.CardId + 1}"
        };

        UpdateCardVisual(cardObj, cardData);

        Vector2 startPos = GetStartPosition(cardInfo.PlayerRef);
        cardRect.anchoredPosition = startPos;

        Sequence cardSequence = DOTween.Sequence();

        cardSequence.Append(cardRect.DOAnchorPos(centerPosition, playAnimationDuration)
            .SetEase(Ease.OutBack));

        cardSequence.AppendInterval(0.3f);

        Vector2 finalPosition = restPosition + new Vector2(playedCardObjects.Count * cardSpacing, 0);

        cardSequence.Append(cardRect.DOAnchorPos(finalPosition, playAnimationDuration)
            .SetEase(Ease.OutBack));

        playedCardObjects.Add(cardRect);
    }

    private Vector2 GetStartPosition(PlayerRef playerRef)
    {
        if (playerRef == Runner.LocalPlayer)
        {
            return new Vector2(0, -300);
        }
        else
        {
            return new Vector2(0, 300);
        }
    }

    private void UpdateCardVisual(GameObject cardObject, NetworkedCardData data)
    {
        Image cardImage = cardObject.GetComponentInChildren<Image>();
        if (cardImage != null)
        {
            var sprite = Resources.Load<Sprite>(data.imagePath.Value);
            if (sprite != null)
            {
                cardImage.sprite = sprite;
            }
            else
            {
                Debug.LogWarning($"Could not load sprite from path: {data.imagePath.Value}");
            }
        }

        TMPro.TextMeshProUGUI cardName = cardObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (cardName != null)
        {
            cardName.text = data.cardName.Value;
        }
    }

    private void OnDestroy()
    {
        // Clean up all card objects
        foreach (var cardRect in playedCardObjects)
        {
            if (cardRect != null)
            {
                Destroy(cardRect.gameObject);
            }
        }
        playedCardObjects.Clear();
    }
}