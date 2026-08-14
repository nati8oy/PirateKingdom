using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Character", menuName = "Scriptable Objects/Character")]
public class Character : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id used by save data. Stamped automatically by ContentDatabase's 'Rebuild From Project'. " +
             "Once runs have been saved against it, DO NOT change it — saves reference characters by this string.")]
    [SerializeField] private string id;

    /// <summary>Stable save-data id. Empty until ContentDatabase stamps it.</summary>
    public string Id => id;

#if UNITY_EDITOR
    /// <summary>Editor-only. Set by ContentDatabase when stamping ids; never call at runtime.</summary>
    public void EditorAssignId(string newId) => id = newId;
#endif

    [Header("Basic Info")]
    [Tooltip("Display name. Crew spawned from a run use their run record's name instead where they " +
             "have one, so generated crew keep their rolled name rather than the archetype's.")]
    public string characterName;

    private const int ACTION_SLOTS = 6;

    [Tooltip("The actions this character can use, in button order. Up to 6.\n\n" +
             "Note: per-crew loadouts in run state are saved but NOT yet applied — this array is " +
             "still what the action bar reads.")]
    public Action[] actionSlots = new Action[ACTION_SLOTS];

    [Tooltip("Unused. Intended as a pool to draw actionSlots from; nothing reads it yet.")]
    [SerializeField] private List<Action> availableActions = new List<Action>();

    [Tooltip("Level used to resolve stats when this character has NO run record — i.e. enemies, and " +
             "crew played outside a run. Crew in a run use their CrewMemberState.level instead.\n\n" +
             "Growth per level comes from the Class Definition, so raising this on an enemy asset is " +
             "how you make a tougher version of the same class.")]
    public int level = 1;

    [Tooltip("Unused — authored but never read. Kept as a hook for a future meta currency or " +
             "loyalty stat; delete it if that never happens.")]
    [SerializeField] public float reputation = 0f;
    
    [Header("Audio")]
    [Tooltip("Sound effect played when this character dies")]
    public AudioClip deathSoundEffect;

    /// <remarks>
    /// Serialized by integer value, so members may be RENAMED freely but must not be reordered —
    /// <c>WitchDoctor</c> became <c>Healer</c> without touching a single asset, because it stayed at
    /// position 3. Append new classes at the end.
    /// </remarks>
    public enum CharacterClass
    {
        Duelist,
        Muscle,
        Musketeer,
        Healer
    }
    public enum Allegiance
    {
        Player,
        Enemy
    }

    [Tooltip("Archetype label. Combat resolves stats from the Class Definition asset below, not from " +
             "this — but the two should agree, and a mismatch is reported by 'Log Resolved Stats'. " +
             "Recruitment and generation will use it.")]
    [SerializeField] public CharacterClass characterClass;

    [Tooltip("Which side this fights on. Load-bearing: only Player-allegiance characters bind to " +
             "run state and carry health between encounters, and only they can use action buttons " +
             "on their turn. An enemy-allegiance character used as crew silently never persists.")]
    [SerializeField] public Allegiance allegiance;

    [Header("Art")]
    [Tooltip("Body sprite, applied to the combatant's SpriteRenderer on spawn. Shared by crew and " +
             "enemies since Enemy subclasses Character. Leave empty to keep whatever the prefab has.")]
    [SerializeField] public Sprite characterSprite;

    [Tooltip("Multiplied over the sprite. White is untinted. Lets several characters share one " +
             "sprite and still read as different — a red Skeleton Elite against a grey Skeleton.")]
    [SerializeField] public Color characterTint = Color.white;


    [Header("Class")]
    [Tooltip("REQUIRED. Supplies every stat this character has — health, accuracy, defense, speed, " +
             "drive rates and status resistances — scaled by level.\n\n" +
             "There are no per-character stat overrides: two characters of the same class at the same " +
             "level have identical numbers and differ only in name, art, audio and action slots. If a " +
             "named character needs different numbers, give it its own one-off Class Definition.\n\n" +
             "Use 'Log Resolved Stats' on this asset's ⋮ menu to see what the numbers come out as.")]
    [SerializeField] private ClassDefinition classDefinition;


    /// <summary>The class supplying this character's stats. Required — see <see cref="ValidateClassSetup"/>.</summary>
    public ClassDefinition Class => classDefinition;

    /// <summary>False means this character has no stats at all — an authoring error.</summary>
    public bool UsesClass => classDefinition != null;

    /// <summary>
    /// Resistance to one effect kind, from this character's class. 0 when there's no class or the
    /// class doesn't list that kind.
    /// </summary>
    /// <remarks>
    /// Read through <see cref="CharacterManager.GetResistance"/>, never directly — that's the single
    /// accessor the equipment layer will slot into later, and where the value is clamped.
    /// </remarks>
    public float GetStatusResistance(StatusEffectKind kind)
    {
        return classDefinition != null ? classDefinition.GetStatusResistance(kind) : 0f;
    }

    // ---------------------------------------------------------------------------------------
    // RESOLVED STATS — supplied entirely by the class, scaled by level.
    //
    // Every consumer goes through these. There is deliberately no per-character override: two
    // characters of the same class at the same level have identical numbers, and differ only in
    // name, art, audio and action slots. If a named character ever needs to differ numerically,
    // give it its own one-off ClassDefinition rather than reintroducing per-character deltas.
    // ---------------------------------------------------------------------------------------

    public float ResolveMaxHealth(int level) =>
        classDefinition != null ? classDefinition.MaxHealthAtLevel(level) : 0f;

    public float ResolveAttackPower(int level) =>
        classDefinition != null ? classDefinition.AttackPowerAtLevel(level) : 0f;

    public float ResolveDefenseValue(int level) =>
        classDefinition != null ? classDefinition.DefenseValueAtLevel(level) : 0f;

    public float ResolveSpeed(int level) =>
        classDefinition != null ? classDefinition.SpeedAtLevel(level) : 0f;

    // Flat class traits — deliberately NOT level-scaled. These are identity dials rather than power
    // curves, and compounding them with level would make a high-level character disproportionately
    // better at everything at once.
    public float ResolveBuffNextActionMultiplier() =>
        classDefinition != null ? classDefinition.buffNextActionMultiplier : 1f;

    public float ResolveDamageInflictedDriveMultiplier() =>
        classDefinition != null ? classDefinition.damageInflictedDriveMultiplier : 0f;

    public float ResolveDamageTakenDriveMultiplier() =>
        classDefinition != null ? classDefinition.damageTakenDriveMultiplier : 0f;

    public float ResolveParryBonusDriveMultiplier() =>
        classDefinition != null ? classDefinition.parryBonusDriveMultiplier : 0f;


    /// <summary>
    /// Whether this asset is in a coherent state. Currently only catches the class asset and the
    /// authored <see cref="characterClass"/> enum disagreeing.
    /// </summary>
    /// <remarks>
    /// The two are redundant by design for now — combat resolves stats from the asset, while the enum
    /// is what recruitment and generation will read. Letting them drift means the character plays as
    /// one class and gets recruited as another.
    ///
    /// <b>Only checked for crew.</b> Enemies inherit the whole class system (Enemy subclasses
    /// Character), but they're never recruited and nothing reads their <see cref="characterClass"/>,
    /// so forcing an enemy class asset to also claim one of the crew archetypes would be friction for
    /// no benefit. An enemy can use a ClassDefinition and simply ignore the enum.
    /// </remarks>
    public bool ValidateClassSetup(out string problem)
    {
        problem = null;

        if (classDefinition == null)
        {
            problem = $"'{name}' has no Class Definition assigned. Every stat now comes from the class, so " +
                      "this character resolves to zero health, accuracy, defense, speed, drive and resistances " +
                      "— it will be clamped to 1 HP and be useless in a fight. Assign one.";
            return false;
        }

        if (allegiance != Allegiance.Player) return true;

        if (classDefinition.classId != characterClass)
        {
            problem = $"'{name}' is authored as {characterClass} but references the " +
                      $"{classDefinition.classId} class asset. Combat reads the asset, so the enum is the " +
                      "one that's wrong — but they should agree.";
            return false;
        }

        return true;
    }

#if UNITY_EDITOR


    /// <summary>
    /// Editor-only. Prints what this character actually resolves to. The counter to deltas being
    /// harder to read than absolutes — "+3" doesn't tell you Black Sam hits at 15.
    /// </summary>
    [ContextMenu("Log Resolved Stats")]
    private void LogResolvedStats()
    {
        int atLevel = Mathf.Max(1, level);

        string source = classDefinition != null
            ? $"{classDefinition.DisplayName} at level {atLevel} + this character's deltas"
            : "its own flat values (no class assigned)";

        Debug.Log($"[Character] '{name}' resolves from {source}:\n" +
                  $"  Health  {ResolveMaxHealth(atLevel):0.##}\n" +
                  $"  Attack  {ResolveAttackPower(atLevel):0.##}\n" +
                  $"  Defense {ResolveDefenseValue(atLevel):0.##}\n" +
                  $"  Speed   {ResolveSpeed(atLevel):0.##}\n" +
                  $"  Drive   next-action x{ResolveBuffNextActionMultiplier():0.##}, " +
                  $"dealt {ResolveDamageInflictedDriveMultiplier():0.##}, " +
                  $"taken {ResolveDamageTakenDriveMultiplier():0.##}, " +
                  $"parry {ResolveParryBonusDriveMultiplier():0.##}", this);

        if (!ValidateClassSetup(out string problem)) Debug.LogWarning($"[Character] {problem}", this);
    }
#endif

    /// <remarks>
    /// Unity serializes enums by their integer value, so these may be renamed but **must not be
    /// reordered** — moving a member silently repoints every authored Action asset at a different
    /// buff. Append new types at the end.
    /// </remarks>
    public enum BuffType
    {
        /// <summary>
        /// Modifies the to-hit roll only, never damage. Damage bonuses are drive's job — keeping
        /// the two separate is deliberate, so a character can't stack an accuracy buff and a drive
        /// spend into one compounded damage number.
        /// </summary>
        Accuracy,
        Defense,
        Health,
        Speed,

        /// <summary>
        /// Percentage mitigation applied to incoming damage — the "protection" buff. Authored as a
        /// FRACTION (0.25 = take 25% less), unlike every type above, which are flat stat points.
        /// </summary>
        /// <remarks>
        /// The odd one out, deliberately: the others are additive modifiers to a stat and are folded
        /// into <c>CharacterManager.RefreshStats()</c>. This one isn't a stat at all — there is no
        /// "damage taken" field to modify — so it's read in <c>CharacterManager.TakeDamage()</c>
        /// instead, at the moment damage is applied. Don't try to route it through RefreshStats.
        ///
        /// Negative values are a vulnerability debuff (take MORE damage), which is what a Debuff
        /// action authored against this type produces for free.
        /// </remarks>
        DamageReduction,

        /// <summary>
        /// Widens the critical hit window. Authored as FLAT POINTS on the d20, where each point is
        /// one more face that crits — so +2 crits on 18-20, i.e. 15% instead of the base 5%.
        /// </summary>
        /// <remarks>
        /// Points rather than a percentage because crits are decided by a d20 face, and a
        /// percentage can only ever land on a multiple of 5 anyway — asking for "12%" would silently
        /// become 10% or 15%. Read via <c>CharacterManager.CritThreshold</c>.
        ///
        /// Like the others, it is the ATTACKER's own stat: it never reads anything off the target,
        /// so a negative value is self-inflicted clumsiness rather than crit resistance.
        /// </remarks>
        CritChance
    }

    public class ActiveBuff
    {
        public BuffType Type { get; private set; }
        public float Value { get; private set; }
        public int TurnsRemaining { get; private set; }

        public ActiveBuff(BuffType type, float value, int turnsDuration)
        {
            Type = type;
            Value = value;
            TurnsRemaining = turnsDuration;
        }

        public void ReduceTurns()
        {
            TurnsRemaining--;
        }

        public bool IsExpired()
        {
            return TurnsRemaining <= 0;
        }
    }

    // NOTE: this ScriptableObject is *authored data only*. Per-battle mutable state — active
    // buffs, action cooldowns, current health — lives on the runtime CharacterManager, because
    // several scene objects can point at the same asset (both Skeletons share Skeleton.asset) and
    // because in the Editor writes to a ScriptableObject persist into the project asset on disk.
    // Do not reintroduce mutable fields here.
    //
    // `Initialize()` used to live here and zeroed `reputation` — the last thing in the codebase
    // writing to a Character asset at runtime. Its only caller was CrewManager, deleted in Phase 2c,
    // so it went with it rather than sitting around as a ready-made way to reintroduce the problem.
    // `reputation` itself is still authored and still never read; see META.md §7.

    public void ResetActionsToDefault()
    {
        actionSlots = new Action[ACTION_SLOTS];
        actionSlots[0] = ScriptableObject.CreateInstance<Action>();
        actionSlots[0].actionName = "Move";
    }

    private int GetValidActionCount()
    {
        int count = 0;
        if (actionSlots != null)
        {
            foreach (Action action in actionSlots)
            {
                if (action != null && !string.IsNullOrEmpty(action.actionName))
                    count++;
            }
        }
        return count;
    }

    public float Reputation
    {
        get => reputation;
    }

    public void AddAction(Action action)
    {
        if (availableActions.Count < ACTION_SLOTS)
        {
            availableActions.Add(action);
        }
    }

    public Action[] GetActions()
    {
        return actionSlots;
    }

    public void ClearActionSlots()
    {
        for (int i = 0; i < ACTION_SLOTS; i++)
        {
            actionSlots[i] = null;
        }
    }
}