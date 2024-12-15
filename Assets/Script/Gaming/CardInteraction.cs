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
    private Vector2 basePosition;    // �s�W�G�O���򥻦�m
    private Vector3 baseRotation;    // �s�W�G�O���򥻱���
    private Vector3 baseScale;       // �s�W�G�O�����Y��
    private bool isHovered = false;
    public bool isSelected = false;

    [SerializeField] private float hoverHeight = 50f;
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;

    private void Update()
    {
        // �p�G�d�P�Q�襤�B���U Enter
        if (isSelected && Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Enter pressed on selected card");
            PlayCard();
        }
    }

    // �]�w�ڬO���i�d
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
            // ����e���p�⤤�ߦ�m
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                rectTransform.DOKill(); // ����Ҧ��i�椤���ʵe

                // �O�s��l��T
                originalParent = transform.parent;
                originalPosition = rectTransform.anchoredPosition;
                originalRotation = rectTransform.eulerAngles;
                originalScale = rectTransform.localScale; // ���s�O�s��e��ҡA�קK���Ʃ�j

                Sequence selectSequence = DOTween.Sequence();

                // ���ʨ�e�����ߡA��j�A�æ^��
                selectSequence.Append(rectTransform.DOAnchorPos(Vector2.zero, 0.3f).SetEase(Ease.OutCubic));
                selectSequence.Join(rectTransform.DOScale(Vector3.one * 1.5f, 0.3f).SetEase(Ease.OutCubic)); // �ϥΩT�w����j����
                selectSequence.Join(rectTransform.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutCubic));

                var cardOnHand = originalParent.GetComponent<CardOnHand>();
                if (cardOnHand != null)
                {
                    cardOnHand.OnCardSelected(this);
                }

                // �T�O�d���b�̤W�h
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
            // �O�s�򥻪��A�]���|�Qhover�v�T�����A�^
            basePosition = rectTransform.anchoredPosition;
            baseRotation = rectTransform.eulerAngles;
            baseScale = rectTransform.localScale;

            // ��e���A����򥻪��A
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
            // �ϥΰ򥻪��A�Ӥ��O�i��Qhover�v�T��originalPosition
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

        // �����ϥΰ򥻪��A
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

            // �q�� CardOnHand �B�z��P�޿�
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