using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class CardOnHand : NetworkBehaviour
{
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform deckPosition;
    [SerializeField] private RectTransform handContainer;
    [SerializeField] private Transform cardSelectLayer;
    [SerializeField] private float defaultYPosition = 0f;
    [SerializeField] private float drawDuration = 0.5f;
    [SerializeField] private float drawDelay = 0.2f;

    private NetworkRunner runner;
    private PlayerStatus playerStatus;
    private const int MaxCards = 5;
    private List<RectTransform> cardsInHand = new List<RectTransform>();
    private Dictionary<RectTransform, NetworkedCardData> cardDataMap = new Dictionary<RectTransform, NetworkedCardData>();
    private CardInteraction currentSelectedCard;

    public bool IsInitialized { get; private set; }

    protected void CompleteInitialization()
    {
        IsInitialized = true;
    }

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("CardOnHand Spawned started");

        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            if (handContainer != null)
                handContainer.gameObject.SetActive(false);
            return;
        }

        StartCoroutine(InitializeAfterSpawn());
    }

    private IEnumerator InitializeAfterSpawn()
    {
        while (runner == null)
        {
            runner = FindObjectOfType<NetworkRunner>();
            if (runner == null) yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"Found NetworkRunner, LocalPlayer: {runner.LocalPlayer}");

        while (GameManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log("Found GameManager");

        GameManager.Instance.RegisterPlayerCard(runner.LocalPlayer, this);
        Debug.Log($"Attempted to register player {runner.LocalPlayer}");

        while (PlayerStatus.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        playerStatus = PlayerStatus.Instance;
        Debug.Log("註冊玩家狀態事件");
        playerStatus.OnInitialHandDrawn += HandleInitialHand;
        playerStatus.OnCardDrawn += HandleNewCard;
        playerStatus.OnCardRemoved += HandleCardRemoved;
        playerStatus.OnDeckShuffled += HandleDeckShuffled;

        CompleteInitialization();
    }

    private void OnDestroy()
    {
        if (playerStatus != null)
        {
            playerStatus.OnInitialHandDrawn -= HandleInitialHand;
            playerStatus.OnCardDrawn -= HandleNewCard;
            playerStatus.OnCardRemoved -= HandleCardRemoved;
            playerStatus.OnDeckShuffled -= HandleDeckShuffled;
        }

        foreach (var card in cardsInHand)
        {
            if (card != null) Destroy(card.gameObject);
        }
        cardsInHand.Clear();
        cardDataMap.Clear();
    }

    public void HandleInitialHand(NetworkedCardData[] cards)
    {
        Debug.Log("準備抽牌動畫");
        StartCoroutine(DrawInitialCards(cards));
    }

    public void HandleNewCard(NetworkedCardData cardData)
    {
        if (cardsInHand.Count >= MaxCards) return;

        Vector2 startPos = deckPosition.anchoredPosition;
        CreateAndAnimateCard(cardData, startPos, cardsInHand.Count);
        RearrangeCards();
    }

    public void HandleCardPlayed(int cardIndex)
    {
        Debug.Log($"HandleCardPlayed: Card played at index {cardIndex}");

        if (playerStatus != null)
        {
            playerStatus.currentdeck.In_Hand_Count--;
        }

        StartCoroutine(DrawCardAfterDelay());
    }

    public void HandleCardRemoved(int index)
    {
        if (index >= 0 && index < cardsInHand.Count)
        {
            var cardRect = cardsInHand[index];
            if (cardDataMap.ContainsKey(cardRect))
            {
                cardDataMap.Remove(cardRect);
            }

            if (cardRect != null)
            {
                Destroy(cardRect.gameObject);
            }

            cardsInHand.RemoveAt(index);
            RearrangeCards();
        }
    }

    private IEnumerator DrawCardAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (playerStatus != null && playerStatus.currentdeck.Deck_Left_Count > 0)
        {
            Debug.Log("Drawing new card after play");
            playerStatus.DrawCard();
        }
    }

    public void HandleDeckShuffled()
    {
        Debug.Log("Deck has been shuffled");
    }

    private IEnumerator DrawInitialCards(NetworkedCardData[] cards)
    {
        if (handContainer == null)
        {
            Debug.LogError("Required references are missing!");
            yield break;
        }

        for (int i = 0; i < cards.Length; i++)
        {
            Vector2 startPos = deckPosition.anchoredPosition;
            yield return new WaitForSeconds(drawDelay);
            CreateAndAnimateCard(cards[i], startPos, i);
        }
    }

    private void CreateAndAnimateCard(NetworkedCardData cardData, Vector2 startPos, int index)
    {
        var cardObject = Instantiate(cardPrefab, deckPosition.position, Quaternion.identity, handContainer);
        var cardRect = cardObject.GetComponent<RectTransform>();
        Debug.Log($"Card {cardData.cardName.Value} rect size: {cardRect.rect.size}");
        Debug.Log($"Card {cardData.cardName.Value} position: {cardRect.anchoredPosition}");

        var cardInteraction = cardObject.GetComponent<CardInteraction>();

        if (cardRect == null || cardInteraction == null)
        {
            Debug.LogError("Card prefab is missing required components!");
            Destroy(cardObject);
            return;
        }

        cardsInHand.Add(cardRect);
        cardDataMap[cardRect] = cardData;

        cardInteraction.SetCardData(cardData);
        UpdateCardVisual(cardObject, cardData);

        cardRect.anchoredPosition = startPos;
        cardRect.localScale = Vector3.one * 0.5f;

        Sequence drawSequence = DOTween.Sequence();
        drawSequence.Append(cardRect.DOScale(Vector3.one, drawDuration).SetEase(Ease.OutBack));
        drawSequence.OnComplete(() => {
            UpdateCardPositions();
        });
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
        }

        TMPro.TextMeshProUGUI cardName = cardObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (cardName != null)
        {
            cardName.text = data.cardName.Value;
        }
    }

    private void UpdateCardPositions()
    {
        if (cardsInHand.Count == 0) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Cannot find Canvas in parents!");
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width;

        float usableWidth = canvasWidth * 0.9f; 
        float cardSpacing = usableWidth / (MaxCards - 1); 

        float startX = -usableWidth / 2;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            RectTransform cardRect = cardsInHand[i];
            if (cardRect != null)
            {
                float xPos = cardsInHand.Count == 1 ?
                    0 : 
                    startX + (i * cardSpacing); 

                Vector2 targetPosition = new Vector2(xPos, defaultYPosition);

                cardRect.SetSiblingIndex(i);
                cardRect.DOKill();
                cardRect.DOAnchorPos(targetPosition, drawDuration).SetEase(Ease.OutBack);
                cardRect.DORotate(Vector3.zero, drawDuration).SetEase(Ease.OutBack);

                var cardInteraction = cardRect.GetComponent<CardInteraction>();
                if (cardInteraction != null)
                {
                    DOVirtual.DelayedCall(drawDuration, () => {
                        cardInteraction.SaveOriginalState();
                    });
                }
            }
        }
    }

    public void OnCardSelected(CardInteraction card)
    {
        if (currentSelectedCard != null && currentSelectedCard != card)
        {
            currentSelectedCard.ForceReset();
        }

        currentSelectedCard = card;

        if (card != null && cardSelectLayer != null)
        {
            card.transform.SetParent(cardSelectLayer);
        }
    }

    public void ReturnCardToHand(CardInteraction card)
    {
        if (card != null)
        {
            card.transform.SetParent(handContainer);
            if (card == currentSelectedCard)
            {
                currentSelectedCard = null;
            }
            UpdateCardPositions();
        }
    }

    public void RearrangeCards()
    {
        if (cardsInHand.Count == 0) { Debug.Log("cardsInHand count = 0"); return; }

        // 同樣使用 Canvas 寬度
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.Log("canvas is null!"); return; }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width;
        float slotWidth = canvasWidth / MaxCards;
        Debug.Log($"canvaswidth:{canvasWidth}");

        float startX = -canvasWidth / 2 + slotWidth / 2;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            RectTransform card = cardsInHand[i];
            if (card != null)
            {
                float xPos = startX + (i * slotWidth);

                card.DOKill();
                card.SetSiblingIndex(i);
                card.DOAnchorPos(new Vector2(xPos, 0), 0.3f).SetEase(Ease.OutBack);
                card.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutBack);
            }
        }
    }

    public NetworkedCardData GetCardData(int index)
    {
        if (index >= 0 && index < cardsInHand.Count)
        {
            return cardDataMap[cardsInHand[index]];
        }
        throw new System.IndexOutOfRangeException("Card index out of range");
    }

    public int GetCardIndex(CardInteraction card)
    {
        var cardTransform = card.GetComponent<RectTransform>();
        return cardsInHand.IndexOf(cardTransform);
    }
}