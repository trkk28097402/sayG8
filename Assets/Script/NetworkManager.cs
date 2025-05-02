using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using Fusion.Sockets;
using System.Linq;
using System.Collections;

public class NetworkRunnerHandler : MonoBehaviour
{
    private void Awake()
    {
        // 確保這個組件在啟動時就被禁用
        enabled = false;
    }
}

public class NetworkManager : MonoBehaviour
{
    [SerializeField] private GameObject PlayerPrefab;
    [SerializeField] private GameObject gameDeckManagerPrefab;
    [SerializeField] private GameObject observerManagerPrefab;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    private bool _isRunning = false;
    private bool _managersSpawned = false;
    private static bool isReturningFromGame = false;
    private bool _isInitializingDeckSelectors = false;

    public static NetworkManager Instance { get; private set; }

    // 公開getter讓其他組件能夠檢查NetworkRunner
    public NetworkRunner Runner => _runner;

    private IEnumerator WaitForObserverManagerAndRegister(PlayerRef player)
    {
        Debug.Log("等待 ObserverManager 初始化...");

        ObserverManager observerManager = null;
        float timeoutDuration = 10f; // 10秒超时
        float elapsedTime = 0f;

        while (observerManager == null && elapsedTime < timeoutDuration)
        {
            observerManager = GameObject.FindObjectOfType<ObserverManager>();
            if (observerManager == null)
            {
                elapsedTime += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
        }

        if (observerManager == null)
        {
            Debug.LogError("找不到 ObserverManager！");
            yield break;
        }

        // 等待 NetworkObject 初始化完成
        while (observerManager.Object == null || !observerManager.Object.IsValid)
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"ObserverManager 已找到且初始化完成");
        observerManager.RegisterObserver(player);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // 註冊場景加載事件
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // 取消註冊場景加載事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        Debug.Log("[NetworkManager] Start called");

        if (isReturningFromGame)
        {
            Debug.Log("[NetworkManager] Detected return from game, restarting game");
            isReturningFromGame = false;
            RestartGame();
        }
        else
        {
            StartGame();
        }
    }

    public void RestartGame()
    {
        Debug.Log("[NetworkManager] RestartGame called");

        // 如果已經在運行，先停止
        if (_isRunning && _runner != null)
        {
            Debug.Log("[NetworkManager] Game already running, stopping first");
            StopGame();
        }

        StartGame();
    }

    //private IEnumerator DelayedGameStart()
    //{
    //    Debug.Log("[NetworkManager] Delayed game start initiated");
    //    yield return new WaitForSeconds(0.5f);
        
    //}

    public void StopGame()
    {
        Debug.Log("[NetworkManager] StopGame called");

        if (_runner != null)
        {
            _isRunning = false;
            _runner.Shutdown();
            Debug.Log("[NetworkManager] Runner shutdown completed");
        }
    }


    private async void StartGame()
    {
        if (_isRunning) return;

        if (_runner == null)
        {
            var runnerObject = new GameObject("NetworkRunner");
            runnerObject.transform.parent = transform;
            _runner = runnerObject.AddComponent<NetworkRunner>();

            _sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

            var behaviourConfiguration = runnerObject.AddComponent<NetworkRunnerHandler>();
            behaviourConfiguration.enabled = false;

            DontDestroyOnLoad(runnerObject);
        }

        var startGameArgs = new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = "TestRoom",
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = _sceneManager,
            PlayerCount = 4, // 一定要4，不知道為什麼
            DisableNATPunchthrough = true
        };

        _runner.ProvideInput = true;

        try
        {
            Debug.Log("Starting Network Runner...");
            _runner.AddCallbacks(new CallbackHandler(this, PlayerPrefab));
            var result = await _runner.StartGame(startGameArgs);

            if (!result.Ok)
            {
                Debug.LogError($"Failed to start game: {result.ShutdownReason}");
                _isRunning = false;
            }
            else
            {
                _isRunning = true;

                // 確保只創建一次管理器
                if (_runner.IsSharedModeMasterClient && !_managersSpawned)
                {
                    SpawnNetworkManagers();
                }

                runner_has_spawned();
                Debug.Log("Network Runner started successfully");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting game: {e.Message}");
            _isRunning = false;
        }
    }

    private void SpawnNetworkManagers()
    {
        // 檢查是否已經存在管理器實例
        GameDeckManager existingDeckManager = FindObjectOfType<GameDeckManager>();
        ObserverManager existingObserverManager = FindObjectOfType<ObserverManager>();

        // 只在不存在時才創建新的
        if (existingDeckManager == null && gameDeckManagerPrefab != null)
        {
            Debug.Log("正在創建 GameDeckManager");
            _runner.Spawn(gameDeckManagerPrefab);
        }

        if (existingObserverManager == null && observerManagerPrefab != null)
        {
            Debug.Log("正在創建 ObserverManager");
            _runner.Spawn(observerManagerPrefab);
        }

        _managersSpawned = true;
    }

    public static void MarkReturningFromGame()
    {
        isReturningFromGame = true;
        Debug.Log("[NetworkManager] Marked as returning from game");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[NetworkManager] 場景已加載: {scene.name}, 建立索引: {scene.buildIndex}");

        // 最簡單的方案：場景加載後呼叫一次 runner_has_spawned
        if (_isRunning && _runner != null)
        {
            // 給一點延遲確保所有對象都已加載完成
            runner_has_spawned();
          //StartCoroutine(CallRunnerHasSpawnedAfterDelay());
        }

        if (scene.buildIndex == 0 && isReturningFromGame)
        {
            Debug.Log("[NetworkManager] 檢測到從遊戲返回到大廳");
            StartCoroutine(ReInitializeLobbyComponents());
        }
    }

    private IEnumerator CallRunnerHasSpawnedAfterDelay()
    {
        // 等待一幀確保所有對象都加載完成
        yield return null;

        // 直接呼叫 runner_has_spawned 來初始化所有 DeckSelector
        Debug.Log("[NetworkManager] 場景載入後呼叫 runner_has_spawned");
        runner_has_spawned();
    }

    private IEnumerator ReInitializeLobbyComponents()
    {
        Debug.Log("[NetworkManager] 重新初始化大廳組件");

        // 等待一幀確保所有物件都已創建
        yield return null;

        // 查找並初始化 DeckSelector
        DeckSelector[] deckSelectors = FindObjectsOfType<DeckSelector>();
        foreach (DeckSelector deckSelector in deckSelectors)
        {
            if (deckSelector != null)
            {
                Debug.Log($"[NetworkManager] 重新初始化 DeckSelector {deckSelector.GetInstanceID()}");
                deckSelector.Wait_Runner_Spawned();
            }
        }

        // 查找 CanvasManager 並確保它顯示初始頁面
        CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
        if (canvasManager != null)
        {
            Debug.Log("[NetworkManager] 找到 CanvasManager，正在重新初始化");
            // 強制刷新 Canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                canvas.enabled = false;
                canvas.enabled = true;
            }

            // 顯示初始頁面
            canvasManager.ShowPage("RuleDescriptionCanvas1");
        }

        // 如果需要，重置 GameDeckManager
        if (GameDeckManager.Instance != null)
        {
            Debug.Log("[NetworkManager] 重新初始化 GameDeckManager");
            // 清除之前的卡組選擇
            foreach (var player in _runner.ActivePlayers)
            {
                GameDeckManager.Instance.SetPlayerDeck(player, 0);
            }
        }

        isReturningFromGame = false;
    }

    private void runner_has_spawned()
    {
        Debug.Log("[NetworkManager] runner_has_spawned called");

        // 查找所有 DeckSelector 實例
        DeckSelector[] deckSelectors = FindObjectsOfType<DeckSelector>(true);
        Debug.Log($"[NetworkManager] Found {deckSelectors.Length} DeckSelector instances");

        foreach (DeckSelector deckSelector in deckSelectors)
        {
            if (deckSelector != null)
            {
                Debug.Log($"[NetworkManager] Initializing DeckSelector instance {deckSelector.GetInstanceID()}");
                deckSelector.Wait_Runner_Spawned();
            }
        }

        // 查找 CanvasManager 並確保正確頁面顯示
        //CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
        //if (canvasManager != null)
        //{
        //    Debug.Log("[NetworkManager] Found CanvasManager, showing initial page");
        //    canvasManager.ShowPage("DeckSelectCanvas");
        //}
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_SyncActivePage(string pageName)
    {
        CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
        if (canvasManager != null)
        {
            Debug.Log($"RPC received: Showing page {pageName}");
            canvasManager.ShowPage(pageName);
        }
    }
    private class CallbackHandler : INetworkRunnerCallbacks
    {
        private readonly NetworkManager _manager;
        private readonly GameObject _playerPrefab;
        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
        private bool _localPlayerSpawned = false;

        public CallbackHandler(NetworkManager manager, GameObject playerPrefab)
        {
            _manager = manager;
            _playerPrefab = playerPrefab;
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerJoined: {player} Local:{runner.LocalPlayer} Already spawned: {_localPlayerSpawned}");

            if (player.PlayerId > 2)
            {
                Debug.Log($"玩家 {player} 以觀察者身份加入");
                _manager.StartCoroutine(_manager.WaitForObserverManagerAndRegister(player));
                return;
            }

            if (player == runner.LocalPlayer && !_localPlayerSpawned)
            {
                if (_spawnedCharacters.ContainsKey(player))
                {
                    Debug.Log($"Player {player} already has a spawned character!");
                    return;
                }

                Debug.Log($"Spawning character for player {player}");
                Vector3 spawnPosition = new Vector3(0, 1, 0);
                //NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
                //Debug.Log($"Rotation = {Quaternion.identity}");

                //if (networkPlayerObject != null)
                //{
                //    _spawnedCharacters[player] = networkPlayerObject;
                //    _localPlayerSpawned = true;
                //    Debug.Log($"Successfully spawned character for player {player}");
                //}
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerLeft: {player}");

            if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
            {
                Debug.Log($"Despawning character for player {player}");
                runner.Despawn(networkObject);
                _spawnedCharacters.Remove(player);

                if (player == runner.LocalPlayer)
                {
                    _localPlayerSpawned = false;
                }
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"OnShutdown: {shutdownReason}");

            foreach (var kvp in _spawnedCharacters)
            {
                if (kvp.Value != null && runner != null)
                {
                    runner.Despawn(kvp.Value);
                }
            }

            _spawnedCharacters.Clear();
            _localPlayerSpawned = false;
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to Server");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected from Server: {reason}");
            _localPlayerSpawned = false;
        }


        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}