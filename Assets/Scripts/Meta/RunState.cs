using System.Collections.Generic;
using UnityEngine;

// NOTE: deliberately no `using System;` in this file — it would make `System.Action` collide with
// the game's own `Action` ScriptableObject. System types are fully qualified instead.

/// <summary>
/// One crew member's state for the current run: who they are, how beaten up they are, and what
/// they've learned. This is the permadeath unit — when <see cref="isDead"/> flips, they're gone
/// for the rest of the run.
/// </summary>
[System.Serializable]
public class CrewMemberState
{
    [Tooltip("Character.Id — resolved through ContentDatabase. Never store an object reference here; this gets JSON-serialized.")]
    public string characterId;

    [Tooltip("Shown in UI. Separate from the Character asset so generated crew can carry their own rolled name.")]
    public string displayName;

    public float currentHealth;
    public int level = 1;
    public int xp;

    [Tooltip("Action.Ids currently slotted, max 6.")]
    public List<string> actionLoadout = new List<string>();

    public bool isDead;

    /// <summary>Builds a fresh, undamaged crew member from an authored Character asset.</summary>
    public static CrewMemberState CreateFrom(Character character)
    {
        if (character == null) return null;

        if (string.IsNullOrEmpty(character.Id))
        {
            Debug.LogError($"[CrewMemberState] Character '{character.name}' has no Id, so it can't be saved. " +
                           "Run Tools > Pirate Kingdom > Rebuild Content Database.");
        }

        if (character.allegiance != Character.Allegiance.Player)
        {
            Debug.LogWarning($"[CrewMemberState] '{character.name}' is authored with allegiance " +
                             $"{character.allegiance}, but is being added as crew. Only Player-allegiance " +
                             "characters bind to run state, so this one will never carry health between " +
                             "encounters. Change its allegiance if it's meant to be crew.");
        }

        var state = new CrewMemberState
        {
            characterId = character.Id,
            displayName = character.characterName,
            currentHealth = character.maxHealth,
            level = 1,
            xp = 0,
            isDead = false
        };

        if (character.actionSlots != null)
        {
            foreach (Action action in character.actionSlots)
            {
                if (action != null && !string.IsNullOrEmpty(action.Id))
                {
                    state.actionLoadout.Add(action.Id);
                }
            }
        }

        return state;
    }
}

/// <summary>
/// Everything that persists across encounters within a single voyage. Plain serializable C# —
/// no ScriptableObjects, no MonoBehaviours — so it can be written straight to JSON.
/// </summary>
/// <remarks>
/// The voyage map, current node, and loot pool land here in Phase 3. Adding fields later is
/// cheap (absent fields just take their defaults on load); renaming or removing them is not.
/// </remarks>
[System.Serializable]
public class RunState
{
    [Tooltip("Bumped when the save format changes incompatibly, so old saves can be detected.")]
    public int saveVersion = CurrentSaveVersion;

    public const int CurrentSaveVersion = 1;

    public int plunder;

    [Tooltip("Encounters won on this run. Additive field — old saves load it as 0, so no version bump.")]
    public int encountersSurvived;

    [Tooltip("Position in the RunManager's EncounterSequence. Persists because the encounter loop " +
             "is a scene reload — nothing in the scene survives to remember it. Phase 3 replaces " +
             "this with currentNodeId.")]
    public int sequenceIndex;

    public List<CrewMemberState> crew = new List<CrewMemberState>();

    /// <summary>Crew who can still fight — excludes the permanently dead.</summary>
    public IEnumerable<CrewMemberState> LivingCrew
    {
        get
        {
            foreach (CrewMemberState member in crew)
            {
                if (member != null && !member.isDead) yield return member;
            }
        }
    }

    public int LivingCrewCount
    {
        get
        {
            int count = 0;
            foreach (CrewMemberState member in crew)
            {
                if (member != null && !member.isDead) count++;
            }
            return count;
        }
    }

    public CrewMemberState FindCrewMember(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;

        foreach (CrewMemberState member in crew)
        {
            if (member != null && member.characterId == characterId) return member;
        }

        return null;
    }
}
