using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LocalInteractiveUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private float interactionDistance = 1f;
    [SerializeField] private Canvas worldSpaceCanvas;
    [SerializeField] private TextMeshProUGUI interactionText;

    [Header("Optional Settings")]
    [SerializeField] private string promptText = "Press E to interact";
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    private Camera mainCamera;
    private bool isPlayerInRange;

    private void Start()
    {
        mainCamera = Camera.main;

        /*
        // 初始化UI
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
        */

        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.worldCamera = mainCamera;
        }

        if (interactionText != null)
        {
            interactionText.text = promptText;
            interactionText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // 檢查玩家是否在範圍內
        if (mainCamera != null)
        {
            float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
            bool newInRange = distance <= interactionDistance;

            // 當玩家進入/離開範圍時更新UI
            if (newInRange != isPlayerInRange)
            {
                isPlayerInRange = newInRange;
                if (interactionText != null)
                {
                    interactionText.gameObject.SetActive(isPlayerInRange);
                }
            }
        }

        // 處理玩家輸入
        if (isPlayerInRange && Input.GetKeyDown(interactionKey))
        {
            //ToggleUI();
        }

        // 更新UI朝向
        if (worldSpaceCanvas != null && mainCamera != null)
        {
            worldSpaceCanvas.transform.LookAt(mainCamera.transform);
            worldSpaceCanvas.transform.Rotate(0, 180, 0);
        }
    }

    /*
    private void ToggleUI()
    {
        if (uiPanel != null)
        {
            bool newState = !uiPanel.activeSelf;
            uiPanel.SetActive(newState);

            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(!newState);
            }
        }
    }
    */

    private void OnDestroy()
    {
        if (uiPanel != null)
        {
            Destroy(uiPanel);
        }
    }
}