using UnityEngine;
using Fusion;

public class GameDeckManager : NetworkBehaviour
{
    private static GameDeckManager instance;
    public static GameDeckManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("GameDeckManager");
                instance = go.AddComponent<GameDeckManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Networked, Capacity(4)]
    private NetworkDictionary<PlayerRef, NetworkBool> IsInitialized { get; }

    [Networked, Capacity(4)]
    private NetworkArray<int> DeckIds { get; }

    public override void Spawned()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void SetPlayerDeck(PlayerRef playerRef, int deckId)
    {
        if (!Object.HasStateAuthority)
        {
            Rpc_SetPlayerDeck(playerRef, deckId);
            return;
        }

        if (!Runner.IsSharedModeMasterClient)
        {
            Debug.LogWarning("Only the master client can set deck IDs directly");
            return;
        }

        // 使用 PlayerId 屬性來獲取正確的索引
        int playerIndex = playerRef.PlayerId;
        if (playerIndex >= 0 && playerIndex < DeckIds.Length)
        {
            DeckIds.Set(playerIndex, deckId);
            IsInitialized.Set(playerRef, true);
            Debug.Log($"Set deck {deckId} for player {playerRef}");
        }
        else
        {
            Debug.LogError($"Invalid player index: {playerIndex}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetPlayerDeck(PlayerRef playerRef, int deckId)
    {
        SetPlayerDeck(playerRef, deckId);
    }

    public int GetPlayerDeck(PlayerRef playerRef)
    {
        int playerIndex = playerRef.PlayerId;
        if (playerIndex >= 0 && playerIndex < DeckIds.Length && IsInitialized.TryGet(playerRef, out var initialized) && initialized)
        {
            return DeckIds[playerIndex];
        }
        return -1;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}