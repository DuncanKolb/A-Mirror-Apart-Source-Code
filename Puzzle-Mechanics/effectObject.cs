using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class EffectObject : MonoBehaviour
{
    public enum EffectActivationType
    {
        Pressure,
        SwitchCycle
    }

    [System.Serializable]
    public class EffectWaypoint
    {
        [Header("Waypoint Local Transform")]
        [Tooltip("Only the fields matching your enabled tween types below (Position/Rotation/Scale) are actually used.")]
        public Vector3 localPosition;

        public Vector3 localEulerAngles;

        public Vector3 localScale = Vector3.one;

        [Header("Timing")]
        [Range(0.01f, 1f)]
        [Tooltip("What percent of this state's total tween duration it takes to reach this waypoint. Multiple waypoints each take their own slice; whatever time is left over goes to the final leg into the actual state.")]
        public float durationPercent = 0.3f;

        [Tooltip("Ease used for the leg leading into this waypoint.")]
        public Ease ease = Ease.OutCubic;
    }

    [System.Serializable]
    public class EffectState
    {
        public string name = "State";

        [Header("Target Local Transform")]
        public Vector3 localPosition;

        public Vector3 localEulerAngles;

        public Vector3 localScale = Vector3.one;

        [Header("Optional In-Between Waypoints")]
        [Tooltip("Optional points the object passes through on its way to this state, in order. Leave empty for a direct tween (default behavior).")]
        public List<EffectWaypoint> waypoints = new List<EffectWaypoint>();
    }

    private struct TweenLeg
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
        public float duration;
        public Ease ease;
    }

    // =========================================================
    // TWEEN SETTINGS
    // =========================================================

    [Header("Tween Settings")]
    public float tweenDuration = 0.4f;

    public float tweenStartDelay = 0f;

    public Ease tweenEase = Ease.OutCubic;

    // =========================================================
    // OPTIONAL SOUND
    // =========================================================

    [Header("Optional Effect Sound")]
    [Tooltip("Optional sound that plays while this object is moving. Leave empty for no sound.")]
    public AudioClip effectSound;

    [Range(0f, 1f)]
    [Tooltip("Volume of this object's movement sound.")]
    public float effectSoundVolume = 0.5f;

    [Tooltip("Small fade when the movement finishes so the sound does not cut off harshly.")]
    public float effectSoundFadeOut = 0.05f;

    // =========================================================
    // EFFECT SETTINGS
    // =========================================================

    [Header("Effect Type")]
    public EffectActivationType activationType = EffectActivationType.Pressure;

    public bool IsPressureEffect
    {
        get { return activationType == EffectActivationType.Pressure; }
    }

    public bool IsSwitchEffect
    {
        get { return activationType == EffectActivationType.SwitchCycle; }
    }

    [Header("States after the starting state")]
    [Tooltip("State 0 is captured automatically from the object's starting transform. This list starts at State 1.")]
    public List<EffectState> extraStates = new List<EffectState>();

    [Header("Waypoints for returning to Starting State (State 0)")]
    [Tooltip("Optional points the object passes through on its way back to State 0, in order. Leave empty for a direct tween back to start (default behavior).")]
    public List<EffectWaypoint> startingStateWaypoints =
        new List<EffectWaypoint>();

    [Header("Pressure Settings")]
    [Tooltip("Usually 1. When pressure is on, the object moves to this state. When pressure is off, it returns to State 0.")]
    public int pressureOnStateIndex = 1;

    [Header("Switch Settings")]
    public bool loopSwitchStates = true;

    [Header("What to Tween")]
    public bool tweenPosition = true;

    public bool tweenRotation = false;

    public bool tweenScale = false;

    // =========================================================
    // INTERNAL
    // =========================================================

    private EffectState startingState;

    private int currentStateIndex = 0;

    private Tween currentTween;

    private AudioSource effectAudioSource;

    private Tween soundFadeTween;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        startingState = new EffectState
        {
            name = "Starting State",
            localPosition = transform.localPosition,
            localEulerAngles = transform.localEulerAngles,
            localScale = transform.localScale,
            waypoints = startingStateWaypoints
        };
    }

    private void Start()
    {
        if (LayerButtonManager.Instance == null)
        {
            Debug.LogWarning(
                "[EffectObject] No LayerButtonManager found in scene. Please add one."
            );

            return;
        }

        LayerButtonManager.Instance.RegisterEffect(this);
    }

    private void OnDestroy()
    {
        soundFadeTween?.Kill();

        if (effectAudioSource != null)
        {
            effectAudioSource.Stop();
        }

        currentTween?.Kill();

        if (LayerButtonManager.Instance != null)
        {
            LayerButtonManager.Instance.UnregisterEffect(this);
        }
    }

    // =========================================================
    // ACTIVATION
    // =========================================================

    public void SetPressureState(bool on)
    {
        Debug.Log("SetPressureState");

        if (activationType != EffectActivationType.Pressure)
        {
            return;
        }

        int targetState = on ? pressureOnStateIndex : 0;

        Debug.Log("move to next state. pressure ");

        MoveToState(targetState);
    }

    public void OnButtonPressedPulse()
    {
        if (activationType != EffectActivationType.SwitchCycle)
        {
            Debug.Log("return 1");
            return;
        }

        if (GetStateCount() < 2)
        {
            Debug.LogWarning(
                $"[EffectObject] '{gameObject.name}' is set to SwitchCycle but has no extra states."
            );

            return;
        }

        int nextState = currentStateIndex + 1;

        if (nextState >= GetStateCount())
        {
            if (loopSwitchStates)
            {
                nextState = 0;
            }
            else
            {
                nextState = GetStateCount() - 1;
            }
        }

        Debug.Log("move to next state");

        MoveToState(nextState);
    }

    // =========================================================
    // MOVE TO STATE
    // =========================================================

    private void MoveToState(int stateIndex)
    {
        if (stateIndex < 0 || stateIndex >= GetStateCount())
        {
            Debug.LogWarning(
                $"[EffectObject] '{gameObject.name}' tried to move to invalid state index {stateIndex}."
            );

            return;
        }

        EffectState targetState = GetState(stateIndex);

        // Stop current movement and sound
        currentTween?.Kill();

        StopEffectSoundImmediate();

        Sequence sequence = DOTween.Sequence();

        // Start delay
        if (tweenStartDelay > 0f)
        {
            sequence.AppendInterval(tweenStartDelay);
        }

        List<TweenLeg> legs = BuildLegs(targetState);

        bool hasAddedAnyTweenOverall = false;

        // Start movement sound
        sequence.AppendCallback(StartEffectSound);

        foreach (TweenLeg leg in legs)
        {
            bool hasAddedFirstTweenThisLeg = false;

            if (tweenPosition)
            {
                Tweener moveTween = transform
                    .DOLocalMove(
                        leg.localPosition,
                        leg.duration
                    )
                    .SetEase(leg.ease);

                if (!hasAddedFirstTweenThisLeg)
                {
                    sequence.Append(moveTween);
                    hasAddedFirstTweenThisLeg = true;
                }
                else
                {
                    sequence.Join(moveTween);
                }
            }

            if (tweenRotation)
            {
                Tweener rotateTween = transform
                    .DOLocalRotate(
                        leg.localEulerAngles,
                        leg.duration
                    )
                    .SetEase(leg.ease);

                if (!hasAddedFirstTweenThisLeg)
                {
                    sequence.Append(rotateTween);
                    hasAddedFirstTweenThisLeg = true;
                }
                else
                {
                    sequence.Join(rotateTween);
                }
            }

            if (tweenScale)
            {
                Tweener scaleTween = transform
                    .DOScale(
                        leg.localScale,
                        leg.duration
                    )
                    .SetEase(leg.ease);

                if (!hasAddedFirstTweenThisLeg)
                {
                    sequence.Append(scaleTween);
                    hasAddedFirstTweenThisLeg = true;
                }
                else
                {
                    sequence.Join(scaleTween);
                }
            }

            if (hasAddedFirstTweenThisLeg)
            {
                hasAddedAnyTweenOverall = true;
            }
        }

        if (!hasAddedAnyTweenOverall)
        {
            Debug.LogWarning(
                $"[EffectObject] '{gameObject.name}' has no tween type enabled."
            );

            return;
        }

        // Stop sound when movement finishes
        sequence.OnComplete(StopEffectSound);

        currentTween = sequence;

        currentTween.Play();

        currentStateIndex = stateIndex;
    }

    // =========================================================
    // EFFECT AUDIO
    // =========================================================

    private void EnsureEffectAudioSource()
    {
        if (effectAudioSource != null)
        {
            return;
        }

        effectAudioSource =
            gameObject.AddComponent<AudioSource>();

        effectAudioSource.playOnAwake = false;

        effectAudioSource.loop = false;

        // 2D effect sound
        effectAudioSource.spatialBlend = 0f;
    }

    private void StartEffectSound()
    {
        if (effectSound == null)
        {
            return;
        }

        EnsureEffectAudioSource();

        soundFadeTween?.Kill();

        effectAudioSource.Stop();

        effectAudioSource.clip = effectSound;

        float masterVolume = 1f;

        if (SFXManager.Instance != null)
        {
            masterVolume =
                SFXManager.Instance.GetMasterSFXVolume();
        }

        effectAudioSource.volume =
            effectSoundVolume * masterVolume;

        effectAudioSource.time = 0f;

        effectAudioSource.Play();
    }

    private void StopEffectSound()
    {
        if (effectAudioSource == null ||
            !effectAudioSource.isPlaying)
        {
            return;
        }

        soundFadeTween?.Kill();

        if (effectSoundFadeOut <= 0f)
        {
            StopEffectSoundImmediate();
            return;
        }

        // Short fade prevents sound cutoff
        soundFadeTween = effectAudioSource
            .DOFade(
                0f,
                effectSoundFadeOut
            )
            .OnComplete(() =>
            {
                effectAudioSource.Stop();

                float masterVolume = 1f;

                if (SFXManager.Instance != null)
                {
                    masterVolume =
                        SFXManager.Instance.GetMasterSFXVolume();
                }

                effectAudioSource.volume =
                    effectSoundVolume * masterVolume;
            });
    }

    private void StopEffectSoundImmediate()
    {
        soundFadeTween?.Kill();

        if (effectAudioSource == null)
        {
            return;
        }

        effectAudioSource.Stop();

        float masterVolume = 1f;

        if (SFXManager.Instance != null)
        {
            masterVolume =
                SFXManager.Instance.GetMasterSFXVolume();
        }

        effectAudioSource.volume =
            effectSoundVolume * masterVolume;
    }

    // =========================================================
    // WAYPOINTS / TIMING
    // =========================================================

    private List<TweenLeg> BuildLegs(
        EffectState targetState)
    {
        List<TweenLeg> legs =
            new List<TweenLeg>();

        float remainingPercent = 1f;

        if (targetState.waypoints != null)
        {
            foreach (
                EffectWaypoint waypoint
                in targetState.waypoints)
            {
                float pct = Mathf.Clamp(
                    waypoint.durationPercent,
                    0.01f,
                    1f
                );

                remainingPercent -= pct;

                legs.Add(
                    new TweenLeg
                    {
                        localPosition =
                            waypoint.localPosition,

                        localEulerAngles =
                            waypoint.localEulerAngles,

                        localScale =
                            waypoint.localScale,

                        duration =
                            tweenDuration * pct,

                        ease =
                            waypoint.ease
                    }
                );
            }
        }

        // Final state
        float finalDuration = Mathf.Max(
            tweenDuration * remainingPercent,
            tweenDuration * 0.05f
        );

        legs.Add(
            new TweenLeg
            {
                localPosition =
                    targetState.localPosition,

                localEulerAngles =
                    targetState.localEulerAngles,

                localScale =
                    targetState.localScale,

                duration =
                    finalDuration,

                ease =
                    tweenEase
            }
        );

        return legs;
    }

    // =========================================================
    // STATE HELPERS
    // =========================================================

    private int GetStateCount()
    {
        return 1 + extraStates.Count;
    }

    private EffectState GetState(int index)
    {
        if (index == 0)
        {
            return startingState;
        }

        return extraStates[index - 1];
    }
}