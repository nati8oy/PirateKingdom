using UnityEngine;

[CreateAssetMenu(fileName = "Action", menuName = "Scriptable Objects/Action")]
public class Action : ScriptableObject
{
    /// <remarks>
    /// Unity serializes enums by their integer value, so these may be renamed but **must not be
    /// reordered** — moving a member silently repoints every authored Action asset at a different
    /// behaviour. Append new types at the end, as DriveGrant/DriveDrain were. Same rule as
    /// <see cref="Character.BuffType"/>.
    /// </remarks>
    public enum ActionType
    {
        Attack,
        Heal,
        Buff,
        Debuff,

        /// <summary>Adds <see cref="driveAmount"/> raw drive to the target's meter.</summary>
        DriveGrant,

        /// <summary>Strips <see cref="driveAmount"/> raw drive from the target's meter.</summary>
        DriveDrain,

        /// <summary>
        /// Attacks like <see cref="Attack"/> — same to-hit roll, same crit, same drive multiplier —
        /// and heals the attacker for <see cref="healthDrainRatio"/> of the health the target
        /// actually lost. A miss heals nothing.
        /// </summary>
        HealthDrain
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
             "Debuff — the same, negated\n" +
             "Drive Grant — adds Drive Amount to the target's drive meter\n" +
             "Drive Drain — removes Drive Amount from the target's drive meter\n" +
             "Health Drain — rolls Min/Max Damage, then heals the attacker for Health Drain Ratio " +
             "of what the target actually lost")]
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

    [Header("Health Drain")]
    [Tooltip("Share of the health the target ACTUALLY LOST that the attacker heals back. Used only " +
             "by Health Drain actions. 0.5 = half. Damage itself comes from Min/Max Damage above.\n\n" +
             "Measured against health lost rather than damage rolled, so overkill isn't rewarded: " +
             "a 20-damage hit on a target with 3hp left drains 3, not 20.\n\n" +
             "Watch the rounding — healing is rounded like damage, so a low ratio on a small damage " +
             "range heals 0 and the action reads as broken. Sanity-check against Min Damage: at " +
             "Min Damage 3 a ratio below ~0.17 rounds away to nothing.")]
    [Range(0f, 1f)]
    public float healthDrainRatio = 0.5f;

    [Header("Drive Manipulation")]
    [Tooltip("Drive moved by a Drive Grant or Drive Drain action. Author it POSITIVE either way — " +
             "Drive Drain removes it, exactly as Debuff negates Buff Value.\n\n" +
             "This is RAW DRIVE, not segments. The meter lives on the character PREFAB, not in " +
             "code: PlayerCharacter.prefab is currently 400 drive over 4 segments, so 100 = one " +
             "full segment. Check the prefab's Drive Meter before tuning, and note that a grant " +
             "which doesn't cross a segment boundary buys the target nothing they can spend.")]
    [Min(0f)]
    public float driveAmount = 100f;

    //[Header("Requirements")]
    //public int minimumLevel;
    //public Character.CharacterClass[] allowedClasses;
    
    /// <summary>
    /// Get the attack windup time for this action
    /// </summary>
    public float AttackWindupTime => attackWindupTime;
}