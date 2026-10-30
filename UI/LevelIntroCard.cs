using System.Collections;
using TMPro;
using UnityEngine;

public class LevelIntroCard : MonoBehaviour
{
    [Header("Text")]
    public string areaTitle = "THE FIRST REFLECTION";
    public string levelTitle = "I";

    [TextArea]
    public string blurb = "";

    [Header("References")]
    public TMP_Text areaText;
    public TMP_Text levelText;
    public TMP_Text blurbText;
    public CanvasGroup canvasGroup;

    [Header("Timing")]
    public float fadeInTime = 0.5f;
    public float holdTime = 2f;
    public float fadeOutTime = 0.6f;

    [Header("Optional")]
    [Tooltip("Player input object disabled while the title card is showing.")]
    public GameObject inputObject;

    private void Start()
    {
        areaText.text = areaTitle;
        levelText.text = levelTitle;

        if (blurbText != null)
        {
            blurbText.text = blurb;
            blurbText.gameObject.SetActive(!string.IsNullOrWhiteSpace(blurb));
        }

        StartCoroutine(ShowIntro());
    }

    private IEnumerator ShowIntro()
    {
        if (inputObject != null)
            inputObject.SetActive(false);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;

        // Fade in
        float t = 0f;

        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInTime);
            yield return null;
        }

        canvasGroup.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(holdTime);

        // Fade out
        t = 0f;

        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutTime);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        if (inputObject != null)
            inputObject.SetActive(true);

        gameObject.SetActive(false);
    }
}