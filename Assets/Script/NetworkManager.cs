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
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isRunning = false;
    public static NetworkManager Instance { get; private set; }

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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartGame();
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
            PlayerCount = 4,
            DisableNATPunchthrough = true
        };

        _runner.ProvideInput = true;

        if (_runner.IsSharedModeMasterClient)
        {
            //_runner.Spawn(gameDeckManagerPrefab);
            //_runner.Spawn(observerManagerPrefab);
        }

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

    private void runner_has_spawned()
    {
        DeckSelector deckSelector = FindObjectOfType<DeckSelector>();
        deckSelector.Wait_Runner_Spawned();
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
                NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
                Debug.Log($"Rotation = {Quaternion.identity}");

                if (networkPlayerObject != null)
                {
                    _spawnedCharacters[player] = networkPlayerObject;
                    _localPlayerSpawned = true;
                    Debug.Log($"Successfully spawned character for player {player}");
                }
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