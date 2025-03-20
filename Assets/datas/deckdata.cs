[System.Serializable]
public class GameDeckData
{
    public string deckName;
    public int cardCount;
    public int id;
    public string preview_imgae_path;
    public string description;
    public string deck_path;
    public string victory_image_path; // 新增胜利图片路径字段
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
            description = "由知名動畫MyGO內容組成，含有大量實用對話，是能很好應對各種氣氛的的六邊形牌組",
            deck_path = "Decks/Mygo",
            victory_image_path = "VictoryAnimations/mygo_victory" // 胜利图片路径
        },
        new GameDeckData
        {
            deckName = "SpongeBob",
            cardCount = 40,
            id = 1,
            preview_imgae_path = "UI/Deck_preview_Image/spongebob_pre",
            description = "童年回憶卡通海綿寶寶為主題，擁有各種嗆聲名言，是對於提升火爆值非常有幫助的牌組",
            deck_path = "Decks/SpongeBob",
            victory_image_path = "VictoryAnimations/spongebob_victory" // 胜利图片路径
        },
        new GameDeckData
        {
            deckName = "八點檔",
            cardCount = 40,
            id = 2,
            preview_imgae_path = "UI/Deck_preview_Image/8oclock",
            description = "台灣白爛的八點檔梗圖",
            deck_path = "Decks/8oclock",
            victory_image_path = "VictoryAnimations/8oclock_victory" // 胜利图片路径
        },
        new GameDeckData
        {
            deckName = "台灣新聞",
            cardCount = 40,
            id = 3,
            preview_imgae_path = "UI/Deck_preview_Image/News",
            description = "台灣各式各異的新聞截圖",
            deck_path = "Decks/News",
            victory_image_path = "VictoryAnimations/news_victory" // 胜利图片路径
        },
        new GameDeckData
        {
            deckName = "銀魂",
            cardCount = 40,
            id = 4,
            preview_imgae_path = "UI/Deck_preview_Image/GinTama",
            description = "銀魂",
            deck_path = "Decks/GinTama",
            victory_image_path = "VictoryAnimations/gintama_victory" // 胜利图片路径
        },
    };
    public GameDeckData GetDeckById(int id)
    {
        return Decks[id];
    }
}