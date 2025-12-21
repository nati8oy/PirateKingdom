
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using MoreMountains.Feedbacks;


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
    [SerializeField] private int requiredSegments = 3; 
    
    [Header("Drive Action Costs")]
    [SerializeField] private int buffAttackCost = 1;
    [SerializeField] private int healSelfCost = 2;
    [SerializeField] private int removeDebuffCost = 1;
    [SerializeField] private int reduceCooldownCost = 3;
    
    [Header("Drive Action Effects")]
    private float buffAttackMultiplier;
    [SerializeField] private float healAmount = 100f;
    [SerializeField] private int cooldownReduction = 1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip driveSegmentUnlockedSound;
    [SerializeField] private AudioSource audioSource;
    
    [Tooltip("Feeback player for using drive")]
    public MMF_Player driveFeedbacks;
    
    private CharacterManager characterManager;
    private bool nextAttackBuffed;
    private bool isDriveMode = false;
    private int previousSegmentCount = 0; // Track previous segments for audio

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

        buffAttackMultiplier = characterManager.characterData.buffNextActionMultiplier;
        
        characterManager = GetComponent<CharacterManager>();
        driveMeter.Initialize();
        previousSegmentCount = driveMeter.AvailableSegments;
        
        // Get or create AudioSource component
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Subscribe to drive meter events
        driveMeter.OnSegmentsChanged += OnSegmentsChanged;
    }
    
    void Start()
    {
        if (characterManager == null)
        {
            Debug.LogError($"DriveManager on {gameObject.name} requires a CharacterManager component!");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (driveMeter != null)
        {
            driveMeter.OnSegmentsChanged -= OnSegmentsChanged;
        }
    }
    
    private void OnSegmentsChanged(int newSegmentCount)
    {
        // Only play sound if segments increased (not decreased) and this is a player character
        if (newSegmentCount > previousSegmentCount && IsPlayerCharacter())
        {
            PlayDriveSegmentUnlockedSound();
        }
        
        previousSegmentCount = newSegmentCount;
    }
    
    private bool IsPlayerCharacter()
    {
        // Check if this character is a player character (not an enemy)
        if (characterManager?.characterData != null)
        {
            return characterManager.characterData.allegiance == Character.Allegiance.Player;
        }
        
        return false; // Default to false if we can't determine
    }
    
    private void PlayDriveSegmentUnlockedSound()
    {
        if (driveSegmentUnlockedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(driveSegmentUnlockedSound);
            Debug.Log($"Drive segment unlocked sound played for {characterManager?.characterData?.characterName ?? gameObject.name}");
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
            float finalHealAmount = healAmount * GetNextHealingMultiplier();
            characterManager.Heal(finalHealAmount);
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
    
    
    public float GetNextHealingMultiplier()
    {
        float multiplier = 1f;
    
        // Apply drive mode multiplier if active
        if (isDriveMode)
        {
            multiplier *= buffAttackMultiplier; // Using the same multiplier for consistency
            Debug.Log($"Drive mode active: applying {buffAttackMultiplier}x healing multiplier. Current total: {multiplier}x");
        }
        else
        {
            Debug.Log("Drive mode not active, no drive mode healing multiplier applied");
        }
    
        Debug.Log($"Final healing multiplier: {multiplier}x");
        return multiplier;
    }

    // Rename the existing method to be more general
    public void OnActionPerformed()
    {
        if (isDriveMode)
        {
            Debug.Log($"Action performed with drive mode active. Deactivating drive mode for {characterManager?.characterData?.characterName ?? gameObject.name}");
            characterManager.driveParticles.Stop();
            ExitDriveMode();
        }

        // Also consume single-use attack buff
        nextAttackBuffed = false;
    }

    // Keep the old method for backward compatibility, but make it call the new general method
    public void OnAttackPerformed()
    {
        OnActionPerformed();
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
            driveFeedbacks.PlayFeedbacks();
            
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
    
    public bool IsDriveModeAvailable()
    {
        return isDriveMode;
    }
}