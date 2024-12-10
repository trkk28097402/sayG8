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
                Debug.LogWarning("GameDeckManager instance 不存在，請確保已經透過 NetworkRunner 生成");
            }
            return instance;
        }
    }

    [Networked, Capacity(4)]
    private NetworkArray<int> DeckIds { get; }

    public override void Spawned()
    {
        // 網路物件生成時的初始化
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // 如果是主機端，初始化網路變數
            if (Object.HasStateAuthority)
            {
                for (int i = 0; i < DeckIds.Length; i++)
                {
                    DeckIds.Set(i, -1);  // 設置預設值
                }
            }

            Debug.Log("GameDeckManager 已在網路中初始化完成");
        }
        else if (instance != this)
        {
            Debug.LogWarning("檢測到多個 GameDeckManager 實例，銷毀重複的實例");
            Destroy(gameObject);
        }
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