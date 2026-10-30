using System.Collections;
using UnityEngine;

public enum GameFlowButtonAction
{
    LoadHub,
    LoadArea,
    LoadPuzzle,
    CompletePuzzle,
    CompleteAreaGoal,
    ResetProgress
}

public class GameFlowButton : MonoBehaviour
{
    [Header("Action")]
    public GameFlowButtonAction action;

    [Header("Target")]
    public int areaIndex = 0;
    public int puzzleIndex = 0;

    [Header("Lock / unlock behavior")]
    [Tooltip("If true, this button checks whether it should be usable before allowing the action.")]
    public bool useUnlockRules = true;

    [Tooltip("If true, completed puzzles/areas can still be clicked again for replay.")]
    public bool allowReplayWhenCompleted = true;

    [Header("Optional simple visuals")]
    public GameObject unlockedVisual;
    public GameObject lockedVisual;
    public GameObject completedVisual;

    [Header("Optional button state animator")]
    [Tooltip("Animator that controls Locked / Available / Completed button states.")]
    public Animator stateAnimator;

    [Tooltip("Integer parameter used by the button state animator. 0 = Locked, 1 = Available, 2 = Completed.")]
    public string stateAnimatorInt = "FlowState";

    [Header("Optional local animation before action")]
    [Tooltip("Optional animator on this button, portal, doorway, mirror, etc.")]
    public Animator optionalAnimator;

    [Tooltip("Trigger to play before the game-flow action happens.")]
    public string optionalTrigger = "Open";

    [Tooltip("Delay before loading/completing after the local animation starts.")]
    public float delayBeforeAction = 0.25f;

    [Header("Complete Puzzle Collect Effect")]
    [Tooltip("How long a CompletePuzzle object takes to shrink away when collected.")]
    public float collectShrinkDuration = 0.75f;

    [Header("Optional Complete Puzzle End Card")]
    [Tooltip("Optional card shown after the shard disappears but before the puzzle completes. Leave empty for no end card.")]
    public LevelTextCard completePuzzleEndCard;

    private bool isRunning = false;
    private Coroutine runningRoutine;

    private Collider buttonCollider;
    private ButtonPressed buttonPressed;

    private Renderer[] objectRenderers;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider>();
        buttonPressed = GetComponent<ButtonPressed>();

        objectRenderers =
            GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        RefreshButtonState();
    }

    private void OnEnable()
    {
        RefreshButtonState();
    }

    public void RefreshButtonState()
    {
        if (GameFlowManager.Instance == null)
        {
            return;
        }

        bool unlocked = IsUnlocked();
        bool completed = IsCompleted();

        bool clickable = unlocked;

        if (completed && allowReplayWhenCompleted)
        {
            clickable = true;
        }

        if (completed && !allowReplayWhenCompleted)
        {
            clickable = false;
        }

        if (buttonCollider != null)
        {
            buttonCollider.enabled = clickable;
        }

        if (buttonPressed != null)
        {
            buttonPressed.enabled = clickable;
        }

        if (unlockedVisual != null)
        {
            unlockedVisual.SetActive(
                unlocked && !completed
            );
        }

        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!unlocked);
        }

        if (completedVisual != null)
        {
            completedVisual.SetActive(completed);
        }

        UpdateStateAnimator(unlocked, completed);
    }

    private void UpdateStateAnimator(
        bool unlocked,
        bool completed)
    {
        if (stateAnimator == null ||
            string.IsNullOrEmpty(stateAnimatorInt))
        {
            return;
        }

        int stateValue = 0;

        if (completed)
        {
            stateValue = 2;
        }
        else if (unlocked)
        {
            stateValue = 1;
        }
        else
        {
            stateValue = 0;
        }

        stateAnimator.SetInteger(
            stateAnimatorInt,
            stateValue
        );
    }

    public bool CanRun()
    {
        if (!useUnlockRules)
        {
            return true;
        }

        if (GameFlowManager.Instance == null)
        {
            return false;
        }

        bool unlocked = IsUnlocked();
        bool completed = IsCompleted();

        if (!unlocked)
        {
            return false;
        }

        if (completed && !allowReplayWhenCompleted)
        {
            return false;
        }

        return true;
    }

    public void Run()
    {
        if (isRunning)
        {
            return;
        }

        RefreshButtonState();

        if (!CanRun())
        {
            Debug.Log(
                $"[GameFlowButton] Button is locked: {gameObject.name}"
            );

            return;
        }

        isRunning = true;

        runningRoutine =
            StartCoroutine(RunRoutine());
    }

    private IEnumerator RunRoutine()
    {
        float actionStartTime = Time.time;

        // Prevent repeated CompletePuzzle triggers
        if (action == GameFlowButtonAction.CompletePuzzle)
        {
            if (buttonCollider != null)
            {
                buttonCollider.enabled = false;
            }

            if (buttonPressed != null)
            {
                buttonPressed.enabled = false;
            }
        }

        // Local animation
        if (optionalAnimator != null &&
            !string.IsNullOrEmpty(optionalTrigger))
        {
            optionalAnimator.SetTrigger(optionalTrigger);
        }

        // =====================================================
        // COMPLETE PUZZLE
        // =====================================================

        if (action == GameFlowButtonAction.CompletePuzzle)
        {
            yield return StartCoroutine(
                ShrinkAndDisappear()
            );

            // Include shrink time in delay
            float timeAlreadySpent =
                Time.time - actionStartTime;

            float remainingDelay =
                delayBeforeAction - timeAlreadySpent;

            if (remainingDelay > 0f)
            {
                yield return new WaitForSeconds(
                    remainingDelay
                );
            }

            // End card
            if (completePuzzleEndCard != null)
            {
                yield return StartCoroutine(
                    completePuzzleEndCard.ShowCardAndWait()
                );
            }
        }

        // =====================================================
        // OTHER ACTIONS
        // =====================================================

        else
        {
            if (delayBeforeAction > 0f)
            {
                yield return new WaitForSeconds(
                    delayBeforeAction
                );
            }
        }

        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning(
                $"[GameFlowButton] No GameFlowManager found. Button: {gameObject.name}"
            );

            isRunning = false;

            yield break;
        }

        switch (action)
        {
            case GameFlowButtonAction.LoadHub:

                GameFlowManager.Instance.LoadHub();

                break;

            case GameFlowButtonAction.LoadArea:

                GameFlowManager.Instance.LoadArea(
                    areaIndex
                );

                break;

            case GameFlowButtonAction.LoadPuzzle:

                GameFlowManager.Instance.LoadPuzzle(
                    areaIndex,
                    puzzleIndex
                );

                break;

            case GameFlowButtonAction.CompletePuzzle:

                GameFlowManager.Instance
                    .CompleteCurrentPuzzle();

                break;

            case GameFlowButtonAction.CompleteAreaGoal:

                GameFlowManager.Instance
                    .CompleteAreaGoal(areaIndex);

                break;

            case GameFlowButtonAction.ResetProgress:

                GameFlowManager.Instance
                    .ResetProgress();

                break;
        }

        isRunning = false;
    }

    private IEnumerator ShrinkAndDisappear()
    {
        Vector3 startingScale =
            transform.localScale;

        if (collectShrinkDuration <= 0f)
        {
            transform.localScale =
                Vector3.zero;

            DisableRenderers();

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < collectShrinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / collectShrinkDuration
            );

            float smoothT =
                Mathf.SmoothStep(0f, 1f, t);

            transform.localScale =
                Vector3.Lerp(
                    startingScale,
                    Vector3.zero,
                    smoothT
                );

            yield return null;
        }

        transform.localScale =
            Vector3.zero;

        DisableRenderers();
    }

    private void DisableRenderers()
    {
        if (objectRenderers == null)
        {
            return;
        }

        foreach (Renderer rend in objectRenderers)
        {
            if (rend != null)
            {
                rend.enabled = false;
            }
        }
    }

    private bool IsUnlocked()
    {
        if (!useUnlockRules)
        {
            return true;
        }

        if (GameFlowManager.Instance == null)
        {
            return false;
        }

        switch (action)
        {
            case GameFlowButtonAction.LoadArea:

                return GameFlowManager.Instance
                    .IsAreaUnlocked(areaIndex);

            case GameFlowButtonAction.LoadPuzzle:

                return GameFlowManager.Instance
                    .IsPuzzleUnlocked(
                        areaIndex,
                        puzzleIndex
                    );

            case GameFlowButtonAction.CompleteAreaGoal:

                return GameFlowManager.Instance
                    .IsAreaGoalUnlocked(areaIndex);

            case GameFlowButtonAction.CompletePuzzle:

                return true;

            case GameFlowButtonAction.LoadHub:

                return true;

            case GameFlowButtonAction.ResetProgress:

                return true;
        }

        return true;
    }

    private bool IsCompleted()
    {
        if (GameFlowManager.Instance == null)
        {
            return false;
        }

        switch (action)
        {
            case GameFlowButtonAction.LoadArea:

                return GameFlowManager.Instance
                    .IsAreaFinished(areaIndex);

            case GameFlowButtonAction.LoadPuzzle:

                return GameFlowManager.Instance
                    .IsPuzzleComplete(
                        areaIndex,
                        puzzleIndex
                    );

            case GameFlowButtonAction.CompleteAreaGoal:

                return GameFlowManager.Instance
                    .IsAreaFinished(areaIndex);
        }

        return false;
    }

    private void OnDestroy()
    {
        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }
    }
}