using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

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

    [Tooltip("Authored starting level. Run state tracks level per crew member, but it does not yet " +
             "feed stat resolution — that arrives with progression.")]
    public int level = 1;

    [Tooltip("Unused — authored but never read. Kept as a hook for a future meta currency or " +
             "loyalty stat; delete it if that never happens.")]
    [SerializeField] public float reputation = 0f;
    
    [Header("Audio")]
    [Tooltip("Sound effect played when this character dies")]
    public AudioClip deathSoundEffect;

    public enum CharacterClass
    {
        Duelist,
        Muscle,
        Musketeer,
        WitchDoctor
    }
    public enum Allegiance
    {
        Player,
        Enemy
    }

    [Tooltip("Archetype label. Not read by combat yet — recruitment and generation will use it.")]
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


    [Header("Stats")]
    [Tooltip("Full health. Crew carry damage between encounters, so this is the ceiling they heal " +
             "back to, not what they start every fight on.")]
    [SerializeField] public float maxHealth = 100f;

    [Tooltip("ACCURACY, not damage. Added to a d20 roll and compared against the target's Defense " +
             "Value — it never affects how hard a hit lands.\n\n" +
             "Minimum roll to hit = target Defense - this + 1 (floored at 2, since a natural 1 " +
             "always misses). So only the GAP between the two matters.")]
    [SerializeField] public float attackPower = 10f;

    [Tooltip("The number attackers must reach to hit this character.\n\n" +
             "Set it BELOW an attacker's Attack Power and the roll clamps — they hit 95% of the " +
             "time and this stat does nothing. It only starts mattering at roughly Attack Power + 5 " +
             "or higher.")]
    [SerializeField] public float defenseValue = 5f;

    [Tooltip("Turn order. Initiative = this + a random 1-9, re-rolled every round, so a gap smaller " +
             "than about 8 will not reliably win the first turn.")]
    [SerializeField] public float speed = 5f;

    [Tooltip("Damage multiplier from the FIRST drive stack. Later stacks add diminishing amounts of " +
             "the same bonus, hard-capped at 2x.\n\n" +
             "Below about 1.5 the bonus rounds away to nothing on small damage ranges, and spending " +
             "a drive segment appears to do nothing.")]
    [FormerlySerializedAs("buffAttackMultiplier")] [SerializeField] public float buffNextActionMultiplier = 1.5f;

    [Header("Drive Generation")]
    [Tooltip("Drive gained per point of damage this character DEALS.")]
    [SerializeField] public float damageInflictedDriveMultiplier = 4f;

    [Tooltip("Drive gained per point of damage this character TAKES. A tank identity dial.\n\n" +
             "Note a parry pays out twice — the parry bonus below, plus this on the 25% that still " +
             "lands.")]
    [SerializeField] public float damageTakenDriveMultiplier = 4f;
    [Tooltip("Drive gained on a successful parry = damage prevented x this value. Target is ~25 drive (a quarter segment) for a typical 5-damage hit.")]
    [SerializeField] public float parryBonusDriveMultiplier = 5f;
    
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
        Speed
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