using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class LevelTextCard : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("If enabled, this card automatically appears when the scene begins.")]
    public bool showOnStart = false;

    [Tooltip("Disable player input while this card is visible.")]
    public GameObject inputObject;

    [Header("Text")]
    public string title = "";
    public string secondaryTitle = "";

    [TextArea]
    public string blurb = "";

    [Header("Text References")]
    public TMP_Text titleText;
    public TMP_Text secondaryTitleText;
    public TMP_Text blurbText;

    [Header("Visual")]
    public CanvasGroup canvasGroup;

    [Header("Timing")]
    public float fadeInTime = 0.5f;
    public float holdTime = 2f;
    public float fadeOutTime = 0.6f;

    [Header("What happens afterward")]
    public UnityEvent onFinished;

    private bool isRunning = false;

    public bool IsRunning
    {
        get { return isRunning; }
    }

    private void Start()
    {
        ApplyText();

        if (isRunning)
        {
            return;
        }

        if (showOnStart)
        {
            // Skip intro once after restarting
            if (ExitMenu.skipNextLevelIntro)
            {
                ExitMenu.skipNextLevelIntro = false;

                HideImmediately();
                return;
            }

            ShowCard();
        }
        else
        {
            SetInvisible();
        }
    }

    private void SetInvisible()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void HideImmediately()
    {
        SetInvisible();

        gameObject.SetActive(false);
    }

    private void ApplyText()
    {
        if (titleText != null)
        {
            titleText.text = title;

            titleText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(title)
            );
        }

        if (secondaryTitleText != null)
        {
            secondaryTitleText.text = secondaryTitle;

            secondaryTitleText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(secondaryTitle)
            );
        }

        if (blurbText != null)
        {
            blurbText.text = blurb;

            blurbText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(blurb)
            );
        }
    }

    public void ShowCard()
    {
        if (isRunning)
        {
            return;
        }

        gameObject.SetActive(true);

        ApplyText();

        StartCoroutine(CardRoutine());
    }

    // Show card and wait until finished
    public IEnumerator ShowCardAndWait()
    {
        ShowCard();

        while (isRunning)
        {
            yield return null;
        }
    }

    private IEnumerator CardRoutine()
    {
        isRunning = true;

        if (inputObject != null)
        {
            inputObject.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.alpha = 0f;
        }

        // =========================
        // FADE IN
        // =========================

        if (fadeInTime > 0f)
        {
            float t = 0f;

            while (t < fadeInTime)
            {
                t += Time.unscaledDeltaTime;

                if (canvasGroup != null)
                {
                    canvasGroup.alpha =
                        Mathf.Clamp01(t / fadeInTime);
                }

                yield return null;
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        // =========================
        // HOLD
        // =========================

        if (holdTime > 0f)
        {
            yield return new WaitForSecondsRealtime(holdTime);
        }

        // =========================
        // FADE OUT
        // =========================

        if (fadeOutTime > 0f)
        {
            float t = 0f;

            while (t < fadeOutTime)
            {
                t += Time.unscaledDeltaTime;

                if (canvasGroup != null)
                {
                    canvasGroup.alpha =
                        1f - Mathf.Clamp01(t / fadeOutTime);
                }

                yield return null;
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (inputObject != null)
        {
            inputObject.SetActive(true);
        }

        isRunning = false;

        onFinished?.Invoke();

        gameObject.SetActive(false);
    }
}