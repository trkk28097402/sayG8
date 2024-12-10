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
    [SerializeField] private TextMeshProUGUI firstPlayerAnnouncement;  // 新增用於顯示誰先手的文字

    [Header("UI Animation Settings")]
    [SerializeField] private float announcementDisplayTime = 3f;  // 顯示先手公告的時間
    [SerializeField] private Color turnHighlightColor = Color.yellow;  // 當前回合的高亮顏色
    [SerializeField] private Color normalTextColor = Color.white;  // 正常文字顏色

    [Networked]
    public PlayerRef CurrentTurnPlayer { get; set; }

    [Networked]
    private TickTimer TurnTimer { get; set; }

    [Networked]
    private NetworkBool IsGameStarted { get; set; }

    private const float TURN_DURATION = 30.0f;
    public static TurnManager Instance { get; private set; }
    private GameManager gameManager;
    private NetworkRunner runner;

    // 音效相關
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip turnStartSound;
    [SerializeField] private AudioClip turnEndSound;
    [SerializeField] private AudioClip timeWarningSound;

    private bool hasPlayedWarningSound = false;
    private const float WARNING_TIME = 5f;  // 剩餘5秒時播放警告音效

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // 確保有 AudioSource
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        runner = Object.Runner;
        gameManager = GameManager.Instance;

        if (Object.HasStateAuthority)
        {
            StartCoroutine(WaitForPlayersAndStart());
        }

        // 初始化 UI
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (turnText != null) turnText.text = "等待遊戲開始...";
        if (timerText != null) timerText.text = "";
        if (firstPlayerAnnouncement != null) firstPlayerAnnouncement.gameObject.SetActive(false);
    }

    private IEnumerator WaitForPlayersAndStart()
    {
        while (gameManager.NetworkedPlayerStatuses.Count < GameManager.MAX_PLAYERS)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 隨機決定先手玩家
        DetermineFirstPlayer();
    }

    private void DetermineFirstPlayer()
    {
        if (!Object.HasStateAuthority) return;

        // 獲取所有已連接的玩家
        PlayerRef[] players = gameManager.GetConnectedPlayers();

        // 確保我們有足夠的玩家
        if (players.Length >= 2)
        {
            // 隨機選擇先手玩家
            int randomIndex = UnityEngine.Random.Range(0, 2);
            CurrentTurnPlayer = players[randomIndex];

            // 開始第一個回合
            IsGameStarted = true;
            StartTurn();

            // 通知所有玩家誰是先手
            Rpc_AnnounceFirstPlayer(CurrentTurnPlayer);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceFirstPlayer(PlayerRef firstPlayer)
    {
        string playerName = firstPlayer == runner.LocalPlayer ? "你" : "對手";

        // 顯示先手公告
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.text = $"遊戲開始！{playerName}先攻";
            firstPlayerAnnouncement.gameObject.SetActive(true);
            StartCoroutine(HideAnnouncementAfterDelay());
        }

        // 播放開始音效
        if (audioSource != null && turnStartSound != null)
        {
            audioSource.PlayOneShot(turnStartSound);
        }
    }

    private IEnumerator HideAnnouncementAfterDelay()
    {
        yield return new WaitForSeconds(announcementDisplayTime);
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.gameObject.SetActive(false);
        }
    }

    private void StartTurn()
    {
        if (!Object.HasStateAuthority) return;

        TurnTimer = TickTimer.CreateFromSeconds(runner, TURN_DURATION);
        hasPlayedWarningSound = false;

        // 播放回合開始音效
        Rpc_PlayTurnStartSound();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTurnStartSound()
    {
        if (audioSource != null && turnStartSound != null)
        {
            audioSource.PlayOneShot(turnStartSound);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsGameStarted) return;

        if (Object.HasStateAuthority && TurnTimer.IsRunning)
        {
            float remainingTime = TurnTimer.RemainingTime(runner).GetValueOrDefault();

            // 檢查是否需要播放警告音效
            if (!hasPlayedWarningSound && remainingTime <= WARNING_TIME)
            {
                hasPlayedWarningSound = true;
                Rpc_PlayTimeWarningSound();
            }

            // 檢查回合時間是否結束
            if (TurnTimer.Expired(runner))
            {
                SwitchToNextPlayer();
            }
        }

        // 更新 UI
        UpdateTimerUI();
        UpdateTurnUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTimeWarningSound()
    {
        if (audioSource != null && timeWarningSound != null)
        {
            audioSource.PlayOneShot(timeWarningSound);
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null && TurnTimer.IsRunning)
        {
            float remainingTime = TurnTimer.RemainingTime(runner).GetValueOrDefault();
            timerText.text = $"剩餘時間: {remainingTime:F1}秒";

            // 當時間少於5秒時改變顏色
            if (remainingTime <= WARNING_TIME)
            {
                timerText.color = Color.red;
            }
            else
            {
                timerText.color = normalTextColor;
            }
        }
    }

    private void UpdateTurnUI()
    {
        if (turnText != null && IsGameStarted)
        {
            bool isLocalPlayerTurn = CurrentTurnPlayer == runner.LocalPlayer;
            string playerName = isLocalPlayerTurn ? "你的" : "對手的";
            turnText.text = $"現在是{playerName}回合";

            // 更新顏色
            turnText.color = isLocalPlayerTurn ? turnHighlightColor : normalTextColor;
        }
    }

    public void SwitchToNextPlayer()
    {
        if (!Object.HasStateAuthority) return;

        // 播放回合結束音效
        Rpc_PlayTurnEndSound();

        // 獲取當前玩家的對手
        PlayerRef nextPlayer = gameManager.GetOpponentPlayer(CurrentTurnPlayer);
        if (nextPlayer != PlayerRef.None)
        {
            CurrentTurnPlayer = nextPlayer;
            // 重置回合計時器
            StartTurn();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTurnEndSound()
    {
        if (audioSource != null && turnEndSound != null)
        {
            audioSource.PlayOneShot(turnEndSound);
        }
    }

    // 檢查是否為當前玩家的回合
    public bool IsPlayerTurn(PlayerRef playerRef)
    {
        return IsGameStarted && CurrentTurnPlayer == playerRef;
    }

    // 取得當前回合玩家
    public PlayerRef GetCurrentTurnPlayer()
    {
        return CurrentTurnPlayer;
    }

    // 取得剩餘時間
    public float GetRemainingTime()
    {
        if (TurnTimer.IsRunning)
        {
            return TurnTimer.RemainingTime(runner).GetValueOrDefault();
        }
        return 0f;
    }
}