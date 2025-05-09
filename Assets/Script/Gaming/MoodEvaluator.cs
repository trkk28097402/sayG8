﻿using UnityEngine;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fusion;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Linq;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    public string model = "claude-3-7-sonnet-20250219";
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
    private const float WINNING_THRESHOLD = 100f; //勝利條件

    private const string CLAUDE_URL = "https://api.anthropic.com/v1/messages";

    [SerializeField] private string apiKey;
    [SerializeField] private UnityEngine.UI.Slider moodSlider;
    [SerializeField] private TMPro.TextMeshProUGUI moodValueText;
    [SerializeField] private UnityEngine.UI.Slider opponentMoodSlider;
    [SerializeField] private TMPro.TextMeshProUGUI opponentMoodValueText;
    [SerializeField] private UnityEngine.UI.Image playerMoodIcon;
    [SerializeField] private UnityEngine.UI.Image opponentMoodIcon;
    [SerializeField] private TurnNotificationManager turnNotificationManager;

    [SerializeField] private AudioManagerClassroom audioManager;

    [Header("Game End UI")]
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private float buttonShowDelay = 3.0f;
    private const float AUTO_RETURN_TO_LOBBY_DELAY = 8f;

    private Dictionary<string, string> moodIconPaths = new Dictionary<string, string>()
    {
        { "火爆", "UIresource/emoji/angry" },
        { "敷衍", "UIresource/emoji/fu_yen" },
        { "嘲諷", "UIresource/emoji/ridicule" },
        { "理性", "UIresource/emoji/rational" },
        { "白目", "UIresource/emoji/bai_mu" },
        { "歡樂", "UIresource/emoji/happy" }
    };

    [SerializeField] private string anthropicVersion = "2023-06-01";
    [SerializeField] private GameObject responseBG;

    [Networked]
    public NetworkDictionary<PlayerRef, MoodState> PlayerMoods { get; }

    [Networked]
    public NetworkBool IsGameOver { get; private set; }

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

    [SerializeField] private TMPro.TextMeshProUGUI winnerText;
    [SerializeField] private Image winningImage;
    [SerializeField] private TMPro.TextMeshProUGUI analysisText; // 用於顯示分析結果的UI文字

    public override void Spawned()
    {
        base.Spawned();

        // Find TurnNotificationManager if not already assigned
        if (turnNotificationManager == null)
        {
            turnNotificationManager = FindObjectOfType<TurnNotificationManager>();
        }

        if (audioManager == null)
        {
            audioManager = FindObjectOfType<AudioManagerClassroom>();
        }

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.RemoveAllListeners();
            returnToLobbyButton.onClick.AddListener(ReturnToLobby);
            returnToLobbyButton.gameObject.SetActive(false);
        }

        gameManager = GameManager.Instance;
        playedCardsManager = FindObjectOfType<PlayedCardsManager>();
        turnManager = FindObjectOfType<TurnManager>();
        Debug.Log($"[MoodEvaluator] Spawned called, IsStateAuthority: {Object.HasStateAuthority}");


        // 如果是StateAuthority，開始初始化流程
        if (Object.HasStateAuthority)
        {
            StartCoroutine(InitializeMoodsWithRetry());
        }
        // 所有客戶端都請求初始化
        else if (Runner.LocalPlayer != null)
        {
            Rpc_RequestInitialization(Runner.LocalPlayer);
        }
    }

    private IEnumerator InitializeMoodsWithRetry()
    {
        while (gameManager.GetConnectedPlayers().Length < 2)
        {
            yield return new WaitForSeconds(1f);
        }
        InitializeMoods();

        // 初始化完成後，通知所有客戶端
        foreach (var player in gameManager.GetConnectedPlayers())
        {
            if (PlayerMoods.TryGet(player, out var mood))
            {
                Rpc_InitializeClient(player, mood.AssignedMood, mood.MoodValue);
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestInitialization(PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority) return;

        if (PlayerMoods.TryGet(requestingPlayer, out var mood))
        {
            Rpc_InitializeClient(requestingPlayer, mood.AssignedMood, mood.MoodValue);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_InitializeClient(PlayerRef targetPlayer, NetworkString<_32> assignedMood, float moodValue)
    {
        bool isObserver = ObserverManager.Instance != null &&
                         ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);

        // 設置默認UI值
        if (moodSlider != null)
        {
            moodSlider.value = 50f;
        }
        if (moodValueText != null)
        {
            moodValueText.text = "50.0";
        }
        if (opponentMoodSlider != null)
        {
            opponentMoodSlider.value = 50f;
        }
        if (opponentMoodValueText != null)
        {
            opponentMoodValueText.text = "50.0";
        }
        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }
        if (winningImage != null)
        {
            winningImage.gameObject.SetActive(false);
        }
        if (analysisText != null)
        {
            analysisText.gameObject.SetActive(isObserver);
            analysisText.text = string.Empty;
        }
        if (responseBG != null)
        {
            responseBG.SetActive(isObserver);
        }

        // 更新網絡狀態
        if (PlayerMoods.TryGet(targetPlayer, out var currentMood))
        {
            var newMood = new MoodState
            {
                AssignedMood = assignedMood,
                MoodValue = moodValue
            };
            PlayerMoods.Set(targetPlayer, newMood);
        }

        // 更新UI和圖標
        UpdateMoodUI(moodValue, targetPlayer);
        UpdateMoodIcon(assignedMood, targetPlayer == Runner.LocalPlayer);
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
                MoodValue = 50f
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
            Debug.Log($"[MoodEvaluator] Sending initial mood to player {requestingPlayer}: {mood.MoodValue}, Mood: {mood.AssignedMood}");
            Rpc_SyncMoodValue(requestingPlayer, mood.MoodValue, mood.AssignedMood);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_SyncMoodValue(PlayerRef targetPlayer, float moodValue, NetworkString<_32> assignedMood)
    {
        Debug.Log($"[MoodEvaluator] Received mood sync - Player: {targetPlayer}, Value: {moodValue}, Mood: {assignedMood}");

        // 更新網路狀態
        if (PlayerMoods.ContainsKey(targetPlayer))
        {
            var newMood = new MoodState
            {
                AssignedMood = assignedMood,
                MoodValue = moodValue
            };
            PlayerMoods.Set(targetPlayer, newMood);
        }
        else
        {
            // 如果玩家不存在於PlayerMoods中，添加它
            var newMood = new MoodState
            {
                AssignedMood = assignedMood,
                MoodValue = moodValue
            };
            PlayerMoods.Add(targetPlayer, newMood);
            Debug.Log($"[MoodEvaluator] Added new mood entry for player {targetPlayer}: {assignedMood}");
        }

        // 更新UI和icon
        UpdateMoodUI(moodValue, targetPlayer);

        // 直接調用更新icon (以防 UpdateMoodUI 中的條件沒有觸發)
        if (PlayerMoods.TryGet(targetPlayer, out var mood))
        {
            bool isLocalPlayer = targetPlayer == Runner.LocalPlayer;
            UpdateMoodIcon(mood.AssignedMood, isLocalPlayer);
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
            // Show AI analysis notification
            if (turnNotificationManager != null)
            {
                Rpc_ShowAIAnalysisNotification();
            }

            string response = await GetMoodEvaluation(player);
            ProcessMoodResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error evaluating mood: {e.Message}");

            // Hide notification in case of error
            if (turnNotificationManager != null)
            {
                Rpc_HideAIAnalysisNotification();
            }

            // Handle error and continue the game
            Rpc_NotifyAIError();

            // Continue the game by resuming turn timer and notifying that evaluation is complete
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
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyAIError()
    {
        // Display error message to all clients
        if (analysisText != null)
        {
            bool isObserver = ObserverManager.Instance != null &&
                             ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);

            analysisText.gameObject.SetActive(isObserver);
            if (isObserver)
            {
                analysisText.text = "AI分析當機了！跳過這回合...";
            }
        }

        if (responseBG != null)
        {
            bool isObserver = ObserverManager.Instance != null &&
                     ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);
            responseBG.SetActive(isObserver);
        }

        Debug.Log("AI分析當機了！跳過這回合...");
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowAIAnalysisNotification()
    {
        if (turnNotificationManager != null)
        {
            turnNotificationManager.ShowAIAnalysisNotification();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_HideAIAnalysisNotification()
    {
        if (turnNotificationManager != null)
        {
            turnNotificationManager.HideNotification();
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
            model = "claude-3-7-sonnet-20250219",
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

                // 設置超時以防止請求卡住太久
                webRequest.timeout = 15; // 15秒超時

                var operation = webRequest.SendWebRequest();

                // 設置安全超時，防止請求卡住
                float timeoutSeconds = 20f;
                float elapsedTime = 0f;

                while (!operation.isDone)
                {
                    await Task.Delay(100); // 每100ms檢查一次
                    elapsedTime += 0.1f;

                    if (elapsedTime >= timeoutSeconds)
                    {
                        Debug.LogError("API請求在 " + timeoutSeconds + " 秒後超時");
                        webRequest.Abort(); // 強制中止請求
                        throw new TimeoutException("API請求超時");
                    }
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Claude response: {webRequest.downloadHandler.text}");
                    var response = JsonConvert.DeserializeObject<ClaudeResponse>(webRequest.downloadHandler.text);
                    if (response?.content == null)
                    {
                        throw new Exception("空或無效的API回應");
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
            throw; // 重新拋出異常，讓上層的錯誤處理邏輯處理
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

                Rpc_UpdateAnalysisText(analysisText);
            }

            CheckWinCondition();

            if (turnNotificationManager != null)
            {
                Rpc_HideAIAnalysisNotification();
            }

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

            if (turnNotificationManager != null)
            {
                Rpc_HideAIAnalysisNotification();
            }

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

    private void UpdateMoodUI(float value, PlayerRef player)
    {
        if (!UnityEngine.Application.isPlaying) return;

        bool isObserver = ObserverManager.Instance != null &&
                         ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);

        if (PlayerMoods.TryGet(player, out var currentMood))
        {
            if (isObserver)
            {
                var players = gameManager.GetConnectedPlayers();
                if (players.Length >= 2)
                {
                    if (player == players[0])
                    {
                        if (moodSlider != null) moodSlider.value = value;
                        if (moodValueText != null) moodValueText.text = value.ToString("F1");
                        UpdateMoodIcon(currentMood.AssignedMood, true);
                    }
                    else if (player == players[1])
                    {
                        if (opponentMoodSlider != null) opponentMoodSlider.value = value;
                        if (opponentMoodValueText != null) opponentMoodValueText.text = value.ToString("F1");
                        UpdateMoodIcon(currentMood.AssignedMood, false);
                    }
                }
            }
            else
            {
                bool isLocalPlayer = player == Runner.LocalPlayer;
                if (isLocalPlayer)
                {
                    if (moodSlider != null) moodSlider.value = value;
                    if (moodValueText != null) moodValueText.text = value.ToString("F1");
                    UpdateMoodIcon(currentMood.AssignedMood, true);
                }
                else
                {
                    if (opponentMoodSlider != null) opponentMoodSlider.value = value;
                    if (opponentMoodValueText != null) opponentMoodValueText.text = value.ToString("F1");
                    UpdateMoodIcon(currentMood.AssignedMood, false);
                }
            }
        }
    }

    private void CheckWinCondition()
    {
        Debug.Log("CheckWinCondition called");

        foreach (var player in gameManager.GetConnectedPlayers())
        {
            if (PlayerMoods.TryGet(player, out var mood) && mood.MoodValue >= WINNING_THRESHOLD)
            {
                Debug.Log($"Player {player} won with mood value {mood.MoodValue}");

                // Set game over state
                IsGameOver = true;

                // Pause the turn timer when a player wins
                if (Object.HasStateAuthority && turnManager != null)
                {
                    turnManager.PauseTimerPermanently();
                    Debug.Log("Turn timer paused permanently");
                }

                Debug.Log($"Announcing winner: {player} with mood: {mood.AssignedMood}");
                Rpc_AnnounceWinnerAndStartAutoReturn(player, mood.AssignedMood.Value);

                return;
            }
        }

        Debug.Log("No winner found yet");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceWinnerAndStartAutoReturn(PlayerRef winner, NetworkString<_32> mood)
    {
        string playerName;
        bool isLocalPlayerWinner = false;

        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            playerName = winner.PlayerId == 1 ? "玩家一" : "玩家二";
        }
        else
        {
            isLocalPlayerWinner = winner == Runner.LocalPlayer;
            playerName = isLocalPlayerWinner ? "你" : "對手";
        }

        string msg = $"{playerName}成功營造出{mood}的氛圍！\n將在 {AUTO_RETURN_TO_LOBBY_DELAY} 秒後返回大廳...";

        if (winnerText != null)
        {
            winnerText.text = msg;
            winnerText.gameObject.SetActive(true); // 確保文字可見
        }
        if (winningImage != null)
        {
            winningImage.gameObject.SetActive(true);
        }

        Debug.Log($"遊戲結束！{msg}");

        if (audioManager != null)
        {
            audioManager.PlayGameEndMusic(isLocalPlayerWinner);
        }

        Debug.Log("啟動自動返回大廳計時器協程");

        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
        }

        // 啟動新的協程並儲存引用
        autoReturnCoroutine = StartCoroutine(AutoReturnToLobbyAfterDelay());
    }

    // 添加一個字段來儲存協程引用
    private Coroutine autoReturnCoroutine;

    private IEnumerator AutoReturnToLobbyAfterDelay()
    {
        // Wait for the specified delay
        float remainingTime = AUTO_RETURN_TO_LOBBY_DELAY;

        // Store the base text (only extract once)
        string baseText = "";
        if (winnerText != null && !string.IsNullOrEmpty(winnerText.text))
        {
            // Extract the first part of the message (before the countdown)
            int countdownIndex = winnerText.text.IndexOf("\n將在");
            if (countdownIndex > 0)
            {
                baseText = winnerText.text.Substring(0, countdownIndex);
            }
        }

        while (remainingTime > 0)
        {
            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;

            // Update the countdown text
            if (winnerText != null && !string.IsNullOrEmpty(baseText))
            {
                winnerText.text = $"{baseText}\n將在 {remainingTime:0} 秒後返回大廳...";
            }
        }

        // Auto return to lobby
        ReturnToLobby();
    }


    public void ReturnToLobby()
    {
        if (Runner != null && Runner.IsRunning)
        {
            // 如果我們有狀態權限，為所有客戶端初始化場景更改
            if (Object.HasStateAuthority)
            {
                Rpc_ReturnToLobby();
            }
            else
            {
                // 否則，請求主機更改場景
                Rpc_RequestReturnToLobby(Runner.LocalPlayer);
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestReturnToLobby(PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"Player {requestingPlayer} requested to return to lobby");
        Rpc_ReturnToLobby();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ReturnToLobby()
    {
        Debug.Log("Returning to lobby");
        LoadLobbyScene();
    }

    private IEnumerator PrepareForLobbyReturn()
    {
        Debug.Log("Preparing to return to lobby...");

        // 確保所有客戶端都有時間清理必要的資源
        CleanupBeforeSceneChange();

        // 等待一幀，確保所有清理操作完成
        yield return null;

        // 實際進行場景加載
        LoadLobbyScene();
    }

    private void CleanupBeforeSceneChange()
    {
        Debug.Log("Cleaning up resources before scene change");

        // 停止所有相關協程
        StopAllCoroutines();

        // 停止所有音效
        if (audioManager != null && audioManager.musicSource != null)
        {
            audioManager.musicSource.Stop();
        }

        // 清理任何可能的引用
        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }
        if (winningImage != null)
        {
            winningImage.gameObject.SetActive(false);
        }

        // 重置遊戲狀態
        IsGameOver = false;

        // 通知其他系統清理
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PrepareForSceneChange();
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PrepareForSceneChange();
        }
    }


    private async void LoadLobbyScene()
    {
        try
        {
            if (Object == null || !Object.IsValid)
            {
                Debug.LogError("Cannot load lobby scene: NetworkObject is null or invalid");
                return;
            }

            if (Runner == null)
            {
                Debug.LogError("Cannot load lobby scene: NetworkRunner is null");
                return;
            }

            if (!Object.HasStateAuthority)
            {
                Debug.Log("Non-authority client waiting for scene change");
                return;
            }

            Debug.Log("Authority client is loading lobby scene");

            // 在這裡不要使用 RPC，因為場景加載後可能會導致 NetworkObject 失效
            // 相反，我們使用更直接的方法通知其他客戶端

            // 假設大廳是場景索引 1
            SceneRef lobbyScene = SceneRef.FromIndex(1);

            // 使用 Single 模式完全重新載入場景
            await Runner.LoadScene(lobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);

            Debug.Log("大廳場景成功載入");

            // 場景加載完成後，不再呼叫 RPC，因為之前的 NetworkObject 已經不存在
            // 相反，我們依賴場景加載後的初始化邏輯
        }
        catch (System.Exception e)
        {
            Debug.LogError($"載入大廳場景失敗: {e.Message}\nStack trace: {e.StackTrace}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PrepareForSceneLoad()
    {
        Debug.Log("Received notification to prepare for scene load");
        // 確保 UI 元素顯示加載狀態
        if (winnerText != null)
        {
            winnerText.text = "正在返回大廳...";
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SceneLoadComplete()
    {
        Debug.Log("Scene load complete notification received");
        StartCoroutine(InitializeLobby());
    }

    private IEnumerator InitializeLobby()
    {
        Debug.Log("Initializing lobby scene");

        // 等待一幀以確保所有對象都已加載
        yield return null;

        // 等待網絡運行器就緒
        NetworkRunner runner = FindObjectOfType<NetworkRunner>();
        float timeout = 0f;
        while (runner == null && timeout < 5f)
        {
            runner = FindObjectOfType<NetworkRunner>();
            timeout += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (runner == null)
        {
            Debug.LogError("Could not find NetworkRunner in the scene");
            yield break;
        }

        Debug.Log("Found NetworkRunner, initializing DeckSelector");

        // 初始化卡組選擇器
        DeckSelector deckSelector = FindObjectOfType<DeckSelector>();
        if (deckSelector != null)
        {
            deckSelector.Wait_Runner_Spawned();
        }

        // 查找 CanvasManager 並確保它顯示正確的頁面
        CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
        if (canvasManager != null)
        {
            // 強制刷新 Canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                canvas.enabled = false;
                canvas.enabled = true;
            }

            // 設置初始頁面
            canvasManager.ShowPage("DeckSelectCanvas");
        }

        Debug.Log("Lobby initialization complete");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PrepareForLobbyReturn()
    {
        Debug.Log("準備返回大廳，清理當前場景資源...");

        // 找到所有需要在場景轉換前清理的物件
        // 例如：停止所有協程、關閉所有 UI 等

        // 停止所有音效
        if (audioManager != null && audioManager.musicSource != null)
        {
            audioManager.musicSource.Stop();
        }

        // 清除任何可能干擾新場景的靜態變數或單例引用
        // 注意：不要清除 NetworkRunner 或其他網路相關的必要組件
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_LobbySceneLoaded()
    {
        Debug.Log("大廳場景已載入，初始化大廳物件...");

        StartCoroutine(InitializeLobbyObjects());
    }

    private IEnumerator InitializeLobbyObjects()
    {
        // 等待一幀，確保場景完全載入
        yield return null;

        // 查找並初始化關鍵的大廳物件
        DeckSelector deckSelector = FindObjectOfType<DeckSelector>();
        if (deckSelector != null)
        {
            deckSelector.Wait_Runner_Spawned();
        }

        // 查找 CanvasManager 並確保它正確顯示初始頁面
        CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
        if (canvasManager != null)
        {
            // 強制刷新 Canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                canvas.enabled = false;
                canvas.enabled = true;
            }

            // 強制顯示初始頁面
            canvasManager.ShowPage("RuleDescriptCanvas1");
        }
    }

    public bool IsGameFinished()
    {
        return IsGameOver;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyMoodUpdate(PlayerRef player, float newValue, float change)
    {
        string playerName = player == Runner.LocalPlayer ? "你" : "對手";
        string changeText = change >= 0 ? $"+{change}" : change.ToString();
        Debug.Log($"{playerName}的{PlayerMoods.Get(player).AssignedMood}氛圍值 {changeText} (當前: {newValue})");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_UpdateAnalysisText(string analysis)
    {
        bool isObserver = ObserverManager.Instance != null &&
                 ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);

        if (analysisText != null)
        {
            analysisText.gameObject.SetActive(isObserver);
            if (isObserver)
            {
                analysisText.text = analysis;
            }
        }

        if (responseBG != null)
        {
            responseBG.SetActive(isObserver);
        }
    }

    private void UpdateMoodIcon(NetworkString<_32> mood, bool isPlayerIcon)
    {
        if (moodIconPaths.TryGetValue(mood.Value, out string resourcePath))
        {
            Debug.Log("updating icon");

            Sprite iconSprite = Resources.Load<Sprite>(resourcePath);

            if (iconSprite != null)
            {
                if (isPlayerIcon && playerMoodIcon != null)
                {
                    playerMoodIcon.sprite = iconSprite;
                }
                else if (!isPlayerIcon && opponentMoodIcon != null)
                {
                    opponentMoodIcon.sprite = iconSprite;
                }
            }
            else
            {
                Debug.LogError($"Failed to load mood icon at path: {resourcePath}");
            }
        }
    }

    private void Update()
    {
        // 檢查是否按下了 P 鍵
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("強制結束遊戲");

            // 如果為 StateAuthority，直接結束遊戲
            if (Object.HasStateAuthority)
            {
                // 設置遊戲結束狀態
                IsGameOver = true;

                // 暫停計時器
                if (turnManager != null)
                {
                    turnManager.PauseTimerPermanently();
                }

                // 通知所有客戶端遊戲被強制結束
                Rpc_AnnounceForceGameEnd();
            }
            else
            {
                // 非 StateAuthority 客戶端請求強制結束遊戲
                Rpc_RequestForceGameEnd(Runner.LocalPlayer);
            }
        }
    }

    // 從非 StateAuthority 客戶端請求強制結束遊戲
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestForceGameEnd(PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"玩家 {requestingPlayer} 請求強制結束遊戲");

        // 設置遊戲結束狀態
        IsGameOver = true;

        // 暫停計時器
        if (turnManager != null)
        {
            turnManager.PauseTimerPermanently();
        }

        // 通知所有客戶端遊戲被強制結束
        Rpc_AnnounceForceGameEnd();
    }

    // 通知所有客戶端遊戲被強制結束
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceForceGameEnd()
    {
        if (winnerText != null)
        {
            winnerText.text = "遊戲已被強制結束！";
            winnerText.gameObject.SetActive(true);
        }
        if (winningImage != null)
        {
            winningImage.gameObject.SetActive(true);
        }

        Debug.Log("遊戲已被強制結束！");

        // 啟動返回大廳倒計時
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
        }

        autoReturnCoroutine = StartCoroutine(AutoReturnToLobbyAfterDelay());
    }
    private void OnDestroy()
    {
        if (audioManager != null)
        {
            if (audioManager.musicSource != null && audioManager.musicSource.isPlaying)
            {
                audioManager.musicSource.Stop();
            }
        }
    }
}