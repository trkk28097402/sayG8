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
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject loadingUI;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private CanvasGroup canvasGroup;
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
    private bool isInputEnabled = false;

    // 添加一個標志來追蹤是否已經初始化
    private bool isInitialized = false;

    private void Awake()
    {
        if (audioManagerLobby == null)
        {
            GameObject audioObject = GameObject.FindGameObjectWithTag("Audio");
            if (audioObject != null)
            {
                audioManagerLobby = audioObject.GetComponent<AudioManagerLobby>();
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInParent<CanvasGroup>();

            if (canvasGroup == null && transform.parent != null)
            {
                canvasGroup = transform.parent.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 初始化 UI 元素
        SetupUI();
    }

    private void SetupUI()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelReady);
            cancelButton.gameObject.SetActive(false);
        }

        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    // 由 PageInputHandler 調用，啟用或禁用輸入
    public void SetInputEnabled(bool enabled)
    {
        isInputEnabled = enabled;
        Debug.Log($"GameReadySystem input enabled: {enabled}");

        if (!enabled && countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // 確保我們已經初始化，並且輸入是啟用的
        if (!isInitialized || !isInputEnabled)
        {
            return;
        }

        // 輸入處理：準備按鈕（Enter 鍵）
        if (enableEnterKeyReady && !isLocalPlayerReady && !isLoading && Time.time - lastInputTime > inputCooldown)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Debug.Log("Enter key pressed for Ready");
                OnReadyButtonClicked();
                lastInputTime = Time.time;
            }
        }
        // 輸入處理：取消準備（Escape 鍵）
        else if (isLocalPlayerReady && !isLoading && Time.time - lastInputTime > inputCooldown)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("Escape key pressed for Cancel");
                CancelReady();
                lastInputTime = Time.time;
            }
        }

        // 顯示倒數計時
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

        // 確保 UI 設置正確
        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // 重設本地狀態
        isLocalPlayerReady = false;
        isLoading = false;

        // 如果按鈕在此時才可用，重新添加監聽器
        if (readyButton != null && readyButton.onClick.GetPersistentEventCount() == 0)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (cancelButton != null && cancelButton.onClick.GetPersistentEventCount() == 0)
        {
            cancelButton.onClick.AddListener(CancelReady);
            cancelButton.gameObject.SetActive(false);
        }

        // 開始檢查 ObserverManager
        StartCoroutineSafely(WaitForObserverManager());

        // 標記為已初始化
        isInitialized = true;
        // 默認不啟用輸入，等待 PageInputHandler 調用 SetInputEnabled
        isInputEnabled = false;
    }

    private Coroutine StartCoroutineSafely(IEnumerator routine)
    {
        if (this != null && this.gameObject != null && this.gameObject.activeInHierarchy)
        {
            return StartCoroutine(routine);
        }
        else
        {
            Debug.LogWarning("Cannot start coroutine on inactive GameObject");
            return null;
        }
    }

    private IEnumerator WaitForObserverManager()
    {
        float timeoutCounter = 0f;
        float totalTimeout = 10f;

        while (!isObserverSetup && timeoutCounter < totalTimeout)
        {
            if (ObserverManager.Instance != null)
            {
                isObserverSetup = true;
                Debug.Log("ObserverManager is loaded in gamereadysystem");
                break;
            }
            timeoutCounter += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (!isObserverSetup)
        {
            Debug.LogWarning("ObserverManager setup timed out after " + totalTimeout + " seconds");
            isObserverSetup = true;
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
        // 添加更多的日誌來追蹤問題
        Debug.Log("Ready button clicked or Enter pressed");

        // 確保網絡連接準備就緒
        if (Runner == null || !Runner.IsRunning)
        {
            Debug.LogWarning("Network not ready yet, cannot send ready state");
            return;
        }

        // 檢查是否為觀察者
        if (isObserverSetup && ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            Debug.Log("Player is observer, ignoring ready command");
            return;
        }

        if (isLoading || isLocalPlayerReady)
        {
            Debug.Log("Already loading or ready, ignoring command");
            return;
        }

        if (audioManagerLobby != null)
        {
            audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        }

        // 發送準備就緒 RPC
        Debug.Log("Sending RPC_PlayerReady for: " + Runner.LocalPlayer);
        RPC_PlayerReady(Runner.LocalPlayer);

        // 更新 UI
        UpdateUIForReady();

        // 設定本地玩家準備狀態和倒數計時
        isLocalPlayerReady = true;
        readyTimestamp = Time.time;

        // 啟動超時計時器
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
        }
        timeoutCoroutine = StartCoroutineSafely(ReadyTimeout());
    }

    private void UpdateUIForReady()
    {
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
            readyButton.gameObject.SetActive(false);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(true);
        }
    }

    private IEnumerator ReadyTimeout()
    {
        yield return new WaitForSeconds(readyTimeoutSeconds);

        if (isLocalPlayerReady && !isLoading)
        {
            CancelReady();
        }
    }

    public void CancelReady()
    {
        if (!isLocalPlayerReady || isLoading) return;

        if (Runner == null || !Runner.IsRunning)
        {
            Debug.LogWarning("Network not ready yet, cannot cancel ready state");
            return;
        }

        if (audioManagerLobby != null)
        {
            audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        }

        // 發送取消準備 RPC
        RPC_PlayerCancelReady(Runner.LocalPlayer);

        // 更新 UI
        UpdateUIForCancelReady();

        isLocalPlayerReady = false;

        // 停止計時器
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    private void UpdateUIForCancelReady()
    {
        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(true);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_PlayerReady(PlayerRef player)
    {
        // 確保 RPC 不會被觀察者觸發
        if (isObserverSetup && ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player))
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
        if (isObserverSetup && ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player))
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
            if (isObserverSetup && ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player))
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

                // 在載入失敗時重置玩家狀態
                if (Object.HasStateAuthority)
                {
                    // 清空所有玩家的準備狀態
                    foreach (var player in Runner.ActivePlayers)
                    {
                        if (PlayersReady.TryGet(player, out bool _))
                        {
                            PlayersReady.Remove(player);
                        }
                    }

                    // 通知所有玩家載入失敗
                    RPC_NotifyLoadingFailed();
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyLoadingFailed()
    {
        isLoading = false;
        isLocalPlayerReady = false;

        // 更新 UI 以反映載入失敗
        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(true);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // 顯示錯誤訊息
        if (loadingText != null)
        {
            loadingText.text = "載入失敗，請稍後再試";
            StartCoroutineSafely(HideLoadingMessageAfterDelay(3f));
        }
    }

    private IEnumerator HideLoadingMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (loadingText != null)
        {
            loadingText.text = "";
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

    // 當頁面被激活時，重新啟用輸入並更新 UI 狀態
    public void OnPageActivated()
    {
        Debug.Log("GameReadySystem - Page Activated");

        // 確保輸入被啟用
        SetInputEnabled(true);

        // 確保 UI 狀態與當前遊戲狀態匹配
        if (isLocalPlayerReady)
        {
            UpdateUIForReady();
        }
        else
        {
            UpdateUIForCancelReady();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // 清理按鈕監聽器
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
        }

        // 停止協程
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        // 重置狀態
        isLocalPlayerReady = false;
        isLoading = false;
        isInitialized = false;
        isInputEnabled = false;
    }
}