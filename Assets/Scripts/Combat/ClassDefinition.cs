using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The numeric baseline for a character class: what every Duelist starts with, and how those numbers
/// grow per level. Referenced by <see cref="Character"/>, which then only has to author what makes
/// that particular character <i>different</i>.
/// </summary>
/// <remarks>
/// <para><b>Assigning one is optional, and that's what keeps this safe.</b> A <c>Character</c> with no
/// class assigned resolves exactly as it did before this existed — its own flat values, used
/// verbatim. So the roster converts one asset at a time with everything unconverted behaving
/// identically, and enemies can opt out entirely rather than inheriting a crew class's curve.</para>
///
/// <para><b>Enemies can use these too.</b> <c>Enemy</c> subclasses <c>Character</c>, so an Enemy asset
/// inherits the class reference, the level field and every resolver — a "Skeleton Grunt" class plus
/// level 1 / 3 / 5 Enemy assets is a scaling family with the numbers authored once. Note the enemy's
/// <b>AI</b> config (action weights, targeting strategy, drive config, plunder) lives on
/// <c>Enemy</c> itself and is <i>not</i> covered here, so behaviour is still per-asset.</para>
///
/// <para>Class covers <b>numbers only</b>. Names, art, audio, action slots and per-character deltas
/// stay on the Character. Class-gated <i>abilities</i> are deliberately a separate, still-open
/// decision — see "Defining hero classes" in <c>TODO.md</c> — because letting class drive identity,
/// stats and loadout at once means no single thing is the real constraint.</para>
///
/// <para>Not registered in <c>ContentDatabase</c> and not referenced by save data: <c>RunState</c>
/// stores a <c>characterId</c>, and the Character asset carries the class reference. So this needs no
/// stable id and no <c>saveVersion</c> bump.</para>
/// </remarks>
[CreateAssetMenu(fileName = "ClassDefinition", menuName = "Scriptable Objects/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Which crew archetype this defines. Used for recruitment and generation later; combat " +
             "reads the numbers below, not this.\n\n" +
             "Only meaningful for CREW classes — a crew Character whose own Character Class disagrees " +
             "with this is reported as an error. Enemy classes can leave it at anything, since enemies " +
             "are never recruited and nothing reads their Character Class.")]
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
    [Tooltip("Baseline resistance to each status kind for this class — the 'all Duelists shrug off " +
             "poison equally' dial. A character's own list is ADDED on top of this, and the total is " +
             "clamped to 0-1.\n\n" +
             "Kinds with no entry resist at 0.")]
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

    /// <summary>Health for a given level, before any per-character delta.</summary>
    public float MaxHealthAtLevel(int level) => maxHealth + LevelsAbove(level) * healthPerLevel;

    public float AttackPowerAtLevel(int level) => attackPower + LevelsAbove(level) * attackPerLevel;
    public float DefenseValueAtLevel(int level) => defenseValue + LevelsAbove(level) * defensePerLevel;
    public float SpeedAtLevel(int level) => speed + LevelsAbove(level) * speedPerLevel;

    /// <summary>Levels gained beyond the first. Level 1 grows nothing; anything below 1 is treated as 1.</summary>
    private static int LevelsAbove(int level) => Mathf.Max(0, Mathf.Max(1, level) - 1);
}
