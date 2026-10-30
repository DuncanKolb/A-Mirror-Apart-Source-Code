using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The top-right X / Exit button.")]
    public GameObject exitButton;

    [Tooltip("The Restart / Cancel / Exit popup panel.")]
    public GameObject exitPanel;

    [Header("Cards That Hide Exit Button")]
    [Tooltip("Add the CanvasGroups for Level Intro and End Card here.")]
    public CanvasGroup[] blockingCards;

    [Header("Editor Testing Fallback")]
    [Tooltip(
        "Only used if the puzzle was launched directly in the Unity Editor " +
        "and GameFlowManager does not know the current area. " +
        "Area A = 0, Area B = 1, Area C = 2, etc."
    )]
    public int fallbackAreaIndex = 0;

    private bool isOpen = false;

    public static bool skipNextLevelIntro = false;

    private void Start()
    {
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }

        RefreshExitButtonVisibility();
    }

    private void Update()
    {
        // Escape toggles the exit menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isOpen && IsAnyCardShowing())
            {
                return;
            }

            if (isOpen)
            {
                Cancel();
            }
            else
            {
                OpenMenu();
            }
        }

        // Update visibility during card fades
        RefreshExitButtonVisibility();
    }

    public void OpenMenu()
    {
        if (exitPanel == null)
        {
            return;
        }

        if (IsAnyCardShowing())
        {
            return;
        }

        exitPanel.SetActive(true);

        isOpen = true;

        Time.timeScale = 0f;

        RefreshExitButtonVisibility();
    }

    public void Cancel()
    {
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }

        isOpen = false;

        Time.timeScale = 1f;

        RefreshExitButtonVisibility();
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;

        // Skip intro card after restart
        skipNextLevelIntro = true;

        Scene currentScene =
            SceneManager.GetActiveScene();

        SceneManager.LoadScene(
            currentScene.buildIndex
        );
    }

    public void ExitToArea()
    {
        Time.timeScale = 1f;

        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning(
                "[ExitMenu] No GameFlowManager found."
            );

            return;
        }

        int targetArea =
            GameFlowManager.Instance.CurrentAreaIndex;

        // Use fallback when scene is launched directly in Editor
        if (targetArea < 0)
        {
            targetArea = fallbackAreaIndex;

            Debug.LogWarning(
                $"[ExitMenu] CurrentAreaIndex was not set. " +
                $"Using fallback area {fallbackAreaIndex}."
            );
        }

        GameFlowManager.Instance.LoadArea(
            targetArea
        );
    }

    private bool IsAnyCardShowing()
    {
        if (blockingCards == null)
        {
            return false;
        }

        foreach (CanvasGroup card in blockingCards)
        {
            if (card != null &&
                card.gameObject.activeInHierarchy &&
                card.alpha > 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshExitButtonVisibility()
    {
        if (exitButton == null)
        {
            return;
        }

        bool shouldShow =
            !isOpen &&
            !IsAnyCardShowing();

        exitButton.SetActive(shouldShow);
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            Time.timeScale = 1f;
        }
    }
}