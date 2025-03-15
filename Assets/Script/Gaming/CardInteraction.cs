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
    private Vector2 basePosition;    // ·sјWЎG«O¦s°тҐ»¦мёm
    private Vector3 baseRotation;    // ·sјWЎG«O¦s°тҐ»±ЫВа
    private Vector3 baseScale;       // ·sјWЎG«O¦s°тҐ»БY©с
    private bool isHovered = false;
    public bool isSelected = false;

    [SerializeField] private float hoverHeight = 100f;
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;

    AudioManagerClassroom audioManagerClassroom;//yu

    private void Update()
    {
        // This individual card Enter key handler is removed
        // All keyboard handling is now done in CardOnHand to ensure
        // proper two-step process (first Enter selects, second Enter plays)
    }

    // і]ёm¬O§_¬°№кЕйҐd
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

    // ·sјW: і]ёmҐdµPЄєhoverЄ¬єA
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

    // Make this method public so it can be called from CardOnHand
    public void ToggleSelected()
    {
        audioManagerClassroom.PlaySoundEffectClassroom(audioManagerClassroom.CardTouchSound);//yu
        isSelected = !isSelected;
        if (isSelected)
        {
            // Іѕ¦Ьµe­±­pєв¤¤ҐЎ¦мёm
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                rectTransform.DOKill(); // °±¤о©Т¦і¶i¦ж¤¤Єє°Кµe

                // «O¦s­м©lёк°T
                originalParent = transform.parent;
                originalPosition = rectTransform.anchoredPosition;
                originalRotation = rectTransform.eulerAngles;
                originalScale = rectTransform.localScale; // ­«·s«O¦s·н«e¤Ш¤oЎAБЧ§K­«Е|©с¤j

                Sequence selectSequence = DOTween.Sequence();

                // °х¦ж°Кµe¦Ь¤¤ҐЎЎA©с¤jЎAЁГ¦^Ґї
                selectSequence.Append(rectTransform.DOAnchorPos(new Vector2(0, 200), 0.3f).SetEase(Ease.OutCubic));
                selectSequence.Join(rectTransform.DOScale(Vector3.one * 1.5f, 0.3f).SetEase(Ease.OutCubic)); // ЁПҐО©T©wЄє©с¤jјЖ­И
                selectSequence.Join(rectTransform.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutCubic));

                var cardOnHand = originalParent.GetComponent<CardOnHand>();
                if (cardOnHand != null)
                {
                    cardOnHand.OnCardSelected(this);
                }

                // ЅT«OҐdµP¦bіМ¤Wјh
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
            // «O¦s°тҐ»Є¬єAЎ]¤Ј·|іQhoverјvЕTЄєЄ¬єAЎ^
            basePosition = rectTransform.anchoredPosition;
            baseRotation = rectTransform.eulerAngles;
            baseScale = rectTransform.localScale;

            // ·н«eЄ¬єAµҐ©у°тҐ»Є¬єA
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
            // ЁПҐО°тҐ»Є¬єA¦У¤Ј¬OҐiЇаіQhoverјvЕTЄєoriginalPosition
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

        // ЄЅ±µЁПҐО°тҐ»Є¬єA
        rectTransform.anchoredPosition = basePosition;
        rectTransform.eulerAngles = baseRotation;
        rectTransform.localScale = baseScale;
    }

    public void PlayCard()
    {
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

            // іqЄѕ CardOnHand іBІzҐdµPІѕ°Ј
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