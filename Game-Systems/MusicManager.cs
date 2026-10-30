using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music")]
    public AudioClip hubMusic;
    public AudioClip puzzleMusic;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float musicVolume = 0.7f;

    [Tooltip("How long it takes to crossfade between Hub and Puzzle music.")]
    public float crossfadeDuration = 1f;

    private AudioSource sourceA;
    private AudioSource sourceB;

    private AudioSource activeSource;
    private AudioSource inactiveSource;

    private Coroutine fadeRoutine;

    private float hubMusicTime = 0f;
    private float puzzleMusicTime = 0f;

    private MusicMode currentMode = MusicMode.None;

    private enum MusicMode
    {
        None,
        Hub,
        Puzzle
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sourceA = GetComponent<AudioSource>();

        // Second AudioSource for crossfading
        sourceB = gameObject.AddComponent<AudioSource>();

        SetupAudioSource(sourceA);
        SetupAudioSource(sourceB);

        activeSource = sourceA;
        inactiveSource = sourceB;
    }

    private void SetupAudioSource(AudioSource source)
    {
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        string sceneName = scene.name;

        // Puzzle scenes
        if (sceneName.StartsWith("Level", StringComparison.OrdinalIgnoreCase))
        {
            PlayMode(MusicMode.Puzzle);
            return;
        }

        // Hub and area scenes
        if (sceneName.Equals("OVERALL HUB", StringComparison.OrdinalIgnoreCase) ||
            sceneName.StartsWith("Area", StringComparison.OrdinalIgnoreCase))
        {
            PlayMode(MusicMode.Hub);
            return;
        }
    }

    private void PlayMode(MusicMode newMode)
    {
        if (currentMode == newMode &&
            activeSource != null &&
            activeSource.isPlaying)
        {
            return;
        }

        SaveCurrentPosition();

        AudioClip newClip = null;
        float savedTime = 0f;

        if (newMode == MusicMode.Hub)
        {
            newClip = hubMusic;
            savedTime = hubMusicTime;
        }
        else if (newMode == MusicMode.Puzzle)
        {
            newClip = puzzleMusic;
            savedTime = puzzleMusicTime;
        }

        if (newClip == null)
        {
            return;
        }

        // Stop active crossfade
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(
            CrossfadeTo(newClip, savedTime, newMode)
        );
    }

    private IEnumerator CrossfadeTo(
        AudioClip newClip,
        float savedTime,
        MusicMode newMode)
    {
        AudioSource oldSource = activeSource;
        AudioSource newSource = inactiveSource;

        // Prepare new track
        newSource.clip = newClip;

        if (savedTime >= 0f && savedTime < newClip.length)
        {
            newSource.time = savedTime;
        }
        else
        {
            newSource.time = 0f;
        }

        newSource.volume = 0f;
        newSource.Play();

        float oldStartingVolume = oldSource.isPlaying
            ? oldSource.volume
            : 0f;

        // Immediate switch
        if (crossfadeDuration <= 0f)
        {
            oldSource.Stop();
            oldSource.volume = 0f;

            newSource.volume = musicVolume;

            activeSource = newSource;
            inactiveSource = oldSource;

            currentMode = newMode;

            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / crossfadeDuration
            );

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            oldSource.volume =
                Mathf.Lerp(oldStartingVolume, 0f, smoothT);

            newSource.volume =
                Mathf.Lerp(0f, musicVolume, smoothT);

            yield return null;
        }

        oldSource.volume = 0f;
        oldSource.Stop();

        newSource.volume = musicVolume;

        // Swap active AudioSources
        activeSource = newSource;
        inactiveSource = oldSource;

        currentMode = newMode;

        fadeRoutine = null;
    }

    private void SaveCurrentPosition()
    {
        if (activeSource == null ||
            activeSource.clip == null ||
            !activeSource.isPlaying)
        {
            return;
        }

        if (currentMode == MusicMode.Hub)
        {
            hubMusicTime = activeSource.time;
        }
        else if (currentMode == MusicMode.Puzzle)
        {
            puzzleMusicTime = activeSource.time;
        }
    }
}