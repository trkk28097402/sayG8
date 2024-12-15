using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class CardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private NetworkedCardData cardData;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector3 originalRotation;
    private Vector3 originalScale;
    private Vector2 basePosition;    // 新增：記錄基本位置
    private Vector3 baseRotation;    // 新增：記錄基本旋轉
    private Vector3 baseScale;       // 新增：記錄基本縮放
    private bool isHovered = false;
    public bool isSelected = false;

    [SerializeField] private float hoverHeight = 50f;
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;

    private void Update()
    {
        // 如果卡牌被選中且按下 Enter
        if (isSelected && Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Enter pressed on selected card");
            PlayCard();
        }
    }

    // 設定我是哪張卡
    public void SetCardData(NetworkedCardData data)
    {
        cardData = data;
    }

    private Transform originalParent;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalParent = transform.parent;
        if (rectTransform != null)
        {
            SaveOriginalState();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;

        isHovered = true;
        rectTransform.DOKill();
        rectTransform.DOScale(Vector3.one * hoverScale, hoverDuration);
        rectTransform.DOAnchorPos(basePosition + new Vector2(0, hoverHeight), hoverDuration);
        rectTransform.DORotate(Vector3.zero, hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return;

        isHovered = false;
        rectTransform.DOKill();
        rectTransform.DOScale(baseScale, hoverDuration);
        rectTransform.DOAnchorPos(basePosition, hoverDuration);
        rectTransform.DORotate(baseRotation, hoverDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleSelected();
    }

    private void ToggleSelected()
    {
        isSelected = !isSelected;
        if (isSelected)
        {
            // 獲取畫布計算中心位置
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                rectTransform.DOKill(); // 停止所有進行中的動畫

                // 保存原始資訊
                originalParent = transform.parent;
                originalPosition = rectTransform.anchoredPosition;
                originalRotation = rectTransform.eulerAngles;
                originalScale = rectTransform.localScale; // 重新保存當前比例，避免重複放大

                Sequence selectSequence = DOTween.Sequence();

                // 移動到畫面中心，放大，並回正
                selectSequence.Append(rectTransform.DOAnchorPos(Vector2.zero, 0.3f).SetEase(Ease.OutCubic));
                selectSequence.Join(rectTransform.DOScale(Vector3.one * 1.5f, 0.3f).SetEase(Ease.OutCubic)); // 使用固定的放大倍數
                selectSequence.Join(rectTransform.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutCubic));

                var cardOnHand = originalParent.GetComponent<CardOnHand>();
                if (cardOnHand != null)
                {
                    cardOnHand.OnCardSelected(this);
                }

                // 確保卡片在最上層
                transform.SetAsLastSibling();
            }
        }
        else
        {
            ResetCard();
        }
    }

    public void SaveOriginalState()
    {
        if (rectTransform != null)
        {
            // 保存基本狀態（不會被hover影響的狀態）
            basePosition = rectTransform.anchoredPosition;
            baseRotation = rectTransform.eulerAngles;
            baseScale = rectTransform.localScale;

            // 當前狀態等於基本狀態
            originalPosition = basePosition;
            originalRotation = baseRotation;
            originalScale = baseScale;
        }
    }

    public void ResetCard()
    {
        isSelected = false;
        if (!isHovered)
        {
            var cardOnHand = originalParent.GetComponent<CardOnHand>();
            if (cardOnHand != null)
            {
                cardOnHand.ReturnCardToHand(this);
            }

            rectTransform.DOKill();
            Sequence resetSequence = DOTween.Sequence();
            // 使用基本狀態而不是可能被hover影響的originalPosition
            resetSequence.Append(rectTransform.DOScale(baseScale, hoverDuration).SetEase(Ease.OutCubic));
            resetSequence.Join(rectTransform.DOAnchorPos(basePosition, hoverDuration).SetEase(Ease.OutCubic));
            resetSequence.Join(rectTransform.DORotate(baseRotation, hoverDuration).SetEase(Ease.OutCubic));
        }
    }

    public void ForceReset()
    {
        isHovered = false;
        isSelected = false;
        rectTransform.DOKill();

        // 直接使用基本狀態
        rectTransform.anchoredPosition = basePosition;
        rectTransform.eulerAngles = baseRotation;
        rectTransform.localScale = baseScale;
    }

    public void PlayCard()
    {
        Debug.Log("PlayCard method called");
        var cardOnHand = GetComponentInParent<CardOnHand>();
        if (cardOnHand == null)
        {
            Debug.LogError("CardOnHand not found");
            return;
        }

        int cardIndex = cardOnHand.GetCardIndex(this);
        Debug.Log($"Card index: {cardIndex}");

        var cardData = cardOnHand.GetCardData(cardIndex);
        Debug.Log($"Card data retrieved: {cardData.cardName}");

        var playedCardsManager = FindObjectOfType<PlayedCardsManager>();
        if (playedCardsManager != null)
        {
            Debug.Log("Found PlayedCardsManager, calling PlayCard");
            playedCardsManager.PlayCard(cardData, cardIndex);

            // 通知 CardOnHand 處理抽牌邏輯
            cardOnHand.HandleCardPlayed(cardIndex);
        }
        else
        {
            Debug.LogError("PlayedCardsManager not found");
        }
    }

    private void OnDestroy()
    {
        rectTransform?.DOKill();
    }
}