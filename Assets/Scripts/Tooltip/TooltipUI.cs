

using UnityEngine;
using TMPro;

public class TooltipUI : MonoBehaviour
{
    [Header("Tooltip Components")]
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private string defaultText = "Hover over actions or targets for information";
    
    // TMP only recognises a SHORT list of colour NAMES — black, blue, green, orange, purple, red,
    // white, yellow. Anything else (cyan, magenta, grey…) silently fails to parse and the tooltip
    // renders the raw "<color=cyan>" as literal text. That's exactly what happened to the Buff label.
    // Hex always parses, so every tag here is hex and no new one should use a name.
    private const string ColourAttack = "#f0862a";
    private const string ColourHeal   = "#5fbf5f";
    private const string ColourBuff   = "#45c8dc";
    private const string ColourDebuff = "#e0473c";
    private const string ColourDrive  = "#e8c341";
    private const string ColourStatus = "#ff8080";

    private static TooltipUI instance;
    public static TooltipUI Instance => instance;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        ShowDefaultText();
    }
    
    public void ShowActionTooltip(Action action)
    {
        if (action == null) 
        {
            ShowDefaultText();
            return;
        }
        
        string tooltipContent = $"<b>{action.actionName}</b>";
        
        // Add action type information
        switch (action.actionType)
        {
            case Action.ActionType.Attack:
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                break;
            case Action.ActionType.Heal:
                tooltipContent += $"\nHealing: {action.minHeal:F0}-{action.maxHeal:F0}";
                break;
            case Action.ActionType.Buff:
                tooltipContent += $"\nBuff: {DescribeBuff(action.buffType, action.buffValue)}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.Debuff:
                tooltipContent += $"\nDebuff: {DescribeBuff(action.buffType, -action.buffValue)}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.DriveGrant:
                tooltipContent += $"\nDrive: +{action.driveAmount:F0}";
                break;
            case Action.ActionType.DriveDrain:
                tooltipContent += $"\nDrive: -{action.driveAmount:F0}";
                break;
            case Action.ActionType.HealthDrain:
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                tooltipContent += $"\nDrains: {action.healthDrainRatio:P0} of health taken";
                break;
        }

        tooltipContent += DescribeStatusRiders(action);
        tooltipContent += DescribeSelfCast(action);

        // Add cooldown information if applicable
        if (action.cooldown > 0)
        {
            tooltipContent += $"\nCooldown: {Mathf.RoundToInt(action.cooldown)} rounds";
        }

        tooltipText.text = tooltipContent;
    }

    /// <summary>
    /// Lists an action's status riders, or returns empty when it has none.
    /// </summary>
    /// <param name="target">
    /// Optional. When supplied, the chance shown is the REAL one against that character — their
    /// resistance subtracted — rather than the authored base. A tooltip that promises 100% bleed
    /// against a bleed-resistant target is lying at exactly the moment it matters.
    /// </param>
    private static string DescribeStatusRiders(Action action, CharacterManager target = null)
    {
        if (action == null || action.statusEffects == null || action.statusEffects.Count == 0)
        {
            return string.Empty;
        }

        // Only these two roll riders; showing them on anything else would be a lie about the data.
        if (action.actionType != Action.ActionType.Attack && action.actionType != Action.ActionType.HealthDrain)
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder();

        foreach (StatusApplication application in action.statusEffects)
        {
            if (application == null) continue;

            float chance = application.chanceToApply;
            if (target != null) chance = Mathf.Clamp01(chance - target.GetResistance(application.kind));

            text.Append($"\n<color={ColourStatus}>{application.kind}</color>");

            if (application.damagePerTurn > 0f)
            {
                text.Append($" {application.damagePerTurn:F0}/turn");
            }

            text.Append($" for {application.turns} turn{(application.turns == 1 ? "" : "s")}");

            // A guaranteed effect doesn't need a percentage cluttering the line.
            if (chance < 1f) text.Append($" ({chance:P0})");

            if (Mathf.Approximately(chance, 0f)) text.Append($" <color={ColourDebuff}>— immune</color>");
        }

        return text.ToString();
    }

    /// <summary>
    /// Flags a self-cast action, or returns empty for a normal one.
    /// </summary>
    /// <remarks>
    /// Worth its own line because it changes what clicking the button *does*: there's no target step
    /// and no chance to reconsider, so the click commits the whole turn. A player who expects to
    /// pick a target next has already spent it.
    /// </remarks>
    private static string DescribeSelfCast(Action action)
    {
        return action != null && action.autoTargetSelf
            ? $"\n<color={ColourDrive}>Self-cast — resolves at once and ends your turn</color>"
            : string.Empty;
    }

    /// <summary>
    /// Formats a buff amount in the units that type is actually authored in — percent for
    /// DamageReduction, signed flat points for everything else.
    /// </summary>
    /// <remarks>
    /// Without this a 25% protection buff renders as "+0", because <c>buffValue</c> holds 0.25 and
    /// every call site formatted it with <c>:F0</c>. Pass the SIGNED value: callers that negate for
    /// a debuff must negate before calling, so a vulnerability reads as "-25%".
    /// </remarks>
    private static string DescribeBuffAmount(Character.BuffType type, float value)
    {
        return type == Character.BuffType.DamageReduction
            ? $"{value:P0}"
            : $"{value:+0;-0}";
    }

    /// <summary>Human-readable buff label, e.g. "Damage Reduction 25%" or "Accuracy +2".</summary>
    private static string DescribeBuff(Character.BuffType type, float signedValue)
    {
        // Two-word types read badly as raw enum names. Everything else is already one word.
        string label = type switch
        {
            Character.BuffType.DamageReduction => "Damage Reduction",
            Character.BuffType.CritChance => "Crit Chance",
            _ => type.ToString()
        };

        // Crit points are only meaningful as the chance they buy — +2 means nothing, +10% does.
        if (type == Character.BuffType.CritChance)
        {
            return $"{label} {signedValue * 5f:+0;-0}%";
        }

        return $"{label} {DescribeBuffAmount(type, signedValue)}";
    }

    /// <summary>
    /// Renders a drive amount as segments where the target's meter makes that meaningful, e.g.
    /// "100 (1 segment)". Falls back to the raw number when there's no meter to measure against.
    /// </summary>
    /// <remarks>
    /// Segment size is authored on the character PREFAB's DriveMeter, not in code, and it's the
    /// number that decides whether a grant is actually spendable — 60 raw drive against a
    /// 100-per-segment meter buys nothing on its own. Showing only the raw value hides that.
    /// </remarks>
    private static string DescribeDrive(float amount, CharacterManager reference)
    {
        DriveManager driveManager = reference != null ? reference.GetDriveManager() : null;

        if (driveManager?.Drive == null || driveManager.Drive.SegmentValue <= 0f)
        {
            return $"{amount:F0}";
        }

        float segments = amount / driveManager.Drive.SegmentValue;

        return $"{amount:F0} ({segments:0.##} segment{(Mathf.Approximately(segments, 1f) ? "" : "s")})";
    }
    
    /// <summary>
    /// Shows detailed action tooltip with cooldown status from specific character
    /// </summary>
    public void ShowActionTooltipWithCharacter(Action action, CharacterManager character)
    {
        if (action == null) 
        {
            ShowDefaultText();
            return;
        }
        
        string tooltipContent = $"{action.actionName}";
        
        // Add action type information with more details
        switch (action.actionType)
        {
            case Action.ActionType.Attack:
                tooltipContent += $"\n<color={ColourAttack}>Attack</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
             
                break;
            case Action.ActionType.Heal:
                tooltipContent += $"\n<color={ColourHeal}>Heal</color>";
                tooltipContent += $"\nHealing: {action.minHeal:F0}-{action.maxHeal:F0}";
                break;
            case Action.ActionType.Buff:
                tooltipContent += $"\n<color={ColourBuff}>Buff</color>";
                tooltipContent += $"\nEffect: {DescribeBuff(action.buffType, action.buffValue)}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.Debuff:
                tooltipContent += $"\n<color={ColourDebuff}>Debuff</color>";
                tooltipContent += $"\nEffect: {DescribeBuff(action.buffType, -action.buffValue)}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.DriveGrant:
                tooltipContent += $"\n<color={ColourDrive}>Drive Grant</color>";
                // Measured against the caster's own meter — every character's is authored on the
                // same prefab today, and it's the only meter available at hover time.
                tooltipContent += $"\nDrive: +{DescribeDrive(action.driveAmount, character)}";
                break;
            case Action.ActionType.DriveDrain:
                tooltipContent += $"\n<color={ColourDrive}>Drive Drain</color>";
                tooltipContent += $"\nDrive: -{DescribeDrive(action.driveAmount, character)}";
                break;
            case Action.ActionType.HealthDrain:
                tooltipContent += $"\n<color={ColourAttack}>Health Drain</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                // Shown as a healing range rather than a bare percentage — the ratio alone doesn't
                // tell you whether it survives the rounding in Heal().
                tooltipContent += $"\nHeals you: {action.minDamage * action.healthDrainRatio:F0}-" +
                                  $"{action.maxDamage * action.healthDrainRatio:F0}";
                break;
        }

        // Add target type information
        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                tooltipContent += $" / Single";
                break;
            case Action.TargetType.SingleAlly:
                tooltipContent += $" / Single";
                break;
            case Action.TargetType.AllAllies:
                tooltipContent += $" / Multiple";
                break;
            case Action.TargetType.AllEnemies:
                tooltipContent += $" / Multiple";
                break;
        }
        
        tooltipContent += DescribeStatusRiders(action);
        tooltipContent += DescribeSelfCast(action);

        // Show cooldown information with character-specific status
        if (action.cooldown > 0)
        {
            tooltipContent += $"\nCooldown: {Mathf.RoundToInt(action.cooldown)} rounds";
            
            // Show current cooldown status if character is provided
            if (character != null)
            {
                bool isAvailable = character.IsActionAvailable(action);
                if (!isAvailable)
                {
                    int cooldownRemaining = character.GetActionCooldownRemaining(action);
                    tooltipContent += $"\n<color={ColourDebuff}> Active in {cooldownRemaining} rounds</color>";
                }
                else
                {
                    tooltipContent += $" ";
                }
            }
        }
       
        
        tooltipText.text = tooltipContent;
    }
    
    /// <summary>
    /// Shows enemy action information during their turn
    /// </summary>
    public void ShowEnemyActionTooltip(Action action, string enemyName)
    {
        if (action == null) 
        {
            ShowDefaultText();
            return;
        }
        
        string tooltipContent = $"<color={ColourDebuff}>{enemyName}</color>";
        
        // Add action type information with more details
        switch (action.actionType)
        {
            case Action.ActionType.Attack:
                tooltipContent += $"\n<color={ColourAttack}>Attack</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                break;
            case Action.ActionType.Heal:
                tooltipContent += $"\n<color={ColourHeal}>Heal</color>";
                tooltipContent += $"\nHealing: {action.minHeal:F0}-{action.maxHeal:F0}";
                break;
            case Action.ActionType.Buff:
                tooltipContent += $"\n<color={ColourBuff}>Buff</color>";
                tooltipContent += $"\nEffect: {DescribeBuff(action.buffType, action.buffValue)}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.Debuff:
                tooltipContent += $"\n<color={ColourDebuff}>Debuff</color>";
                tooltipContent += $"\nEffect: {DescribeBuff(action.buffType, -action.buffValue)}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.DriveGrant:
                tooltipContent += $"\n<color={ColourDrive}>Drive Grant</color>";
                tooltipContent += $"\nDrive: +{action.driveAmount:F0}";
                break;
            case Action.ActionType.DriveDrain:
                // Worth telegraphing loudly: this is the enemy about to spend the crew's meter for
                // them, and the player can't see it coming from the damage number.
                tooltipContent += $"\n<color={ColourDrive}>Drive Drain</color>";
                tooltipContent += $"\nDrive: -{action.driveAmount:F0}";
                break;
            case Action.ActionType.HealthDrain:
                // The heal is the part worth reading before deciding whether to spend the parry —
                // parrying this cuts the enemy's healing, not just the damage.
                tooltipContent += $"\n<color={ColourAttack}>Health Drain</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                tooltipContent += $"\nHeals them: {action.healthDrainRatio:P0} of health taken";
                break;
        }

        // No target known here, so this shows the base chance. Still worth it — "this one bleeds"
        // is exactly what the player needs before deciding whether to spend their parry on it.
        tooltipContent += DescribeStatusRiders(action);

        /*
        // Add target type information
        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                tooltipContent += $"\nTargeting: Single Player";
                break;
            case Action.TargetType.SingleAlly:
                tooltipContent += $"\nTargeting: Single Ally";
                break;
            case Action.TargetType.AllAllies:
                tooltipContent += $"\nTargeting: All Allies";
                break;
            case Action.TargetType.AllEnemies:
                tooltipContent += $"\nTargeting: All Players";
                break;
        }
        
        
        // Add wind-up time if it's an attack
        if (action.actionType == Action.ActionType.Attack && action.AttackWindupTime != 1.0f)
        {
            tooltipContent += $"\nWind-up Time: {action.AttackWindupTime:F1}s";
        }
        */
        tooltipText.text = tooltipContent;
    }
    
    public void ShowTargetTooltip(Action selectedAction, CharacterManager attacker, CharacterManager target)
    {
        if (selectedAction == null || attacker == null || target == null) 
        {
            ShowDefaultText();
            return;
        }
        
        // Check if this is a valid target for the selected action
        if (!IsValidTarget(selectedAction, target))
        {
            // Show just the character name and HP without combat details
            tooltipText.text = $"<b>{target.characterData.characterName} \n({target.CurrentHealth:F0}/{target.MaxHealth:F0} HP)</b>";
            return;
        }
        
        string tooltipContent = $"<b>{target.characterData.characterName} ({target.CurrentHealth:F0}/{target.MaxHealth:F0} HP)</b>\n";
        
        switch (selectedAction.actionType)
        {
            // HealthDrain previews as an attack — same roll, same crit, same drive scaling — with
            // the siphoned healing appended below.
            case Action.ActionType.HealthDrain:
            case Action.ActionType.Attack:
                // Calculate hit chance
                int hitChance = CalculateHitChance(attacker, target);
                tooltipContent += $"Hit Chance: {hitChance}%\n";
                
                // Read off the attacker rather than hardcoded, so a CritChance buff shows up in the
                // preview. 5% unbuffed.
                tooltipContent += $"Crit Chance: {attacker.CritChancePercent}%\n";

                float minDamage = selectedAction.minDamage;
                float maxDamage = selectedAction.maxDamage;

                // Everything that will actually modify this damage gets applied here, in the same
                // order combat applies it, so the preview can't promise a number that never lands.
                DriveManager driveManager = attacker.GetComponent<DriveManager>();
                float multiplier = driveManager != null ? driveManager.GetNextAttackDamageMultiplier() : 1f;

                if (multiplier > 1f)
                {
                    minDamage *= multiplier;
                    maxDamage *= multiplier;
                }

                // The target's protection is part of the outcome too. Left out, a hover on a
                // shielded enemy quotes full damage and the player reads the shortfall as a bug.
                float protection = target.DamageReductionFraction;

                if (!Mathf.Approximately(protection, 0f))
                {
                    minDamage *= 1f - protection;
                    maxDamage *= 1f - protection;
                }

                tooltipContent += $"Damage: {minDamage:F0}-{maxDamage:F0}";

                // Both annotations are shown, so a number that looks wrong always explains itself.
                if (multiplier > 1f) tooltipContent += $" (drive x{multiplier:F1})";
                if (protection > 0f) tooltipContent += $" (<color={ColourBuff}>-{protection:P0} protected</color>)";
                else if (protection < 0f) tooltipContent += $" (<color={ColourDebuff}>+{-protection:P0} vulnerable</color>)";

                if (selectedAction.actionType == Action.ActionType.HealthDrain)
                {
                    // Derived from the numbers above, which already carry both the drive multiplier
                    // and the target's protection — correct on both counts, because the siphon is a
                    // share of health ACTUALLY LOST, and protection reduces that.
                    tooltipContent += $"\nHeals you: {minDamage * selectedAction.healthDrainRatio:F0}-" +
                                      $"{maxDamage * selectedAction.healthDrainRatio:F0}" +
                                      $" ({selectedAction.healthDrainRatio:P0} of health taken)";
                }

                // The only call site that knows the target, so this is the one place the rider
                // chance can be the REAL number rather than the authored base.
                tooltipContent += DescribeStatusRiders(selectedAction, target);
                break;

            case Action.ActionType.Heal:
                // Healing always succeeds, so 100% hit chance
                tooltipContent += $"Success Chance: 100%\n";
                tooltipContent += $"Crit Chance: {attacker.CritChancePercent}%\n"; // Heals crit on the same threshold
                
                float minHeal = selectedAction.minHeal;
                float maxHeal = selectedAction.maxHeal;
                
                DriveManager healerDriveManager = attacker.GetComponent<DriveManager>();
                if (healerDriveManager != null)
                {
                    float healMultiplier = healerDriveManager.GetNextHealingMultiplier();
                    if (healMultiplier > 1f)
                    {
                        minHeal *= healMultiplier;
                        maxHeal *= healMultiplier;
                        tooltipContent += $"Healing: {minHeal:F0}-{maxHeal:F0} (x{healMultiplier:F1})";
                    }
                    else
                    {
                        tooltipContent += $"Healing: {minHeal:F0}-{maxHeal:F0}";
                    }
                }
                else
                {
                    tooltipContent += $"Healing: {minHeal:F0}-{maxHeal:F0}";
                }
                break;
                
            case Action.ActionType.Buff:
            case Action.ActionType.Debuff:
                tooltipContent += $"Success Chance: 100%\n";
                tooltipContent += $"Effect: {DescribeBuff(selectedAction.buffType, selectedAction.actionType == Action.ActionType.Buff ? selectedAction.buffValue : -selectedAction.buffValue)}\n";
                tooltipContent += $"Duration: {Mathf.RoundToInt(selectedAction.duration)} turns";
                break;

            case Action.ActionType.DriveGrant:
            case Action.ActionType.DriveDrain:
                tooltipContent += $"Success Chance: 100%\n";
                tooltipContent += DescribeDriveOutcome(selectedAction, target);
                break;
        }

        tooltipText.text = tooltipContent;
    }

    /// <summary>
    /// Previews what a drive action will actually do to this target: their meter before and after,
    /// in segments.
    /// </summary>
    /// <remarks>
    /// Shows the <b>clamped</b> result rather than the authored amount, because the two often
    /// disagree in exactly the cases that matter — draining an enemy who has no drive banked, or
    /// topping up an ally who is already full. Both are wasted turns, and the player should be able
    /// to see that before committing rather than after.
    /// </remarks>
    private static string DescribeDriveOutcome(Action action, CharacterManager target)
    {
        DriveManager driveManager = target != null ? target.GetDriveManager() : null;

        if (driveManager?.Drive == null)
        {
            return $"{target?.characterData?.characterName ?? "Target"} has no drive meter — no effect.";
        }

        DriveMeter meter = driveManager.Drive;
        bool granting = action.actionType == Action.ActionType.DriveGrant;

        float before = meter.CurrentDrive;
        float after = granting
            ? Mathf.Min(meter.MaxDriveValue, before + action.driveAmount)
            : Mathf.Max(0f, before - action.driveAmount);

        float actual = Mathf.Abs(after - before);

        int segmentsBefore = meter.AvailableSegments;
        int segmentsAfter = meter.SegmentValue > 0f ? Mathf.FloorToInt(after / meter.SegmentValue) : 0;

        string line = $"Drive: {(granting ? "+" : "-")}{actual:F0}\n" +
                      $"{before:F0} → {after:F0}  ({segmentsBefore} → {segmentsAfter} segments)";

        if (Mathf.Approximately(actual, 0f))
        {
            line += granting ? $"\n<color={ColourDebuff}>Already full — no effect.</color>"
                             : $"\n<color={ColourDebuff}>No drive to drain — no effect.</color>";
        }
        else if (segmentsBefore == segmentsAfter)
        {
            // The meter moves but nothing becomes spendable (or unspendable), which is the trap
            // this preview exists to expose. Segments are the unit that actually buys anything.
            line += $"\n<color={ColourDrive}>No segment change.</color>";
        }

        return line;
    }
    
    private bool IsValidTarget(Action action, CharacterManager target)
    {
        if (action == null || target == null)
            return false;

        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                return target.gameObject.CompareTag("Enemy");
            case Action.TargetType.SingleAlly:
                return target.gameObject.CompareTag("Player");
            case Action.TargetType.AllAllies:
                return target.gameObject.CompareTag("Player");
            case Action.TargetType.AllEnemies:
                return target.gameObject.CompareTag("Enemy");
            default:
                return false;
        }
    }
    
    private int CalculateHitChance(CharacterManager attacker, CharacterManager target)
    {
        // Base hit chance calculation similar to your combat system
        // Assuming d20 roll + attack power vs defense
        int attackPower = Mathf.RoundToInt(attacker.AttackPower);
        int defense = Mathf.RoundToInt(target.DefenseValue);
        
        // Calculate the minimum roll needed to hit
        int minRollToHit = Mathf.Max(2, defense - attackPower + 1);
        
        // Cap at 20 (since rolling 1 is always a miss, rolling 20 is always a hit)
        minRollToHit = Mathf.Clamp(minRollToHit, 2, 20);
        
        // Calculate hit percentage with proper type conversion
        int successfulRolls = 21 - minRollToHit;
        int hitChancePercent = (successfulRolls * 100) / 20;
        
        return Mathf.Clamp(hitChancePercent, 5, 95); // Always at least 5% chance, max 95%
    }
    
    public void ShowDefaultText()
    {
        tooltipText.text = defaultText;
    }
    
    public void ShowCustomText(string text)
    {
        tooltipText.text = text;
    }
}