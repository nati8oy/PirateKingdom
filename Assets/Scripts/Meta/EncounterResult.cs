using System.Collections.Generic;

// NOTE: deliberately no `using System;` in this file — it would make `System.Action` collide with
// the game's own `Action` ScriptableObject. System types are fully qualified instead.

/// <summary>
/// What one encounter did to the run: who came out of it and on what health, who died, and what it
/// paid. Produced once, at the battle's end, by <see cref="RunManager.CompleteEncounter"/>.
/// </summary>
/// <remarks>
/// <para>This is a <b>report</b>, not saved state — <see cref="RunState"/> is the thing that
/// persists. It exists so the encounter has a single, inspectable boundary: everything the fight
/// changed about the run is applied and written to disk in one place, and the same object can drive
/// a results screen.</para>
///
/// <para>No XP or level-up yet. <see cref="CrewMemberState.xp"/> is still only ever read, and
/// awarding it is Phase 5's job along with folding level into stat resolution.</para>
/// </remarks>
[System.Serializable]
public class EncounterResult
{
    /// <summary>One crew member's outcome. Covers survivors and this encounter's casualties.</summary>
    [System.Serializable]
    public class CrewOutcome
    {
        public string characterId;
        public string displayName;

        /// <summary>Health they finished on. Zero for casualties.</summary>
        public float endHealth;

        /// <summary>True only for crew who died in <i>this</i> encounter, not earlier ones.</summary>
        public bool died;
    }

    public string encounterName;
    public bool playerVictory;

    /// <summary>Plunder this encounter paid out. Zero on defeat, or with no encounter definition.</summary>
    public int plunderAwarded;

    /// <summary>The run's plunder total after <see cref="plunderAwarded"/> was added.</summary>
    public int plunderTotal;

    /// <summary>Encounters won on this run, including this one. Unchanged by a defeat.</summary>
    public int encountersSurvived;

    /// <summary>Survivors plus anyone who died here. Crew lost in earlier fights are excluded.</summary>
    public List<CrewOutcome> crew = new List<CrewOutcome>();

    public int DeathCount
    {
        get
        {
            int count = 0;
            foreach (CrewOutcome outcome in crew)
            {
                if (outcome != null && outcome.died) count++;
            }
            return count;
        }
    }

    public int SurvivorCount
    {
        get
        {
            int count = 0;
            foreach (CrewOutcome outcome in crew)
            {
                if (outcome != null && !outcome.died) count++;
            }
            return count;
        }
    }

    /// <summary>One-line description, for the battle-end log.</summary>
    public string Summary()
    {
        string outcome = playerVictory ? "VICTORY" : "DEFEAT";
        string plunder = plunderAwarded > 0 ? $", +{plunderAwarded} plunder (total {plunderTotal})" : "";
        return $"'{encounterName}' {outcome} — {SurvivorCount} survived, {DeathCount} lost{plunder}";
    }
}
