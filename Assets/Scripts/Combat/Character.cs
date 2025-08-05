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
        Health
    }

    public class ActiveBuff
    {
        public BuffType Type { get; private set; }
        public float Value { get; private set; }
        public float Duration { get; private set; }

        public ActiveBuff(BuffType type, float value, float duration)
        {
            Type = type;
            Value = value;
            Duration = duration;
        }

        public void UpdateDuration(float deltaTime)
        {
            Duration -= deltaTime;
        }
    }

    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    
    public void Initialize()
    {
        reputation = 0f;
        // Don't touch actionSlots at all - let them be set up in Inspector or elsewhere
        Debug.Log($"Character {characterName} initialized with {GetValidActionCount()} actions");
    }

// Call this method only when you want to reset actions to defaults
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
   
    public float GetAttackPower()
    {
        float totalAttack = attackPower;
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == BuffType.Attack)
            {
                totalAttack += buff.Value;
            }
        }
        return totalAttack;
        
    }


    public void UpdateBuffs(float deltaTime)
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].UpdateDuration(deltaTime);
            if (activeBuffs[i].Duration <= 0)
            {
                activeBuffs.RemoveAt(i);
            }
        }
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
