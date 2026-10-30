using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("Turn this on while testing if you want progress to reset every time you press Play. Turn it off for the actual game.")]
    public bool resetProgressOnPlay = false;

    public static GameFlowManager Instance { get; private set; }

    [System.Serializable]
    public class AreaData
    {
        public string areaName;
        public string areaSceneName;
        public List<string> puzzleSceneNames = new List<string>();
    }

    [Header("Main scenes")]
    public string hubSceneName = "Hub";

    [Header("Areas")]
    public List<AreaData> areas = new List<AreaData>();

    [Header("Optional global transition animation")]
    [Tooltip("Optional persistent transition animator. Best if this is on a child of the GameFlowManager object.")]
    public Animator transitionAnimator;

    public string fadeOutTrigger = "FadeOut";
    public string fadeInTrigger = "FadeIn";
    public float fadeOutDelay = 0.35f;
    public float postLoadDelay = 0.05f;

    public int CurrentAreaIndex { get; private set; } = -1;
    public int CurrentPuzzleIndex { get; private set; } = -1;

    private const string SavePrefix = "MirrorGame_";
    private const string HighestUnlockedAreaKey = SavePrefix + "HighestUnlockedArea";

    // Saved location
    // 0 = Hub
    // 1 = Area
    // 2 = Puzzle
    private const string LastLocationTypeKey = SavePrefix + "LastLocationType";
    private const string LastAreaKey = SavePrefix + "LastArea";
    private const string LastPuzzleKey = SavePrefix + "LastPuzzle";

    private int highestUnlockedArea = 0;
    private bool isLoadingScene = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (resetProgressOnPlay)
        {
            ClearSavedProgressOnly();
        }

        highestUnlockedArea = PlayerPrefs.GetInt(HighestUnlockedAreaKey, 0);

        if (areas.Count > 0)
        {
            highestUnlockedArea = Mathf.Clamp(
                highestUnlockedArea,
                0,
                areas.Count - 1
            );
        }
        else
        {
            highestUnlockedArea = 0;
        }
    }

    public bool IsAreaUnlocked(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= areas.Count)
        {
            return false;
        }

        return areaIndex <= highestUnlockedArea;
    }

    private void ClearSavedProgressOnly()
    {
        PlayerPrefs.DeleteKey(HighestUnlockedAreaKey);

        PlayerPrefs.DeleteKey(LastLocationTypeKey);
        PlayerPrefs.DeleteKey(LastAreaKey);
        PlayerPrefs.DeleteKey(LastPuzzleKey);

        for (int area = 0; area < areas.Count; area++)
        {
            PlayerPrefs.DeleteKey(GetAreaFinishedKey(area));

            for (
                int puzzle = 0;
                puzzle < areas[area].puzzleSceneNames.Count;
                puzzle++
            )
            {
                PlayerPrefs.DeleteKey(
                    GetPuzzleCompleteKey(area, puzzle)
                );
            }
        }

        PlayerPrefs.Save();

        highestUnlockedArea = 0;
        CurrentAreaIndex = -1;
        CurrentPuzzleIndex = -1;
    }

    public bool IsAreaFinished(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= areas.Count)
        {
            return false;
        }

        return PlayerPrefs.GetInt(
            GetAreaFinishedKey(areaIndex),
            0
        ) == 1;
    }

    public bool AreAllPuzzlesComplete(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= areas.Count)
        {
            return false;
        }

        if (areas[areaIndex].puzzleSceneNames.Count == 0)
        {
            return false;
        }

        for (
            int i = 0;
            i < areas[areaIndex].puzzleSceneNames.Count;
            i++
        )
        {
            if (!IsPuzzleComplete(areaIndex, i))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsAreaGoalUnlocked(int areaIndex)
    {
        if (!IsAreaUnlocked(areaIndex))
        {
            return false;
        }

        return AreAllPuzzlesComplete(areaIndex);
    }

    public bool IsPuzzleUnlocked(int areaIndex, int puzzleIndex)
    {
        if (!IsAreaUnlocked(areaIndex))
        {
            return false;
        }

        if (!IsValidPuzzle(areaIndex, puzzleIndex))
        {
            return false;
        }

        // Completed puzzles stay unlocked
        if (IsPuzzleComplete(areaIndex, puzzleIndex))
        {
            return true;
        }

        // First puzzle in an unlocked area
        if (puzzleIndex == 0)
        {
            return true;
        }

        // Unlock after previous puzzle is complete
        return IsPuzzleComplete(areaIndex, puzzleIndex - 1);
    }

    public bool IsPuzzleComplete(int areaIndex, int puzzleIndex)
    {
        if (!IsValidPuzzle(areaIndex, puzzleIndex))
        {
            return false;
        }

        return PlayerPrefs.GetInt(
            GetPuzzleCompleteKey(areaIndex, puzzleIndex),
            0
        ) == 1;
    }

    // Load saved game location
    public void LoadSavedLocation()
    {
        // New save starts at first puzzle
        if (!PlayerPrefs.HasKey(LastLocationTypeKey))
        {
            LoadPuzzle(0, 0);
            return;
        }

        int locationType = PlayerPrefs.GetInt(
            LastLocationTypeKey,
            0
        );

        int savedArea = PlayerPrefs.GetInt(
            LastAreaKey,
            -1
        );

        int savedPuzzle = PlayerPrefs.GetInt(
            LastPuzzleKey,
            -1
        );

        switch (locationType)
        {
            case 0:
                LoadHub();
                break;

            case 1:
                if (
                    savedArea >= 0 &&
                    savedArea < areas.Count &&
                    IsAreaUnlocked(savedArea)
                )
                {
                    LoadArea(savedArea);
                }
                else
                {
                    LoadPuzzle(0, 0);
                }

                break;

            case 2:
                if (
                    IsValidPuzzle(savedArea, savedPuzzle) &&
                    IsPuzzleUnlocked(savedArea, savedPuzzle)
                )
                {
                    LoadPuzzle(savedArea, savedPuzzle);
                }
                else
                {
                    LoadPuzzle(0, 0);
                }

                break;

            default:
                LoadPuzzle(0, 0);
                break;
        }
    }

    public void LoadHub()
    {
        if (isLoadingScene)
        {
            return;
        }

        CurrentAreaIndex = -1;
        CurrentPuzzleIndex = -1;

        // Save hub location
        PlayerPrefs.SetInt(LastLocationTypeKey, 0);
        PlayerPrefs.SetInt(LastAreaKey, -1);
        PlayerPrefs.SetInt(LastPuzzleKey, -1);
        PlayerPrefs.Save();

        StartCoroutine(
            LoadSceneRoutine(hubSceneName)
        );
    }

    public void LoadArea(int areaIndex)
    {
        if (isLoadingScene)
        {
            return;
        }

        if (areaIndex < 0 || areaIndex >= areas.Count)
        {
            Debug.LogWarning(
                $"[GameFlowManager] Invalid area index: {areaIndex}"
            );

            return;
        }

        if (!IsAreaUnlocked(areaIndex))
        {
            Debug.LogWarning(
                $"[GameFlowManager] Area {areaIndex} is locked."
            );

            return;
        }

        CurrentAreaIndex = areaIndex;
        CurrentPuzzleIndex = -1;

        string areaSceneName =
            areas[areaIndex].areaSceneName;

        if (string.IsNullOrEmpty(areaSceneName))
        {
            Debug.LogWarning(
                $"[GameFlowManager] Area {areaIndex} does not have a scene name."
            );

            return;
        }

        // Save area location
        PlayerPrefs.SetInt(LastLocationTypeKey, 1);
        PlayerPrefs.SetInt(LastAreaKey, areaIndex);
        PlayerPrefs.SetInt(LastPuzzleKey, -1);
        PlayerPrefs.Save();

        StartCoroutine(
            LoadSceneRoutine(areaSceneName)
        );
    }

    public void LoadPuzzle(
        int areaIndex,
        int puzzleIndex
    )
    {
        if (isLoadingScene)
        {
            return;
        }

        if (!IsValidPuzzle(areaIndex, puzzleIndex))
        {
            Debug.LogWarning(
                $"[GameFlowManager] Invalid puzzle: area {areaIndex}, puzzle {puzzleIndex}"
            );

            return;
        }

        if (!IsPuzzleUnlocked(areaIndex, puzzleIndex))
        {
            Debug.LogWarning(
                $"[GameFlowManager] Puzzle {areaIndex}-{puzzleIndex} is locked."
            );

            return;
        }

        CurrentAreaIndex = areaIndex;
        CurrentPuzzleIndex = puzzleIndex;

        string puzzleSceneName =
            areas[areaIndex].puzzleSceneNames[puzzleIndex];

        if (string.IsNullOrEmpty(puzzleSceneName))
        {
            Debug.LogWarning(
                $"[GameFlowManager] Puzzle {areaIndex}-{puzzleIndex} does not have a scene name."
            );

            return;
        }

        // Save puzzle location
        PlayerPrefs.SetInt(LastLocationTypeKey, 2);
        PlayerPrefs.SetInt(LastAreaKey, areaIndex);
        PlayerPrefs.SetInt(LastPuzzleKey, puzzleIndex);
        PlayerPrefs.Save();

        StartCoroutine(
            LoadSceneRoutine(puzzleSceneName)
        );
    }

    public void CompleteCurrentPuzzle()
    {
        if (isLoadingScene)
        {
            return;
        }

        if (
            !IsValidPuzzle(
                CurrentAreaIndex,
                CurrentPuzzleIndex
            )
        )
        {
            Debug.LogWarning(
                "[GameFlowManager] Tried to complete a puzzle, but CurrentAreaIndex or CurrentPuzzleIndex is invalid."
            );

            Debug.LogWarning(
                $"[GameFlowManager] CurrentAreaIndex: {CurrentAreaIndex}, CurrentPuzzleIndex: {CurrentPuzzleIndex}"
            );

            return;
        }

        int areaToReturnTo = CurrentAreaIndex;

        PlayerPrefs.SetInt(
            GetPuzzleCompleteKey(
                CurrentAreaIndex,
                CurrentPuzzleIndex
            ),
            1
        );

        // Finish area when all puzzles are complete
        if (AreAllPuzzlesComplete(areaToReturnTo))
        {
            MarkAreaFinishedAndUnlockNext(
                areaToReturnTo
            );
        }

        PlayerPrefs.Save();

        LoadArea(areaToReturnTo);
    }

    private void MarkAreaFinishedAndUnlockNext(
        int areaIndex
    )
    {
        if (
            areaIndex < 0 ||
            areaIndex >= areas.Count
        )
        {
            return;
        }

        PlayerPrefs.SetInt(
            GetAreaFinishedKey(areaIndex),
            1
        );

        int nextArea = areaIndex + 1;

        if (nextArea < areas.Count)
        {
            highestUnlockedArea = Mathf.Max(
                highestUnlockedArea,
                nextArea
            );

            PlayerPrefs.SetInt(
                HighestUnlockedAreaKey,
                highestUnlockedArea
            );
        }
    }

    public void CompleteAreaGoal(int areaIndex)
    {
        if (isLoadingScene)
        {
            return;
        }

        if (
            areaIndex < 0 ||
            areaIndex >= areas.Count
        )
        {
            Debug.LogWarning(
                $"[GameFlowManager] Invalid area goal index: {areaIndex}"
            );

            return;
        }

        if (!IsAreaGoalUnlocked(areaIndex))
        {
            Debug.LogWarning(
                $"[GameFlowManager] Area goal for area {areaIndex} is locked. Complete all puzzles first."
            );

            return;
        }

        PlayerPrefs.SetInt(
            GetAreaFinishedKey(areaIndex),
            1
        );

        int nextArea = areaIndex + 1;

        if (nextArea < areas.Count)
        {
            highestUnlockedArea = Mathf.Max(
                highestUnlockedArea,
                nextArea
            );

            PlayerPrefs.SetInt(
                HighestUnlockedAreaKey,
                highestUnlockedArea
            );
        }

        PlayerPrefs.Save();

        // Return to hub
        LoadHub();
    }

    public void CompleteCurrentAreaGoal()
    {
        if (CurrentAreaIndex < 0)
        {
            Debug.LogWarning(
                "[GameFlowManager] Tried to complete current area goal, but CurrentAreaIndex is invalid."
            );

            return;
        }

        CompleteAreaGoal(CurrentAreaIndex);
    }

    public void ResetProgress()
    {
        if (isLoadingScene)
        {
            return;
        }

        PlayerPrefs.DeleteKey(
            HighestUnlockedAreaKey
        );

        PlayerPrefs.DeleteKey(
            LastLocationTypeKey
        );

        PlayerPrefs.DeleteKey(
            LastAreaKey
        );

        PlayerPrefs.DeleteKey(
            LastPuzzleKey
        );

        for (
            int area = 0;
            area < areas.Count;
            area++
        )
        {
            PlayerPrefs.DeleteKey(
                GetAreaFinishedKey(area)
            );

            for (
                int puzzle = 0;
                puzzle < areas[area].puzzleSceneNames.Count;
                puzzle++
            )
            {
                PlayerPrefs.DeleteKey(
                    GetPuzzleCompleteKey(
                        area,
                        puzzle
                    )
                );
            }
        }

        PlayerPrefs.Save();

        highestUnlockedArea = 0;
        CurrentAreaIndex = -1;
        CurrentPuzzleIndex = -1;

        LoadHub();
    }

    private IEnumerator LoadSceneRoutine(
        string sceneName
    )
    {
        isLoadingScene = true;

        if (
            transitionAnimator != null &&
            !string.IsNullOrEmpty(fadeOutTrigger)
        )
        {
            transitionAnimator.SetTrigger(
                fadeOutTrigger
            );

            yield return new WaitForSeconds(
                fadeOutDelay
            );
        }

        SceneManager.LoadScene(sceneName);

        if (postLoadDelay > 0f)
        {
            yield return new WaitForSeconds(
                postLoadDelay
            );
        }

        if (
            transitionAnimator != null &&
            !string.IsNullOrEmpty(fadeInTrigger)
        )
        {
            transitionAnimator.SetTrigger(
                fadeInTrigger
            );
        }

        isLoadingScene = false;
    }

    private bool IsValidPuzzle(
        int areaIndex,
        int puzzleIndex
    )
    {
        if (
            areaIndex < 0 ||
            areaIndex >= areas.Count
        )
        {
            return false;
        }

        if (
            puzzleIndex < 0 ||
            puzzleIndex >=
            areas[areaIndex].puzzleSceneNames.Count
        )
        {
            return false;
        }

        return true;
    }

    private string GetPuzzleCompleteKey(
        int areaIndex,
        int puzzleIndex
    )
    {
        return SavePrefix
            + "Area_"
            + areaIndex
            + "_Puzzle_"
            + puzzleIndex
            + "_Complete";
    }

    private string GetAreaFinishedKey(
        int areaIndex
    )
    {
        return SavePrefix
            + "Area_"
            + areaIndex
            + "_Finished";
    }
}