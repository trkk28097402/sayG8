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

    private NetworkRunner runner;
    private PlayerStatus playerStatus;

    private int CardMaxCount = 10;
    private List<RectTransform> cardsInHand = new List<RectTransform>();
    private Dictionary<RectTransform, NetworkedCardData> cardDataMap = new Dictionary<RectTransform, NetworkedCardData>();

    private float drawDuration = 0.5f;
    private float drawDelay = 0.2f;

    private CardInteraction currentSelectedCard;

    private void Awake()
    {

    }

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("CardOnHand Spawned started");

        // 使用協程來確保所有必要組件都已初始化
        StartCoroutine(InitializeAfterSpawn());
    }

    private IEnumerator InitializeAfterSpawn()
    {
        // 等待找到 NetworkRunner
        while (runner == null)
        {
            runner = FindObjectOfType<NetworkRunner>();
            if (runner == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        Debug.Log($"Found NetworkRunner, LocalPlayer: {runner.LocalPlayer}");

        // 等待找到 GameManager
        while (GameManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log("Found GameManager");

        // 註冊到 GameManager
        GameManager.Instance.RegisterPlayerCard(runner.LocalPlayer, this);
        Debug.Log($"Attempted to register player {runner.LocalPlayer}");

        // 註冊玩家狀態事件
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

        // 清理資源
        foreach (var card in cardsInHand)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
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
        if (cardsInHand.Count >= CardMaxCount) return;

        Vector2 startPos = deckPosition.anchoredPosition;
        CreateAndAnimateCard(cardData, startPos, cardsInHand.Count);
        RearrangeCards();
    }

    public void HandleCardRemoved(int index)
    {
        Debug.Log($"HandleCardRemoved called with index: {index}");
        if (index >= 0 && index < cardsInHand.Count)
        {
            Debug.Log("Index is valid, removing card");
            var cardRect = cardsInHand[index];
            if (cardDataMap.ContainsKey(cardRect))
            {
                cardDataMap.Remove(cardRect);
            }
            Destroy(cardRect.gameObject);
            cardsInHand.RemoveAt(index);
            RearrangeCards();
        }
        else
        {
            Debug.LogError($"Invalid index: {index}, cardsInHand.Count: {cardsInHand.Count}");
        }
    }

    // 新增一個方法來確保玩家狀態也更新
    public void RemoveCard(int index)
    {
        if (playerStatus != null)
        {
            playerStatus.currentdeck.In_Hand_Count--;
        }
        HandleCardRemoved(index);
    }

    public void HandleDeckShuffled()
    {
        // 可以添加洗牌動畫效果
        Debug.Log("Deck has been shuffled");
    }

    [SerializeField] private float cardSpacing = 200f;        // 減少間距讓卡片重疊
    [SerializeField] private float cardRotationRange = 45f;  // 扇形展開角度
    [SerializeField] private float defaultYPosition = 0f;  // 手牌基準Y座標
    [SerializeField] private float cardForwardTilt = 5f;     // 向前傾斜角度

    private IEnumerator DrawInitialCards(NetworkedCardData[] cards)
    {
        Debug.Log("抽牌動畫啟動!");
        if (handContainer == null || deckPosition == null)
        {
            Debug.LogError("Required references are missing!");
            yield break;
        }

        float totalWidth = (cards.Length - 1) * cardSpacing;
        float startX = -totalWidth / 2;

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
        var cardInteraction = cardObject.GetComponent<CardInteraction>();

        if (cardRect == null || cardInteraction == null)
        {
            Debug.LogError("Card prefab is missing required components!");
            Destroy(cardObject);
            return;
        }

        cardsInHand.Add(cardRect);
        cardDataMap[cardRect] = cardData;

        if (cardInteraction != null)
        {
            cardInteraction.SetCardData(cardData);
        }

        UpdateCardVisual(cardObject, cardData);

        // 設置初始位置
        cardRect.anchoredPosition = startPos;
        cardRect.localScale = Vector3.one * 0.5f;

        // 先做放大動畫
        Sequence drawSequence = DOTween.Sequence();
        drawSequence.Append(cardRect.DOScale(Vector3.one, drawDuration).SetEase(Ease.OutBack));
        drawSequence.OnComplete(() => {
            UpdateCardPositions(); // 更新所有卡片位置
        });
    }

    private void UpdateCardVisual(GameObject cardObject, NetworkedCardData data)
    {
        if (cardObject == null) return;

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

    private void UpdateCardPositions()
    {
        if (cardsInHand.Count == 0) return;

        float totalWidth = (cardsInHand.Count - 1) * cardSpacing;
        float startX = -totalWidth / 2;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            RectTransform cardRect = cardsInHand[i];
            if (cardRect != null)
            {
                float xPos = startX + (i * cardSpacing);
                float normalizedIndex = cardsInHand.Count > 1 ? (float)i / (cardsInHand.Count - 1) : 0.5f;
                // 修改這裡：把左右旋轉角度反過來
                float rotation = Mathf.Lerp(cardRotationRange, -cardRotationRange, normalizedIndex);

                // 設置基礎位置和旋轉
                Vector2 targetPosition = new Vector2(xPos, defaultYPosition);
                // 修改這裡：cardForwardTilt 改為正值，讓卡片向後傾斜
                Vector3 targetRotation = new Vector3(-cardForwardTilt, 0, rotation);

                cardRect.DOAnchorPos(targetPosition, drawDuration).SetEase(Ease.OutBack);
                cardRect.DORotate(targetRotation, drawDuration).SetEase(Ease.OutBack);

                // 更新 CardInteraction 的原始狀態
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
        // 如果已經有選中的卡片且不是同一張，先重置它
        if (currentSelectedCard != null && currentSelectedCard != card)
        {
            currentSelectedCard.ForceReset();
        }

        currentSelectedCard = card;

        // 如果有卡片被選中，將它移到特殊層級
        if (card != null && cardSelectLayer != null)
        {
            card.transform.SetParent(cardSelectLayer);
        }
    }

    // 新增這個方法
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
        float totalWidth = (cardsInHand.Count - 1) * cardSpacing;
        float startX = -totalWidth / 2;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            float xPos = startX + (i * cardSpacing);
            float rotation = Mathf.Lerp(cardRotationRange, -cardRotationRange, (float)i / (cardsInHand.Count - 1));

            RectTransform card = cardsInHand[i];
            if (card != null)
            {
                card.DOAnchorPos(new Vector2(xPos, 0), 0.3f).SetEase(Ease.OutBack);
                card.DORotateQuaternion(Quaternion.Euler(0, 0, rotation), 0.3f).SetEase(Ease.OutBack);
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