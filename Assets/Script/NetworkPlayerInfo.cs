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
        Debug.Log($"GameDeckManager: Spawned �Q�եΡARunner: {Runner}, HasStateAuthority: {Object?.HasStateAuthority}");

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // ��l�ƥN�X...

            Debug.Log("GameDeckManager �w�b��������l�Ƨ���");
        }
        else if (instance != this)
        {
            Debug.LogWarning("�˴���h�� GameDeckManager ��ҡA�P�����ƪ����");

            // �T�O�w���P��
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
        Debug.Log($"GameDeckManager: Despawned �Q�եΡAHasState: {hasState}");

        // �u����Q�P�����O��e��Үɤ~���m
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
        // ���ˬd
        if (Object == null)
        {
            Debug.LogError("NetworkObject is null! GameDeckManager �i���٥��b���������T��l��");
            return;
        }

        if (Runner == null)
        {
            Debug.LogError("NetworkRunner is null! �����s���i���٥��إ�");
            return;
        }

        int playerIndex = playerRef.PlayerId;
        Debug.Log($"�ǳƳ]�m���a {playerIndex} ���d��");

        if (playerIndex >= 0 && playerIndex < DeckIds.Length)
        {
            try
            {
                DeckIds.Set(playerIndex, deckId);
                Debug.Log($"���\�]�m���a {playerRef} ���d�լ� {deckId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"�]�m�d�ծɵo�Ϳ��~: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"�L�Ī����a����: {playerIndex}");
        }
    }

    /*
    public PlayerRef GetPlayerRef()
    {
        
    }
    */

    public int GetPlayerDeck(PlayerRef playerRef)
    {
        // �ˬd��������O�_�w��l��
        if (Object == null || Runner == null)
        {
            Debug.LogWarning("�L�k������a�d�աG��������l��");
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