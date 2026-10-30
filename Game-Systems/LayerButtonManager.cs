using System.Collections.Generic;
using UnityEngine;

public class LayerButtonManager : MonoBehaviour
{
    public static LayerButtonManager Instance { get; private set; }

    // Active button count per layer
    private Dictionary<int, int> activeButtonCount = new Dictionary<int, int>();

    // Effect objects registered per layer
    private Dictionary<int, List<EffectObject>> effectsByLayer = new Dictionary<int, List<EffectObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterEffect(EffectObject effect)
    {
        if (effect == null) return;

        int layer = effect.gameObject.layer;

        if (!effectsByLayer.ContainsKey(layer))
            effectsByLayer[layer] = new List<EffectObject>();

        if (!effectsByLayer[layer].Contains(effect))
            effectsByLayer[layer].Add(effect);
    }

    public void UnregisterEffect(EffectObject effect)
    {
        if (effect == null) return;

        int layer = effect.gameObject.layer;

        if (!effectsByLayer.ContainsKey(layer)) return;

        effectsByLayer[layer].Remove(effect);
    }

    public void ButtonActivated(int layer)
    {
        if (!activeButtonCount.ContainsKey(layer))
            activeButtonCount[layer] = 0;

        bool layerWasInactive = activeButtonCount[layer] == 0;

        activeButtonCount[layer]++;

        // Trigger when layer changes from inactive to active
        if (layerWasInactive)
        {
            SendButtonPressedPulse(layer);
            SetPressureEffectsState(layer, true);
        }
    }

    public void ButtonDeactivated(int layer)
    {
        if (!activeButtonCount.ContainsKey(layer)) return;

        activeButtonCount[layer] = Mathf.Max(0, activeButtonCount[layer] - 1);

        // Release after all buttons on the layer are released
        if (activeButtonCount[layer] == 0)
        {
            SetPressureEffectsState(layer, false);
        }
    }

    private void SendButtonPressedPulse(int layer)
    {
        if (!effectsByLayer.ContainsKey(layer)) return;

        foreach (EffectObject effect in effectsByLayer[layer])
        {
            if (effect == null) continue;

            if (effect.IsSwitchEffect)
                effect.OnButtonPressedPulse();
        }
    }

    private void SetPressureEffectsState(int layer, bool state)
    {
        if (!effectsByLayer.ContainsKey(layer)) return;

        foreach (EffectObject effect in effectsByLayer[layer])
        {
            if (effect == null) continue;

            if (effect.IsPressureEffect)
                effect.SetPressureState(state);
        }
    }
}