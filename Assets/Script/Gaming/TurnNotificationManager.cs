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
        // �Ыسq�����O
        GameObject panel = new GameObject("NotificationPanel", typeof(RectTransform));
        notificationPanel = panel;
        panel.transform.SetParent(transform, false);

        // �]�mRectTransform
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400, 100);

        // �K�[CanvasGroup�Ω�H�J�H�X
        panelCanvasGroup = panel.AddComponent<CanvasGroup>();

        // �Ыؤ奻
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

        // �T�O��RectTransform
        panelRect = notificationPanel.GetComponent<RectTransform>();
        if (!panelRect)
        {
            Debug.LogError("TurnNotificationManager: RectTransform missing on notification panel!");
            return;
        }

        // �T�O��CanvasGroup
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

        string playerText = TurnManager.Instance.GetCurrentTurnPlayer() == runner.LocalPlayer ? "�A" : "���";
        ShowNotification($"�C���}�l�I{playerText}����", gameStartColor);
    }

    private void ShowTurnChangeNotification(PlayerRef currentPlayer)
    {
        if (!isInitialized || !runner)
            return;

        bool isLocalPlayerTurn = currentPlayer == runner.LocalPlayer;
        string message = isLocalPlayerTurn ? "����A���^�X�I" : "�����⪺�^�X";
        Color color = isLocalPlayerTurn ? yourTurnColor : opponentTurnColor;

        ShowNotification(message, color);
    }

    private void ShowNotification(string message, Color textColor)
    {
        if (!isInitialized || !notificationPanel || !notificationText || !panelRect || !panelCanvasGroup)
        {
            Debug.LogError("TurnNotificationManager: Cannot show notification - components not initialized");
            return;
        }

        // �����e�ʵe
        if (currentAnimation != null && currentAnimation.IsPlaying())
        {
            currentAnimation.Kill();
        }

        // �]�m�q�����e
        notificationPanel.SetActive(true);
        notificationText.text = message;
        notificationText.color = textColor;

        // ���m��m�M�z����
        panelCanvasGroup.alpha = 0f;
        panelRect.anchoredPosition = new Vector2(0, -slideDistance);

        // �Ыطs�ʵe�ǦC
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