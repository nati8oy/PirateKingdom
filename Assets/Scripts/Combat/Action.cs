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

    [Header("Spell Effects")]
    public float minDamage;
    public float maxDamage;
    public float minHeal;
    public float maxHeal;

    public float baseValue;
    public float duration;
    public Character.BuffType buffType;

    //[Header("Requirements")]
    //public int minimumLevel;
    //public Character.CharacterClass[] allowedClasses;
    
    
    
}
