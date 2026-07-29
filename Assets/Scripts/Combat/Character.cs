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
    public string characterName;
    private const int ACTION_SLOTS = 6;
    public Action[] actionSlots = new Action[ACTION_SLOTS];
    [SerializeField] private List<Action> availableActions = new List<Action>();
    public int level = 1;
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

    [SerializeField] public CharacterClass characterClass;
    [SerializeField] public Allegiance allegiance;
    
    [Header("Stats")]
    [SerializeField] public float maxHealth = 100f;
    [SerializeField] public float attackPower = 10f;
    [SerializeField] public float defenseValue = 5f;
    [SerializeField] public float speed = 5f;
    [FormerlySerializedAs("buffAttackMultiplier")] [SerializeField] public float buffNextActionMultiplier = 1.5f;
    
    [Header("Drvive Generation")]
    [SerializeField] public float damageInflictedDriveMultiplier = 4f; 
    [SerializeField] public float damageTakenDriveMultiplier = 4f;
    [Tooltip("Drive gained on a successful parry = damage prevented x this value. Target is ~25 drive (a quarter segment) for a typical 5-damage hit.")]
    [SerializeField] public float parryBonusDriveMultiplier = 5f;
    
    public enum BuffType
    {
        Attack,
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

    public void Initialize()
    {
        reputation = 0f;
    }

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