using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Character", menuName = "Scriptable Objects/Character")]
public class Character : ScriptableObject
{
    [Header("Basic Info")]
    public string characterName;
    private const int ACTION_SLOTS = 6;
    public Action[] actionSlots = new Action[ACTION_SLOTS];
    [SerializeField] private List<Action> availableActions = new List<Action>();
    public int level = 1;
    [SerializeField] public float reputation = 0f;
    public enum CharacterClass
    {
        Duelist,
        Trader,
        Doctor,
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

    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    
    // Dictionary to track action cooldowns - maps action to turns remaining until usable
    private Dictionary<Action, int> actionCooldowns = new Dictionary<Action, int>();
    
    public void Initialize()
    {
        reputation = 0f;
        activeBuffs.Clear();
        actionCooldowns.Clear();
        Debug.Log($"Character {characterName} initialized with {GetValidActionCount()} actions");
    }

    public void ResetActionsToDefault()
    {
        actionSlots = new Action[ACTION_SLOTS];
        actionSlots[0] = ScriptableObject.CreateInstance<Action>();
        actionSlots[0].actionName = "Move";
        actionCooldowns.Clear();
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

    public void AddBuff(BuffType buffType, float value, int turnsDuration)
    {
        ActiveBuff newBuff = new ActiveBuff(buffType, value, turnsDuration);
        activeBuffs.Add(newBuff);
        Debug.Log($"Added {(value > 0 ? "buff" : "debuff")} to {characterName}: {buffType} {value:+0;-0} for {turnsDuration} turns");
    }

    public void UpdateBuffsForCharacterTurn()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].ReduceTurns();
            if (activeBuffs[i].IsExpired())
            {
                Debug.Log($"Buff/Debuff expired on {characterName}: {activeBuffs[i].Type} after completing turn");
                activeBuffs.RemoveAt(i);
            }
        }
    }

    // Update action cooldowns when the character completes their turn
    public void UpdateActionCooldowns()
    {
        List<Action> actionsToRemove = new List<Action>();
        
        foreach (var kvp in actionCooldowns)
        {
            Action action = kvp.Key;
            int turnsRemaining = kvp.Value - 1;
            
            if (turnsRemaining <= 0)
            {
                actionsToRemove.Add(action);
                Debug.Log($"{characterName}: {action.actionName} cooldown expired and is now available");
            }
            else
            {
                actionCooldowns[action] = turnsRemaining;
            }
        }
        
        foreach (Action action in actionsToRemove)
        {
            actionCooldowns.Remove(action);
        }
    }

    // Use an action and put it on cooldown
    public void UseAction(Action action)
    {
        if (action.cooldown > 0)
        {
            int cooldownTurns = Mathf.RoundToInt(action.cooldown);
            actionCooldowns[action] = cooldownTurns;
            Debug.Log($"{characterName} used {action.actionName}, cooldown: {cooldownTurns} turns");
        }
    }

    // Check if an action is available (not on cooldown)
    public bool IsActionAvailable(Action action)
    {
        if (action == null) return false;
        
        // If the action has no cooldown, it's always available
        if (action.cooldown <= 0) return true;
        
        // Check if the action is currently on cooldown
        return !actionCooldowns.ContainsKey(action);
    }

    // Get the remaining cooldown turns for an action
    public int GetActionCooldownRemaining(Action action)
    {
        if (action == null || !actionCooldowns.ContainsKey(action))
            return 0;
        
        return actionCooldowns[action];
    }

    // Deprecated method - kept for backwards compatibility
    public void UpdateBuffsForNewRound()
    {
        // This method is now deprecated since we use turn-based buffs
        // The method is kept to avoid breaking existing code, but does nothing
        Debug.LogWarning($"UpdateBuffsForNewRound() is deprecated for {characterName}. Use UpdateBuffsForCharacterTurn() instead.");
    }

    // Get modified stats with buffs applied
    public float GetModifiedAttackPower()
    {
        float totalAttack = attackPower;
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == BuffType.Attack)
            {
                totalAttack += buff.Value;
            }
        }
        return Mathf.Max(0, totalAttack); // Ensure it doesn't go negative
    }

    public float GetModifiedDefenseValue()
    {
        float totalDefense = defenseValue;
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == BuffType.Defense)
            {
                totalDefense += buff.Value;
            }
        }
        return Mathf.Max(0, totalDefense);
    }

    public float GetModifiedSpeed()
    {
        float totalSpeed = speed;
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == BuffType.Speed)
            {
                totalSpeed += buff.Value;
            }
        }
        return Mathf.Max(0.1f, totalSpeed); // Minimum speed of 0.1 to prevent issues
    }

    public float GetModifiedMaxHealth()
    {
        float totalHealth = maxHealth;
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == BuffType.Health)
            {
                totalHealth += buff.Value;
            }
        }
        return Mathf.Max(1, totalHealth); // Minimum health of 1
    }

    public List<ActiveBuff> GetActiveBuffs()
    {
        return new List<ActiveBuff>(activeBuffs);
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

    // Obsolete methods for backwards compatibility
    public float GetAttackPower()
    {
        return GetModifiedAttackPower();
    }

    public void UpdateBuffs(float deltaTime)
    {
        // This method is now obsolete since we use turn-based buffs
        // Keep for backwards compatibility but don't use it
    }
}