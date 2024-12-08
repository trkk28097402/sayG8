using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Cards
{
    public int In_Hand_Count; // ��P�ƥ�
    public int Deck_Left_Count; // �P�ճѾl�ƥ�
}

public class PlayerStatus : NetworkBehaviour
{
    private NetworkRunner runner; // runner.LocalPlayer = playerref

    public int deckid = -1; // �P�սs��
    public int totalcard = 40;
    public Cards cards;
    public static PlayerStatus Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found in scene!");
        }
        Debug.Log($"Runner state: {runner.State}");
    }

    void Initialized_Cards()
    {
        cards = new Cards();

        deckid = GameDeckManager.Instance.GetPlayerDeck(runner.LocalPlayer);
        cards.In_Hand_Count = 5;
        cards.Deck_Left_Count = 35;
    }
}
