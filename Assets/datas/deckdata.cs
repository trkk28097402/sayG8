[System.Serializable]
public class GameDeckData
{
    public string deckName;
    public int cardCount;
    public int id;
    public string preview_imgae_path;
    public string description;
    public string deck_path;
}

public class GameDeckDatabase
{
    public const string DECK_PATH_PREFIX = "Assets/Resources/Decks/";

    public static GameDeckData[] Decks = new GameDeckData[]
    {
        new GameDeckData
        {
            deckName = "Mygo",
            cardCount = 40,
            id = 0,
            preview_imgae_path = "UI/Deck_preview_Image/mygo_pre",
            description = "�Ѫ��W�ʵeMyGO���e�զ��A�t���j�q��ι�ܡA�O��ܦn����U�خ�^��������εP��",
            deck_path = "Decks/Mygo"
        },
        new GameDeckData
        {
            deckName = "SpongeBob",
            cardCount = 40,
            id = 1,
            preview_imgae_path = "UI/Deck_preview_Image/spongebob_pre",
            description = "���~�^�Хd�q�����_�_���D�D�A�֦��U�ض��n�W���A�O��󴣤ɤ��z�ȫD�`�����U���P��",
            deck_path = "Decks/SpongeBob"
        },
        new GameDeckData
        {
            deckName = "�K�I��",
            cardCount = 40,
            id = 2,
            preview_imgae_path = "UI/Deck_preview_Image/8oclock",
            description = "�x�W���ꪺ�K�I�ɱ��",
            deck_path = "Decks/8oclock"
        },
    };

    public GameDeckData GetDeckById(int id)
    {
        return Decks[id];
    }
}