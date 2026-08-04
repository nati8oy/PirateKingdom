using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lets settings UI talk to <see cref="GameSettings"/>. The settings store is static so it can be
/// read from anywhere without scene wiring, but a static class can't be the target of a UnityEvent
/// — this component is the bridge a Toggle's <c>onValueChanged</c> can actually point at.
///
/// Put one on the settings panel. It's stateless, so it doesn't matter which scene it lives in.
/// </summary>
public class SettingsBridge : MonoBehaviour
{
    [System.Serializable]
    public class BoolEvent : UnityEvent<bool> { }

    [Header("Current Values")]
    [Tooltip("Fires on enable and whenever the setting changes, with the current value. " +
             "Wire this to a Toggle's SetIsOnWithoutNotify so the UI reflects the stored value without re-triggering itself.")]
    public BoolEvent onShowTargetingLineChanged;

    public bool ShowTargetingLine => GameSettings.ShowTargetingLine;

    private void OnEnable()
    {
        GameSettings.Changed += Broadcast;
        Broadcast();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Broadcast;
    }

    /// <summary>Wire to a Toggle's onValueChanged (dynamic bool).</summary>
    public void SetShowTargetingLine(bool value)
    {
        GameSettings.ShowTargetingLine = value;
    }

    /// <summary>Wire to a "Restore Defaults" button.</summary>
    public void ResetToDefaults()
    {
        GameSettings.ResetToDefaults();
    }

    private void Broadcast()
    {
        onShowTargetingLineChanged?.Invoke(GameSettings.ShowTargetingLine);
    }
}
