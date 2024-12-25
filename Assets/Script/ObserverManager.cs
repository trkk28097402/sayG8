using Fusion;
using UnityEngine;

public class ObserverManager : NetworkBehaviour
{
    [Networked, Capacity(4)]
    private NetworkArray<PlayerRef> ObserverPlayers { get; }

    [SerializeField] private GameObject observerUI;
    [SerializeField] private GameObject playerUI;

    private NetworkRunner runner;
    private bool isObserver = false;

    public static ObserverManager Instance { get; private set; }

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
        Debug.Log("ObserverManager has spawned");
    }

    public void RegisterObserver(PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            Rpc_RegisterObserver(player);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_RegisterObserver(PlayerRef player)
    {
        // �����ϥΰ}�C�x�s�[���
        for (int i = 0; i < ObserverPlayers.Length; i++)
        {
            if (ObserverPlayers.Get(i) == PlayerRef.None)
            {
                ObserverPlayers.Set(i, player);
                break;
            }
        }

        if (player == Runner.LocalPlayer)
        {
            isObserver = true;
            SetupObserverUI();
        }
    }

    private void SetupObserverUI()
    {
        if (observerUI != null) observerUI.SetActive(true);
        if (playerUI != null) playerUI.SetActive(false);
    }

    public bool IsObserver()
    {
        return isObserver;
    }

    public bool IsPlayerObserver(PlayerRef player)
    {
        // �p�G�٨S�� Runner�A�� PlayerId �P�_
        if (Runner == null) return player.PlayerId >= 2;

        // �ˬd�}�C���O�_�]�t�Ӫ��a
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