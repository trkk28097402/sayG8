using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;

public class GameReadySystem : NetworkBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private GameObject loadingUI;
    [SerializeField] private TextMeshProUGUI loadingText;

    private SceneRef[] availableScenes;

    [Networked] private SceneRef SelectedScene { get; set; }

    [Networked] private NetworkDictionary<PlayerRef, bool> PlayersReady { get; }

    private bool isLoading = false;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeSceneRefs();
        }
        else
        {
            Debug.Log("Not State Authority, skipping InitializeSceneRefs");
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
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
        if (isLoading) return;

        RPC_PlayerReady(Runner.LocalPlayer);

        if (loadingUI != null)
        {
            loadingUI.SetActive(true);
            if (loadingText != null)
            {
                loadingText.text = "Waiting...";
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
        PlayersReady.Set(player, true);
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (!Object.HasStateAuthority) return;

        // random scene
        int randomIndex = Random.Range(0, availableScenes.Length);
        SelectedScene = availableScenes[randomIndex];

        bool allReady = true;
        foreach (var player in Runner.ActivePlayers)
        {
            if (!PlayersReady.TryGet(player, out bool isReady) || !isReady)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            RPC_StartGame();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGame()
    {
        if (isLoading) return;
        
        if (loadingText != null)
        {
            loadingText.text = $"Loading...";
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
                Debug.LogError($"Failed to load {SelectedScene}");
                Debug.LogError($"Failed to load scene: {e.Message}");
                isLoading = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!isLoading) return;

        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyButtonClicked);
        }
    }
}