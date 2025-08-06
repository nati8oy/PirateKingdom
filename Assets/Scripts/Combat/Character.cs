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
        public int RoundsRemaining { get; private set; }

        public ActiveBuff(BuffType type, float value, int roundsDuration)
        {
            Type = type;
            Value = value;
            RoundsRemaining = roundsDuration;
        }

        public void ReduceRounds()
        {
            RoundsRemaining--;
        }

        public bool IsExpired()
        {
            return RoundsRemaining <= 0;
        }
    }

    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    
    public void Initialize()
    {
        reputation = 0f;
        activeBuffs.Clear();
        Debug.Log($"Character {characterName} initialized with {GetValidActionCount()} actions");
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

    public void AddBuff(BuffType buffType, float value, int roundsDuration)
    {
        ActiveBuff newBuff = new ActiveBuff(buffType, value, roundsDuration);
        activeBuffs.Add(newBuff);
        Debug.Log($"Added {(value > 0 ? "buff" : "debuff")} to {characterName}: {buffType} {value:+0;-0} for {roundsDuration} rounds");
    }

    public void UpdateBuffsForNewRound()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].ReduceRounds();
            if (activeBuffs[i].IsExpired())
            {
                Debug.Log($"Buff/Debuff expired on {characterName}: {activeBuffs[i].Type}");
                activeBuffs.RemoveAt(i);
            }
        }
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
        // This method is now obsolete since we use round-based buffs
        // Keep for backwards compatibility but don't use it
    }
}