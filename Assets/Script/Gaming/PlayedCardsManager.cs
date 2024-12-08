using Fusion;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using System;

public struct PlayedCardInfo : INetworkStruct
{
    public PlayerRef PlayerRef;
    public NetworkedCardData CardData;
}

public class PlayedCardsManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject playedCardPrefab;
    [SerializeField] private Image playAreaImage; // 改用 Image

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

    private RectTransform PlayArea => playAreaImage.rectTransform;

    public override void Spawned()
    {
        base.Spawned();
        runner = FindObjectOfType<NetworkRunner>();
        gameManager = FindObjectOfType<GameManager>();

        if (playAreaImage == null)
        {
            Debug.LogError("Play Area Image is not assigned!");
        }
    }

    public void PlayCard(NetworkedCardData cardData, int handIndex)
    {
        Debug.Log($"Attempting to play card with index {handIndex}");

        if (!Object.HasStateAuthority)
        {
            Debug.Log("No state authority, returning");
            return;
        }

        if (runner == null)
        {
            Debug.LogError("NetworkRunner is null");
            return;
        }

        PlayedCardInfo newCard = new PlayedCardInfo
        {
            PlayerRef = runner.LocalPlayer,
            CardData = cardData
        };

        if (CurrentPlayedCardCount < PlayedCards.Length)
        {
            PlayedCards.Set(CurrentPlayedCardCount, newCard);
            CurrentPlayedCardCount++;

            if (GameManager.Instance != null)
            {
                Debug.Log($"Current registered players: {string.Join(", ", GameManager.Instance.localPlayerCards.Keys)}");
                if (GameManager.Instance.localPlayerCards.ContainsKey(runner.LocalPlayer))
                {
                    var playerCard = GameManager.Instance.localPlayerCards[runner.LocalPlayer];
                    if (playerCard != null)
                    {
                        Debug.Log($"Removing card {handIndex} from player {runner.LocalPlayer}");
                        playerCard.HandleCardRemoved(handIndex);
                    }
                }
                else
                {
                    Debug.LogError($"Player {runner.LocalPlayer} not found in GameManager's dictionary");
                }
            }
            else
            {
                Debug.LogError("GameManager.Instance is null");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        while (playedCardObjects.Count < CurrentPlayedCardCount)
        {
            CreatePlayedCard(PlayedCards.Get(playedCardObjects.Count));
        }
    }

    private void CreatePlayedCard(PlayedCardInfo cardInfo)
    {
        GameObject cardObj = Instantiate(playedCardPrefab, PlayArea);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();

        // 設置卡牌的基本屬性
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);

        UpdateCardVisual(cardObj, cardInfo.CardData);

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
        if (playerRef == runner.LocalPlayer)
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
        }

        TMPro.TextMeshProUGUI cardName = cardObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (cardName != null)
        {
            cardName.text = data.cardName.Value;
        }
    }
}