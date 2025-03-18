using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections;

public class GameReadySystem : NetworkBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private GameObject loadingUI;
    [SerializeField] private TextMeshProUGUI loadingText;
    AudioManagerLobby audioManagerLobby;

    [Header("Input Settings")]
    [SerializeField] private bool enableEnterKeyReady = true;
    [SerializeField] private float inputCooldown = 0.3f;

    [Header("Auto-Cancel Settings")]
    [SerializeField] private float readyTimeoutSeconds = 30f;
    [SerializeField] private TextMeshProUGUI countdownText;

    private SceneRef[] availableScenes;
    [Networked] private SceneRef SelectedScene { get; set; }
    [Networked] private NetworkDictionary<PlayerRef, bool> PlayersReady { get; }
    private bool isLoading = false;
    private bool isObserverSetup = false;
    private bool isLocalPlayerReady = false;
    private float lastInputTime = 0f;
    private float readyTimestamp = 0f;
    private Coroutine timeoutCoroutine;

    private void Awake()
    {
        audioManagerLobby = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManagerLobby>();
    }

    private void Update()
    {
        // 檢查 Enter 鍵輸入
        if (enableEnterKeyReady && !isLocalPlayerReady && !isLoading && Time.time - lastInputTime > inputCooldown)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnReadyButtonClicked();
                lastInputTime = Time.time;
            }
        }

        // 顯示倒數計時（如果玩家已準備好且倒數文字存在）
        if (isLocalPlayerReady && !isLoading && countdownText != null)
        {
            float remainingTime = readyTimeoutSeconds - (Time.time - readyTimestamp);
            if (remainingTime > 0)
            {
                countdownText.text = $"自動取消: {Mathf.CeilToInt(remainingTime)}";
                countdownText.gameObject.SetActive(true);
            }
            else
            {
                countdownText.gameObject.SetActive(false);
            }
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeSceneRefs();
        }

        // 設置初始UI
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // 開始檢查 ObserverManager
        StartCoroutine(WaitForObserverManager());
    }

    private IEnumerator WaitForObserverManager()
    {
        while (!isObserverSetup)
        {
            if (ObserverManager.Instance != null)
            {
                isObserverSetup = true;
                Debug.Log("ObserverManager is loaded in gamereadysystem");
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void InitializeSceneRefs()
    {
        var scenes = new System.Collections.Generic.List<SceneRef>();

        foreach (var sceneData in GameSceneDatabase.Scenes)
        {
            try
            {
                var sceneRef = SceneRef.FromIndex(sceneData.buildIndex);
                scenes.Add(sceneRef);
                Debug.Log($"Successfully added scene: {sceneData.sceneName} with index: {sceneData.buildIndex}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create SceneRef for {sceneData.sceneName}: {e.Message}");
            }
        }

        if (scenes.Count == 0)
        {
            Debug.LogError("No scenes found in GameSceneDatabase!");
            return;
        }

        availableScenes = scenes.ToArray();
    }

    private void OnReadyButtonClicked()
    {
        // 檢查是否為觀察者（如果 ObserverManager 已存在）
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            return;
        }

        if (isLoading) return;

        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        RPC_PlayerReady(Runner.LocalPlayer);

        if (loadingUI != null)
        {
            loadingUI.SetActive(true);
            if (loadingText != null)
            {
                loadingText.text = "等待其他玩家...";
            }
        }

        if (readyButton != null)
        {
            readyButton.interactable = false;
        }

        // 設定本地玩家準備狀態和倒數計時
        isLocalPlayerReady = true;
        readyTimestamp = Time.time;

        // 啟動超時計時器
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
        }
        timeoutCoroutine = StartCoroutine(ReadyTimeout());
    }

    // 超時自動取消準備的協程
    private IEnumerator ReadyTimeout()
    {
        yield return new WaitForSeconds(readyTimeoutSeconds);

        // 如果仍在等待中（尚未開始載入遊戲）
        if (isLocalPlayerReady && !isLoading)
        {
            CancelReady();
        }
    }

    // 取消準備的方法
    public void CancelReady()
    {
        if (!isLocalPlayerReady || isLoading) return;

        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        RPC_PlayerCancelReady(Runner.LocalPlayer);

        // 重設UI
        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        if (readyButton != null)
        {
            readyButton.interactable = true;
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        isLocalPlayerReady = false;

        // 停止計時器
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_PlayerReady(PlayerRef player)
    {
        // 確保 RPC 不會被觀察者觸發
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player))
        {
            return;
        }

        PlayersReady.Set(player, true);
        Debug.Log($"{player} is ready!");
        CheckAllPlayersReady();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_PlayerCancelReady(PlayerRef player)
    {
        // 確保 RPC 不會被觀察者觸發
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player))
        {
            return;
        }

        if (PlayersReady.TryGet(player, out bool _))
        {
            PlayersReady.Remove(player);
            Debug.Log($"{player} canceled ready!");

            // 通知所有其他玩家某位玩家取消準備
            RPC_NotifyPlayerCanceledReady(player);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerCanceledReady(PlayerRef player)
    {
        // 如果是本地玩家，UI 已在 CancelReady 方法中更新
        if (player == Runner.LocalPlayer)
            return;

        // 如果本地玩家已準備且正在等待，顯示通知
        if (isLocalPlayerReady && !isLoading && loadingText != null)
        {
            loadingText.text = "有玩家取消準備，繼續等待...";
        }
    }

    private void CheckAllPlayersReady()
    {
        if (!Object.HasStateAuthority) return;

        bool allReady = true;
        int readyPlayerCount = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            // 檢查是否為觀察者
            if (ObserverManager.Instance.IsPlayerObserver(player))
            {
                Debug.Log($"bypass observer {player}");
                continue;
            }

            readyPlayerCount++;
            if (!PlayersReady.TryGet(player, out bool isReady) || !isReady)
            {
                Debug.Log($"{player} is not ready");
                allReady = false;
                break;
            }
        }

        if (allReady && readyPlayerCount == 2)
        {
            int randomIndex = Random.Range(0, availableScenes.Length);
            SelectedScene = availableScenes[randomIndex];
            RPC_StartGame();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGame()
    {
        if (isLoading) return;

        if (loadingText != null)
        {
            loadingText.text = "載入遊戲中...";
        }

        // 停止計時器
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        isLoading = true;
        LoadSelectedScene();
    }

    private async void LoadSelectedScene()
    {
        if (Object.HasStateAuthority)
        {
            try
            {
                await Runner.LoadScene(SelectedScene, LoadSceneMode.Single);
                Debug.Log($"Scene loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load scene: {e.Message}");
                isLoading = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!isLoading) return;

        if (loadingUI != null &&
            (ObserverManager.Instance == null || !ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer)))
        {
            loadingUI.SetActive(false);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
        }

        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
        }
    }
}