using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class CardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector3 originalRotation;
    private Vector3 originalScale;
    private bool isHovered = false;
    private bool isSelected = false;

    [SerializeField] private float hoverHeight = 50f;
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            SaveOriginalState();
        }
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
        ToggleSelected();
    }

    private void ToggleSelected()
    {
        isSelected = !isSelected;

        if (isSelected)
        {
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale * 1.3f, 0.2f);
            rectTransform.DOAnchorPos(originalPosition + new Vector2(0, hoverHeight * 1.5f), 0.2f);
            rectTransform.DORotate(Vector3.zero, 0.2f);
            transform.parent.GetComponent<CardOnHand>()?.OnCardSelected(this);
        }
        else
        {
            ResetCard();
        }
    }

    public void SaveOriginalState()
    {
        originalPosition = rectTransform.anchoredPosition;
        originalRotation = rectTransform.eulerAngles;
        originalScale = rectTransform.localScale;
    }

    public void ResetCard()
    {
        isSelected = false;
        if (!isHovered)
        {
            rectTransform.DOKill();
            rectTransform.DOScale(originalScale, hoverDuration);
            rectTransform.DOAnchorPos(originalPosition, hoverDuration);
            rectTransform.DORotate(originalRotation, hoverDuration);
        }
    }

    private void OnDestroy()
    {
        rectTransform?.DOKill();
    }
}