

using UnityEngine;
using TMPro;

public class TooltipUI : MonoBehaviour
{
    [Header("Tooltip Components")]
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private string defaultText = "Hover over actions or targets for information";
    
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
                tooltipContent += $"\nBuff: {action.buffType} +{action.buffValue:F0}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.Debuff:
                tooltipContent += $"\nDebuff: {action.buffType} -{action.buffValue:F0}";
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

        // Add cooldown information if applicable
        if (action.cooldown > 0)
        {
            tooltipContent += $"\nCooldown: {Mathf.RoundToInt(action.cooldown)} rounds";
        }

        tooltipText.text = tooltipContent;
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
                tooltipContent += $"\n<color=orange>Attack</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
             
                break;
            case Action.ActionType.Heal:
                tooltipContent += $"\n<color=green>Heal</color>";
                tooltipContent += $"\nHealing: {action.minHeal:F0}-{action.maxHeal:F0}";
                break;
            case Action.ActionType.Buff:
                tooltipContent += $"\n<color=cyan>Buff</color>";
                tooltipContent += $"\nEffect: {action.buffType} +{action.buffValue:F0}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.Debuff:
                tooltipContent += $"\n<color=red>Debuff</color>";
                tooltipContent += $"\nEffect: {action.buffType} -{action.buffValue:F0}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.DriveGrant:
                tooltipContent += $"\n<color=yellow>Drive Grant</color>";
                // Measured against the caster's own meter — every character's is authored on the
                // same prefab today, and it's the only meter available at hover time.
                tooltipContent += $"\nDrive: +{DescribeDrive(action.driveAmount, character)}";
                break;
            case Action.ActionType.DriveDrain:
                tooltipContent += $"\n<color=yellow>Drive Drain</color>";
                tooltipContent += $"\nDrive: -{DescribeDrive(action.driveAmount, character)}";
                break;
            case Action.ActionType.HealthDrain:
                tooltipContent += $"\n<color=orange>Health Drain</color>";
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
                    tooltipContent += $"\n<color=red> Active in {cooldownRemaining} rounds</color>";
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
        
        string tooltipContent = $"<color=red>{enemyName}</color>";
        
        // Add action type information with more details
        switch (action.actionType)
        {
            case Action.ActionType.Attack:
                tooltipContent += $"\n<color=orange>Attack</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                break;
            case Action.ActionType.Heal:
                tooltipContent += $"\n<color=green>Heal</color>";
                tooltipContent += $"\nHealing: {action.minHeal:F0}-{action.maxHeal:F0}";
                break;
            case Action.ActionType.Buff:
                tooltipContent += $"\n<color=cyan>Buff</color>";
                tooltipContent += $"\nEffect: {action.buffType} +{action.buffValue:F0}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.Debuff:
                tooltipContent += $"\n<color=red>Debuff</color>";
                tooltipContent += $"\nEffect: {action.buffType} -{action.buffValue:F0}";
                tooltipContent += $"\nDuration: {Mathf.RoundToInt(action.duration)} turns";
                break;
            case Action.ActionType.DriveGrant:
                tooltipContent += $"\n<color=yellow>Drive Grant</color>";
                tooltipContent += $"\nDrive: +{action.driveAmount:F0}";
                break;
            case Action.ActionType.DriveDrain:
                // Worth telegraphing loudly: this is the enemy about to spend the crew's meter for
                // them, and the player can't see it coming from the damage number.
                tooltipContent += $"\n<color=yellow>Drive Drain</color>";
                tooltipContent += $"\nDrive: -{action.driveAmount:F0}";
                break;
            case Action.ActionType.HealthDrain:
                // The heal is the part worth reading before deciding whether to spend the parry —
                // parrying this cuts the enemy's healing, not just the damage.
                tooltipContent += $"\n<color=orange>Health Drain</color>";
                tooltipContent += $"\nDamage: {action.minDamage:F0}-{action.maxDamage:F0}";
                tooltipContent += $"\nHeals them: {action.healthDrainRatio:P0} of health taken";
                break;
        }

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
                
                // Calculate crit chance (assuming crit on natural 20)
                int critChance = 5; // 5% base crit chance (1 in 20)
                tooltipContent += $"Crit Chance: {critChance}%\n";
                
                // Show damage range with potential drive multipliers
                float minDamage = selectedAction.minDamage;
                float maxDamage = selectedAction.maxDamage;
                
                DriveManager driveManager = attacker.GetComponent<DriveManager>();
                if (driveManager != null)
                {
                    float multiplier = driveManager.GetNextAttackDamageMultiplier();
                    if (multiplier > 1f)
                    {
                        minDamage *= multiplier;
                        maxDamage *= multiplier;
                        tooltipContent += $"Damage: {minDamage:F0}-{maxDamage:F0} (x{multiplier:F1})";
                    }
                    else
                    {
                        tooltipContent += $"Damage: {minDamage:F0}-{maxDamage:F0}";
                    }
                }
                else
                {
                    tooltipContent += $"Damage: {minDamage:F0}-{maxDamage:F0}";
                }

                if (selectedAction.actionType == Action.ActionType.HealthDrain)
                {
                    // minDamage/maxDamage already carry the drive multiplier applied above, which is
                    // right: the siphon is a share of damage dealt, so drive scales it too — once.
                    tooltipContent += $"\nHeals you: {minDamage * selectedAction.healthDrainRatio:F0}-" +
                                      $"{maxDamage * selectedAction.healthDrainRatio:F0}" +
                                      $" ({selectedAction.healthDrainRatio:P0} of health taken)";
                }
                break;

            case Action.ActionType.Heal:
                // Healing always succeeds, so 100% hit chance
                tooltipContent += $"Success Chance: 100%\n";
                tooltipContent += $"Crit Chance: 5%\n"; // Same crit chance as attacks
                
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
                tooltipContent += $"Effect: {selectedAction.buffType} {(selectedAction.actionType == Action.ActionType.Buff ? "+" : "-")}{selectedAction.buffValue:F0}\n";
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
            line += granting ? "\n<color=red>Already full — no effect.</color>"
                             : "\n<color=red>No drive to drain — no effect.</color>";
        }
        else if (segmentsBefore == segmentsAfter)
        {
            // The meter moves but nothing becomes spendable (or unspendable), which is the trap
            // this preview exists to expose. Segments are the unit that actually buys anything.
            line += "\n<color=yellow>No segment change.</color>";
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