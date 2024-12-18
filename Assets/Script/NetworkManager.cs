using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using Fusion.Sockets;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] private GameObject PlayerPrefab;
    [SerializeField] private NetworkPrefabRef gameDeckManagerPrefab;
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isRunning = false;

    // Netwrok runner生成事件
    //public static event Action<NetworkRunner> OnNetworkRunnerInitialized;

    // 確保 NetworkManager 是單例
    public static NetworkManager Instance { get; private set; }

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

        // 確保只建立一次 NetworkRunner
        if (_runner == null)
        {
            var runnerObject = new GameObject("NetworkRunner");
            runnerObject.transform.parent = transform;
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
            DontDestroyOnLoad(runnerObject);
        }

        _runner.ProvideInput = true;

        var startGameArgs = new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = "TestRoom",
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = _sceneManager
        };

        if (_runner.IsSharedModeMasterClient)
        {
            _runner.Spawn(gameDeckManagerPrefab);
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
                //OnNetworkRunnerInitialized?.Invoke(_runner);

                Debug.Log("Network Runner started successfully");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting game: {e.Message}");
            _isRunning = false;
        }
    }

    private void runner_has_spawned() {
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

                    //networkPlayerObject.transform.position = spawnPosition;
                    //Debug.Log($"Player pos: {networkPlayerObject.transform.position}");

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

            // 清理所有生成的物件
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

        // ... 其他必要的 callback 實作 ...
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