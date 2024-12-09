using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Networked]
    public NetworkDictionary<PlayerRef, NetworkId> NetworkedPlayerCards { get; }
    [Networked]
    public NetworkDictionary<PlayerRef, NetworkId> NetworkedPlayerStatuses { get; }

    public Dictionary<PlayerRef, CardOnHand> localPlayerCards = new Dictionary<PlayerRef, CardOnHand>();
    public Dictionary<PlayerRef, PlayerStatus> localPlayerStatuses = new Dictionary<PlayerRef, PlayerStatus>();

    [Networked]
    private NetworkBool GameStarted { get; set; }

    public static GameManager Instance { get; private set; }
    public const int MAX_PLAYERS = 2;

    private NetworkRunner runner;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        runner = Object.Runner;
        Debug.Log("GameManager Spawned");
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }
    }

    public void RegisterPlayerCard(PlayerRef player, CardOnHand cardHand)
    {
        Debug.Log($"Attempting to register player {player}");
        if (!localPlayerCards.ContainsKey(player))
        {
            localPlayerCards[player] = cardHand;
            if (Object.HasStateAuthority)
            {
                NetworkedPlayerCards.Add(player, cardHand.Object.Id);
            }
            Debug.Log($"Successfully registered player {player}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RegisterPlayerStatus(PlayerRef player, NetworkId statusId)
    {
        Debug.Log($"Rpc_RegisterPlayerStatus called for player {player}");

        // 先檢查這個玩家是否已經註冊
        bool isNewPlayer = !NetworkedPlayerStatuses.ContainsKey(player);

        if (isNewPlayer)
        {
            NetworkedPlayerStatuses.Add(player, statusId);
            Debug.Log($"Added player {player} to NetworkedPlayerStatuses, current players: {NetworkedPlayerStatuses.Count}");

            Rpc_RegisterLocalPlayerStatus(player, statusId);

            // 如果有足夠的玩家且是有權限的客戶端，開始遊戲
            if (Object.HasStateAuthority && NetworkedPlayerStatuses.Count >= MAX_PLAYERS)
            {
                Debug.Log($"Starting game with {NetworkedPlayerStatuses.Count} players");
                StartGame();
            }
        }
        else
        {
            Debug.Log($"Player {player} was already registered");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_RegisterLocalPlayerStatus(PlayerRef player, NetworkId statusId) {

        var status = Runner.FindObject(statusId).GetComponent<PlayerStatus>();
        if (status != null)
        {
            localPlayerStatuses[player] = status;
            Debug.Log($"Added player {player} to localPlayerStatuses, total local: {localPlayerStatuses.Count}");
        }
    }

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log("Host is starting the game");
        // 通知所有客戶端開始遊戲
        Rpc_StartGameForAll();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_StartGameForAll()
    {
        Debug.Log($"Player {Runner.LocalPlayer} received start game signal");

        // 每個客戶端初始化自己的牌組
        if (localPlayerStatuses.TryGetValue(Runner.LocalPlayer, out var status))
        {
            Debug.Log("Start game!");
            status.Initialized_Cards();
        }

        GameStarted = true;
    }

    private void InitializeGame()
    {
        // 只在遊戲一開始時清空，而不是每次註冊玩家時都清空
        if (NetworkedPlayerStatuses.Count == 0)
        {
            Debug.Log("First time initialization");
            NetworkedPlayerCards.Clear();
            localPlayerCards.Clear();
            localPlayerStatuses.Clear();
            GameStarted = false;
        }
    }

    public CardOnHand GetOpponentCard(PlayerRef currentPlayer)
    {
        foreach (var kvp in localPlayerCards)
        {
            if (kvp.Key != currentPlayer)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    public PlayerStatus GetOpponentStatus(PlayerRef currentPlayer)
    {
        foreach (var kvp in localPlayerStatuses)
        {
            if (kvp.Key != currentPlayer)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (NetworkedPlayerCards.ContainsKey(player))
        {
            NetworkedPlayerCards.Remove(player);
            NetworkedPlayerStatuses.Remove(player);
            localPlayerCards.Remove(player);
            localPlayerStatuses.Remove(player);
            Debug.Log($"Player {player} has left the game");
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 在這裡處理遊戲狀態更新
        if (Object.HasStateAuthority && GameStarted)
        {
            // Debug.Log($"Game in progress with {NetworkedPlayerStatuses.Count} players");
        }
    }
}