using UnityEngine;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;
using Fusion;

[RequireComponent(typeof(Canvas))]
public class TurnNotificationManager : NetworkBehaviour
{
    [Header("Required References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;

    [Header("Optional References")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Animation Settings")]
    [SerializeField] private float showDuration = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float slideDistance = 100f;

    [Header("Colors")]
    [SerializeField] private Color yourTurnColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color opponentTurnColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color gameStartColor = new Color(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color normalTextColor = new Color(0f, 0f, 1f);

    private RectTransform panelRect;
    private NetworkRunner runner;
    private Sequence currentAnimation;
    private bool isInitialized = false;
    private CanvasGroup panelCanvasGroup;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();

        if (notificationPanel == null)
            notificationPanel = transform.Find("NotificationPanel")?.gameObject;

        if (notificationPanel != null && notificationText == null)
            notificationText = notificationPanel.GetComponentInChildren<TextMeshProUGUI>();
    }
#endif

    private void Awake()
    {
        CreateUIIfNeeded();
        ValidateComponents();
    }

    private void CreateUIIfNeeded()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
            if (targetCanvas == null)
            {
                targetCanvas = gameObject.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 999;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        if (notificationPanel == null)
        {
            CreateNotificationPanel();
        }
    }

    private void CreateNotificationPanel()
    {
        // 創建通知面板
        GameObject panel = new GameObject("NotificationPanel", typeof(RectTransform));
        notificationPanel = panel;
        panel.transform.SetParent(transform, false);

        // 設置RectTransform
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400, 100);

        // 添加CanvasGroup用於淡入淡出
        panelCanvasGroup = panel.AddComponent<CanvasGroup>();

        // 創建文本
        GameObject textObj = new GameObject("NotificationText", typeof(RectTransform));
        textObj.transform.SetParent(panel.transform, false);
        notificationText = textObj.AddComponent<TextMeshProUGUI>();
        notificationText.alignment = TextAlignmentOptions.Center;
        notificationText.fontSize = 36;
        notificationText.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
    }

    private void ValidateComponents()
    {
        if (!notificationPanel || !notificationText)
        {
            Debug.LogError("TurnNotificationManager: Essential components are missing!");
            return;
        }

        // 確保有RectTransform
        panelRect = notificationPanel.GetComponent<RectTransform>();
        if (!panelRect)
        {
            Debug.LogError("TurnNotificationManager: RectTransform missing on notification panel!");
            return;
        }

        // 確保有CanvasGroup
        panelCanvasGroup = notificationPanel.GetComponent<CanvasGroup>();
        if (!panelCanvasGroup)
        {
            panelCanvasGroup = notificationPanel.AddComponent<CanvasGroup>();
        }

        isInitialized = true;
        notificationPanel.SetActive(false);
        Debug.Log("TurnNotificationManager: Successfully validated all components");
    }

    public override void Spawned()
    {
        base.Spawned();
        runner = Object.Runner;

        if (TurnManager.Instance != null)
        {
            StartCoroutine(WaitForTurnManagerInitialization());
        }
        else
        {
            Debug.LogError("TurnNotificationManager: TurnManager.Instance is null!");
        }
    }

    private IEnumerator WaitForTurnManagerInitialization()
    {
        while (TurnManager.Instance == null || !TurnManager.Instance.IsFullyInitialized())
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("TurnNotificationManager: TurnManager initialized");
        InvokeRepeating(nameof(CheckTurnChanges), 0f, 0.1f);
    }

    private PlayerRef lastTurnPlayer;

    private void CheckTurnChanges()
    {
        if (!isInitialized || !TurnManager.Instance || !TurnManager.Instance.IsFullyInitialized())
            return;

        PlayerRef currentTurnPlayer = TurnManager.Instance.GetCurrentTurnPlayer();

        if (currentTurnPlayer != lastTurnPlayer)
        {
            if (lastTurnPlayer == PlayerRef.None)
            {
                ShowGameStartNotification();
            }
            else
            {
                ShowTurnChangeNotification(currentTurnPlayer);
            }
            lastTurnPlayer = currentTurnPlayer;
        }
    }

    private void ShowGameStartNotification()
    {
        if (!isInitialized || !TurnManager.Instance || !runner)
            return;
        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(runner.LocalPlayer))
        {
            string playerText = TurnManager.Instance.GetCurrentTurnPlayer().PlayerId == 1 ? "玩家1" : "玩家2";
            ShowNotification($"遊戲開始！{playerText}先手", gameStartColor);
        }
        else
        {
            string playerText = TurnManager.Instance.GetCurrentTurnPlayer() == runner.LocalPlayer ? "你" : "對手";
            ShowNotification($"遊戲開始！{playerText}先手", gameStartColor);
        }

    }

    private void ShowTurnChangeNotification(PlayerRef currentPlayer)
    {
        if (!isInitialized || !runner)
            return;

        string message;

        if (ObserverManager.Instance != null && ObserverManager.Instance.IsPlayerObserver(runner.LocalPlayer))
        {
            string playerName = currentPlayer.PlayerId == 1 ? "玩家1" : "玩家2";
            message = $"輪到{playerName}的回合";
            ShowNotification(message, normalTextColor);
            return;
        }

        bool isLocalPlayerTurn = currentPlayer == runner.LocalPlayer;
        message = isLocalPlayerTurn ? "輪到你的回合！" : "輪到對手的回合";
        Color color = isLocalPlayerTurn ? yourTurnColor : opponentTurnColor;

        ShowNotification(message, color);
    }

    public void ShowNotification(string message, Color textColor)
    {
        if (!isInitialized || !notificationPanel || !notificationText || !panelRect || !panelCanvasGroup)
        {
            Debug.LogError("TurnNotificationManager: Cannot show notification - components not initialized");
            return;
        }

        // 停止當前動畫
        if (currentAnimation != null && currentAnimation.IsPlaying())
        {
            currentAnimation.Kill();
        }

        // 設置通知內容
        notificationPanel.SetActive(true);
        notificationText.text = message;
        notificationText.color = textColor;

        // 重置位置和透明度
        panelCanvasGroup.alpha = 0f;
        panelRect.anchoredPosition = new Vector2(0, -slideDistance);

        // 創建新動畫序列
        currentAnimation = DOTween.Sequence();

        currentAnimation
            .Append(panelRect.DOAnchorPosY(0, fadeDuration).SetEase(Ease.OutBack))
            .Join(panelCanvasGroup.DOFade(1, fadeDuration))
            .AppendInterval(showDuration)
            .Append(panelRect.DOAnchorPosY(slideDistance, fadeDuration).SetEase(Ease.InBack))
            .Join(panelCanvasGroup.DOFade(0, fadeDuration))
            .OnComplete(() => {
                notificationPanel.SetActive(false);
                currentAnimation = null;
            });
    }

    public void ShowAIAnalysisNotification()
    {
        if (!isInitialized || !notificationPanel || !notificationText || !panelRect || !panelCanvasGroup)
        {
            Debug.LogError("TurnNotificationManager: Cannot show AI analysis notification - components not initialized");
            return;
        }

        // Stop current animation if playing
        if (currentAnimation != null && currentAnimation.IsPlaying())
        {
            currentAnimation.Kill();
        }

        // Set notification content
        notificationPanel.SetActive(true);
        notificationText.text = "AI正在進行分析...";
        notificationText.color = Color.white; // Use white or another appropriate color

        // Reset position and transparency
        panelCanvasGroup.alpha = 0f;
        panelRect.anchoredPosition = new Vector2(0, -slideDistance);

        // Create new animation sequence - keep it visible until explicitly hidden
        currentAnimation = DOTween.Sequence();

        currentAnimation
            .Append(panelRect.DOAnchorPosY(0, fadeDuration).SetEase(Ease.OutBack))
            .Join(panelCanvasGroup.DOFade(1, fadeDuration));

        // Note: This animation doesn't include the auto-hide feature
        // It will remain visible until HideNotification is called
    }

    // Add this method to hide the notification
    public void HideNotification()
    {
        if (!isInitialized || !notificationPanel || !notificationText || !panelRect || !panelCanvasGroup)
        {
            return;
        }

        // Stop current animation if playing
        if (currentAnimation != null && currentAnimation.IsPlaying())
        {
            currentAnimation.Kill();
        }

        // Create hide animation
        currentAnimation = DOTween.Sequence();

        currentAnimation
            .Append(panelRect.DOAnchorPosY(slideDistance, fadeDuration).SetEase(Ease.InBack))
            .Join(panelCanvasGroup.DOFade(0, fadeDuration))
            .OnComplete(() => {
                notificationPanel.SetActive(false);
                currentAnimation = null;
            });
    }

    private void OnDestroy()
    {
        if (currentAnimation != null)
        {
            currentAnimation.Kill();
        }

        DOTween.Kill(panelRect);
        CancelInvoke();
    }
}