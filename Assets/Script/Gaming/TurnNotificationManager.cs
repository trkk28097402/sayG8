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

    [Header("Game Start Info Panel")]
    [SerializeField] private GameObject gameStartInfoPanel; // 遊戲開始信息面板
    [SerializeField] private TextMeshProUGUI gameStartInfoText; // 遊戲開始信息文本

    [Header("Optional References")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Animation Settings")]
    [SerializeField] private float showDuration = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float slideDistance = 100f;
    [SerializeField] private float gameStartInfoDuration = 5f; // New: Duration for game start info notification

    [Header("Colors")]
    [SerializeField] private Color yourTurnColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color opponentTurnColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color gameStartColor = new Color(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color normalTextColor = new Color(0f, 0f, 1f);
    [SerializeField] private Color gameInfoColor = new Color(0.8f, 0.6f, 0.2f); // New: Color for game info notification

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

        if (gameStartInfoPanel == null)
        {
            CreateGameStartInfoPanel();
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

        // 驗證遊戲開始信息面板
        if (gameStartInfoPanel != null)
        {
            if (gameStartInfoText == null)
            {
                gameStartInfoText = gameStartInfoPanel.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (gameStartInfoPanel.GetComponent<CanvasGroup>() == null)
            {
                gameStartInfoPanel.AddComponent<CanvasGroup>();
            }

            gameStartInfoPanel.SetActive(false);
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

    // 添加一個方法來創建新的遊戲開始信息面板
    private void CreateGameStartInfoPanel()
    {
        // 創建遊戲開始信息面板
        GameObject panel = new GameObject("GameStartInfoPanel", typeof(RectTransform));
        gameStartInfoPanel = panel;
        panel.transform.SetParent(transform, false);

        // 設置背景圖像
        Image bgImage = panel.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 深色半透明背景

        // 添加CanvasGroup用於淡入淡出
        panel.AddComponent<CanvasGroup>();

        // 設置RectTransform
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500, 200); // 較大的面板

        // 創建標題文本
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
        titleObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "遊戲開始";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.8f, 0.2f); // 金色標題

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 50);
        titleRect.anchoredPosition = new Vector2(0, -10);

        // 創建內容文本
        GameObject contentObj = new GameObject("ContentText", typeof(RectTransform));
        contentObj.transform.SetParent(panel.transform, false);
        gameStartInfoText = contentObj.AddComponent<TextMeshProUGUI>();
        gameStartInfoText.alignment = TextAlignmentOptions.Center;
        gameStartInfoText.fontSize = 24;
        gameStartInfoText.color = Color.white;

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(-40, -70);
        contentRect.anchoredPosition = new Vector2(0, -25);

        panel.SetActive(false);
    }

    // 新的方法來顯示遊戲開始信息通知，使用單獨的面板
    public void ShowGameStartInfoNotification(string player1Info, string player2Info)
    {
        if (!isInitialized)
        {
            Debug.LogError("TurnNotificationManager: 無法顯示遊戲開始信息 - 組件未初始化");
            return;
        }

        // 如果在编辑器中未指定面板，创建一个
        if (gameStartInfoPanel == null)
        {
            CreateGameStartInfoPanel();
        }

        if (gameStartInfoText == null && gameStartInfoPanel != null)
        {
            gameStartInfoText = gameStartInfoPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (gameStartInfoText == null)
            {
                Debug.LogError("TurnNotificationManager: 無法找到遊戲開始信息文本組件");
                return;
            }
        }

        // 停止當前動畫（如果有）
        if (currentAnimation != null && currentAnimation.IsPlaying())
        {
            currentAnimation.Kill();
        }

        // 确保先手公告不会显示
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.DisableFirstPlayerAnnouncement();
        }

        // 直接设置内容并显示面板，无需等待
        gameStartInfoPanel.SetActive(true);

        // 創建組合消息
        string combinedMessage = $"{player1Info}\n{player2Info}\n";
        gameStartInfoText.text = combinedMessage;

        // 獲取CanvasGroup和RectTransform
        CanvasGroup canvasGroup = gameStartInfoPanel.GetComponent<CanvasGroup>();

        // 直接显示，无淡入效果
        canvasGroup.alpha = 1f;

        // 设置延时隐藏
        if (gameStartInfoHideCoroutine != null)
        {
            StopCoroutine(gameStartInfoHideCoroutine);
        }
        gameStartInfoHideCoroutine = StartCoroutine(HideGameStartInfoAfterDelay());
    }

    private Coroutine gameStartInfoHideCoroutine;

    private IEnumerator HideGameStartInfoAfterDelay()
    {
        // 等待显示时间
        yield return new WaitForSeconds(gameStartInfoDuration);

        // 执行淡出动画
        CanvasGroup canvasGroup = gameStartInfoPanel.GetComponent<CanvasGroup>();
        float startTime = Time.time;
        float duration = fadeDuration;

        // 确保文本组件也会淡出
        if (gameStartInfoText != null)
        {
            // 使用DOTween对文本颜色的alpha同步进行淡出
            Color textStartColor = gameStartInfoText.color;
            Color textEndColor = new Color(textStartColor.r, textStartColor.g, textStartColor.b, 0f);

            // 使用DOTween淡出文本
            DOTween.To(() => gameStartInfoText.color, x => gameStartInfoText.color = x, textEndColor, duration);
        }

        // 使用CanvasGroup淡出整个面板
        while (Time.time < startTime + duration)
        {
            float t = (Time.time - startTime) / duration;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        // 确保完全透明
        canvasGroup.alpha = 0f;
        gameStartInfoPanel.SetActive(false);

        // 通知TurnManager通知已完成
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.StartTimerAfterNotification();
        }

        gameStartInfoHideCoroutine = null;
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

        // 清除所有DOTween動畫
        if (panelRect != null)
        {
            DOTween.Kill(panelRect);
        }

        if (gameStartInfoPanel != null)
        {
            DOTween.Kill(gameStartInfoPanel.GetComponent<CanvasGroup>());
            DOTween.Kill(gameStartInfoPanel.GetComponent<RectTransform>());
        }

        // 确保清除文本组件的DOTween动画
        if (gameStartInfoText != null)
        {
            DOTween.Kill(gameStartInfoText);
        }

        CancelInvoke();
    }
}