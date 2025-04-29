using UnityEngine;
using Fusion;

public class GameDeckManager : NetworkBehaviour
{
    private static GameDeckManager instance;
    public static GameDeckManager Instance
    {
        get
        {
            return instance;
        }
    }

    [Networked, Capacity(4)]
    private NetworkArray<int> DeckIds { get; }

    public override void Spawned()
    {
        Debug.Log($"GameDeckManager: Spawned 被調用，Runner: {Runner}, HasStateAuthority: {Object?.HasStateAuthority}");

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // 初始化代碼...

            Debug.Log("GameDeckManager 已在網絡中初始化完成");
        }
        else if (instance != this)
        {
            Debug.LogWarning("檢測到多個 GameDeckManager 實例，銷毀重複的實例");

            // 確保安全銷毀
            if (Object != null && Runner != null)
            {
                Runner.Despawn(Object);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Debug.Log($"GameDeckManager: Despawned 被調用，HasState: {hasState}");

        // 只有當被銷毀的是當前單例時才重置
        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool IsValid()
    {
        return instance != null && instance.Object != null && instance.Object.IsValid;
    }

    public void SetPlayerDeck(PlayerRef playerRef, int deckId)
    {
        // 基本檢查
        if (Object == null)
        {
            Debug.LogError("NetworkObject is null! GameDeckManager 可能還未在網路中正確初始化");
            return;
        }

        if (Runner == null)
        {
            Debug.LogError("NetworkRunner is null! 網路連接可能還未建立");
            return;
        }

        int playerIndex = playerRef.PlayerId;
        Debug.Log($"準備設置玩家 {playerIndex} 的卡組");

        if (playerIndex >= 0 && playerIndex < DeckIds.Length)
        {
            try
            {
                DeckIds.Set(playerIndex, deckId);
                Debug.Log($"成功設置玩家 {playerRef} 的卡組為 {deckId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"設置卡組時發生錯誤: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"無效的玩家索引: {playerIndex}");
        }
    }

    /*
    public PlayerRef GetPlayerRef()
    {
        
    }
    */

    public int GetPlayerDeck(PlayerRef playerRef)
    {
        // 檢查網路物件是否已初始化
        if (Object == null || Runner == null)
        {
            Debug.LogWarning("無法獲取玩家卡組：網路未初始化");
            return -1;
        }

        int playerIndex = playerRef.PlayerId;
        return DeckIds[playerIndex];
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}