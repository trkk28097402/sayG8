using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct InGameDeck
{
    public int In_Hand_Count; // ��P�ƥ�
    public int Deck_Left_Count; // �P�ճѾl�ƥ�
    public int id; // �P��id

    private int[] CardOrder; // ��δ��q�Ʋ�
    public int CurrentIndex { get; set; }

    // ��l�ƵP�ն��Ǫ���k
    public void InitializeCardOrder(List<int> initialOrder)
    {
        Debug.Log("��l�Ƭ~�P!");
        CardOrder = new int[40]; // �T�w�j�p��40
        for (int i = 0; i < initialOrder.Count && i < CardOrder.Length; i++)
        {
            CardOrder[i] = initialOrder[i];
        }
        CurrentIndex = 0;
    }

    public int DrawNextCard()
    {
        Debug.Log("��@�i�P!");
        if (CurrentIndex >= CardOrder.Length || Deck_Left_Count <= 0)
        {
            return -1; // �P�դw��
        }

        int nextCard = CardOrder[CurrentIndex];
        CurrentIndex++;
        Deck_Left_Count--;
        return nextCard;
    }

    public void Shuffle()
    {
        List<int> tempList = new List<int>();
        for (int i = CurrentIndex; i < CardOrder.Length; i++)
        {
            tempList.Add(CardOrder[i]);
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
            CardOrder[CurrentIndex + i] = tempList[i];
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

    public bool IsInitialized { get; set; }

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

    private GameManager gameManager;

    // OnNetworkSpawned �O�b�������󧹥��ǳƦn��~�I�s��
    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("PlayerStatus.Spawned called");
        runner = Object.Runner;
        StartCoroutine(InitializeAfterSpawn());
    }

    private IEnumerator InitializeAfterSpawn()
    {
        while (runner == null)
        {
            runner = Object.Runner;
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"Runner found, LocalPlayer: {runner.LocalPlayer}");

        while (GameManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log("GameManager found");

        // �q���Ҧ����a���s���a�[�J
        Rpc_NotifyHost(runner.LocalPlayer, Object.Id);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_NotifyHost(PlayerRef player, NetworkId statusId)
    {
        // �o�Ӥ�k�u�|�b�ХD�ݰ���
        Debug.Log($"Host received notification from player {player}");
        GameManager.Instance.Rpc_RegisterPlayerStatus(player, statusId);
    }

    public void Initialized_Cards()
    {

        if (IsInitialized) return;  // ����ƪ�l��

        Debug.Log($"���b�����a {Runner.LocalPlayer} ��l�ƥd�P");
        try
        {
            gameDeckDatabase = new GameDeckDatabase();
            currentdeck = new InGameDeck();
            currentdeck.id = GameDeckManager.Instance.GetPlayerDeck(Runner.LocalPlayer);
            currentdeck.In_Hand_Count = 5;
            currentdeck.Deck_Left_Count = 35;

            List<int> initialOrder = new List<int>();
            for (int i = 0; i < totalcard; i++)
            {
                initialOrder.Add(i);
            }

            Debug.Log("�}�l��l�ƥd�P����");
            currentdeck.InitializeCardOrder(initialOrder);
            currentdeck.Shuffle();
            Debug.Log("�����~�P");

            IsInitialized = true;
            DrawInitialHand();
            Debug.Log($"���a {Runner.LocalPlayer} ������l��P");
        }
        catch (Exception e)
        {
            Debug.LogError($"��l�ƥd�P�ɵo�Ϳ��~: {e}");
        }
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