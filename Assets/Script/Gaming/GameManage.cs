﻿using Fusion;
using System.Collections;
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

    public const int MAX_PLAYERS = 2;

    [Networked]
    private NetworkBool GameStarted { get; set; }
    [Networked, Capacity(MAX_PLAYERS)]
    private NetworkArray<PlayerRef> ConnectedPlayers { get; }
    [Networked]
    private int ConnectedPlayerCount { get; set; }

    public static GameManager Instance { get; private set; }
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
        StartCoroutine(InitializeAfterSpawn());

        Debug.Log("GameManager Spawned");
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }
    }

    private IEnumerator InitializeAfterSpawn()
    {

        while (runner == null)
        {
            runner = FindObjectOfType<NetworkRunner>();
            if (runner == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

    }

    public void RegisterPlayerCard(PlayerRef player, CardOnHand cardHand)
    {
        Debug.Log($"Attempting to register player {player}");
        StartCoroutine(WaitForCardInitialization(player, cardHand));
    }

    private IEnumerator WaitForCardInitialization(PlayerRef player, CardOnHand cardHand)
    {
        // 等待 CardOnHand 完成初始化
        while (!cardHand.IsInitialized)
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (!localPlayerCards.ContainsKey(player))
        {
            localPlayerCards[player] = cardHand;
            if (Object.HasStateAuthority)
            {
                NetworkedPlayerCards.Add(player, cardHand.Object.Id);
            }
            Debug.Log($"Successfully registered player {player} after initialization");

            // 向 StateAuthority 發送同步請求
            Rpc_RequestSyncPlayerCard(player, cardHand.Object.Id);
        }
    }

    // 玩家向 StateAuthority 發送同步請求
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestSyncPlayerCard(PlayerRef player, NetworkId cardId)
    {
        if (!Object.HasStateAuthority) return;

        // StateAuthority 驗證後廣播給所有客戶端
        Rpc_BroadcastPlayerCard(player, cardId);
    }

    // StateAuthority 向所有客戶端廣播
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_BroadcastPlayerCard(PlayerRef player, NetworkId cardId)
    {
        var cardHand = Runner.FindObject(cardId).GetComponent<CardOnHand>();
        if (cardHand != null)
        {
            localPlayerCards[player] = cardHand;
            Debug.Log($"Synced player {player} card hand to local dictionary");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RegisterPlayerStatus(PlayerRef player, NetworkId statusId)
    {
        Debug.Log($"Rpc_RegisterPlayerStatus called for player {player}");

        // 檢查這個玩家是否已經註冊
        bool isNewPlayer = !NetworkedPlayerStatuses.ContainsKey(player);
        Debug.Log($"已經有{ConnectedPlayerCount}位玩家連線");
        if (isNewPlayer && ConnectedPlayerCount < MAX_PLAYERS)
        {
            NetworkedPlayerStatuses.Add(player, statusId);
            // 添加到已連接玩家列表
            ConnectedPlayers.Set(ConnectedPlayerCount, player);
            ConnectedPlayerCount++;

            Debug.Log($"Added player {player} to NetworkedPlayerStatuses, current players: {ConnectedPlayerCount}");

            Rpc_RegisterLocalPlayerStatus(player, statusId);

            // 如果有足夠的玩家且是有權限的客戶端，開始遊戲
            if (Object.HasStateAuthority && ConnectedPlayerCount >= MAX_PLAYERS)
            {
                Debug.Log($"Starting game with {ConnectedPlayerCount} players");
                StartGame();
            }
        }
        else
        {
            Debug.Log($"Player {player} was already registered or max players reached");
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

    public PlayerRef[] GetConnectedPlayers()
    {
        PlayerRef[] players = new PlayerRef[ConnectedPlayerCount];
        for (int i = 0; i < ConnectedPlayerCount; i++)
        {
            players[i] = ConnectedPlayers.Get(i);
            Debug.Log($"get connect : {players[i]}");
        }
        return players;
    }

    public PlayerRef GetOpponentPlayer(PlayerRef currentPlayer)
    {
        for (int i = 0; i < ConnectedPlayerCount; i++)
        {
            PlayerRef player = ConnectedPlayers.Get(i);
            if (player != currentPlayer)
            {
                return player;
            }
        }
        return PlayerRef.None;
    }

    public void PrepareForSceneChange()
    {
        Debug.Log("GameManager preparing for scene change");

        // 清理本地字典
        localPlayerCards.Clear();
        localPlayerStatuses.Clear();

        // 如果有狀態權限，還要清理網絡字典
        if (Object.HasStateAuthority)
        {
            // NetworkedPlayerCards.Clear();
            // NetworkedPlayerStatuses.Clear();
        }

        // 重置遊戲狀態
        GameStarted = false;
        ConnectedPlayerCount = 0;

    }

    // 場景加載完成後的初始化
    public void ReInitializeAfterSceneLoad()
    {
        Debug.Log("Re-initializing GameManager after scene load");

        // 重新初始化本地變數
        localPlayerCards.Clear();
        localPlayerStatuses.Clear();

        // 如果有狀態權限，重新初始化網絡狀態
        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }

        // 等待 NetworkRunner 再次準備好並重新註冊玩家
        StartCoroutine(InitializeAfterSpawn());
    }

    public override void FixedUpdateNetwork()
    {
    }
}