using UnityEngine;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fusion;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Linq;
using System.Collections;

public struct MoodState : INetworkStruct
{
    public NetworkString<_32> AssignedMood;
    public float MoodValue;
}

[Serializable]
public class Message
{
    public string role;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string content;
    public List<ContentPart> parts;
}

[Serializable]
public class ContentPart
{
    public string type;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string text;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageUrl image_url;
}

[Serializable]
public class ImageUrl
{
    public string url;
}

[Serializable]
public class ClaudeRequest
{
    public string model = "claude-3-5-sonnet-20241022";
    public int max_tokens = 1024;
    public string system;  // 頂層系統提示
    public List<ClaudeMessage> messages;
}

[Serializable]
public class ClaudeMessage
{
    public string role;
    public List<ClaudeContent> content;
}

[Serializable]
public class ClaudeContent
{
    public string type;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string text;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ClaudeImageSource source;
}

[Serializable]
public class ClaudeImageSource
{
    public string type = "base64";
    public string media_type = "image/jpeg";
    public string data;
}

[Serializable]
public class ClaudeResponse
{
    public string id;
    public string model;
    public string role;
    public List<ClaudeContent> content;
}

public class MoodEvaluator : NetworkBehaviour
{
    private const float WINNING_THRESHOLD = 100f;
    private const string CLAUDE_URL = "https://api.anthropic.com/v1/messages";

    [SerializeField] private string apiKey;
    [SerializeField] private UnityEngine.UI.Slider moodSlider;
    [SerializeField] private TMPro.TextMeshProUGUI moodValueText;
    [SerializeField] private string anthropicVersion = "2023-06-01";

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
    private PlayedCardsManager playedCardsManager;
    private TurnManager turnManager;

    public override void Spawned()
    {
        base.Spawned();
        gameManager = GameManager.Instance;
        playedCardsManager = FindObjectOfType<PlayedCardsManager>();
        turnManager = FindObjectOfType<TurnManager>();
        Debug.Log($"[MoodEvaluator] Spawned called, IsStateAuthority: {Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            StartCoroutine(InitializeMoodsWithRetry());
        }

        // 所有玩家都請求初始氣氛值
        if (Runner.LocalPlayer != null)
        {
            Rpc_RequestInitialMood(Runner.LocalPlayer);
        }
    }

    private IEnumerator InitializeMoodsWithRetry()
    {
        while (gameManager.GetConnectedPlayers().Length < 2)
        {
            yield return new WaitForSeconds(1f);
        }
        InitializeMoods();
    }

    private string[] availableMoods = { "火爆", "敷衍", "嘲諷", "理性", "白目", "歡樂" };
    private int moodIndex1, moodIndex2;

    private void InitializeMoods()
    {
        var players = gameManager.GetConnectedPlayers();
        if (players.Length < 2)
        {
            Debug.Log("等待更多玩家連接...");
            return;
        }

        PlayerMoods.Clear();
        System.Random random = new System.Random();

        // 為每個玩家分配不同情緒
        var usedMoods = new HashSet<int>();
        foreach (var player in players)
        {
            int moodIndex;
            do
            {
                moodIndex = random.Next(availableMoods.Length);
            } while (usedMoods.Contains(moodIndex));

            usedMoods.Add(moodIndex);
            var mood = new MoodState
            {
                AssignedMood = availableMoods[moodIndex],
                MoodValue = 0f
            };
            PlayerMoods.Add(player, mood);
            Debug.Log($"玩家 {player} 的初始情緒為: {mood.AssignedMood}");
        }
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestInitialMood(PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority) return;

        if (PlayerMoods.TryGet(requestingPlayer, out var mood))
        {
            Debug.Log($"[MoodEvaluator] Sending initial mood to player {requestingPlayer}: {mood.MoodValue}");
            Rpc_SyncMoodValue(requestingPlayer, mood.MoodValue, mood.AssignedMood);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncMoodValue(PlayerRef targetPlayer, float moodValue, NetworkString<_32> assignedMood)
    {
        Debug.Log($"[MoodEvaluator] Received mood sync - Player: {targetPlayer}, Value: {moodValue}, Mood: {assignedMood}");

        if (targetPlayer == Runner.LocalPlayer)
        {
            UpdateMoodUI(moodValue);

            if (PlayerMoods.TryGet(targetPlayer, out var currentMood))
            {
                var newMood = new MoodState
                {
                    AssignedMood = assignedMood,
                    MoodValue = moodValue
                };
                PlayerMoods.Set(targetPlayer, newMood);
            }
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

            if (Object.HasStateAuthority && turnManager != null)
            {
                turnManager.PauseTurnTimer();
            }

            // Rest of the existing OnCardPlayed logic...
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

                Rpc_RecordCardPlayed(player, deckData.deckName, cardData.cardId + 1, cardData.imagePath.Value);
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
    private void Rpc_RecordCardPlayed(PlayerRef player, string deckName, int cardNumber, string imagePath)
    {
        if (!Object.HasStateAuthority) return;

        var cardContext = new PlayedCardContext
        {
            Player = player,
            DeckName = deckName,
            CardNumber = cardNumber,
            ImagePath = imagePath
        };

        gameHistory.Add(cardContext);
        Debug.Log($"State Authority recorded card - Player: {player}, " +
                  $"Deck: {deckName}, Card: {cardNumber}, " +
                  $"ImagePath: {imagePath}");
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestEvaluateMood(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;
        EvaluateMood(player);
    }

    private async Task<string> GetMoodEvaluation(PlayerRef player)
    {
        if (gameHistory.Count == 0)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            { "火爆", 0 },
            { "敷衍", 0 },
            { "嘲諷", 0 },
            { "理性", 0 },
            { "白目", 0 },
            { "歡樂", 0 },
            { "分析", "沒有足夠的卡牌記錄進行分析" }
        });
        }

        string systemPrompt = @"你是一個專業的圖像氛圍分析師，專門分析網路梗圖和表情包。請根據以下步驟進行分析：

        1. 圖像解讀：
           - 觀察圖中人物/角色的表情、動作和姿態
           - 注意圖像中的文字內容（如果有）

        2. 上下文考慮：
           - 已提供先前出牌的圖片記錄
           - 考慮當前的氛圍值和遊戲進展
           - 評估最新這張圖片如何延續或改變對話氣氛

        3. 綜合分析：
           - 主要分析最新的圖片，先前的圖片僅作為參考
           - 判斷圖片是否在試圖引發幽默效果或激烈情緒
           - 評估梗圖的表達方式（如：反諷、戲劇化、逗趣等）
           - 考慮梗圖在當前語境下的效果

        請使用以下 JSON 格式回應（必須嚴格遵守此格式）：
        {
            ""火爆"": <-20到+20的數值>,
            ""敷衍"": <-20到+20的數值>,
            ""嘲諷"": <-20到+20的數值>,
            ""理性"": <-20到+20的數值>,
            ""白目"": <-20到+20的數值>,
            ""歡樂"": <-20到+20的數值>,
            ""分析"": ""<簡短說明你對圖片氛圍的理解以及為何給出這樣的評分>""
        }

        評分說明：
        - 正值表示增強該氛圍，負值表示減弱該氛圍
        - 絕對值越大表示影響越強烈
        - 不同氛圍可以同時存在

        注意：
        1. 請使用英文標點符號
        2. 僅返回 JSON 格式內容，不要有任何額外文字
        3. 分析要精簡有力，直指要害，不需要詳細描述圖片內容";

        // 準備用戶消息內容
        var contentParts = new List<ClaudeContent>();
        if (PlayerMoods.TryGet(player, out var currentMood))
        {
            contentParts.Add(new ClaudeContent
            {
                type = "text",
                text = $"當前氛圍值：玩家的{currentMood.AssignedMood}氛圍值為{currentMood.MoodValue}"
            });
        }

        // 添加圖片
        int recentCount = Math.Min(gameHistory.Count, 3);
        for (int i = gameHistory.Count - recentCount; i < gameHistory.Count; i++)
        {
            var card = gameHistory[i];
            try
            {
                Texture2D cardTexture = Resources.Load<Texture2D>(card.ImagePath);
                if (cardTexture != null)
                {
                    byte[] imageBytes = cardTexture.EncodeToJPG();
                    string base64Data = Convert.ToBase64String(imageBytes);

                    string contextText = i < gameHistory.Count - 1 ?
                        $"這是先前第{i + 1}張出的牌" :
                        "這是最新出的一張牌，請主要分析這張圖片的氛圍";

                    contentParts.Add(new ClaudeContent
                    {
                        type = "text",
                        text = contextText
                    });

                    contentParts.Add(new ClaudeContent
                    {
                        type = "image",
                        source = new ClaudeImageSource
                        {
                            type = "base64",
                            media_type = "image/jpeg",
                            data = base64Data
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing card image: {ex.Message}");
            }
        }

        contentParts.Add(new ClaudeContent
        {
            type = "text",
            text = "請分析圖中展現的氛圍，並按照規定的 JSON 格式回應。"
        });

        var claudeMessages = new List<ClaudeMessage>();
        claudeMessages.Add(new ClaudeMessage
        {
            role = "user",
            content = contentParts
        });

        var request = new ClaudeRequest
        {
            model = "claude-3-5-sonnet-20241022",
            max_tokens = 1024,
            system = systemPrompt,
            messages = claudeMessages
        };

        return await SendClaudeRequest(request);
    }

    private async Task<string> SendClaudeRequest(ClaudeRequest request)
    {
        try
        {
            string requestJson = JsonConvert.SerializeObject(request, Formatting.Indented);
            Debug.Log($"Request JSON: {requestJson}");

            using (var webRequest = new UnityWebRequest(CLAUDE_URL, "POST"))
            {
                byte[] jsonToSend = new UTF8Encoding().GetBytes(requestJson);
                webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("x-api-key", apiKey);
                webRequest.SetRequestHeader("anthropic-version", anthropicVersion);

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Claude response: {webRequest.downloadHandler.text}");
                    var response = JsonConvert.DeserializeObject<ClaudeResponse>(webRequest.downloadHandler.text);
                    if (response?.content == null)
                    {
                        throw new Exception("Empty or invalid API response");
                    }

                    var textParts = response.content
                        .Where(p => p.type == "text")
                        .Select(p => p.text);

                    return string.Join("\n", textParts);
                }
                else
                {
                    string errorResponse = webRequest.downloadHandler?.text ?? "No response body";
                    Debug.LogError($"API request failed: {webRequest.error}\nResponse: {errorResponse}");
                    throw new Exception($"API request failed: {webRequest.error}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in SendClaudeRequest: {ex.Message}\nStack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void ProcessMoodResponse(string response)
    {
        try
        {
            Debug.Log($"Raw API response: {response}");

            if (string.IsNullOrEmpty(response))
            {
                throw new Exception("Empty response received");
            }

            // 清理 JSON 字符串
            response = response.Trim();

            // 如果回應被包在 markdown 代碼塊中，移除它
            if (response.StartsWith("```json"))
            {
                response = response.Substring(7);
            }
            if (response.EndsWith("```"))
            {
                response = response.Substring(0, response.Length - 3);
            }

            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.Default,
                Formatting = Formatting.None
            };

            var moodData = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                response,
                settings
            );

            if (moodData == null)
            {
                throw new Exception("Failed to parse mood data");
            }

            foreach (var player in gameManager.GetConnectedPlayers())
            {
                // Debug.Log($"{player} is connected!");
                if (PlayerMoods.TryGet(player, out var currentMood))
                {
                    string moodKey = currentMood.AssignedMood.Value;
                    if (moodData.TryGetValue(moodKey, out object moodValue))
                    {
                        float moodChange = ParseMoodValue(moodValue);
                        UpdatePlayerMood(player, moodChange);
                    }
                }
                else Debug.Log($"{player} do not have {currentMood}");
            }

            if (moodData.TryGetValue("分析", out object analysis))
            {
                string analysisText = analysis?.ToString() ?? "無分析內容";
                Debug.Log($"情緒分析: {analysisText}");
            }

            CheckWinCondition();

            if (Object.HasStateAuthority)
            {
                if (turnManager != null)
                {
                    turnManager.ResumeTurnTimer();
                }

                if (playedCardsManager != null)
                {
                    playedCardsManager.Rpc_NotifyMoodEvaluationComplete();
                }
            }
        }
        catch (Exception e)  // handle失敗情況，可以讓遊戲繼續

        {
            Debug.LogError($"Error processing mood response: {e.Message}\nResponse: {response}");

            try
            {
                if (response.Contains($"{availableMoods[moodIndex1]}") && response.Contains($"{availableMoods[moodIndex2]}"))
                {
                    var firstMatch = System.Text.RegularExpressions.Regex.Match(response, $@"""{availableMoods[moodIndex1]}""\s*:\s*(-?\d+)");
                    var secondMatch = System.Text.RegularExpressions.Regex.Match(response, $@"""{availableMoods[moodIndex2]}""\s*:\s*(-?\d+)");

                    if (firstMatch.Success && secondMatch.Success)
                    {
                        float firstMoodValue = float.Parse(firstMatch.Groups[1].Value);
                        float secondMoodValue = float.Parse(secondMatch.Groups[1].Value);

                        foreach (var player in gameManager.GetConnectedPlayers())
                        {
                            if (PlayerMoods.TryGet(player, out var currentMood))
                            {
                                float moodChange = currentMood.AssignedMood.Value == availableMoods[moodIndex1] ? firstMoodValue : secondMoodValue;
                                UpdatePlayerMood(player, moodChange);
                            }
                        }

                        Debug.Log("Successfully extracted mood values using backup method");
                    }
                }


                if (Object.HasStateAuthority && playedCardsManager != null)
                {
                    playedCardsManager.Rpc_NotifyMoodEvaluationComplete();
                }
            }
            catch (Exception backupError)
            {
                Debug.LogError($"Backup parsing also failed: {backupError.Message}");
            }
        }
    }

    private float ParseMoodValue(object value)
    {
        try
        {
            Debug.Log($"[MoodEvaluator] Parsing mood value of type {value?.GetType()}: {value}");

            if (value == null) return 0f;

            if (value is string stringValue)
            {
                stringValue = stringValue.Trim().Replace("+", "");
                if (float.TryParse(stringValue, out float result))
                {
                    Debug.Log($"[MoodEvaluator] Successfully parsed string value to float: {result}");
                    return result;
                }
            }
            else if (value is long longValue)
            {
                Debug.Log($"[MoodEvaluator] Converting long value to float: {longValue}");
                return (float)longValue;
            }
            else if (value is int intValue)
            {
                Debug.Log($"[MoodEvaluator] Converting int value to float: {intValue}");
                return (float)intValue;
            }
            else if (value is float floatValue)
            {
                Debug.Log($"[MoodEvaluator] Already a float value: {floatValue}");
                return floatValue;
            }
            else if (value is double doubleValue)
            {
                Debug.Log($"[MoodEvaluator] Converting double value to float: {doubleValue}");
                return (float)doubleValue;
            }
            else if (value is decimal decimalValue)
            {
                Debug.Log($"[MoodEvaluator] Converting decimal value to float: {decimalValue}");
                return (float)decimalValue;
            }

            throw new Exception($"Invalid mood value format: {value}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MoodEvaluator] Error parsing mood value: {ex.Message}");
            return 0f;
        }
    }

    private void UpdatePlayerMood(PlayerRef player, float change)
    {
        Debug.Log($"[MoodEvaluator] Updating mood for player {player} with change: {change}");

        if (PlayerMoods.TryGet(player, out var currentMood))
        {
            float newMoodValue = Mathf.Clamp(currentMood.MoodValue + change, 0f, WINNING_THRESHOLD);
            Debug.Log($"[MoodEvaluator] Current mood: {currentMood.MoodValue}, New mood after change: {newMoodValue}");

            var newMood = new MoodState
            {
                AssignedMood = currentMood.AssignedMood,
                MoodValue = newMoodValue
            };
            PlayerMoods.Set(player, newMood);

            Rpc_SyncMoodValue(player, newMoodValue, currentMood.AssignedMood);
            Rpc_NotifyMoodUpdate(player, newMoodValue, change);
        }
    }

    private void UpdateMoodUI(float value)
    {
        if (!UnityEngine.Application.isPlaying) return;

        if (moodSlider != null)
        {
            Debug.Log($"[MoodEvaluator] Updating slider to {value}");
            moodSlider.value = value;
        }
        else
        {
            Debug.LogError("[MoodEvaluator] Mood slider reference is missing!");
        }

        if (moodValueText != null)
        {
            moodValueText.text = value.ToString("F1");
        }
        else
        {
            Debug.LogError("[MoodEvaluator] Mood text reference is missing!");
        }
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
    private void Rpc_NotifyMoodUpdate(PlayerRef player, float newValue, float change)
    {
        string playerName = player == Runner.LocalPlayer ? "你" : "對手";
        string changeText = change >= 0 ? $"+{change}" : change.ToString();
        Debug.Log($"{playerName}的{PlayerMoods.Get(player).AssignedMood}氛圍值 {changeText} (當前: {newValue})");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceWinner(PlayerRef winner, NetworkString<_32> mood)
    {
        string playerName = winner == Runner.LocalPlayer ? "你" : "對手";
        Debug.Log($"遊戲結束！{playerName}成功營造出{mood}的氛圍！");
    }
}