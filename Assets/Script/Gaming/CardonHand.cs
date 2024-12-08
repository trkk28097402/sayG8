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

    private NetworkRunner runner; // runner.LocalPlayer = playerref

    private int CardCount = 5;
    private static int CardMaxCount = 10;
    [Networked, Capacity(10)] private NetworkArray<NetworkedCardData> networkedCardData => default;
    private List<RectTransform> cardsInHand = new List<RectTransform>();

    private float cardSpacing = 120f;
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
        // 存儲卡片數據
        for (int i = 0; i < cards.Length; i++)
        {
            networkedCardData.Set(i, cards[i]);
        }

        // 開始抽牌動畫
        Runner.StartCoroutine(DrawInitialCards());
    }

    private IEnumerator DrawInitialCards()
    {
        float totalWidth = (CardCount - 1) * cardSpacing;
        float startX = -totalWidth / 2;

        for (int i = 0; i < CardCount; i++)
        {
            // 使用普通的 Instantiate 而不是 Runner.Spawn
            var cardObject = Instantiate(cardPrefab, deckPosition.position, Quaternion.identity);
            cardObject.transform.SetParent(handContainer, false);

            var cardRect = cardObject.GetComponent<RectTransform>();
            cardsInHand.Add(cardRect);

            UpdateCardVisual(cardObject, networkedCardData[i]);

            float xPos = startX + (i * cardSpacing);
            float rotation = Mathf.Lerp(cardRotationRange, -cardRotationRange, (float)i / (CardCount - 1));

            cardRect.anchoredPosition = deckPosition.anchoredPosition;

            Sequence drawSequence = DOTween.Sequence();
            drawSequence.Append(cardRect.DOAnchorPos(new Vector2(xPos, 0), drawDuration).SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DORotate(new Vector3(0, 0, rotation), drawDuration).SetEase(Ease.OutBack));
            drawSequence.Join(cardRect.DOScale(Vector3.one, drawDuration).From(Vector3.one * 0.5f).SetEase(Ease.OutBack));

            var cardInteraction = cardObject.GetComponent<CardInteraction>();
            drawSequence.OnComplete(() => cardInteraction.SaveOriginalState());

            yield return new WaitForSeconds(drawDelay);
        }
    }

    private void UpdateCardVisual(GameObject cardObject, NetworkedCardData data)
    {
        Image cardImage = cardObject.GetComponentInChildren<Image>();
        if (cardImage != null)
        {
            cardImage.sprite = Resources.Load<Sprite>(data.imagePath.ToString());
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