using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections;

public class GameReadySystem : NetworkBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private GameObject loadingUI;
    [SerializeField] private TextMeshProUGUI loadingText;
    AudioManagerLobby audioManagerLobby;

    private SceneRef[] availableScenes;
    [Networked] private SceneRef SelectedScene { get; set; }
    [Networked] private NetworkDictionary<PlayerRef, bool> PlayersReady { get; }
    private bool isLoading = false;
    private bool isObserverSetup = false;

    private void Awake()
    {
        audioManagerLobby = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManagerLobby>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeSceneRefs();
        }

        // 設置初始UI
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        // 開始檢查 ObserverManager
        StartCoroutine(WaitForObserverManager());
    }

    private IEnumerator WaitForObserverManager()
    {
        while (!isObserverSetup)
        {
            if (ObserverManager.Instance != null)
            {
                isObserverSetup = true;
                Debug.Log("ObserverManager is loaded in gamereadysystem");
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void InitializeSceneRefs()
    {
        var scenes = new System.Collections.Generic.List<SceneRef>();

        foreach (var sceneData in GameSceneDatabase.Scenes)
        {
            try
            {
                var sceneRef = SceneRef.FromIndex(sceneData.buildIndex);
                scenes.Add(sceneRef);
                Debug.Log($"Successfully added scene: {sceneData.sceneName} with index: {sceneData.buildIndex}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create SceneRef for {sceneData.sceneName}: {e.Message}");
            }
        }

        if (scenes.Count == 0)
        {
            Debug.LogError("No scenes found in GameSceneDatabase!");
            return;
        }

        availableScenes = scenes.ToArray();
    }

    private void OnReadyButtonClicked()
    {
        // 檢查是否為觀察者（如果 ObserverManager 已存在）
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer))
        {
            return;
        }

        if (isLoading) return;

        audioManagerLobby.PlaySoundEffectLobby(audioManagerLobby.ClickSound);
        RPC_PlayerReady(Runner.LocalPlayer);

        if (loadingUI != null)
        {
            loadingUI.SetActive(true);
            if (loadingText != null)
            {
                loadingText.text = "等待其他玩家...";
            }
        }

        if (readyButton != null)
        {
            readyButton.interactable = false;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_PlayerReady(PlayerRef player)
    {
        // 確保 RPC 不會被觀察者觸發
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player))
        {
            return;
        }

        PlayersReady.Set(player, true);
        Debug.Log($"{player} is ready!");
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (!Object.HasStateAuthority) return;

        bool allReady = true;
        int readyPlayerCount = 0;

        foreach (var player in Runner.ActivePlayers)
        {
            // 檢查是否為觀察者
            if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(player)) 
            {
                Debug.Log($"bypass observer {player}");
                continue;
            }
                

            readyPlayerCount++;
            if (!PlayersReady.TryGet(player, out bool isReady) || !isReady)
            {
                Debug.Log($"{player} is not ready");
                allReady = false;
                break;
            }
        }

        if (allReady && readyPlayerCount == 2)
        {
            int randomIndex = Random.Range(0, availableScenes.Length);
            SelectedScene = availableScenes[randomIndex];
            RPC_StartGame();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGame()
    {
        if (isLoading) return;

        if (loadingText != null)
        {
            loadingText.text = "載入遊戲中...";
        }

        isLoading = true;
        LoadSelectedScene();
    }

    private async void LoadSelectedScene()
    {
        if (Object.HasStateAuthority)
        {
            try
            {
                await Runner.LoadScene(SelectedScene, LoadSceneMode.Single);
                Debug.Log($"Scene loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load scene: {e.Message}");
                isLoading = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!isLoading) return;

        if (loadingUI != null &&
            (ObserverManager.Instance == null || !ObserverManager.Instance.IsPlayerObserver(Runner.LocalPlayer)))
        {
            loadingUI.SetActive(false);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
        }
    }
}