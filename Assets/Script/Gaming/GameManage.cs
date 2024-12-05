using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private CardonHand cardHand;
    private GameDeckDatabase deckDatabase;

    private void Awake()
    {
        deckDatabase = new GameDeckDatabase();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }
    }

    private void InitializeGame()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            int deckId = GameDeckManager.Instance.GetPlayerDeck(player);
            deckId = 0;
            if (deckId != -1)
            {
                SetupPlayerDeck(player, deckId);
            }
        }
    }

    private void SetupPlayerDeck(PlayerRef player, int deckId)
    {
        // �q�z���ƾڮw����d�ո��
        GameDeckData deckData = deckDatabase.GetDeckById(deckId);
        if (deckData == null)
        {
            Debug.LogError($"�䤣��ID�� {deckId} ���d��");
            return;
        }

        // �ھڥd�ո�ƳЫش��եd��
        // �o�̰��]�ڭ̥��Ы�5�i�P�@���_�l��P
        int initialHandSize = Mathf.Min(5, deckData.cardCount);
        CardData[] cards = new CardData[initialHandSize];

        for (int i = 0; i < initialHandSize; i++)
        {
            cards[i] = new CardData
            {
                cardName = $"{deckData.deckName} Card {i + 1}",
                cardImage = Resources.Load<Sprite>($"{GameDeckDatabase.DECK_PATH_PREFIX}{deckData.deckName}/card_{i}")
                // �p�G�z����L�d���ݩʡA�i�H�b�o�̳]�m
            };
        }

        // �p�G�o�O���a���a�A�]�m�L����P
        if (player == Runner.LocalPlayer)
        {
            cardHand.SetupCard(cards);
            Debug.Log($"�]�m���a {player} ���d�աG{deckData.deckName}");
        }
    }

    // �Ω���ժ���k
    public void SetupTestDeck(int deckId)
    {
        GameDeckData testDeck = deckDatabase.GetDeckById(deckId);
        if (testDeck == null) return;

        int testHandSize = Mathf.Min(5, testDeck.cardCount);
        CardData[] testCards = new CardData[testHandSize];

        for (int i = 0; i < testHandSize; i++)
        {
            testCards[i] = new CardData
            {
                cardName = $"{testDeck.deckName} Test Card {i + 1}",
                cardImage = null  // �θ��J�w�]�Ϥ�
            };
        }

        cardHand.SetupCard(testCards);
    }
}