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
        actionSlots = new Action[ACTION_SLOTS];
        // Initialize first slot with Move action
        actionSlots[0] = ScriptableObject.CreateInstance<Action>();
        actionSlots[0].actionName = "Move";
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
