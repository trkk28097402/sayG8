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
    [SerializeField] private TextMeshProUGUI firstPlayerAnnouncement;

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
    public float TimerDuration { get; set; }

    [Networked]
    private float TimerStartTime { get; set; }

    private const float TURN_DURATION = 30.0f;
    private const float WARNING_TIME = 5f;
    private bool hasPlayedWarningSound = false;
    private bool localInitialized = false;
    private float localRemainingTime = 0f;

    public static TurnManager Instance { get; private set; }
    private GameManager gameManager;
    private NetworkRunner runner;

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
            StartTurn(CurrentTurnPlayer);
            Rpc_AnnounceFirstPlayer(CurrentTurnPlayer);
        }
        else
        {
            Debug.LogError("Not enough players to start the game");
        }
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}