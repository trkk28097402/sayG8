using Fusion;
using UnityEngine;

public class ObserverManager : NetworkBehaviour
{
    [Networked, Capacity(4)]
    private NetworkArray<PlayerRef> ObserverPlayers { get; }

    private NetworkRunner runner;
    private bool isObserver = false;

    public static ObserverManager Instance { get; private set; }

    private void Awake()
    {

    }

    public override void Spawned()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("ObserverManager 已在網路中初始化完成");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("檢測到多個 ObserverManager 實例，銷毀重複的實例");
            Destroy(gameObject);
        }

        runner = Object.Runner;
    }

    public void RegisterObserver(PlayerRef player)
    {
        if (!Object || !Object.IsValid)
        {
            Debug.LogError("ObserverManager's NetworkObject is not valid!");
            return;
        }

        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("Attempting to register observer without state authority");
            return;
        }

        try
        {
            Rpc_RegisterObserver(player);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error registering observer: {e.Message}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_RegisterObserver(PlayerRef player)
    {
        try
        {
            for (int i = 0; i < ObserverPlayers.Length; i++)
            {
                if (ObserverPlayers.Get(i) == player)
                {
                    Debug.Log($"Player {player} already registered as observer");
                    return;
                }
            }

            for (int i = 0; i < ObserverPlayers.Length; i++)
            {
                if (ObserverPlayers.Get(i) == PlayerRef.None)
                {
                    ObserverPlayers.Set(i, player);
                    Debug.Log($"Successfully registered player {player} as observer");

                    if (player == Runner.LocalPlayer)
                    {
                        isObserver = true;
                    }
                    return;
                }
            }

            Debug.LogWarning($"No available slots to register observer {player}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Rpc_RegisterObserver: {e.Message}");
        }
    }

    public bool IsObserver()
    {
        return isObserver;
    }

    public bool IsPlayerObserver(PlayerRef player)
    {
        // 如果還沒有 Runner，用 PlayerId 判斷
        if (Runner == null) return player.PlayerId > 2;

        // 檢查陣列中是否包含該玩家
        for (int i = 0; i < ObserverPlayers.Length; i++)
        {
            if (ObserverPlayers.Get(i) == player)
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsPlayerObserving(PlayerRef player)
    {
        if (Instance == null) return false;
        return Instance.IsPlayerObserver(player);
    }
}