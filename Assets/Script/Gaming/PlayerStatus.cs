using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct InGameDeck : INetworkStruct
{
    public int In_Hand_Count; // ��P�ƥ�
    public int Deck_Left_Count; // �P�ճѾl�ƥ�
    public int id; // �P��id

    // �ϥΩT�w�j�p���ƲըӦs�x�P�ն���
    [Networked, Capacity(40)]
    public NetworkArray<int> CardOrder { get; }

    // ��e��P��m������
    public int CurrentIndex { get; set; }

    // ��l�ƵP�ն��Ǫ���k
    public void InitializeCardOrder(List<int> initialOrder)
    {
        Debug.Log("��l�Ƭ~�P!");
        for (int i = 0; i < initialOrder.Count && i < CardOrder.Length; i++)
        {
            CardOrder.Set(i, initialOrder[i]);
        }
        CurrentIndex = 0;
    }

    // ����U�@�i�P
    public int DrawNextCard()
    {
        Debug.Log("��@�i�P!");
        if (CurrentIndex >= CardOrder.Length || Deck_Left_Count <= 0)
        {
            return -1; // �P�դw��
        }

        int nextCard = CardOrder.Get(CurrentIndex);
        CurrentIndex++;
        Deck_Left_Count--;
        return nextCard;
    }

    // �~�P��k
    public void Shuffle()
    {
        List<int> tempList = new List<int>();
        for (int i = CurrentIndex; i < CardOrder.Length; i++)
        {
            tempList.Add(CardOrder.Get(i));
        }

        // Fisher-Yates shuffle
        for (int i = tempList.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = tempList[i];
            tempList[i] = tempList[j];
            tempList[j] = temp;
        }

        // �N�~�L���P��^�}�C
        for (int i = 0; i < tempList.Count; i++)
        {
            CardOrder.Set(CurrentIndex + i, tempList[i]);
        }
    }
}

public class PlayerStatus : NetworkBehaviour
{
    private NetworkRunner runner;
    public int totalcard = 40;
    public InGameDeck currentdeck;
    public static PlayerStatus Instance { get; private set; }

    GameDeckDatabase gameDeckDatabase;

    // �w�q�ƥ�
    public event Action<NetworkedCardData[]> OnInitialHandDrawn;
    public event Action<NetworkedCardData> OnCardDrawn;
    public event Action<int> OnCardRemoved;
    public event Action OnDeckShuffled;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void Spawned()
    {
        base.Spawned();

        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
            return;
        }

        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.RegisterPlayerStatus(runner.LocalPlayer, this);
            Debug.Log($"�w���U���a���A�� GameManager: Player {runner.LocalPlayer}");
        }
        else
        {
            Debug.LogError("GameManager not found in scene!");
        }

        Debug.Log($"Runner state: {runner.State}");
    }

    public void Initialized_Cards()
    {
        gameDeckDatabase = new GameDeckDatabase();
        currentdeck = new InGameDeck();
        currentdeck.id = GameDeckManager.Instance.GetPlayerDeck(runner.LocalPlayer);
        currentdeck.In_Hand_Count = 5;
        currentdeck.Deck_Left_Count = 35;

        // ��l�ƨé���_�l��P
        List<int> initialOrder = new List<int>();
        for (int i = 0; i < totalcard; i++)
        {
            initialOrder.Add(i);
        }
        currentdeck.InitializeCardOrder(initialOrder);
        currentdeck.Shuffle();

        DrawInitialHand();
    }

    public void DrawInitialHand()
    {
        // �p�G�S���q�\�̡A���ݤ@�U�A��
        if (OnInitialHandDrawn == null)
        {
            Debug.Log("���ݨƥ�q�\��...");
            StartCoroutine(WaitForSubscriberAndDraw());
            return;
        }

        NetworkedCardData[] initialHand = new NetworkedCardData[5];
        for (int i = 0; i < 5; i++)
        {
            int cardId = currentdeck.DrawNextCard();
            initialHand[i] = GetCardData(cardId);
        }
        OnInitialHandDrawn?.Invoke(initialHand);
    }

    private IEnumerator WaitForSubscriberAndDraw()
    {
        // ���ݪ��즳�q�\��
        while (OnInitialHandDrawn == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // ���q�\�̤F�A�����P
        DrawInitialHand();
    }

    public void DrawCard()
    {
        if (currentdeck.Deck_Left_Count > 0)
        {
            int cardId = currentdeck.DrawNextCard();
            NetworkedCardData cardData = GetCardData(cardId);
            OnCardDrawn?.Invoke(cardData);
            currentdeck.In_Hand_Count++;
        }
    }

    private NetworkedCardData GetCardData(int cardid)
    {
        GameDeckData deckData = gameDeckDatabase.GetDeckById(currentdeck.id);
        return new NetworkedCardData
        {
            cardId = cardid,
            cardName = $"Card {cardid}",  // �ϥ� NetworkString
            imagePath = $"{deckData.deck_path}/{cardid + 1}"  // �ϥ� NetworkString
        };
    }

}