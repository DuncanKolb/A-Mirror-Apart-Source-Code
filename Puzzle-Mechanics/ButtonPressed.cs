using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class ButtonPressed : MonoBehaviour
{
    [Header("Tags allowed to press this button")]
    public string playerTag = "Player";
    public string cloneTag = "Clone";

    [Header("Animator parameter used for the button's own animator")]
    public string buttonAnimatorBool = "ButtonPressed";

    [Header("How close to center the player must be")]
    public float centerThreshold = 0.42f;

    [Header("Optional one-use behavior")]
    [Tooltip("If true, this button can only fire once. Good for one-time switch buttons or scene buttons.")]
    public bool isOnceButton = false;

    [Tooltip("If true, a once button stays visually pressed instead of releasing next frame. Good for scene-change buttons.")]
    public bool holdPressedWhenUsedOnce = false;

    [Header("Existing puzzle/effect button behavior")]
    [Tooltip("Keep this true for normal puzzle/effect buttons. Turn it off for hub/scene transition buttons.")]
    public bool sendsToLayerButtonManager = true;

    [Header("Optional game flow behavior")]
    [Tooltip("Turn this on for hub buttons, area buttons, and puzzle-complete buttons.")]
    public bool sendsToGameFlowButton = false;

    [Header("Sound")]
    [Tooltip("If true, this button plays the normal button down/up sounds. CompletePuzzle shards ignore this and use the shard sound instead.")]
    public bool playButtonSounds = true;

    [Header("Optional extra UnityEvents")]
    [Tooltip("Optional extra actions fired when the button becomes pressed.")]
    public UnityEvent onPressedActions;

    [Tooltip("Optional extra actions fired when the button becomes released.")]
    public UnityEvent onReleasedActions;

    private HashSet<Collider> overlapping = new HashSet<Collider>();

    private bool isPressed = false;
    private bool usedOnce = false;

    private Animator animator;
    private int ownLayer;

    private GameFlowButton gameFlowButton;

    private void Start()
    {
        animator = GetComponent<Animator>();
        ownLayer = gameObject.layer;

        gameFlowButton = GetComponent<GameFlowButton>();

        Collider c = GetComponent<Collider>();

        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning($"[ButtonPressed] Collider on '{gameObject.name}' is not marked as Trigger.");
        }

        if (sendsToLayerButtonManager && LayerButtonManager.Instance == null)
        {
            Debug.LogWarning("[ButtonPressed] No LayerButtonManager present.");
        }

        if (sendsToGameFlowButton && gameFlowButton == null)
        {
            Debug.LogWarning($"[ButtonPressed] '{gameObject.name}' is set to send to GameFlowButton, but no GameFlowButton is attached.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (usedOnce) return;

        if (IsAllowedActor(other))
        {
            overlapping.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (overlapping.Remove(other))
        {
            CheckPressState();
        }
    }

    private void Update()
    {
        if (usedOnce) return;

        overlapping.RemoveWhere(c => c == null);
        CheckPressState();
    }

    private void CheckPressState()
    {
        if (usedOnce) return;

        bool shouldBePressed = false;

        foreach (Collider col in overlapping)
        {
            if (col == null) continue;

            Transform actorTransform = GetActorTransform(col);

            if (actorTransform != null && IsCentered(actorTransform))
            {
                shouldBePressed = true;
                break;
            }
        }

        if (shouldBePressed == isPressed)
        {
            return;
        }

        if (shouldBePressed)
        {
            isPressed = true;
            BecomePressed();

            if (isOnceButton)
            {
                usedOnce = true;

                if (!holdPressedWhenUsedOnce)
                {
                    StartCoroutine(ReleaseNextFrame());
                }
            }
        }
        else
        {
            isPressed = false;
            BecomeReleased();
        }
    }

    private IEnumerator ReleaseNextFrame()
    {
        yield return null;

        isPressed = false;
        BecomeReleased();
    }

    private bool IsCentered(Transform actor)
    {
        Vector3 buttonPos = transform.position;
        Vector3 actorPos = actor.position;

        float dx = Mathf.Abs(actorPos.x - buttonPos.x);
        float dz = Mathf.Abs(actorPos.z - buttonPos.z);

        return dx <= centerThreshold && dz <= centerThreshold;
    }

    private bool IsAllowedActor(Collider col)
    {
        if (col == null) return false;

        Transform root = col.transform.root;

        if (root != null)
        {
            if (root.CompareTag(playerTag) || root.CompareTag(cloneTag))
            {
                return true;
            }
        }

        return col.CompareTag(playerTag) || col.CompareTag(cloneTag);
    }

    private Transform GetActorTransform(Collider col)
    {
        if (col == null) return null;

        if (col.transform.root != null)
        {
            return col.transform.root;
        }

        return col.transform;
    }

    private bool IsShardButton()
    {
        return sendsToGameFlowButton &&
            gameFlowButton != null &&
            gameFlowButton.action == GameFlowButtonAction.CompletePuzzle;
    }

    private void BecomePressed()
    {
        if (sendsToGameFlowButton && gameFlowButton != null && !gameFlowButton.CanRun())
        {
            return;
        }

        // Button sound
        if (SFXManager.Instance != null)
        {
            if (IsShardButton())
            {
                SFXManager.Instance.PlayShardCollected();
            }
            else if (playButtonSounds)
            {
                SFXManager.Instance.PlayButtonDown();
            }
        }

        if (animator != null && !string.IsNullOrEmpty(buttonAnimatorBool))
        {
            animator.SetBool(buttonAnimatorBool, true);
        }

        if (sendsToLayerButtonManager && LayerButtonManager.Instance != null)
        {
            LayerButtonManager.Instance.ButtonActivated(ownLayer);
            Debug.Log("button activated");
        }

        if (sendsToGameFlowButton && gameFlowButton != null)
        {
            gameFlowButton.Run();
        }

        onPressedActions?.Invoke();
    }

    private void BecomeReleased()
    {
        // Button release sound
        if (!IsShardButton() &&
            playButtonSounds &&
            SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayButtonUp();
        }

        if (animator != null && !string.IsNullOrEmpty(buttonAnimatorBool))
        {
            animator.SetBool(buttonAnimatorBool, false);
        }

        if (sendsToLayerButtonManager && LayerButtonManager.Instance != null)
        {
            LayerButtonManager.Instance.ButtonDeactivated(ownLayer);
        }

        onReleasedActions?.Invoke();
    }
}