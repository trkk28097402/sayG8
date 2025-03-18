using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasManager : MonoBehaviour
{
    [System.Serializable]
    public class CanvasPage
    {
        public string pageName;
        public GameObject canvasObject;
        public string nextPageName; // �U�@�������W��
        [HideInInspector] public CanvasGroup canvasGroup; // �ϥ� CanvasGroup ����i����
        public PageInputHandler inputHandler; // ��������J�B�z��
    }

    [Header("Canvas Pages")]
    [SerializeField] private List<CanvasPage> canvasPages = new List<CanvasPage>();
    [SerializeField] private string initialPageName = "DeckSelectCanvas";

    [Header("Navigation Settings")]
    [SerializeField] private bool useEnterKeyNavigation = true;
    [SerializeField] private float inputCooldown = 0.3f;

    private CanvasPage currentActivePage;
    private float lastInputTime = 0f;

    private void Awake()
    {
        // �T�O�Ҧ��������� CanvasGroup �ե�ê�l�ƪ��A
        InitializeAllPages();
    }

    private void InitializeAllPages()
    {
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null)
            {
                // ���T�O�U������GameObject�O�E�����A���q�LCanvasGroup�ӱ���i����
                page.canvasObject.SetActive(true);

                // �T�O�� CanvasGroup �ե�
                page.canvasGroup = page.canvasObject.GetComponent<CanvasGroup>();
                if (page.canvasGroup == null)
                {
                    page.canvasGroup = page.canvasObject.AddComponent<CanvasGroup>();
                }

                // �ˬd�βK�[ PageInputHandler
                page.inputHandler = page.canvasObject.GetComponent<PageInputHandler>();

                // ��l�ɳ]�����i���M���i�椬
                page.canvasGroup.alpha = 0f;
                page.canvasGroup.interactable = false;
                page.canvasGroup.blocksRaycasts = false;

                // �T�O��J�B�z����l�Ƭ��D���ʪ��A
                if (page.inputHandler != null)
                {
                    page.inputHandler.SetActive(false);
                }
            }
            else
            {
                Debug.LogError($"Canvas object is null for page {page.pageName}!");
            }
        }
    }

    private void Start()
    {
        // ��ܪ�l����
        ShowPage(initialPageName);
    }

    private void Update()
    {
        // �o�̥u�B�z���������\��A���魶������J�ѦU�۪� PageInputHandler �B�z
        if (currentActivePage == null || !useEnterKeyNavigation)
            return;

        // �ˬd�N�o�ɶ�
        if (Time.time - lastInputTime < inputCooldown)
            return;

        // �� Enter �������U�@��
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!string.IsNullOrEmpty(currentActivePage.nextPageName))
            {
                ShowPage(currentActivePage.nextPageName);
                lastInputTime = Time.time;
            }
        }
    }

    /// <summary>
    /// ��ܫ��w���������è�L����
    /// </summary>
    /// <param name="pageName">�n��ܪ������W��</param>
    public void ShowPage(string pageName)
    {
        if (string.IsNullOrEmpty(pageName))
            return;

        bool foundPage = false;

        // �M��n��ܪ�����
        CanvasPage pageToShow = null;
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                pageToShow = page;
                foundPage = true;
                break;
            }
        }

        if (!foundPage)
        {
            Debug.LogWarning($"Page '{pageName}' not found in canvas pages list!");
            return;
        }

        // �p�G��e��������J�B�z���A�T�Υ�
        if (currentActivePage != null && currentActivePage.inputHandler != null)
        {
            currentActivePage.inputHandler.SetActive(false);
        }

        // ���n�ק�G���T�ΩҦ��������椬�A�M��A�E���ؼЭ���
        // �o�i�H����b���������L�{�����s�Q�~Ĳ�o
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null && page.canvasGroup != null)
            {
                page.canvasGroup.interactable = false;
                page.canvasGroup.blocksRaycasts = false;
            }
        }

        // ���ݤ@�V�T�O�T�Υͮ�
        StartCoroutine(ActivatePageAfterDelay(pageToShow));

        Debug.Log($"Showing page: {pageName}");
    }

    // �s�W�G����E�������A�T�O���e�������T�Χ����ͮ�
    private IEnumerator ActivatePageAfterDelay(CanvasPage pageToShow)
    {
        // ���ݤ@�V
        yield return null;

        // �]�m�Ҧ��������i����
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null && page.canvasGroup != null)
            {
                bool shouldBeActive = (page == pageToShow);

                // �ϥ� CanvasGroup ����i���ʦӤ��O�ҥ�/�T��GameObject
                page.canvasGroup.alpha = shouldBeActive ? 1f : 0f;
                page.canvasGroup.interactable = shouldBeActive;
                page.canvasGroup.blocksRaycasts = shouldBeActive;

                // �����J�B�z
                if (page.inputHandler != null)
                {
                    page.inputHandler.SetActive(shouldBeActive);
                }
            }
        }

        // ��s��e���ʭ���
        currentActivePage = pageToShow;

        // �b����������j���s UI
        StartCoroutine(ForceRefreshAfterPageChange());
    }

    // ����������j���s UI
    private IEnumerator ForceRefreshAfterPageChange()
    {
        // ���ݤ@�V�T�O UI �ե󦳮ɶ���s
        yield return null;

        // �j���s�Ҧ� Canvas
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = false;
            canvas.enabled = true;
        }

        // �j���s CanvasGroup
        if (currentActivePage != null && currentActivePage.canvasGroup != null)
        {
            // Ĳ�o���sø�s
            currentActivePage.canvasGroup.alpha = 0.99f;
            yield return null;
            currentActivePage.canvasGroup.alpha = 1f;
        }

        // �j���s Canvas
        Canvas.ForceUpdateCanvases();

        // �q����e�����w�Q�E��
        if (currentActivePage != null && currentActivePage.canvasObject != null)
        {
            // �ˬd�O�_�� GameReadySystem ����óq���������w�E��
            GameReadySystem gameReadySystem = currentActivePage.canvasObject.GetComponentInChildren<GameReadySystem>();
            if (gameReadySystem != null)
            {
                gameReadySystem.OnPageActivated();
            }

            // �ˬd�O�_�� DeckSelector ����ñj���s
            DeckSelector deckSelector = currentActivePage.canvasObject.GetComponentInChildren<DeckSelector>();
            if (deckSelector != null && deckSelector.enabled)
            {
                // ����@�V��A�ե� ForceRefreshUI ��k
                StartCoroutine(DelayedDeckSelectorRefresh(deckSelector));
            }
        }
    }

    private IEnumerator DelayedDeckSelectorRefresh(DeckSelector deckSelector)
    {
        yield return null;
        // �Ϯg�եΨp����k ForceRefreshUI (�p�G�L�k�����եΡA�A�i�H�Ҽ{�b DeckSelector ���K�[�@�Ӥ��@��k)
        System.Reflection.MethodInfo method = typeof(DeckSelector).GetMethod("ForceRefreshUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
        {
            method.Invoke(deckSelector, null);
        }
    }

    /// <summary>
    /// ��ܰ���e������ nextPageName ���U�@��
    /// </summary>
    public void ShowNextPage()
    {
        if (currentActivePage != null && !string.IsNullOrEmpty(currentActivePage.nextPageName))
        {
            ShowPage(currentActivePage.nextPageName);
        }
    }

    /// <summary>
    /// �b�B��ɲK�[�s�� Canvas ����
    /// </summary>
    public void AddCanvasPage(string pageName, GameObject canvasObject, string nextPageName = "")
    {
        // �ˬd�����O�_�w�s�b
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                Debug.LogWarning($"Canvas page '{pageName}' already exists!");
                return;
            }
        }

        // �ЫبòK�[�s����
        CanvasPage newPage = new CanvasPage
        {
            pageName = pageName,
            canvasObject = canvasObject,
            nextPageName = nextPageName
        };

        // �T�O�� CanvasGroup �ե�M��J�B�z��
        if (canvasObject != null)
        {
            // �T�O����B��E�����A
            canvasObject.SetActive(true);

            // ����βK�[ CanvasGroup
            newPage.canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            if (newPage.canvasGroup == null)
            {
                newPage.canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            }

            // �����J�B�z��
            newPage.inputHandler = canvasObject.GetComponent<PageInputHandler>();

            // ��l�Ƭ����i��
            newPage.canvasGroup.alpha = 0f;
            newPage.canvasGroup.interactable = false;
            newPage.canvasGroup.blocksRaycasts = false;

            // ��l�ƸT�ο�J
            if (newPage.inputHandler != null)
            {
                newPage.inputHandler.SetActive(false);
            }
        }

        canvasPages.Add(newPage);

        Debug.Log($"Added new canvas page: {pageName}");
    }

    /// <summary>
    /// ��s�{�� Canvas �������U�@��
    /// </summary>
    public void SetNextPage(string pageName, string nextPageName)
    {
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                page.nextPageName = nextPageName;
                return;
            }
        }

        Debug.LogWarning($"Canvas page '{pageName}' not found!");
    }
}

// �O�� PageInputHandler ����
public class PageInputHandler : MonoBehaviour
{
    // �]�t�Ҧ��ݭn�b�������E������J�B�z�}��
    [SerializeField] private List<MonoBehaviour> inputHandlerScripts = new List<MonoBehaviour>();

    // �T�w��������e�O�_���ӱ�����J
    private bool _isActive = false;

    public void SetActive(bool active)
    {
        if (_isActive == active)
            return;

        _isActive = active;

        // �E���θT�ΩҦ���J�B�z�}��
        foreach (var handler in inputHandlerScripts)
        {
            if (handler != null)
            {
                handler.enabled = active;
            }
        }

        // �p�G�E���A�T�O������O�E�����A�o�� Update ��k�|�Q�ե�
        this.enabled = active;
    }

    // ����Q�T�ήɦ۰ʸT�ΩҦ���J�B�z
    private void OnDisable()
    {
        if (_isActive)
        {
            SetActive(false);
        }
    }
}