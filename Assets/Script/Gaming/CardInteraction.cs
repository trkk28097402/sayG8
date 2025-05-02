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
    private Vector2 basePosition;
    private Vector3 baseRotation;
    private Vector3 baseScale;
    private bool isHovered = false;
    public bool isSelected = false;

    [SerializeField] private float hoverHeight = 100f;
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;

    AudioManagerClassroom audioManagerClassroom;

    private void Update()
    {
    }

    public void SetCardData(NetworkedCardData data)
    {
        cardData = data;
    }

    private Transform originalParent;

    private void Awake()
    {
        audioManagerClassroom = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManagerClassroom>();//yu
        rectTransform = GetComponent<RectTransform>();
        originalParent = transform.parent;
        if (rectTransform != null)
        {
            SaveOriginalState();
        }
    }

    public void SetHoverState(bool state)
    {
        if (isSelected) return;

        // Always update the isHovered flag correctly
        isHovered = state;

        // Stop any running animations
        rectTransform.DOKill();

        if (state)
        {
            Debug.Log($"Card {cardData.cardName.Value} set to hover state");
            // Apply hover state animations
            rectTransform.DOScale(Vector3.one * hoverScale, hoverDuration);
            rectTransform.DOAnchorPos(basePosition + new Vector2(0, hoverHeight), hoverDuration);
            rectTransform.DORotate(Vector3.zero, hoverDuration);
        }
        else
        {
            Debug.Log($"Card {cardData.cardName.Value} reset from hover state");
            // Make sure to reset to original positions
            rectTransform.DOScale(baseScale, hoverDuration);
            rectTransform.DOAnchorPos(basePosition, hoverDuration);
            rectTransform.DORotate(baseRotation, hoverDuration);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;

        Debug.Log($"Card {cardData.cardName.Value} entered");
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

    public void ToggleSelected()
    {
        audioManagerClassroom.PlaySoundEffectClassroom(audioManagerClassroom.CardTouchSound);

        // Toggle selection state
        isSelected = !isSelected;

        if (isSelected)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                rectTransform.DOKill();
                originalParent = transform.parent;
                originalPosition = rectTransform.anchoredPosition;
                originalRotation = rectTransform.eulerAngles;
                originalScale = rectTransform.localScale;

                Sequence selectSequence = DOTween.Sequence();

                selectSequence.Append(rectTransform.DOAnchorPos(new Vector2(0, 200), 0.3f).SetEase(Ease.OutCubic));
                selectSequence.Join(rectTransform.DOScale(Vector3.one * 1.5f, 0.3f).SetEase(Ease.OutCubic));
                selectSequence.Join(rectTransform.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutCubic));

                var cardOnHand = originalParent.GetComponent<CardOnHand>();
                if (cardOnHand != null)
                {
                    cardOnHand.OnCardSelected(this);
                }

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
            basePosition = rectTransform.anchoredPosition;
            baseRotation = rectTransform.eulerAngles;
            baseScale = rectTransform.localScale;

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

        rectTransform.anchoredPosition = basePosition;
        rectTransform.eulerAngles = baseRotation;
        rectTransform.localScale = baseScale;
    }

    public void PlayCard()
    {
        // Check if game is over
        var moodEvaluator = FindObjectOfType<MoodEvaluator>();
        if (moodEvaluator != null && moodEvaluator.IsGameFinished())
        {
            Debug.Log("Cannot play card - game is over");
            return;
        }

        audioManagerClassroom.PlaySoundEffectClassroom(audioManagerClassroom.CardUseSound);//yu
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

            // Notify CardOnHand to handle card played
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