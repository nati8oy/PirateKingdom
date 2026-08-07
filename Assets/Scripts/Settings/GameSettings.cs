using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Player-facing settings, persisted in <c>PlayerPrefs</c>.
///
/// **Deliberately not part of <c>RunState</c>.** A run is per-voyage and is discarded on death;
/// settings are per-player and must outlive every run, so they get their own store. PlayerPrefs is
/// chosen over a JSON file beside <c>run.json</c> because settings are a flat handful of scalars
/// with no versioning problem — the day that stops being true, this class is the only thing that
/// has to change.
///
/// Add new settings as properties here rather than reading PlayerPrefs directly at the call site,
/// so keys, defaults and the change notification all live in one place.
/// See the "Settings &amp; accessibility" section of <c>TODO.md</c> for what's queued.
/// </summary>
public static class GameSettings
{
    // Keys are namespaced so they can't collide with anything else written to PlayerPrefs.
    private const string ShowTargetingLineKey = "settings.combat.showTargetingLine";

    /// <summary>
    /// Raised whenever any setting changes, so live objects can re-read without polling.
    /// Fully qualified: a bare <c>Action</c> is the game's own ability ScriptableObject.
    /// </summary>
    public static event System.Action Changed;

    /// <summary>
    /// Draw the line from the acting character to the target being hovered. On by default —
    /// it's a legibility aid for reading who is attacking whom, and players who don't want it
    /// can turn it off. The cursor target feedback is independent and always on.
    /// </summary>
    public static bool ShowTargetingLine
    {
        get => GetBool(ShowTargetingLineKey, true);
        set => SetBool(ShowTargetingLineKey, value);
    }

    /// <summary>Restores every setting to its default.</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(ShowTargetingLineKey);
        PlayerPrefs.Save();

        Changed?.Invoke();
    }

    private static bool GetBool(string key, bool defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
    }

    private static void SetBool(string key, bool value)
    {
        if (GetBool(key, value) == value && PlayerPrefs.HasKey(key)) return;

        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();

        Changed?.Invoke();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only. Flips the setting from the top menu, playing or not — no object to select and
    /// nothing to hunt for. `[ContextMenu]` entries on a component's ⋮ button are too easy to miss,
    /// which is the same reason the RunManager tools live up here.
    /// </summary>
    [MenuItem("Tools/Pirate Kingdom/Toggle Targeting Line")]
    private static void ToggleTargetingLineFromMenu()
    {
        ShowTargetingLine = !ShowTargetingLine;
        Debug.Log($"[GameSettings] Show targeting line: {ShowTargetingLine}");
    }

    [MenuItem("Tools/Pirate Kingdom/Reset Settings To Defaults")]
    private static void ResetSettingsFromMenu()
    {
        ResetToDefaults();
        Debug.Log("[GameSettings] Settings reset to defaults.");
    }
#endif
}
