using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class CardOnHand : NetworkBehaviour
{
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform deckPosition;
    [SerializeField] private RectTransform handContainer;

    private NetworkRunner runner;

    private int CardCount = 5;
    private static int CardMaxCount = 10;
    [Networked, Capacity(10)] private NetworkArray<NetworkedCardData> networkedCardData => default;
    private List<RectTransform> cardsInHand = new List<RectTransform>();

    private float cardSpacing = 200f;
    private float drawDuration = 0.5f;
    private float drawDelay = 0.2f;
    private float cardRotationRange = 15f;

    private CardInteraction currentSelectedCard;

    public override void Spawned()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
            return;
        }
        Debug.Log($"Runner state: {runner.State}, LocalPlayer: {runner.LocalPlayer}");

        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.RegisterPlayerCard(runner.LocalPlayer, this);
            Debug.Log($"Registering card for player {runner.LocalPlayer}");
        }
    }

    public void SetupCards(NetworkedCardData[] cards)
    {
        if (cards == null || cards.Length == 0)
        {
            Debug.LogError("Attempted to setup cards with null or empty array");
            return;
        }

        // Store card data
        for (int i = 0; i < Mathf.Min(cards.Length, CardMaxCount); i++)
        {
            networkedCardData.Set(i, cards[i]);
        }

        // Start drawing animation
        StartCoroutine(DrawInitialCards());
    }

    private IEnumerator DrawInitialCards()
    {
        if (handContainer == null || deckPosition == null)
        {
            Debug.LogError("Required references are missing!");
            yield break;
        }

        float totalWidth = (CardCount - 1) * cardSpacing;
        float startX = -totalWidth / 2;

        for (int i = 0; i < CardCount; i++)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("Card prefab is null!");
                yield break;
            }

            // Instantiate and setup card
            var cardObject = Instantiate(cardPrefab, deckPosition.position, Quaternion.identity, handContainer);
            var cardRect = cardObject.GetComponent<RectTransform>();
            var cardInteraction = cardObject.GetComponent<CardInteraction>();

            if (cardRect == null || cardInteraction == null)
            {
                Debug.LogError("Card prefab is missing required components!");
                Destroy(cardObject);
                continue;
            }

            cardsInHand.Add(cardRect);

            // Update visual elements
            if (i < networkedCardData.Length)
            {
                UpdateCardVisual(cardObject, networkedCardData[i]);
            }

            // Calculate position and rotation
            float xPos = startX + (i * cardSpacing);
            float rotation = Mathf.Lerp(cardRotationRange, -cardRotationRange, (float)i / (CardCount - 1));

            // Set initial position
            cardRect.anchoredPosition = deckPosition.anchoredPosition;

            // Create and store reference to card interaction component
            var cardRef = cardInteraction;

            // Create animation sequence
            Sequence drawSequence = DOTween.Sequence();

            // Add animations to sequence
            drawSequence.Append(cardRect.DOAnchorPos(new Vector2(xPos, 0), drawDuration).SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DORotate(new Vector3(0, 0, rotation), drawDuration).SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DOScale(Vector3.one, drawDuration).From(Vector3.one * 0.5f).SetEase(Ease.OutBack));

            // Only save original state after animation completes and if component still exists
            drawSequence.OnComplete(() => {
                if (cardRef != null && cardRef.gameObject != null)
                {
                    cardRef.SaveOriginalState();
                }
            });

            yield return new WaitForSeconds(drawDelay);
        }
    }

    private void UpdateCardVisual(GameObject cardObject, NetworkedCardData data)
    {
        if (cardObject == null) return;

        Image cardImage = cardObject.GetComponentInChildren<Image>();
        if (cardImage != null)
        {
            var sprite = Resources.Load<Sprite>(data.imagePath.ToString());
            if (sprite != null)
            {
                cardImage.sprite = sprite;
            }
            else
            {
                Debug.LogWarning($"Could not load sprite from path: {data.imagePath}");
            }
        }

        TMPro.TextMeshProUGUI cardName = cardObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (cardName != null)
        {
            cardName.text = data.cardName.ToString();
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

    // 其他方法保持不變
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