using UnityEngine;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fusion;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Linq;

public struct MoodState : INetworkStruct
{
    public NetworkString<_32> AssignedMood;  // 分配的氛圍類型
    public float MoodValue;                  // 當前氛圍值
}

[Serializable]
public class Message
{
    public string role;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string content;  // 為系統消息保留
    public List<ContentPart> parts;  // 為用戶消息添加 parts
}

[Serializable]
public class ContentPart
{
    public string type;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string text;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageUrl image_url;  // 改為 image_url
}

[Serializable]
public class ImageUrl
{
    public string url;
}
[Serializable]
public class ChatRequest
{
    public string model = "gpt-4o";
    public List<Message> messages;
}

[Serializable]
public class ChatResponse
{
    public Choice[] choices;
}

[Serializable]
public class Choice
{
    public Message message;
}

public class MoodEvaluator : NetworkBehaviour
{
    private const float WINNING_THRESHOLD = 100f;
    private const string OPENAI_URL = "https://api.openai.com/v1/chat/completions";
    [SerializeField] private string apiKey;

    [Networked]
    private NetworkDictionary<PlayerRef, MoodState> PlayerMoods { get; }

    private struct PlayedCardContext
    {
        public PlayerRef Player;
        public string DeckName;
        public int CardNumber;
        public string ImagePath;
    }

    private List<PlayedCardContext> gameHistory = new List<PlayedCardContext>();
    private GameManager gameManager;
    private NetworkRunner runner;

    public override void Spawned()
    {
        base.Spawned();
        gameManager = GameManager.Instance;
        if (Object.HasStateAuthority)
        {
            InitializeMoods();
        }
    }

    private void InitializeMoods()
    {
        string[] availableMoods = { "火爆", "幽默" };
        System.Random random = new System.Random();

        foreach (var player in gameManager.GetConnectedPlayers())
        {
            int moodIndex = random.Next(availableMoods.Length);
            var mood = new MoodState
            {
                AssignedMood = availableMoods[moodIndex],
                MoodValue = 0f
            };
            PlayerMoods.Add(player, mood);

            var tempList = new List<string>(availableMoods);
            tempList.RemoveAt(moodIndex);
            availableMoods = tempList.ToArray();
        }
    }

    public void OnCardPlayed(NetworkedCardData cardData, PlayerRef player)
    {
        try
        {
            if (GameDeckManager.Instance == null)
            {
                Debug.LogError("GameDeckManager.Instance is null");
                return;
            }

            int deckId = GameDeckManager.Instance.GetPlayerDeck(player);
            Debug.Log($"OnCardPlayed called - Player: {player}, DeckId: {deckId}, CardId: {cardData.cardId}");

            if (deckId < 0)
            {
                Debug.LogError($"Invalid deck ID {deckId} for player {player}. Ensure deck is properly assigned.");
                return;
            }

            var deckDatabase = new GameDeckDatabase();
            if (deckDatabase == null)
            {
                Debug.LogError("Failed to create GameDeckDatabase");
                return;
            }

            try
            {
                var deckData = deckDatabase.GetDeckById(deckId);
                if (deckData == null)
                {
                    Debug.LogError($"No deck data found for ID: {deckId}");
                    return;
                }

                var cardContext = new PlayedCardContext
                {
                    Player = player,
                    DeckName = deckData.deckName,
                    CardNumber = cardData.cardId + 1,
                    ImagePath = cardData.imagePath.Value
                };

                Debug.Log($"Created card context - Player: {cardContext.Player}, " +
                         $"Deck: {cardContext.DeckName}, Card: {cardContext.CardNumber}, " +
                         $"ImagePath: {cardContext.ImagePath}");

                gameHistory.Add(cardContext);

                Debug.Log($"{Runner.LocalPlayer} 請求評估氣氛");
                Rpc_RequestEvaluateMood(Runner.LocalPlayer);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing deck data: {ex.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Critical error in OnCardPlayed: {e.Message}\nStack trace: {e.StackTrace}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestEvaluateMood(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;
        EvaluateMood(player);
    }

    private async void EvaluateMood(PlayerRef player)
    {
        try
        {
            string response = await GetMoodEvaluation(player);
            ProcessMoodResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error evaluating mood: {e.Message}");
        }
    }

    private async Task<string> GetMoodEvaluation(PlayerRef player)
    {
        // Only proceed if we have game history
        if (gameHistory.Count == 0)
        {
            Debug.LogWarning("No game history available for mood evaluation");
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "火爆", 0 },
                { "幽默", 0 },
                { "分析", "沒有足夠的卡牌記錄進行分析" }
            });
        }

        var messages = new List<Message>
        {
            new Message
            {
                role = "system",
                content = @"你是一個對話氛圍分析師，需要分析卡牌遊戲中玩家通過出牌創造的對話氛圍。
                請分析玩家之間通過卡片形成的對話發展，評估'火爆'和'幽默'兩種氛圍值的變化。
            
                請嚴格按照以下JSON格式回應（確保使用英文標點符號）：
                {
                    ""火爆"": 0,
                    ""幽默"": 0,
                    ""分析"": ""在此填寫分析""
                }
                注意：
                1. 火爆和幽默的值必須是介於-20到+20之間的數字
                2. 必須使用英文的雙引號和冒號
                3. 不要添加任何額外的文字或格式"
            }
        };

        var textParts = new StringBuilder();
        textParts.AppendLine("目前遊戲進度：");

        // Get recent cards (up to 3)
        int recentCount = Math.Min(gameHistory.Count, 3);
        for (int i = gameHistory.Count - recentCount; i < gameHistory.Count; i++)
        {
            var card = gameHistory[i];
            string playerNumber = card.Player.Equals(player) ? "1" : "2";
            textParts.AppendLine($"玩家{playerNumber}使用了{card.DeckName}牌組中的第{card.CardNumber}張卡");
        }

        // 添加當前氛圍值資訊
        if (PlayerMoods.TryGet(player, out var currentMood))
        {
            textParts.AppendLine($"\n當前氛圍值：");
            textParts.AppendLine($"玩家的{currentMood.AssignedMood}氛圍值為{currentMood.MoodValue}");
        }

        var userMessage = new Message
        {
            role = "user",
            content = textParts.ToString(),  // 設置 content 為文字描述
            parts = new List<ContentPart>()
        };

        // 添加圖片部分
        for (int i = gameHistory.Count - recentCount; i < gameHistory.Count; i++)
        {
            var card = gameHistory[i];
            try
            {
                Texture2D cardTexture = Resources.Load<Texture2D>(card.ImagePath);
                if (cardTexture != null)
                {
                    byte[] imageBytes = cardTexture.EncodeToJPG();
                    string base64Image = Convert.ToBase64String(imageBytes);

                    userMessage.parts.Add(new ContentPart
                    {
                        type = "image_url",
                        image_url = new ImageUrl
                        {
                            url = $"data:image/jpeg;base64,{base64Image}"
                        }
                    });
                }
                else
                {
                    Debug.LogError($"Failed to load card image at path: {card.ImagePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing card image: {ex.Message}");
            }
        }

        messages.Add(userMessage);

        var request = new ChatRequest
        {
            model = "gpt-4o",
            messages = messages
        };

        try
        {
            using (var webRequest = new UnityWebRequest(OPENAI_URL, "POST"))
            {
                byte[] jsonToSend = new UTF8Encoding().GetBytes(JsonConvert.SerializeObject(request));
                webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonConvert.DeserializeObject<ChatResponse>(webRequest.downloadHandler.text);
                    if (response?.choices == null || response.choices.Length == 0)
                    {
                        throw new Exception("Empty or invalid API response");
                    }
                    return response.choices[0].message.content;
                }
                else
                {
                    throw new Exception($"API request failed: {webRequest.error}\nResponse: {webRequest.downloadHandler?.text}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in API request: {ex.Message}");
            // Return a safe default response
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "火爆", 0 },
                { "幽默", 0 },
                { "分析", "API請求失敗，無法評估氛圍" }
            });
        }
    }

    private void ProcessMoodResponse(string response)
    {
        try
        {
            Debug.Log($"Raw API response: {response}");

            // Ensure we have valid JSON
            if (string.IsNullOrEmpty(response))
            {
                throw new Exception("Empty response received");
            }

            // Clean up the response
            response = response.Trim();
            response = response.Replace("\\\"", "\"").Replace("\\n", "\n");

            // Try to parse as JSON
            var moodData = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
            if (moodData == null)
            {
                throw new Exception("Failed to parse mood data");
            }

            // Process each player's mood
            foreach (var player in gameManager.GetConnectedPlayers())
            {
                if (PlayerMoods.TryGet(player, out var currentMood))
                {
                    string moodKey = currentMood.AssignedMood.Value;
                    if (moodData.TryGetValue(moodKey, out object moodValue))
                    {
                        float moodChange = ParseMoodValue(moodValue);
                        UpdatePlayerMood(player, moodChange);
                    }
                }
            }

            // Log analysis if available
            if (moodData.TryGetValue("分析", out object analysis))
            {
                Debug.Log($"情緒分析: {analysis}");
            }

            CheckWinCondition();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing mood response: {e.Message}\nResponse: {response}");
        }
    }

    private float ParseMoodValue(object value)
    {
        try
        {
            if (value is string stringValue)
            {
                stringValue = stringValue.Trim().Replace("+", "");
                if (float.TryParse(stringValue, out float result))
                {
                    return result;
                }
            }
            else if (value is long || value is int || value is float || value is double)
            {
                return Convert.ToSingle(value);
            }

            throw new Exception($"Invalid mood value format: {value}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing mood value: {ex.Message}");
            return 0f;
        }
    }

    private void UpdatePlayerMood(PlayerRef player, float change)
    {
        if (PlayerMoods.TryGet(player, out var currentMood))
        {
            var newMood = new MoodState
            {
                AssignedMood = currentMood.AssignedMood,
                MoodValue = Mathf.Clamp(currentMood.MoodValue + change, 0f, WINNING_THRESHOLD)
            };
            PlayerMoods.Set(player, newMood);

            Rpc_NotifyMoodUpdate(player, newMood.MoodValue, change);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyMoodUpdate(PlayerRef player, float newValue, float change)
    {
        string playerName = player == Runner.LocalPlayer ? "你" : "對手";
        string changeText = change >= 0 ? $"+{change}" : change.ToString();
        Debug.Log($"{playerName}的{PlayerMoods.Get(player).AssignedMood}氛圍值 {changeText} (當前: {newValue})");
    }

    private void CheckWinCondition()
    {
        foreach (var player in gameManager.GetConnectedPlayers())
        {
            if (PlayerMoods.TryGet(player, out var mood) && mood.MoodValue >= WINNING_THRESHOLD)
            {
                Rpc_AnnounceWinner(player, mood.AssignedMood.Value);
                return;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceWinner(PlayerRef winner, NetworkString<_32> mood)
    {
        string playerName = winner == Runner.LocalPlayer ? "你" : "對手";
        Debug.Log($"遊戲結束！{playerName}成功營造出{mood}的氛圍！");
    }
}