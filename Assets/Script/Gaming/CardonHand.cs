using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Sequence = DG.Tweening.Sequence;

public class CardData
{
    public string cardName;
    public Sprite cardImage;
}

// 卡牌互動組件
public class CardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector3 originalRotation;
    private Vector3 originalScale;

    private float hoverHeight = 50f; // 懸停上浮高度
    private float hoverScale = 1.2f; // 懸停放大倍數
    private float hoverDuration = 0.2f; // 懸停動畫時長

    private bool isHovered = false;
    private bool isSelected = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        originalPosition = rectTransform.anchoredPosition;
        originalRotation = rectTransform.eulerAngles;
        originalScale = rectTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isSelected)
        {
            isHovered = true;
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale * hoverScale, hoverDuration);
            rectTransform.DOAnchorPos(originalPosition + new Vector2(0, hoverHeight), hoverDuration);
            rectTransform.DORotate(Vector3.zero, hoverDuration);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isSelected)
        {
            isHovered = false;
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale, hoverDuration);
            rectTransform.DOAnchorPos(originalPosition, hoverDuration);
            rectTransform.DORotate(originalRotation, hoverDuration);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        isSelected = !isSelected;
        if (isSelected)
        {
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale * 1.3f, 0.2f);
            rectTransform.DOAnchorPos(originalPosition + new Vector2(0, hoverHeight * 1.5f), 0.2f);
            rectTransform.DORotate(Vector3.zero, 0.2f);

            transform.parent.GetComponent<CardonHand>()?.OnCardSelected(this);
        }
        else
        {
            ResetCard();
        }
    }

    public void ResetCard()
    {
        isSelected = false;
        if (!isHovered)
        {
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale, 0.2f);
            rectTransform.DOAnchorPos(originalPosition, 0.2f);
            rectTransform.DORotate(originalRotation, 0.2f);
        }
    }

    public void SaveOriginalState()
    {
        originalPosition = rectTransform.anchoredPosition;
        originalRotation = rectTransform.eulerAngles;
        originalScale = rectTransform.localScale;
    }
}

public class CardonHand : MonoBehaviour
{
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform deckPosition;
    [SerializeField] private RectTransform handContainer;

    private int CardCount = 5;
    private static int CardMaxCount = 10;
    private CardData[] cardData = new CardData[CardMaxCount];
    private List<RectTransform> cardsInHand = new List<RectTransform>();

    private float cardSpacing = 120f;        // 卡片間距
    private float drawDuration = 0.5f;       // 抽牌動畫時長
    private float drawDelay = 0.2f;          // 抽牌延遲
    private float cardRotationRange = 15f;   // 卡片旋轉角度

    private CardInteraction currentSelectedCard;

    public void SetupCard(CardData[] data)
    {
        var index = 0;
        foreach (CardData card in data)
        {
            cardData[index++] = card;
        }
        StartCoroutine(DrawInitialCards());
    }

    private IEnumerator DrawInitialCards()
    {
        float totalWidth = (CardCount - 1) * cardSpacing;
        float startX = -totalWidth / 2;

        for (int i = 0; i < CardCount; i++)
        {
            GameObject newCard = Instantiate(cardPrefab, deckPosition.position, Quaternion.identity, handContainer);
            RectTransform cardRect = newCard.GetComponent<RectTransform>();

            CardInteraction cardInteraction = newCard.AddComponent<CardInteraction>();
            cardsInHand.Add(cardRect);

            UpdateCardVisual(newCard, cardData[i]);

            float xPos = startX + (i * cardSpacing);
            float rotation = Mathf.Lerp(cardRotationRange, -cardRotationRange, (float)i / (CardCount - 1));

            cardRect.anchoredPosition = deckPosition.anchoredPosition;

            Sequence drawSequence = DOTween.Sequence();
            drawSequence.Append(cardRect.DOAnchorPos(new Vector2(xPos, 0), drawDuration).SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DORotate(new Vector3(0, 0, rotation), drawDuration).SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DOScale(Vector3.one, drawDuration).From(Vector3.one * 0.5f).SetEase(Ease.OutBack));

            drawSequence.OnComplete(() => cardInteraction.SaveOriginalState());

            yield return new WaitForSeconds(drawDelay);
        }
    }

    private void UpdateCardVisual(GameObject cardObject, CardData data)
    {
        Image cardImage = cardObject.GetComponentInChildren<Image>();
        if (cardImage != null && data.cardImage != null)
        {
            cardImage.sprite = data.cardImage;
        }

        TMPro.TextMeshProUGUI cardName = cardObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (cardName != null)
        {
            cardName.text = data.cardName;
        }
    }

    public void OnCardSelected(CardInteraction card)
    {
        if (currentSelectedCard != null && currentSelectedCard != card)
        {
            currentSelectedCard.ResetCard();
        }
        currentSelectedCard = card;
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

            card.DOAnchorPos(new Vector2(xPos, 0), 0.3f).SetEase(Ease.OutBack);
            card.DORotate(new Vector3(0, 0, rotation), 0.3f).SetEase(Ease.OutBack);
        }
    }
}
