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
            description = "From the famous anime \"MYGO\"",
            deck_path = "Decks/Mygo"
        },
        new GameDeckData
        {
            deckName = "SpongeBob",
            cardCount = 40,
            id = 1,
            preview_imgae_path = "UI/Deck_preview_Image/spongebob_pre",
            description = "From the famous cartoon \"Sponge Bob\"",
            deck_path = "Decks/SpongeBob"
        },
    };

    public GameDeckData GetDeckById(int id)
    {
        return Decks[id];
    }
}