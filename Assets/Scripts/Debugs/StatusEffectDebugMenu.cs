#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only menu items for exercising the status effect system without authoring an action and
/// getting an attack to land first.
/// </summary>
/// <remarks>
/// <para><b>Why these exist as menu items rather than only as <c>[ContextMenu]</c> entries.</b>
/// A <c>[ContextMenu]</c> on a MonoBehaviour hides on that <i>component's</i> ⋮ button in the
/// Inspector — not the GameObject's, and never on a Project-window right-click. Combatants are also
/// spawned at runtime by <c>EncounterBootstrapper</c>, so outside Play mode there is no character in
/// the Hierarchy to select at all. That combination makes the context menu genuinely hard to find,
/// which is the same reason <c>RunManager</c>'s tools live under Tools > Pirate Kingdom.</para>
///
/// <para>All of these bypass the resistance roll deliberately. They're for checking that the tick,
/// the floating text, the icon and the feedback all fire — not for testing whether an effect lands.
/// Use a real authored rider for that.</para>
/// </remarks>
public static class StatusEffectDebugMenu
{
    private const string MenuRoot = "Tools/Pirate Kingdom/Debug/";

    private const float TestDamagePerTurn = 3f;
    private const int TestTurns = 3;

    // --- Selected character -------------------------------------------------------------------

    [MenuItem(MenuRoot + "Bleed Selected Character", false, 100)]
    private static void BleedSelected() => ApplyToSelection(StatusEffectKind.Bleed);

    [MenuItem(MenuRoot + "Poison Selected Character", false, 101)]
    private static void PoisonSelected() => ApplyToSelection(StatusEffectKind.Poison);

    [MenuItem(MenuRoot + "Burn Selected Character", false, 102)]
    private static void BurnSelected() => ApplyToSelection(StatusEffectKind.Burn);

    // --- Whole sides --------------------------------------------------------------------------

    [MenuItem(MenuRoot + "Bleed All Enemies", false, 120)]
    private static void BleedAllEnemies() => ApplyToAllegiance(Character.Allegiance.Enemy, StatusEffectKind.Bleed);

    [MenuItem(MenuRoot + "Bleed All Crew", false, 121)]
    private static void BleedAllCrew() => ApplyToAllegiance(Character.Allegiance.Player, StatusEffectKind.Bleed);

    [MenuItem(MenuRoot + "Clear All Status Effects", false, 140)]
    private static void ClearEverything()
    {
        if (!RequirePlayMode()) return;

        int cleared = 0;

        foreach (CharacterManager character in Object.FindObjectsOfType<CharacterManager>())
        {
            if (character.ActiveStatuses.Count == 0) continue;
            character.EditorClearStatuses();
            cleared++;
        }

        Debug.Log($"[Debug] Cleared status effects from {cleared} combatant(s).");
    }

    // --- Plumbing -----------------------------------------------------------------------------

    private static void ApplyToSelection(StatusEffectKind kind)
    {
        if (!RequirePlayMode()) return;

        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("[Debug] Nothing selected. Pick a combatant in the Hierarchy first — while playing, " +
                             "they sit under the Combatants spawn points.");
            return;
        }

        // GetComponentInParent so selecting a child (the sprite, the CharacterUI canvas) still works.
        CharacterManager character = selected.GetComponentInParent<CharacterManager>();

        if (character == null)
        {
            Debug.LogWarning($"[Debug] '{selected.name}' isn't a combatant — no CharacterManager on it or above it.");
            return;
        }

        character.EditorApplyStatus(kind, TestDamagePerTurn, TestTurns);
    }

    private static void ApplyToAllegiance(Character.Allegiance allegiance, StatusEffectKind kind)
    {
        if (!RequirePlayMode()) return;

        int afflicted = 0;

        foreach (CharacterManager character in Object.FindObjectsOfType<CharacterManager>())
        {
            if (character.characterData == null || character.characterData.allegiance != allegiance) continue;

            character.EditorApplyStatus(kind, TestDamagePerTurn, TestTurns);
            afflicted++;
        }

        if (afflicted == 0)
        {
            Debug.LogWarning($"[Debug] No living {allegiance} combatants found to afflict.");
        }
    }

    /// <summary>
    /// Combatants only exist while playing, so every one of these is a no-op in edit mode. Says so
    /// rather than failing silently.
    /// </summary>
    private static bool RequirePlayMode()
    {
        if (Application.isPlaying) return true;

        Debug.LogWarning("[Debug] Status effect tools only work in Play mode — combatants are spawned at runtime, " +
                         "so there is nothing in the scene to afflict until you press Play.");
        return false;
    }
}
#endif
