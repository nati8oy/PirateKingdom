using System;
using System.Collections.Generic;
using UnityEngine;

public enum DriveAction
{
    BuffNextAttack,
    HealSelf,
    RemoveDebuff,
    ReduceCooldown
}

public class DriveManager : MonoBehaviour
{
    [Header("Drive Meter")]
    [SerializeField] private DriveMeter driveMeter = new DriveMeter();
    [SerializeField] private int requiredSegments = 3; // Adjust this value as needed
    
    [Header("Drive Action Costs")]
    [SerializeField] private int buffAttackCost = 1;
    [SerializeField] private int healSelfCost = 2;
    [SerializeField] private int removeDebuffCost = 1;
    [SerializeField] private int reduceCooldownCost = 3;
    
    [Header("Drive Action Effects")]
    [SerializeField] private float buffAttackMultiplier = 1.5f;
    [SerializeField] private float healAmount = 100f;
    [SerializeField] private int cooldownReduction = 1;
    
    private CharacterManager characterManager;
    private bool nextAttackBuffed;
    private bool isDriveMode = false;
    
    public DriveMeter Drive => driveMeter;
    public bool NextAttackBuffed => nextAttackBuffed;
    public float BuffAttackMultiplier => buffAttackMultiplier;
    public bool IsDriveMode => isDriveMode;
    
    public event System.Action<DriveAction, bool> OnDriveActionAttempted;
    public event System.Action OnDriveModeEntered;
    public event System.Action OnDriveModeExited;
    
    void Awake()
    {
        characterManager = GetComponent<CharacterManager>();
        driveMeter.Initialize();
    }
    
    void Start()
    {
        if (characterManager == null)
        {
            Debug.LogError($"DriveManager on {gameObject.name} requires a CharacterManager component!");
        }
    }
    
    public void OnTurnStart()
    {
        driveMeter.RegenerateDrive();
        
        // No need to handle drive mode countdown since it's single-use
        // Drive mode will be deactivated when an attack is made
        
        // Debug.Log($"{characterManager?.characterData?.characterName ?? gameObject.name} regenerated drive. Current: {driveMeter.CurrentDrive}, Segments: {driveMeter.AvailableSegments}");
    }
    
    public void OnTurnEnd()
    {
        // Reset any turn-based effects
        nextAttackBuffed = false;
    }
    
    public bool TryUseDriveAction(DriveAction action)
    {
        int cost = GetActionCost(action);
        
        if (!driveMeter.CanUseSegments(cost))
        {
            OnDriveActionAttempted?.Invoke(action, false);
            Debug.Log($"Not enough drive segments for {action}. Need {cost}, have {driveMeter.AvailableSegments}");
            return false;
        }
        
        if (ExecuteDriveAction(action))
        {
            driveMeter.UseSegments(cost);
            OnDriveActionAttempted?.Invoke(action, true);
            Debug.Log($"{characterManager?.characterData?.characterName ?? gameObject.name} used {action} for {cost} segments");
            return true;
        }
        
        OnDriveActionAttempted?.Invoke(action, false);
        return false;
    }
    
    private int GetActionCost(DriveAction action)
    {
        return action switch
        {
            DriveAction.BuffNextAttack => buffAttackCost,
            DriveAction.HealSelf => healSelfCost,
            DriveAction.RemoveDebuff => removeDebuffCost,
            DriveAction.ReduceCooldown => reduceCooldownCost,
            _ => 1
        };
    }
    
    private bool ExecuteDriveAction(DriveAction action)
    {
        switch (action)
        {
            case DriveAction.BuffNextAttack:
                return ExecuteBuffNextAttack();
            case DriveAction.HealSelf:
                return ExecuteHealSelf();
            case DriveAction.RemoveDebuff:
                return ExecuteRemoveDebuff();
            case DriveAction.ReduceCooldown:
                return ExecuteReduceCooldown();
            default:
                return false;
        }
    }
    
    private bool ExecuteBuffNextAttack()
    {
        nextAttackBuffed = true;
        return true;
    }
    
    private bool ExecuteHealSelf()
    {
        if (characterManager != null)
        {
            characterManager.Heal(healAmount);
            return true;
        }
        return false;
    }
    
    private bool ExecuteRemoveDebuff()
    {
        if (characterManager?.characterData != null)
        {
            // Use the new RemoveFirstDebuff method instead
            bool removed = characterManager.characterData.RemoveFirstDebuff();
        
            if (removed)
            {
                characterManager.RefreshStats();
                Debug.Log($"Removed debuff from {characterManager.characterData.characterName}");
                return true;
            }
            else
            {
                Debug.Log($"{characterManager.characterData.characterName} has no debuffs to remove");
                return false;
            }
        }
        return false;
    }
    
    private bool ExecuteReduceCooldown()
    {
        if (characterManager?.characterData != null)
        {
            // This would need to be implemented based on your action/cooldown system
            // For now, we'll assume it works and return true
            Debug.Log($"Reduced cooldown for {characterManager.characterData.characterName}");
            return true;
        }
        return false;
    }
    
    public float GetNextAttackDamageMultiplier()
    {
        float multiplier = 1f;
        
        // Apply drive mode multiplier if active
        if (isDriveMode)
        {
            multiplier *= buffAttackMultiplier;
            Debug.Log($"Drive mode active: applying {buffAttackMultiplier}x multiplier. Current total: {multiplier}x");
        }
        else
        {
            Debug.Log("Drive mode not active, no drive mode multiplier applied");
        }
        
        // Apply single-use attack buff if active
        if (nextAttackBuffed)
        {
            multiplier *= buffAttackMultiplier;
            Debug.Log($"Next attack buff active: applying {buffAttackMultiplier}x multiplier. Current total: {multiplier}x");
        }
        else
        {
            Debug.Log("Next attack buff not active, no single-use buff applied");
        }
        
        Debug.Log($"Final attack multiplier: {multiplier}x");
        return multiplier;
    }
    
    // NEW METHOD: Call this when an attack is performed to deactivate drive mode
    public void OnAttackPerformed()
    {
        if (isDriveMode)
        {
            Debug.Log($"Attack performed with drive mode active. Deactivating drive mode for {characterManager?.characterData?.characterName ?? gameObject.name}");
            ExitDriveMode();
        }
        
        // Also consume single-use attack buff
        nextAttackBuffed = false;
    }
    
    public void ConsumeAttackBuff()
    {
        nextAttackBuffed = false;
        
        // Also deactivate drive mode when consuming attack buffs
        if (isDriveMode)
        {
            Debug.Log($"Consuming attack buff, also deactivating drive mode for {characterManager?.characterData?.characterName ?? gameObject.name}");
            ExitDriveMode();
        }
    }
    
    // Method to get available actions based on current segments
    public List<DriveAction> GetAvailableActions()
    {
        List<DriveAction> availableActions = new List<DriveAction>();
        
        foreach (DriveAction action in Enum.GetValues(typeof(DriveAction)))
        {
            if (driveMeter.CanUseSegments(GetActionCost(action)))
            {
                // Additional checks for specific actions
                if (action == DriveAction.RemoveDebuff && !HasDebuffs())
                    continue;
                    
                availableActions.Add(action);
            }
        }
        
        return availableActions;
    }
    
    private bool HasDebuffs()
    {
        if (characterManager?.characterData != null)
        {
            var activeBuffs = characterManager.characterData.GetActiveBuffs();
            foreach (var buff in activeBuffs)
            {
                if (buff.Value < 0) return true;
            }
        }
        return false;
    }
    
    public void EnterDriveMode()
    {
        // Check if character has enough drive segments to enter drive mode (e.g., 3 segments)
        if (Drive.CanUseSegments(requiredSegments))
        {
            Drive.UseSegments(requiredSegments); // Consume the segments
            isDriveMode = true;
        
            Debug.Log($"Drive mode activated! isDriveMode = {isDriveMode}");
        
            OnDriveModeEntered?.Invoke();
            Debug.Log($"{gameObject.name} entered drive mode! Used {requiredSegments} segments. Drive mode will be active until next attack.");
        }
        else
        {
            Debug.Log($"{gameObject.name} doesn't have enough drive segments to enter drive mode! Need {requiredSegments}, have {Drive.AvailableSegments}");
        }
    }
    
    public void ExitDriveMode()
    {
        if (isDriveMode)
        {
            isDriveMode = false;
            OnDriveModeExited?.Invoke();
            Debug.Log($"{gameObject.name} exited drive mode.");
        }
    }
}