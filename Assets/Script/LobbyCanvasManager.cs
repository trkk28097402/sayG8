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
        public string nextPageName; // The name of the next page to navigate to
    }

    [Header("Canvas Pages")]
    [SerializeField] private List<CanvasPage> canvasPages = new List<CanvasPage>();
    [SerializeField] private string initialPageName = "DeckSelectCanvas";

    [Header("Navigation Settings")]
    [SerializeField] private bool useEnterKeyNavigation = true;
    [SerializeField] private float inputCooldown = 0.3f;

    private CanvasPage currentActivePage;
    private float lastInputTime = 0f;

    private void Start()
    {
        // Initialize by showing the initial page
        ShowPage(initialPageName);
    }

    private void Update()
    {
        // Skip if no page is active or enter key navigation is disabled
        if (currentActivePage == null || !useEnterKeyNavigation)
            return;

        // Check for cooldown
        if (Time.time - lastInputTime < inputCooldown)
            return;

        // Navigate to next page on Enter key press
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
    /// Shows the specified page and hides all others
    /// </summary>
    /// <param name="pageName">Name of the page to show</param>
    public void ShowPage(string pageName)
    {
        if (string.IsNullOrEmpty(pageName))
            return;

        bool foundPage = false;

        // First pass: find the page we want to show
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

        // Second pass: hide all pages except the one we want to show
        foreach (var page in canvasPages)
        {
            if (page.canvasObject != null)
            {
                bool shouldBeActive = (page == pageToShow);
                page.canvasObject.SetActive(shouldBeActive);
            }
        }

        // Update current active page
        currentActivePage = pageToShow;

        Debug.Log($"Showing page: {pageName}");
    }

    /// <summary>
    /// Shows the next page in sequence based on the current page's nextPageName
    /// </summary>
    public void ShowNextPage()
    {
        if (currentActivePage != null && !string.IsNullOrEmpty(currentActivePage.nextPageName))
        {
            ShowPage(currentActivePage.nextPageName);
        }
    }

    /// <summary>
    /// Add a new canvas page at runtime
    /// </summary>
    public void AddCanvasPage(string pageName, GameObject canvasObject, string nextPageName = "")
    {
        // Check if the page already exists
        foreach (var page in canvasPages)
        {
            if (page.pageName == pageName)
            {
                Debug.LogWarning($"Canvas page '{pageName}' already exists!");
                return;
            }
        }

        // Create and add the new page
        CanvasPage newPage = new CanvasPage
        {
            pageName = pageName,
            canvasObject = canvasObject,
            nextPageName = nextPageName
        };

        canvasPages.Add(newPage);

        // Hide the new page initially
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }

        Debug.Log($"Added new canvas page: {pageName}");
    }

    /// <summary>
    /// Update the next page for an existing canvas page
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