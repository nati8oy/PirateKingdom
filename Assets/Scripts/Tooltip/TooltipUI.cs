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
        }
        
        // Add cooldown information if applicable
        if (action.cooldown > 0)
        {
            tooltipContent += $"\nCooldown: {Mathf.RoundToInt(action.cooldown)} turns";
        }
        
        tooltipText.text = tooltipContent;
    }
    
    public void ShowTargetTooltip(Action selectedAction, CharacterManager attacker, CharacterManager target)
    {
        if (selectedAction == null || attacker == null || target == null) 
        {
            ShowDefaultText();
            return;
        }
        
        string tooltipContent = $"<b>Target: {target.characterData.characterName}</b>\n";
        
        switch (selectedAction.actionType)
        {
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
        }
        
        tooltipText.text = tooltipContent;
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