using Fusion;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

// 這個版本不嘗試使用任何 Fusion 的預測屬性，
// 而是完全依賴 Unity 的 Update 方法來處理計時
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
    private NetworkBool IsGameStarted { get; set; }

    [Networked]
    private NetworkBool NetworkedInitialized { get; set; }

    [Networked]
    private NetworkBool IsTimerRunning { get; set; }

    [Networked]
    private NetworkBool HasShownGameStartInfo { get; set; }

    [Networked]
    public float TimerDuration { get; set; }

    // 本地計時變數
    private float localTimerStartTime;
    private float localRemainingTime = 0f;
    private bool hasPlayedWarningSound = false;
    private bool localInitialized = false;
    private bool localTimerPaused = false;
    private bool isMyTurn = false;

    private const float TURN_DURATION = 60.0f;
    private const float WARNING_TIME = 5f;

    private TurnNotificationManager turnNotificationManager;
    private GameManager gameManager;
    private NetworkRunner runner;
    private MoodEvaluator moodEvaluator;

    public static TurnManager Instance { get; private set; }

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
        Debug.Log($"TurnManager Spawned on player {Runner?.LocalPlayer} - HasStateAuthority: {Object.HasStateAuthority}, HasInputAuthority: {Object.HasInputAuthority}");
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

        turnNotificationManager = FindObjectOfType<TurnNotificationManager>();
        if (turnNotificationManager == null)
        {
            Debug.LogWarning("TurnNotificationManager not found!");
        }

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
            HasShownGameStartInfo = false;

            Rpc_ShowGameStartInfo();
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

        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }

        StartCoroutine(CollectAndShowGameStartInfo());
    }

    private IEnumerator CollectAndShowGameStartInfo()
    {
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.5f);

        PlayerRef[] players = gameManager.GetConnectedPlayers();
        if (players.Length < 2)
        {
            Debug.LogError("Can't show game start info: Missing players");

            if (Object.HasStateAuthority)
            {
                StartTurn(CurrentTurnPlayer);
                Rpc_NotifyGameStarted();
            }
            yield break;
        }

        if (moodEvaluator == null)
        {
            moodEvaluator = FindObjectOfType<MoodEvaluator>();
            if (moodEvaluator == null)
            {
                Debug.LogError("Can't show game start info: MoodEvaluator not found");

                if (Object.HasStateAuthority)
                {
                    StartTurn(CurrentTurnPlayer);
                    Rpc_NotifyGameStarted();
                }
                yield break;
            }
        }

        if (Object.HasStateAuthority)
        {
            Rpc_SyncMoodInfoForGameStart();
            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            Rpc_RequestMoodInfoForGameStart(Runner.LocalPlayer);
            yield return new WaitForSeconds(0.2f);
        }

        // 修改為新的訊息格式
        bool isObserver = ObserverManager.Instance != null &&
                         ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);

        string player1MoodInfo = GetPlayerMoodInfo(players[0]);
        string player1DeckInfo = GetPlayerDeckInfo(players[0]);
        string player2MoodInfo = GetPlayerMoodInfo(players[1]);
        string player2DeckInfo = GetPlayerDeckInfo(players[1]);

        string player1Display;
        string player2Display;

        if (isObserver)
        {
            // 觀察者視角
            player1Display = $"玩家1的目標氣氛: {player1MoodInfo}";
            player2Display = $"玩家2的目標氣氛: {player2MoodInfo}";
        }
        else if (Runner.LocalPlayer == players[0])
        {
            // 玩家1視角
            player1Display = $"你的目標氣氛: {player1MoodInfo}";
            player2Display = $"對手的目標氣氛: {player2MoodInfo}";
        }
        else
        {
            // 玩家2視角
            player1Display = $"對手的目標氣氛: {player1MoodInfo}";
            player2Display = $"你的目標氣氛: {player2MoodInfo}";
        }

        if (turnNotificationManager != null)
        {
            Debug.Log($"Showing game info: {player1Display} | {player2Display}");
            turnNotificationManager.ShowGameStartInfoNotification(player1Display, player2Display);
        }
        else
        {
            Debug.LogError("TurnNotificationManager is null, can't show game start info");

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

        if (moodEvaluator != null)
        {
            PlayerRef[] players = gameManager.GetConnectedPlayers();
            foreach (var player in players)
            {
                if (moodEvaluator.PlayerMoods.TryGet(player, out var mood))
                {
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

        PlayerRef[] players = gameManager.GetConnectedPlayers();
        foreach (var player in players)
        {
            if (moodEvaluator != null && moodEvaluator.PlayerMoods.TryGet(player, out var mood))
            {
                moodEvaluator.Rpc_SyncMoodValue(player, mood.MoodValue, mood.AssignedMood);
                Debug.Log($"Sending mood sync for player {player} to requester {requestingPlayer}: {mood.AssignedMood}");
            }
        }
    }

    private string GetPlayerMoodInfo(PlayerRef player)
    {
        string moodInfo = "未知情緒";

        if (moodEvaluator != null && moodEvaluator.PlayerMoods.TryGet(player, out var mood))
        {
            moodInfo = mood.AssignedMood.Value;
        }

        return moodInfo;
    }

    private string GetPlayerDeckInfo(PlayerRef player)
    {
        string deckInfo = "未知卡組";

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

    public void StartTimerAfterNotification()
    {
        if (Object.HasStateAuthority && !HasShownGameStartInfo)
        {
            Debug.Log("Starting turn after game info notification");
            HasShownGameStartInfo = true;
            StartTurn(CurrentTurnPlayer);

            Rpc_NotifyGameStarted();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyGameStarted()
    {
        Debug.Log("Game started after showing game info");
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

        // 在回合切換時，判斷是否為本地玩家的回合
        isMyTurn = newPlayer == Runner.LocalPlayer;

        // 重置本地計時器狀態
        localTimerPaused = false;
        localTimerStartTime = Time.time;
        localRemainingTime = TURN_DURATION;
        hasPlayedWarningSound = false;

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

        // 向所有客戶端通知開始新回合
        Rpc_NotifyTurnStart();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyTurnStart()
    {
        Debug.Log($"Turn started notification received");
        IsTimerRunning = true;

        // 重新設置本地計時器
        localTimerStartTime = Time.time;
        localRemainingTime = TURN_DURATION;
        localTimerPaused = false;
        hasPlayedWarningSound = false;

        // 更新是否為本地玩家的回合
        isMyTurn = CurrentTurnPlayer == Runner.LocalPlayer;

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

    // 使用 Unity 的標準 Update 方法來處理計時
    private void Update()
    {
        if (!IsFullyInitialized() || !IsGameStarted) return;

        if (IsTimerRunning)
        {
            // 只在自己的回合進行倒數計時
            if (isMyTurn && !localTimerPaused)
            {
                float elapsedTime = Time.time - localTimerStartTime;
                localRemainingTime = Mathf.Max(0, TURN_DURATION - elapsedTime);

                // 低於警告時間播放警告音效
                if (!hasPlayedWarningSound && localRemainingTime <= WARNING_TIME)
                {
                    hasPlayedWarningSound = true;
                    PlaySound(timeWarningSound);
                }

                // 時間到自動結束回合
                if (localRemainingTime <= 0)
                {
                    Debug.Log("Local player's turn timer expired");

                    // 通知伺服器玩家時間已到
                    Rpc_PlayerTimeExpired(Runner.LocalPlayer);

                    // 本地停止計時
                    localTimerPaused = true;
                }
            }

            // 更新UI
            UpdateUI();
        }
    }

    // 保留 FixedUpdateNetwork 以防萬一它在某些客戶端上工作
    public override void FixedUpdateNetwork()
    {
        // 如果可能的話，輸出一條日誌以確認它正在運行
        if (Time.frameCount % 300 == 0) // 大約每 10 秒
        {
            Debug.Log($"FixedUpdateNetwork called on player {Runner?.LocalPlayer}");
        }

        // 不需要在這裡執行任何計時邏輯，因為 Update 方法已經處理了
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_PlayerTimeExpired(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        // 確認是否為當前回合的玩家
        if (player == CurrentTurnPlayer)
        {
            Debug.Log($"Player {player} time expired, switching turns");
            IsTimerRunning = false;
            SwitchToNextPlayer();
        }
    }

    private void UpdateUI()
    {
        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        // 只有在自己的回合才顯示計時器
        if (timerText != null)
        {
            // 觀察者需要特別處理，顯示當前回合玩家的計時器
            bool isObserver = ObserverManager.Instance != null &&
                             ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer);

            if (isObserver)
            {
                // 觀察者顯示當前回合玩家的倒數計時
                if (IsTimerRunning)
                {
                    timerText.text = $"剩餘時間: {localRemainingTime:F1}秒";
                    timerText.color = localRemainingTime <= WARNING_TIME ? Color.red : normalTextColor;
                }
                else
                {
                    timerText.text = "";
                }
            }
            else if (isMyTurn && IsTimerRunning)
            {
                // 顯示自己的倒數計時
                timerText.text = $"剩餘時間: {localRemainingTime:F1}秒";
                timerText.color = localRemainingTime <= WARNING_TIME ? Color.red : normalTextColor;
            }
            else
            {
                // 不是自己的回合，清空計時器顯示
                timerText.text = "";
            }
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
        else if (isMyTurn)
        {
            // 本地暫停計時器
            localTimerPaused = true;
            // 通知服務器暫停計時
            Rpc_RequestPauseTimer(Runner.LocalPlayer, localRemainingTime);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestPauseTimer(PlayerRef player, float remainingTime)
    {
        if (!Object.HasStateAuthority) return;

        // 檢查是否為當前回合的玩家
        if (player == CurrentTurnPlayer)
        {
            Debug.Log($"Pausing timer for player {player} with {remainingTime} seconds remaining");
            IsTimerRunning = false;
            TimerDuration = remainingTime;
            Rpc_SyncTimerState(false, remainingTime);
        }
    }

    public void PauseTimerPermanently()
    {
        if (Object.HasStateAuthority)
        {
            IsTimerRunning = false;
            Rpc_PauseTimerPermanently();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PauseTimerPermanently()
    {
        Debug.Log("Timer permanently paused due to game ending");
        IsTimerRunning = false;
        localTimerPaused = true;

        // Clear the timer display
        if (timerText != null)
        {
            timerText.text = "";
        }
    }

    public void ResumeTurnTimer()
    {
        if (Object.HasStateAuthority)
        {
            IsTimerRunning = true;
            Rpc_SyncTimerState(true, TimerDuration);
        }
        else if (isMyTurn)
        {
            // 本地恢復計時器
            localTimerPaused = false;
            localTimerStartTime = Time.time; // 重設開始時間
            // 通知服務器恢復計時
            Rpc_RequestResumeTimer(Runner.LocalPlayer, localRemainingTime);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestResumeTimer(PlayerRef player, float remainingTime)
    {
        if (!Object.HasStateAuthority) return;

        // 檢查是否為當前回合的玩家
        if (player == CurrentTurnPlayer)
        {
            Debug.Log($"Resuming timer for player {player} with {remainingTime} seconds remaining");
            IsTimerRunning = true;
            TimerDuration = remainingTime;
            Rpc_SyncTimerState(true, remainingTime);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncTimerState(bool isRunning, float remainingTime)
    {
        Debug.Log($"Syncing timer state: isRunning={isRunning}, remainingTime={remainingTime}");
        IsTimerRunning = isRunning;

        // 如果是自己的回合，更新本地計時器狀態
        if (CurrentTurnPlayer == Runner.LocalPlayer)
        {
            if (isRunning)
            {
                localTimerPaused = false;
                localTimerStartTime = Time.time;
                localRemainingTime = remainingTime;
            }
            else
            {
                localTimerPaused = true;
                localRemainingTime = remainingTime;
            }
        }
    }

    public void DisableFirstPlayerAnnouncement()
    {
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }
    }

    public void PrepareForSceneChange()
    {
        Debug.Log("TurnManager preparing for scene change");

        // 停止所有計時器
        IsTimerRunning = false;
        localTimerPaused = true;

        // 重置遊戲狀態
        IsGameStarted = false;
        NetworkedInitialized = false;
        HasShownGameStartInfo = false;

        // 清除 UI
        if (turnText != null) turnText.text = "";
        if (timerText != null) timerText.text = "";
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }

        // 停止所有相關協程
        StopAllCoroutines();
    }

    // 場景加載完成後的重新初始化
    public void ReinitializeAfterSceneLoad()
    {
        Debug.Log("Reinitializing TurnManager after scene load");

        // 重新初始化本地狀態
        localInitialized = false;
        localTimerPaused = false;
        hasPlayedWarningSound = false;
        isMyTurn = false;
        localRemainingTime = 0f;

        // 重新開始初始化流程
        StartCoroutine(InitializeAfterSpawn());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}