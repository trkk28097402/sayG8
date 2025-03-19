using Fusion;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class TurnManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] public TextMeshProUGUI firstPlayerAnnouncement;

    [Header("UI Animation Settings")]
    [SerializeField] private float announcementDisplayTime = 3f;
    [SerializeField] private Color turnHighlightColor = Color.yellow;
    [SerializeField] private Color normalTextColor = Color.white;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip turnStartSound;
    [SerializeField] private AudioClip turnEndSound;
    [SerializeField] private AudioClip timeWarningSound;

    [Networked]
    public PlayerRef CurrentTurnPlayer { get; set; }

    [Networked]
    private TickTimer TurnTimer { get; set; }

    [Networked]
    private NetworkBool IsGameStarted { get; set; }

    [Networked]
    private NetworkBool NetworkedInitialized { get; set; }

    [Networked]
    private NetworkBool IsTimerRunning { get; set; }

    [Networked]
    private NetworkBool HasShownGameStartInfo { get; set; } // Added new networked property to track if we've shown game start info

    [Networked]
    public float TimerDuration { get; set; }

    [Networked]
    private float TimerStartTime { get; set; }

    private const float TURN_DURATION = 60.0f;
    private const float WARNING_TIME = 5f;
    private bool hasPlayedWarningSound = false;
    private bool localInitialized = false;
    private float localRemainingTime = 0f;
    private TurnNotificationManager turnNotificationManager; // Added reference to TurnNotificationManager

    public static TurnManager Instance { get; private set; }
    private GameManager gameManager;
    private NetworkRunner runner;
    private MoodEvaluator moodEvaluator; // Added reference to MoodEvaluator

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("TurnManager instance set in Awake");
        }
        else if (Instance != this)
        {
            Debug.Log($"Destroying duplicate TurnManager. Existing instance: {Instance.GetInstanceID()}, This instance: {GetInstanceID()}");
            Destroy(gameObject);
            return;
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log($"TurnManager Spawned on player {Runner?.LocalPlayer}");
        StartCoroutine(InitializeAfterSpawn());
    }

    private IEnumerator InitializeAfterSpawn()
    {
        Debug.Log($"Starting TurnManager initialization. Instance: {Instance}, This: {this}");

        while (runner == null)
        {
            runner = Object.Runner;
            if (runner == null)
            {
                Debug.Log("Waiting for NetworkRunner...");
                yield return new WaitForSeconds(0.1f);
            }
        }
        Debug.Log("NetworkRunner found");

        while (GameManager.Instance == null)
        {
            Debug.Log("Waiting for GameManager...");
            yield return new WaitForSeconds(0.1f);
        }
        gameManager = GameManager.Instance;
        Debug.Log("GameManager found");

        // Find the TurnNotificationManager
        turnNotificationManager = FindObjectOfType<TurnNotificationManager>();
        if (turnNotificationManager == null)
        {
            Debug.LogWarning("TurnNotificationManager not found!");
        }

        // Find the MoodEvaluator
        moodEvaluator = FindObjectOfType<MoodEvaluator>();
        if (moodEvaluator == null)
        {
            Debug.LogWarning("MoodEvaluator not found!");
        }

        InitializeUI();

        localInitialized = true;
        Debug.Log($"Local initialization completed. Instance: {Instance}, This: {this}");

        if (Object.HasStateAuthority)
        {
            NetworkedInitialized = true;
            Debug.Log($"Network initialization completed. HasStateAuthority: {Object.HasStateAuthority}");
            StartCoroutine(WaitForPlayersAndStart());
        }
    }

    private void InitializeUI()
    {
        if (turnText != null) turnText.text = "遊戲開始...";
        if (timerText != null) timerText.text = "";
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }
    }

    private IEnumerator WaitForPlayersAndStart()
    {
        Debug.Log("Waiting for players to join...");
        while (gameManager.NetworkedPlayerStatuses.Count < GameManager.MAX_PLAYERS)
        {
            yield return new WaitForSeconds(0.5f);
        }
        Debug.Log("All players joined, determining first player");
        DetermineFirstPlayer();
    }

    private void DetermineFirstPlayer()
    {
        if (!Object.HasStateAuthority) return;

        PlayerRef[] players = gameManager.GetConnectedPlayers();
        Debug.Log($"Connected players count: {players.Length}");

        if (players.Length >= 2)
        {
            int randomIndex = UnityEngine.Random.Range(0, 2);
            CurrentTurnPlayer = players[randomIndex];
            Debug.Log($"First player selected: {CurrentTurnPlayer}");

            IsGameStarted = true;
            HasShownGameStartInfo = false; // Initialize this to false

            // Show game start info before announcing first player or starting turn
            Rpc_ShowGameStartInfo();
            // 不要在這裡調用 Rpc_AnnounceFirstPlayer，而是在通知完成後調用
        }
        else
        {
            Debug.LogError("Not enough players to start the game");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowGameStartInfo()
    {
        Debug.Log("Showing game start info notification");

        // 立即禁用先手提示
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }

        // We'll collect the info about both players' moods and decks
        StartCoroutine(CollectAndShowGameStartInfo());
    }

    private IEnumerator CollectAndShowGameStartInfo()
    {
        // 立即禁用先手通知，防止它短暂显示
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }

        // 等待較长时间确保所有客户端都已获取到情緒数据
        yield return new WaitForSeconds(0.5f);

        PlayerRef[] players = gameManager.GetConnectedPlayers();
        if (players.Length < 2)
        {
            Debug.LogError("Can't show game start info: Missing players");

            // If we can't show the notification, just start the turn
            if (Object.HasStateAuthority)
            {
                StartTurn(CurrentTurnPlayer);
                Rpc_NotifyGameStarted();
            }
            yield break;
        }

        // 确保MoodEvaluator组件已完全初始化
        if (moodEvaluator == null)
        {
            moodEvaluator = FindObjectOfType<MoodEvaluator>();
            if (moodEvaluator == null)
            {
                Debug.LogError("Can't show game start info: MoodEvaluator not found");

                // If we can't show the notification, just start the turn
                if (Object.HasStateAuthority)
                {
                    StartTurn(CurrentTurnPlayer);
                    Rpc_NotifyGameStarted();
                }
                yield break;
            }
        }

        // 使用RPC确保所有玩家都能获取到玩家情绪信息
        if (Object.HasStateAuthority)
        {
            // 获取信息前先向所有玩家同步情绪信息
            Rpc_SyncMoodInfoForGameStart();
            // 给同步留出时间
            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            // 非Authority玩家请求获取情绪信息
            Rpc_RequestMoodInfoForGameStart(Runner.LocalPlayer);
            // 等待情绪信息同步
            yield return new WaitForSeconds(0.2f);
        }

        // Get player 1 info
        string player1MoodInfo = GetPlayerMoodInfo(players[0]);
        string player1DeckInfo = GetPlayerDeckInfo(players[0]);
        string player1Display = $"玩家1: {player1MoodInfo}, 使用 {player1DeckInfo}";

        // Get player 2 info
        string player2MoodInfo = GetPlayerMoodInfo(players[1]);
        string player2DeckInfo = GetPlayerDeckInfo(players[1]);
        string player2Display = $"玩家2: {player2MoodInfo}, 使用 {player2DeckInfo}";

        // Show the notification
        if (turnNotificationManager != null)
        {
            Debug.Log($"Showing game info: {player1Display} | {player2Display}");
            turnNotificationManager.ShowGameStartInfoNotification(player1Display, player2Display);
        }
        else
        {
            Debug.LogError("TurnNotificationManager is null, can't show game start info");

            // If notification manager is not available, start the turn immediately
            if (Object.HasStateAuthority)
            {
                StartTurn(CurrentTurnPlayer);
                Rpc_NotifyGameStarted();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncMoodInfoForGameStart()
    {
        Debug.Log("Syncing mood info for game start notification");

        // 这个RPC会触发所有客户端请求最新的情绪信息
        if (moodEvaluator != null)
        {
            PlayerRef[] players = gameManager.GetConnectedPlayers();
            foreach (var player in players)
            {
                if (moodEvaluator.PlayerMoods.TryGet(player, out var mood))
                {
                    // 这里不需要做什么，仅确保情绪信息已同步到所有客户端
                    Debug.Log($"Mood for player {player}: {mood.AssignedMood}");
                }
                else
                {
                    Debug.LogWarning($"No mood data found for player {player}");
                }
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestMoodInfoForGameStart(PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"Player {requestingPlayer} is requesting mood info for game start");

        // 获取并同步所有玩家的情绪信息
        PlayerRef[] players = gameManager.GetConnectedPlayers();
        foreach (var player in players)
        {
            if (moodEvaluator != null && moodEvaluator.PlayerMoods.TryGet(player, out var mood))
            {
                // 向请求的玩家发送每个玩家的情绪信息
                moodEvaluator.Rpc_SyncMoodValue(player, mood.MoodValue, mood.AssignedMood);
                Debug.Log($"Sending mood sync for player {player} to requester {requestingPlayer}: {mood.AssignedMood}");
            }
        }
    }

    private string GetPlayerMoodInfo(PlayerRef player)
    {
        string moodInfo = "未知情緒";

        // Try to get the mood information from the MoodEvaluator
        if (moodEvaluator != null && moodEvaluator.PlayerMoods.TryGet(player, out var mood))
        {
            moodInfo = mood.AssignedMood.Value;
        }

        return moodInfo;
    }

    private string GetPlayerDeckInfo(PlayerRef player)
    {
        string deckInfo = "未知卡組";

        // Try to get the deck information
        if (GameDeckManager.Instance != null)
        {
            int deckId = GameDeckManager.Instance.GetPlayerDeck(player);
            if (deckId >= 0)
            {
                GameDeckDatabase deckDb = new GameDeckDatabase();
                var deckData = deckDb.GetDeckById(deckId);
                if (deckData != null)
                {
                    deckInfo = deckData.deckName;
                }
            }
        }

        return deckInfo;
    }

    // This method will be called when the game start info notification is complete
    public void StartTimerAfterNotification()
    {
        if (Object.HasStateAuthority && !HasShownGameStartInfo)
        {
            Debug.Log("Starting turn after game info notification");
            HasShownGameStartInfo = true;
            StartTurn(CurrentTurnPlayer);

            // 通知玩家遊戲已經開始，但不顯示先手提示（因為已經在遊戲資訊中顯示了）
            Rpc_NotifyGameStarted();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyGameStarted()
    {
        Debug.Log("Game started after showing game info");
        // 只播放音效，不顯示提示
        PlaySound(turnStartSound);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceFirstPlayer(PlayerRef firstPlayer)
    {
        if (firstPlayerAnnouncement != null)
        {
            StartCoroutine(HideAnnouncementAfterDelay());
        }

        PlaySound(turnStartSound);
    }

    private IEnumerator HideAnnouncementAfterDelay()
    {
        yield return new WaitForSeconds(announcementDisplayTime);
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }
    }

    public void SwitchToNextPlayer()
    {
        Debug.Log($"SwitchToNextPlayer called by {Runner?.LocalPlayer}");
        Rpc_RequestSwitchTurn(CurrentTurnPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestSwitchTurn(PlayerRef currentPlayer)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"Rpc_RequestSwitchTurn received for player {currentPlayer}");
        PlayerRef nextPlayer = gameManager.GetOpponentPlayer(currentPlayer);

        if (nextPlayer != PlayerRef.None)
        {
            Debug.Log($"Switching turn from {currentPlayer} to {nextPlayer}");
            CurrentTurnPlayer = nextPlayer;
            StartTurn(nextPlayer);
            Rpc_NotifyTurnSwitched(nextPlayer);
        }
        else
        {
            Debug.LogError("Could not find next player");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyTurnSwitched(PlayerRef newPlayer)
    {
        Debug.Log($"Turn switched to player {newPlayer}");
        CurrentTurnPlayer = newPlayer;
        PlaySound(turnStartSound);
        UpdateUI();
    }

    private void StartTurn(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"Starting turn for player {player}");
        IsTimerRunning = true;
        hasPlayedWarningSound = false;
        TimerDuration = TURN_DURATION;
        TimerStartTime = Time.time;
        TurnTimer = TickTimer.CreateFromSeconds(Runner, TURN_DURATION);
        Rpc_NotifyTurnStart(TimerStartTime);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyTurnStart(float startTime)
    {
        Debug.Log($"Turn started at time: {startTime}");
        IsTimerRunning = true;
        TimerStartTime = startTime;
        localRemainingTime = TURN_DURATION;
        PlaySound(turnStartSound);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTurnStartSound()
    {
        PlaySound(turnStartSound);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTurnEndSound()
    {
        PlaySound(turnEndSound);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTimeWarningSound()
    {
        PlaySound(timeWarningSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsFullyInitialized() || !IsGameStarted) return;

        if (IsTimerRunning)
        {
            float elapsedTime = Time.time - TimerStartTime;
            localRemainingTime = Mathf.Max(0, TURN_DURATION - elapsedTime);

            if (Object.HasStateAuthority)
            {
                if (!hasPlayedWarningSound && localRemainingTime <= WARNING_TIME)
                {
                    hasPlayedWarningSound = true;
                    Rpc_PlayTimeWarningSound();
                }

                if (localRemainingTime <= 0)
                {
                    Debug.Log("Turn timer expired, switching players");
                    IsTimerRunning = false;
                    SwitchToNextPlayer();
                }
            }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText != null && IsTimerRunning)
        {
            timerText.text = $"剩餘時間: {localRemainingTime:F1}秒";
            timerText.color = localRemainingTime <= WARNING_TIME ? Color.red : normalTextColor;
        }
        else if (timerText != null && !IsTimerRunning)
        {
            timerText.text = "";
        }
    }

    public bool IsPlayerTurn(PlayerRef playerRef)
    {
        if (!IsFullyInitialized())
        {
            Debug.Log($"Turn check failed - TurnManager not fully initialized for player {playerRef}");
            return false;
        }

        try
        {
            bool isTurn = IsGameStarted && CurrentTurnPlayer == playerRef;
            Debug.Log($"Turn check for player {playerRef}: GameStarted={IsGameStarted}, CurrentTurnPlayer={CurrentTurnPlayer}, isTurn={isTurn}");
            return isTurn;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in IsPlayerTurn: {e.Message}");
            return false;
        }
    }

    public bool IsFullyInitialized()
    {
        bool objectValid = Object != null && Object.IsValid;
        bool runnerValid = runner != null;
        bool gameManagerValid = gameManager != null;

        return objectValid &&
               runnerValid &&
               gameManagerValid &&
               localInitialized &&
               NetworkedInitialized;
    }

    public PlayerRef GetCurrentTurnPlayer()
    {
        return CurrentTurnPlayer;
    }

    public float GetRemainingTime()
    {
        return localRemainingTime;
    }

    public void PauseTurnTimer()
    {
        if (Object.HasStateAuthority)
        {
            TimerDuration = localRemainingTime;
            IsTimerRunning = false;
            Rpc_SyncTimerState(false, localRemainingTime);
        }
    }

    public void ResumeTurnTimer()
    {
        if (Object.HasStateAuthority)
        {
            TimerStartTime = Time.time;
            IsTimerRunning = true;
            Rpc_SyncTimerState(true, TimerDuration);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncTimerState(bool isRunning, float remainingTime)
    {
        IsTimerRunning = isRunning;
        if (isRunning)
        {
            TimerStartTime = Time.time;
            TimerDuration = remainingTime;
            localRemainingTime = remainingTime;
        }
    }

    // 提供公共方法让外部禁用先手公告
    public void DisableFirstPlayerAnnouncement()
    {
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}