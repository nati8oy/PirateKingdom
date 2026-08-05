using System.Collections.Generic;
using UnityEngine;

// NOTE: deliberately no `using System;` — it would make `System.Action` collide with the game's
// own `Action` ScriptableObject.

/// <summary>
/// An ordered run of encounters — a gauntlet. Built for balancing: point a crew at it and see how
/// far they get before attrition kills them.
///
/// **A linear stand-in for the Phase 3 voyage map.** The position lives in
/// <c>RunState.sequenceIndex</c> and the lookup goes through <see cref="RunManager.GetCurrentEncounter"/>,
/// which is the same seam a generated map graph will use — so this is scaffolding Phase 3 reuses,
/// not a detour.
/// </summary>
[CreateAssetMenu(fileName = "EncounterSequence", menuName = "Scriptable Objects/Encounter Sequence")]
public class EncounterSequence : ScriptableObject
{
    /// <summary>What happens once the crew survives the last step.</summary>
    public enum ExhaustedBehaviour
    {
        [Tooltip("No further encounters. Use when you want the gauntlet to have a definite end.")]
        Stop,
        [Tooltip("Start again from step one.")]
        Loop,
        [Tooltip("Keep serving the final encounter. Best for 'how long can they last' — the sequence escalates, then holds at its hardest fight.")]
        RepeatLast
    }

    /// <summary>
    /// One entry. <see cref="repeat"/> is the authoring shortcut — a 15-fight gauntlet is a few
    /// rows rather than fifteen.
    /// </summary>
    [System.Serializable]
    public class Step
    {
        public EncounterDefinition encounter;

        [Tooltip("How many times in a row to run this encounter before moving on.")]
        [Min(1)] public int repeat = 1;
    }

    [Tooltip("Shown in the balancing log so runs against different gauntlets are distinguishable.")]
    [SerializeField] private string displayName = "Gauntlet";

    [SerializeField] private List<Step> steps = new List<Step>();

    [SerializeField] private ExhaustedBehaviour whenExhausted = ExhaustedBehaviour.RepeatLast;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public ExhaustedBehaviour WhenExhausted => whenExhausted;

    /// <summary>
    /// Total encounters with repeats flattened. Steps with no encounter assigned are skipped, so a
    /// half-filled row in the Inspector can't shift every index after it.
    /// </summary>
    public int Length
    {
        get
        {
            int total = 0;

            foreach (Step step in steps)
            {
                if (step == null || step.encounter == null) continue;
                total += Mathf.Max(1, step.repeat);
            }

            return total;
        }
    }

    /// <summary>
    /// The encounter at a flattened position, applying <see cref="whenExhausted"/> once the index
    /// runs past the end. Returns null when the gauntlet is over (Stop) or nothing is authored.
    /// </summary>
    public EncounterDefinition GetAt(int index)
    {
        int length = Length;
        if (length == 0) return null;

        if (index < 0) index = 0;

        if (index >= length)
        {
            switch (whenExhausted)
            {
                case ExhaustedBehaviour.Loop:
                    index %= length;
                    break;
                case ExhaustedBehaviour.RepeatLast:
                    index = length - 1;
                    break;
                default:
                    return null;
            }
        }

        int cursor = 0;

        foreach (Step step in steps)
        {
            if (step == null || step.encounter == null) continue;

            int count = Mathf.Max(1, step.repeat);
            if (index < cursor + count) return step.encounter;

            cursor += count;
        }

        return null;
    }

    /// <summary>True once the gauntlet has a definite end and the index is past it.</summary>
    public bool IsFinished(int index)
    {
        return whenExhausted == ExhaustedBehaviour.Stop && index >= Length;
    }

    /// <summary>"3/12" for the readout — or "3" when the sequence never ends.</summary>
    public string DescribePosition(int index)
    {
        return whenExhausted == ExhaustedBehaviour.Stop
            ? $"{index + 1}/{Length}"
            : $"{index + 1}";
    }
}
