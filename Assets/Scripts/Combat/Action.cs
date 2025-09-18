using UnityEngine;

[CreateAssetMenu(fileName = "Action", menuName = "Scriptable Objects/Action")]
public class Action : ScriptableObject
{
    public enum ActionType
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }

    public enum TargetType
    {
        SingleEnemy,
        SingleAlly,
        AllAllies,
        AllEnemies
    }
    
    

    [Header("Action Configuration")]
    public string actionName;
    public ActionType actionType;
    public TargetType targetType;
    public float cooldown;
    public Sprite actionIcon;

    [Header("Attack Timing")]
    [SerializeField] private float attackWindupTime = 1.0f; // Default 1 second
    
    [Header("Spell Effects")]
    public float minDamage;
    public float maxDamage;
    public float minHeal;
    public float maxHeal;

    public float buffValue;
    public float duration;
    public Character.BuffType buffType;

    //[Header("Requirements")]
    //public int minimumLevel;
    //public Character.CharacterClass[] allowedClasses;
    
    /// <summary>
    /// Get the attack windup time for this action
    /// </summary>
    public float AttackWindupTime => attackWindupTime;
}