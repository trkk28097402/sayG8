using Fusion;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using System;

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

    private const float TURN_DURATION = 30.0f;
    private const float WARNING_TIME = 5f;
    private bool hasPlayedWarningSound = false;
    private bool localInitialized = false;

    public static TurnManager Instance { get; private set; }
    private GameManager gameManager;
    private NetworkRunner runner;

    private void Awake()
    {
        Debug.Log("TurnManager Awake called");
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
        Debug.Log("Starting TurnManager initialization");

        // ���� NetworkRunner
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

        // ���� GameManager
        while (GameManager.Instance == null)
        {
            Debug.Log("Waiting for GameManager...");
            yield return new WaitForSeconds(0.1f);
        }
        gameManager = GameManager.Instance;
        Debug.Log("GameManager found");

        // ��l�� UI
        InitializeUI();

        // �]�m���a��l�ƼаO
        localInitialized = true;
        Debug.Log("Local initialization completed");

        // �p�G�O�D���A�]�m������l�ƼаO
        if (Object.HasStateAuthority)
        {
            NetworkedInitialized = true;
            Debug.Log("Network initialization completed");
            StartCoroutine(WaitForPlayersAndStart());
        }
    }

    private void InitializeUI()
    {
        if (turnText != null) turnText.text = "���ݹC���}�l...";
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
            StartTurn();
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
        string playerName = firstPlayer == runner.LocalPlayer ? "�A" : "���";

        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.text = $"�C���}�l�I{playerName}����";
            firstPlayerAnnouncement.gameObject.SetActive(true);
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

    private void StartTurn()
    {
        if (!Object.HasStateAuthority) return;

        TurnTimer = TickTimer.CreateFromSeconds(runner, TURN_DURATION);
        hasPlayedWarningSound = false;
        Rpc_PlayTurnStartSound();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTurnStartSound()
    {
        PlaySound(turnStartSound);
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

        if (Object.HasStateAuthority && TurnTimer.IsRunning)
        {
            float remainingTime = TurnTimer.RemainingTime(runner).GetValueOrDefault();

            if (!hasPlayedWarningSound && remainingTime <= WARNING_TIME)
            {
                hasPlayedWarningSound = true;
                Rpc_PlayTimeWarningSound();
            }

            if (TurnTimer.Expired(runner))
            {
                Debug.Log("Turn timer expired, switching players");
                SwitchToNextPlayer();
            }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        UpdateTimerUI();
        UpdateTurnUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTimeWarningSound()
    {
        PlaySound(timeWarningSound);
    }

    private void UpdateTimerUI()
    {
        if (timerText != null && TurnTimer.IsRunning)
        {
            float remainingTime = TurnTimer.RemainingTime(runner).GetValueOrDefault();
            timerText.text = $"�Ѿl�ɶ�: {remainingTime:F1}��";
            timerText.color = remainingTime <= WARNING_TIME ? Color.red : normalTextColor;
        }
    }

    private void UpdateTurnUI()
    {
        if (turnText != null && IsGameStarted)
        {
            bool isLocalPlayerTurn = CurrentTurnPlayer == runner.LocalPlayer;
            string playerName = isLocalPlayerTurn ? "�A��" : "��⪺";
            turnText.text = $"�{�b�O{playerName}�^�X";
            turnText.color = isLocalPlayerTurn ? turnHighlightColor : normalTextColor;
        }
    }

    public void SwitchToNextPlayer()
    {
        if (!Object.HasStateAuthority) return;

        Rpc_PlayTurnEndSound();

        PlayerRef nextPlayer = gameManager.GetOpponentPlayer(CurrentTurnPlayer);
        if (nextPlayer != PlayerRef.None)
        {
            Debug.Log($"Switching turn from {CurrentTurnPlayer} to {nextPlayer}");
            CurrentTurnPlayer = nextPlayer;
            StartTurn();
        }
        else
        {
            Debug.LogError("Could not find next player");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayTurnEndSound()
    {
        PlaySound(turnEndSound);
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

        Debug.Log($"TurnManager initialization check:" +
                 $"\nObject valid: {objectValid}" +
                 $"\nRunner valid: {runnerValid}" +
                 $"\nGameManager valid: {gameManagerValid}" +
                 $"\nLocal initialized: {localInitialized}" +
                 $"\nNetworked initialized: {NetworkedInitialized}");

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
        if (!IsFullyInitialized() || !TurnTimer.IsRunning) return 0f;
        return TurnTimer.RemainingTime(runner).GetValueOrDefault();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}