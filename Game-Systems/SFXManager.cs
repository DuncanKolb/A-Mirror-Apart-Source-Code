using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("One-Shot Sounds")]
    public AudioClip buttonDownSound;
    public AudioClip buttonUpSound;
    public AudioClip clickSound;
    public AudioClip shardCollectedSound;

    [Header("Movement Sounds")]
    public AudioClip walkingSound;
    public AudioClip climbingSound;

    [Header("Volumes")]
    [Range(0f, 1f)]
    public float masterSFXVolume = 0.7f;

    [Range(0f, 1f)]
    public float buttonVolume = 0.4f;

    [Range(0f, 1f)]
    public float clickVolume = 0.3f;

    [Range(0f, 1f)]
    public float shardVolume = 0.5f;

    [Range(0f, 1f)]
    public float walkingVolume = 0.25f;

    [Range(0f, 1f)]
    public float climbingVolume = 0.3f;

    private AudioSource oneShotSource;
    private AudioSource movementSource;

    private MovementSound currentMovementSound = MovementSound.None;

    private enum MovementSound
    {
        None,
        Walking,
        Climbing
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

        // One-shot audio
        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.loop = false;
        oneShotSource.spatialBlend = 0f;

        // Movement audio
        movementSource = gameObject.AddComponent<AudioSource>();
        movementSource.playOnAwake = false;
        movementSource.loop = true;
        movementSource.spatialBlend = 0f;
    }

    // =========================================================
    // MASTER VOLUME
    // =========================================================

    public float GetMasterSFXVolume()
    {
        return masterSFXVolume;
    }

    // =========================================================
    // ONE-SHOT SOUNDS
    // =========================================================

    public void PlayButtonDown()
    {
        PlayOneShot(buttonDownSound, buttonVolume);
    }

    public void PlayButtonUp()
    {
        PlayOneShot(buttonUpSound, buttonVolume);
    }

    public void PlayClick()
    {
        PlayOneShot(clickSound, clickVolume);
    }

    public void PlayShardCollected()
    {
        PlayOneShot(shardCollectedSound, shardVolume);
    }

    public void PlayCustom(AudioClip clip, float volume = 1f)
    {
        PlayOneShot(clip, volume);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(
            clip,
            volume * masterSFXVolume
        );
    }

    // =========================================================
    // MOVEMENT SOUNDS
    // =========================================================

    public void PlayWalking()
    {
        if (currentMovementSound == MovementSound.Walking &&
            movementSource.isPlaying)
        {
            return;
        }

        StartMovementSound(
            walkingSound,
            walkingVolume,
            MovementSound.Walking
        );
    }

    public void PlayClimbing()
    {
        if (currentMovementSound == MovementSound.Climbing &&
            movementSource.isPlaying)
        {
            return;
        }

        StartMovementSound(
            climbingSound,
            climbingVolume,
            MovementSound.Climbing
        );
    }

    private void StartMovementSound(
        AudioClip clip,
        float volume,
        MovementSound movementSound)
    {
        if (clip == null)
        {
            StopMovement();
            return;
        }

        movementSource.Stop();

        movementSource.clip = clip;
        movementSource.volume = volume * masterSFXVolume;
        movementSource.loop = true;

        movementSource.Play();

        currentMovementSound = movementSound;
    }

    public void StopMovement()
    {
        if (movementSource.isPlaying)
        {
            movementSource.Stop();
        }

        movementSource.clip = null;
        currentMovementSound = MovementSound.None;
    }
}