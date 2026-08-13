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

    // Damage 0 and a single turn: a stun steals one turn rather than dealing anything. Applying it
    // repeatedly is the useful test — the resistance ramp should make it stop landing.
    [MenuItem(MenuRoot + "Stun Selected Character", false, 103)]
    private static void StunSelected() => ApplyToSelection(StatusEffectKind.Stun, 0f, 1);

    // --- Whole sides --------------------------------------------------------------------------

    [MenuItem(MenuRoot + "Bleed All Enemies", false, 120)]
    private static void BleedAllEnemies() => ApplyToAllegiance(Character.Allegiance.Enemy, StatusEffectKind.Bleed);

    [MenuItem(MenuRoot + "Bleed All Crew", false, 121)]
    private static void BleedAllCrew() => ApplyToAllegiance(Character.Allegiance.Player, StatusEffectKind.Bleed);

    [MenuItem(MenuRoot + "Stun All Enemies", false, 122)]
    private static void StunAllEnemies() => ApplyToAllegiance(Character.Allegiance.Enemy, StatusEffectKind.Stun, 0f, 1);

    [MenuItem(MenuRoot + "Stun All Crew", false, 123)]
    private static void StunAllCrew() => ApplyToAllegiance(Character.Allegiance.Player, StatusEffectKind.Stun, 0f, 1);

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

    private static void ApplyToSelection(StatusEffectKind kind, float damagePerTurn = TestDamagePerTurn, int turns = TestTurns)
    {
        if (!RequirePlayMode()) return;

        CharacterManager character = ResolveTarget();

        if (character == null) return;

        character.EditorApplyStatus(kind, damagePerTurn, turns);
    }

    /// <summary>
    /// The combatant to afflict: whatever is selected, or failing that whoever currently holds the
    /// turn.
    /// </summary>
    /// <remarks>
    /// The fallback exists because requiring a Hierarchy selection is a genuine trap — combatants are
    /// spawned at runtime and sit nested under the Combatants spawn points, so it's easy to invoke
    /// these with nothing selected and conclude the whole system is broken when the tool simply never
    /// reached a character. Selecting in the <i>Game</i> view doesn't set Selection either.
    /// </remarks>
    private static CharacterManager ResolveTarget()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected != null)
        {
            // GetComponentInParent so selecting a child (the sprite, the CharacterUI canvas) works.
            CharacterManager fromSelection = selected.GetComponentInParent<CharacterManager>();

            if (fromSelection != null) return fromSelection;

            Debug.LogWarning($"[Debug] '{selected.name}' isn't a combatant — no CharacterManager on it or above it. " +
                             "Falling back to whoever's turn it is.");
        }

        TurnManager turnManager = Object.FindObjectOfType<TurnManager>();
        CharacterManager acting = turnManager != null ? turnManager.currentCharacterTurn : null;

        if (acting != null)
        {
            Debug.Log($"[Debug] Nothing suitable selected — using the character whose turn it is " +
                      $"({acting.characterData?.characterName ?? acting.name}).");
            return acting;
        }

        Debug.LogError("[Debug] No combatant selected and no character currently holds the turn — nothing to afflict. " +
                       "Select one in the Hierarchy, or use the 'All Enemies' / 'All Crew' items instead.");
        return null;
    }

    private static void ApplyToAllegiance(Character.Allegiance allegiance, StatusEffectKind kind,
                                          float damagePerTurn = TestDamagePerTurn, int turns = TestTurns)
    {
        if (!RequirePlayMode()) return;

        int afflicted = 0;

        foreach (CharacterManager character in Object.FindObjectsOfType<CharacterManager>())
        {
            if (character.characterData == null || character.characterData.allegiance != allegiance) continue;
            if (!character.IsAlive) continue;

            character.EditorApplyStatus(kind, damagePerTurn, turns);
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
