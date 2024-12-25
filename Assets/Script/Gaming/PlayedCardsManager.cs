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
    [SerializeField] private float playAnimationDuration = 0.5f;
    [SerializeField] private Vector2 centerPosition = new Vector2(0, 0);
    [SerializeField] private int maxVisibleCards = 3;
    [SerializeField] private float cardSpacing = 20f;

    [Networked, Capacity(40)]
    private NetworkArray<PlayedCardInfo> PlayedCards { get; }
    [Networked]
    private int CurrentPlayedCardCount { get; set; }
    [Networked]
    private NetworkBool IsWaitingForMoodEvaluation { get; set; }

    private List<RectTransform> playedCardObjects = new List<RectTransform>();
    private NetworkRunner runner;
    private GameManager gameManager;
    private bool isInitialized = false;
    private GameDeckDatabase gameDeckDatabase;
    private MoodEvaluator moodEvaluator;
    private Queue<PlayedCardInfo> cardProcessingQueue = new Queue<PlayedCardInfo>();
    private bool isProcessingCard = false;

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

        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            isInitialized = true;
            Debug.Log("PlayedCardsManager initialized for observer");
            yield break;
        }

        while (moodEvaluator == null)
        {
            moodEvaluator = FindObjectOfType<MoodEvaluator>();
            if (moodEvaluator == null)
            {
                Debug.Log("Waiting for MoodEvaluator to initialize...");
                yield return new WaitForSeconds(0.1f);
            }
        }

        isInitialized = true;
        Debug.Log("PlayedCardsManager initialized with MoodEvaluator");
    }

    public void PlayCard(NetworkedCardData cardData, int handIndex)
    {
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            return;
        }

        if (!isInitialized || isProcessingCard || IsWaitingForMoodEvaluation)
        {
            Debug.Log("Cannot play card now - system not ready, processing another card, or waiting for mood evaluation");
            return;
        }

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

            IsWaitingForMoodEvaluation = true;
            Rpc_NotifyCardPlayed(handIndex, player, cardId, deckId, CurrentPlayedCardCount - 1);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_NotifyMoodEvaluationComplete()
    {
        if (Object.HasStateAuthority)
        {
            IsWaitingForMoodEvaluation = false;
            TurnManager.Instance.SwitchToNextPlayer();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyCardPlayed(int handIndex, PlayerRef player, int cardId, int deckId, int cardIndex)
    {
        try
        {
            Debug.Log($"Card played notification received: Player {player}, Card {cardId}, Index {cardIndex}");

            // 處理本地玩家的手牌移除
            if (player == runner.LocalPlayer)
            {
                if (GameManager.Instance.localPlayerCards.TryGetValue(player, out var playerCard))
                {
                    Debug.Log($"Removing card {handIndex} from player {player}'s hand");
                    playerCard.HandleCardRemoved(handIndex);
                }
            }

            PlayedCardInfo cardInfo = new PlayedCardInfo
            {
                PlayerRef = player,
                CardId = cardId,
                DeckId = deckId
            };

            cardProcessingQueue.Enqueue(cardInfo);

            if (!isProcessingCard)
            {
                StartCoroutine(ProcessCardQueue());
            }

            // 評估氣氛值（只在本地玩家出牌時）
            if (moodEvaluator != null && player == runner.LocalPlayer)
            {
                var deckData = gameDeckDatabase.GetDeckById(deckId);
                NetworkedCardData cardData = new NetworkedCardData
                {
                    cardId = cardId,
                    cardName = $"Card {cardId + 1}",
                    imagePath = $"{deckData.deck_path}/{cardId + 1}"
                };
                moodEvaluator.OnCardPlayed(cardData, player);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in Rpc_NotifyCardPlayed: {e.Message}\n{e.StackTrace}");
            isProcessingCard = false;
        }
    }

    private IEnumerator ProcessCardQueue()
    {
        if (isProcessingCard)
        {
            yield break;
        }

        isProcessingCard = true;

        while (cardProcessingQueue.Count > 0)
        {
            
            PlayedCardInfo cardInfo = cardProcessingQueue.Dequeue();
            yield return StartCoroutine(CreatePlayedCardCoroutine(cardInfo));
            yield return new WaitForSeconds(0.1f);
        }

        isProcessingCard = false;
    }

    private IEnumerator CreatePlayedCardCoroutine(PlayedCardInfo cardInfo)
    {
        if (!isInitialized) yield break;

        GameObject cardObj = null;
        RectTransform cardRect = null;

        try
        {
            cardObj = Instantiate(playedCardPrefab, PlayArea);
            cardRect = cardObj.GetComponent<RectTransform>();

            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);

            GameDeckData deckData = gameDeckDatabase.GetDeckById(cardInfo.DeckId);
            NetworkedCardData cardData = new NetworkedCardData
            {
                cardId = cardInfo.CardId,
                cardName = $"Card {cardInfo.CardId + 1}",
                imagePath = $"{deckData.deck_path}/{cardInfo.CardId + 1}"
            };

            UpdateCardVisual(cardObj, cardData);

            Vector2 startPos = GetStartPosition(cardInfo.PlayerRef);
            cardRect.anchoredPosition = startPos;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting up card: {e.Message}");
            if (cardObj != null) Destroy(cardObj);
            yield break;
        }

        // 處理舊卡牌的移除
        if (playedCardObjects.Count >= maxVisibleCards)
        {
            var oldestCard = playedCardObjects[0];
            playedCardObjects.RemoveAt(0);

            if (oldestCard != null)
            {
                oldestCard.DOAnchorPos(new Vector2(-1000, 0), playAnimationDuration * 0.5f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => {
                        if (oldestCard != null)
                        {
                            Destroy(oldestCard.gameObject);
                        }
                    });
            }

            yield return new WaitForSeconds(playAnimationDuration * 0.5f);
        }

        // 添加新卡牌
        playedCardObjects.Add(cardRect);

        // 執行動畫
        yield return cardRect.DOAnchorPos(centerPosition, playAnimationDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .WaitForCompletion();

        yield return new WaitForSeconds(0.2f);

        RearrangeAllCards();

        yield return new WaitForSeconds(playAnimationDuration);
    }

    private void RearrangeAllCards()
    {
        if (playedCardObjects == null || playedCardObjects.Count == 0) return;

        float cardWidth = playedCardPrefab.GetComponent<RectTransform>().rect.width;
        float totalWidth = (cardWidth + cardSpacing) * playedCardObjects.Count - cardSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < playedCardObjects.Count; i++)
        {
            if (playedCardObjects[i] != null)
            {
                Vector2 newPos = new Vector2(startX + (cardWidth + cardSpacing) * i, 0);
                playedCardObjects[i].DOAnchorPos(newPos, playAnimationDuration)
                    .SetEase(Ease.OutBack);
            }
        }
    }

    private Vector2 GetStartPosition(PlayerRef playerRef)
    {
        return playerRef == Runner.LocalPlayer
            ? new Vector2(0, -300)
            : new Vector2(0, 300);
    }

    private void UpdateCardVisual(GameObject cardObject, NetworkedCardData data)
    {
        if (cardObject == null) return;

        Image[] images = cardObject.GetComponentsInChildren<Image>();
        Image cardImage = images.Length > 0 ? images[images.Length - 1] : null;
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

    private void OnRectTransformDimensionsChange()
    {
        if (isInitialized && playedCardObjects.Count > 0 && !isProcessingCard)
        {
            RearrangeAllCards();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;
    }

    private void OnDestroy()
    {
        try
        {
            DOTween.KillAll();
            foreach (var cardRect in playedCardObjects)
            {
                if (cardRect != null)
                {
                    Destroy(cardRect.gameObject);
                }
            }
            playedCardObjects.Clear();
            cardProcessingQueue.Clear();
            isProcessingCard = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnDestroy: {e.Message}\n{e.StackTrace}");
        }
    }
}