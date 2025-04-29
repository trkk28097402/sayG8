using UnityEngine;
using Fusion;
using System.Collections;
using UnityEngine.SceneManagement;

public class BootstrapManager : MonoBehaviour
{
    [SerializeField] private GameObject networkManagerPrefab;
    [SerializeField] private float checkInterval = 0.5f;
    [SerializeField] private float maxWaitTime = 15f;

    private void Start()
    {

        // Ґu¦bЁS¦і NetworkManager ®ЙЄм©l¤Ж
        if (FindObjectOfType<NetworkManager>() == null)
        {
            InitializeGame();
        }
        else
        {
        }
    }

    private void InitializeGame()
    {
        if (networkManagerPrefab != null)
        {
            var nmObject = Instantiate(networkManagerPrefab);
            DontDestroyOnLoad(nmObject);
        }

        StartCoroutine(WaitForManagersAndLoadLobby());
    }

    private IEnumerator WaitForManagersAndLoadLobby()
    {
        float elapsed = 0f;

        while (elapsed < maxWaitTime)
        {
            if (GameDeckManager.Instance != null && ObserverManager.Instance != null)
            {

                var runner = FindObjectOfType<NetworkRunner>();
                if (runner != null && runner.IsRunning)
                {

                    if (SceneManager.GetActiveScene().buildIndex != 1)
                    {

                        SceneRef lobbyScene = SceneRef.FromIndex(1);

                        runner.LoadScene(lobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
                    }
                }
                else
                {
                }

                yield break;
            }

            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;

            if (elapsed % 5f < checkInterval)
            {
            }
        }


        var fallbackRunner = FindObjectOfType<NetworkRunner>();
        if (fallbackRunner != null && fallbackRunner.IsRunning)
        {
        }
        else
        {
            SceneManager.LoadScene(1);
        }
    }
}