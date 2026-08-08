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
    
    

    [Header("Identity")]
    [Tooltip("Stable id used by save data. Stamped automatically by ContentDatabase's 'Rebuild From Project'. " +
             "Once runs have been saved against it, DO NOT change it — saved loadouts reference actions by this string.")]
    [SerializeField] private string id;

    /// <summary>Stable save-data id. Empty until ContentDatabase stamps it.</summary>
    public string Id => id;

#if UNITY_EDITOR
    /// <summary>Editor-only. Set by ContentDatabase when stamping ids; never call at runtime.</summary>
    public void EditorAssignId(string newId) => id = newId;
#endif

    [Header("Action Configuration")]
    [Tooltip("Shown on the action button and in tooltips. Free text — nothing keys off it.")]
    public string actionName;

    [Tooltip("Decides which fields below are used, and how enemy AI weights this action.\n\n" +
             "Attack — rolls Min/Max Damage\n" +
             "Heal — rolls Min/Max Heal\n" +
             "Buff — applies Buff Value of Buff Type\n" +
             "Debuff — the same, negated")]
    public ActionType actionType;

    [Tooltip("Who this may be used on. Enforced by CombatController.IsValidTarget, which also drives " +
             "the targeting line's valid/invalid cursor.\n\n" +
             "Note: All Allies / All Enemies currently resolve against a single clicked target.")]
    public TargetType targetType;

    [Tooltip("Turns before this can be used again, counted down at the end of the owner's turn. " +
             "0 means always available. Applies to enemies as well as the player.")]
    public float cooldown;

    [Tooltip("Icon on the action button. Greyed out automatically while on cooldown.")]
    public Sprite actionIcon;

    [Header("Attack Timing")]
    [Tooltip("Seconds an ENEMY spends winding up before this attack lands — the player's window to " +
             "react. The parry window opens near the end of it. Unused for player actions, which " +
             "resolve immediately.")]
    [SerializeField] private float attackWindupTime = 1.0f; // Default 1 second

    [Header("Spell Effects")]
    [Tooltip("Damage roll, inclusive of min and exclusive of max. Used only by Attack actions.\n\n" +
             "Keep the range in mind when tuning drive and buffs — damage is rounded, so bonuses " +
             "worth less than ~1 point on a low roll disappear entirely.")]
    public float minDamage;
    public float maxDamage;

    [Tooltip("Healing roll. Used only by Heal actions. Rounded like damage, so the same rounding " +
             "caveat applies.")]
    public float minHeal;
    public float maxHeal;

    [Tooltip("Size of the stat change. Debuff actions negate this, so author it positive either way.")]
    public float buffValue;

    [Tooltip("How many turns the buff lasts. Authored as a float but rounded to whole turns, ticked " +
             "down at the end of the affected character's own turn.")]
    public float duration;

    [Tooltip("Which stat the buff moves.\n\n" +
             "Accuracy — the to-hit roll only, never damage (damage bonuses are drive's job)\n" +
             "Defense — the number attackers must beat\n" +
             "Speed — initiative\n" +
             "Health — raises max health but grants no actual health; avoid until that's fixed")]
    public Character.BuffType buffType;

    //[Header("Requirements")]
    //public int minimumLevel;
    //public Character.CharacterClass[] allowedClasses;
    
    /// <summary>
    /// Get the attack windup time for this action
    /// </summary>
    public float AttackWindupTime => attackWindupTime;
}