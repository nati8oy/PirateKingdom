using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every number a character has: stats, growth per level, drive rates and status resistances.
/// <see cref="Character"/> holds none of these — it references one of these assets instead.
/// </summary>
/// <remarks>
/// <para><b>Required.</b> A <c>Character</c> without one resolves to zero everything and gets clamped
/// to 1 HP, and says so loudly via <c>ValidateClassSetup</c>. There are no per-character overrides:
/// two characters of the same class at the same level are numerically identical and differ only in
/// name, art, audio and action slots. A named character that needs different numbers gets its own
/// one-off asset rather than a delta.</para>
///
/// <para><b>Enemies use these too.</b> <c>Enemy</c> subclasses <c>Character</c>, so an Enemy asset
/// inherits the class reference, the level field and every resolver — one "Skeleton Grunt" class plus
/// level 1 / 3 / 5 Enemy assets is a scaling family with the numbers authored once. Set
/// <see cref="classId"/> to <c>None</c> on an enemy class; that field is for crew recruitment and
/// means nothing here. The enemy's <b>AI</b> config (action weights, targeting strategy, drive
/// config, plunder) lives on <c>Enemy</c> itself and is <i>not</i> covered here, so behaviour stays
/// per-asset even when stats are shared.</para>
///
/// <para>Class covers <b>numbers only</b>. Class-gated <i>abilities</i> are deliberately a separate,
/// still-open decision — see "Defining hero classes" in <c>TODO.md</c> — because letting class drive
/// identity, stats and loadout at once means no single thing is the real constraint.</para>
///
/// <para>Not registered in <c>ContentDatabase</c> and not referenced by save data: <c>RunState</c>
/// stores a <c>characterId</c>, and the Character asset carries the class reference. So this needs no
/// stable id and no <c>saveVersion</c> bump.</para>
/// </remarks>
[CreateAssetMenu(fileName = "ClassDefinition", menuName = "Scriptable Objects/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Which crew archetype this defines, for recruitment and generation later. Combat reads " +
             "the numbers below, never this.\n\n" +
             "Set it to None for an ENEMY class — enemies are never recruited and nothing reads their " +
             "Character Class, so making a Skeleton claim to be a Duelist is misleading noise.\n\n" +
             "For a crew class it must match the Character Class on every Character using it, or " +
             "that character reports an error: it would play as one archetype and be recruited as " +
             "another.")]
    public Character.CharacterClass classId;

    [Tooltip("Shown in logs and, later, on recruitment screens. Falls back to the asset name.")]
    [SerializeField] private string displayName;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    // ---------------------------------------------------------------------------------------
    // LEVEL 1 BASELINE — what a member of this class has before any per-character difference
    // ---------------------------------------------------------------------------------------

    [Header("Level 1 Baseline")]
    [Tooltip("Full health at level 1.")]
    public float maxHealth = 100f;

    [Tooltip("ACCURACY at level 1, not damage. Added to a d20 roll against the target's Defense.")]
    public float attackPower = 10f;

    [Tooltip("The number attackers must reach to hit, at level 1. Only matters relative to attack " +
             "power — see the tuning band in META.md Phase B.")]
    public float defenseValue = 13f;

    [Tooltip("Initiative base at level 1. Turn order is this plus a random 1-9, re-rolled each round.")]
    public float speed = 10f;

    // ---------------------------------------------------------------------------------------
    // GROWTH — added once per level beyond the first
    // ---------------------------------------------------------------------------------------

    [Header("Growth Per Level")]
    [Tooltip("Added per level beyond 1. A level 3 character gets this twice.\n\n" +
             "Only these four scale with level. The drive and resistance values below are flat — " +
             "they're identity dials rather than power curves, and compounding them with level makes " +
             "a high-level character disproportionately better at everything at once.")]
    public float healthPerLevel = 8f;

    public float attackPerLevel = 1f;
    public float defensePerLevel = 1f;
    public float speedPerLevel = 0.5f;

    // ---------------------------------------------------------------------------------------
    // FLAT CLASS TRAITS — not level-scaled
    // ---------------------------------------------------------------------------------------

    [Header("Drive")]
    [Tooltip("Damage multiplier from the FIRST drive stack. Below about 1.5 the bonus rounds away to " +
             "nothing at current damage ranges; at 2.0 the first stack already hits the hard cap and " +
             "further stacks do nothing. 1.4-1.6 is the band that leaves the curve room.")]
    public float buffNextActionMultiplier = 1.5f;

    [Tooltip("Drive gained per point of damage DEALT.")]
    public float damageInflictedDriveMultiplier = 4f;

    [Tooltip("Drive gained per point of damage TAKEN. The tank identity dial.")]
    public float damageTakenDriveMultiplier = 4f;

    [Tooltip("Drive gained on a successful parry = damage prevented x this. Note a parry already pays " +
             "out twice — this, plus Damage Taken on the 25% chip that still lands.")]
    public float parryBonusDriveMultiplier = 5f;

    [Header("Status Resistance")]
    [Tooltip("Resistance to each status kind for every character of this class — the 'all Duelists " +
             "shrug off poison equally' dial. Subtracted from an effect's Chance To Apply, so 0.4 " +
             "against a 100% bleed attack means it lands 60% of the time.\n\n" +
             "Reduces the CHANCE only, never the damage or duration. Kinds with no entry resist at 0.\n\n" +
             "This is the only source of resistance — characters have no list of their own. Armour and " +
             "carried items become a further layer on top later, added inside " +
             "CharacterManager.GetResistance().")]
    [SerializeField] private List<StatusResistance> statusResistances = new List<StatusResistance>();

    /// <summary>Baseline resistance to one kind, 0 when unlisted.</summary>
    public float GetStatusResistance(StatusEffectKind kind)
    {
        if (statusResistances == null) return 0f;

        foreach (StatusResistance entry in statusResistances)
        {
            if (entry != null && entry.kind == kind) return entry.value;
        }

        return 0f;
    }

    // ---------------------------------------------------------------------------------------

    /// <summary>Health for a given level.</summary>
    public float MaxHealthAtLevel(int level) => maxHealth + LevelsAbove(level) * healthPerLevel;

    public float AttackPowerAtLevel(int level) => attackPower + LevelsAbove(level) * attackPerLevel;
    public float DefenseValueAtLevel(int level) => defenseValue + LevelsAbove(level) * defensePerLevel;
    public float SpeedAtLevel(int level) => speed + LevelsAbove(level) * speedPerLevel;

    /// <summary>Levels gained beyond the first. Level 1 grows nothing; anything below 1 is treated as 1.</summary>
    private static int LevelsAbove(int level) => Mathf.Max(0, Mathf.Max(1, level) - 1);
}
