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
    [SerializeField] private TextMeshProUGUI firstPlayerAnnouncement;  // �s�W�Ω���֥ܽ��⪺��r

    [Header("UI Animation Settings")]
    [SerializeField] private float announcementDisplayTime = 3f;  // ��ܥ��⤽�i���ɶ�
    [SerializeField] private Color turnHighlightColor = Color.yellow;  // ��e�^�X�����G�C��
    [SerializeField] private Color normalTextColor = Color.white;  // ���`��r�C��

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

    // ���Ĭ���
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip turnStartSound;
    [SerializeField] private AudioClip turnEndSound;
    [SerializeField] private AudioClip timeWarningSound;

    private bool hasPlayedWarningSound = false;
    private const float WARNING_TIME = 5f;  // �Ѿl5��ɼ���ĵ�i����

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

        // �T�O�� AudioSource
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

        // ��l�� UI
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (turnText != null) turnText.text = "���ݹC���}�l...";
        if (timerText != null) timerText.text = "";
        if (firstPlayerAnnouncement != null) firstPlayerAnnouncement.gameObject.SetActive(false);
    }

    private IEnumerator WaitForPlayersAndStart()
    {
        while (gameManager.NetworkedPlayerStatuses.Count < GameManager.MAX_PLAYERS)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // �H���M�w���⪱�a
        DetermineFirstPlayer();
    }

    private void DetermineFirstPlayer()
    {
        if (!Object.HasStateAuthority) return;

        // ����Ҧ��w�s�������a
        PlayerRef[] players = gameManager.GetConnectedPlayers();

        // �T�O�ڭ̦����������a
        if (players.Length >= 2)
        {
            // �H����ܥ��⪱�a
            int randomIndex = UnityEngine.Random.Range(0, 2);
            CurrentTurnPlayer = players[randomIndex];

            // �}�l�Ĥ@�Ӧ^�X
            IsGameStarted = true;
            StartTurn();

            // �q���Ҧ����a�֬O����
            Rpc_AnnounceFirstPlayer(CurrentTurnPlayer);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceFirstPlayer(PlayerRef firstPlayer)
    {
        string playerName = firstPlayer == runner.LocalPlayer ? "�A" : "���";

        // ��ܥ��⤽�i
        if (firstPlayerAnnouncement != null)
        {
            firstPlayerAnnouncement.text = $"�C���}�l�I{playerName}����";
            firstPlayerAnnouncement.gameObject.SetActive(true);
            StartCoroutine(HideAnnouncementAfterDelay());
        }

        // ����}�l����
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

        // ����^�X�}�l����
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

            // �ˬd�O�_�ݭn����ĵ�i����
            if (!hasPlayedWarningSound && remainingTime <= WARNING_TIME)
            {
                hasPlayedWarningSound = true;
                Rpc_PlayTimeWarningSound();
            }

            // �ˬd�^�X�ɶ��O�_����
            if (TurnTimer.Expired(runner))
            {
                SwitchToNextPlayer();
            }
        }

        // ��s UI
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
            timerText.text = $"�Ѿl�ɶ�: {remainingTime:F1}��";

            // ��ɶ��֩�5��ɧ����C��
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
            string playerName = isLocalPlayerTurn ? "�A��" : "��⪺";
            turnText.text = $"�{�b�O{playerName}�^�X";

            // ��s�C��
            turnText.color = isLocalPlayerTurn ? turnHighlightColor : normalTextColor;
        }
    }

    public void SwitchToNextPlayer()
    {
        if (!Object.HasStateAuthority) return;

        // ����^�X��������
        Rpc_PlayTurnEndSound();

        // �����e���a�����
        PlayerRef nextPlayer = gameManager.GetOpponentPlayer(CurrentTurnPlayer);
        if (nextPlayer != PlayerRef.None)
        {
            CurrentTurnPlayer = nextPlayer;
            // ���m�^�X�p�ɾ�
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

    // �ˬd�O�_����e���a���^�X
    public bool IsPlayerTurn(PlayerRef playerRef)
    {
        return IsGameStarted && CurrentTurnPlayer == playerRef;
    }

    // ���o��e�^�X���a
    public PlayerRef GetCurrentTurnPlayer()
    {
        return CurrentTurnPlayer;
    }

    // ���o�Ѿl�ɶ�
    public float GetRemainingTime()
    {
        if (TurnTimer.IsRunning)
        {
            return TurnTimer.RemainingTime(runner).GetValueOrDefault();
        }
        return 0f;
    }
}