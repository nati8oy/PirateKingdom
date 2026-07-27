using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles automatic drive usage for enemies based on their configured strategy
/// </summary>
///
///
////// <summary>
/// Defines how an enemy uses their accumulated drive meter
/// </summary>
public enum DriveUsageStrategy
{
    /// <summary>
    /// Enemy doesn't use drive at all
    /// </summary>
    Never,
    
    /// <summary>
    /// Enemy uses drive instantly when they accumulate one full segment
    /// </summary>
    InstantOnSegment,
    
    /// <summary>
    /// Enemy saves drive and uses it only when health drops below a percentage threshold
    /// </summary>
    SaveUntilLowHealth,
    
    /// <summary>
    /// Enemy alternates between saving and using drive
    /// </summary>
    AlternateUsage,
    
    /// <summary>
    /// Enemy uses drive with a random chance each turn
    /// </summary>
    RandomChance
}

/// <summary>
/// Configuration for enemy drive usage behavior
/// </summary>
[System.Serializable]
public class EnemyDriveConfig
{
    [Header("Drive Usage Strategy")]
    [SerializeField]
    public DriveUsageStrategy strategy = DriveUsageStrategy.Never;
    
    [Header("Strategy Parameters")]
    [Tooltip("For SaveUntilLowHealth: Health percentage threshold (0-1) to trigger drive usage")]
    [Range(0f, 1f)]
    [SerializeField]
    public float healthThreshold = 0.5f;
    
    [Tooltip("For RandomChance: Probability of using drive each turn (0-1)")]
    [Range(0f, 1f)]
    [SerializeField]
    public float usageChance = 0.5f;
    
    [Tooltip("For InstantOnSegment: Which drive action to use (if applicable)")]
    [SerializeField]
    public DriveAction preferredAction = DriveAction.BuffNextAttack;
    
    [Tooltip("Minimum segments to accumulate before considering drive usage")]
    [Range(1, 10)]
    [SerializeField]
    public int minimumSegmentsToUse = 1;

    [Tooltip("How many drive stacks the enemy commits when buffing an attack. Diminishing returns apply, so more stacks give progressively less bonus.")]
    [Range(1, 4)]
    [SerializeField]
    public int stacksPerBuff = 2;
}
/// 
public class EnemyDriveManager : MonoBehaviour
{
    private DriveManager driveManager;
    private CharacterManager characterManager;
    private EnemyDriveConfig driveConfig;
    private int turnsSinceLastUsage = 0;

    public void Initialize(EnemyDriveConfig config)
    {
        driveConfig = config;
        driveManager = GetComponent<DriveManager>();
        characterManager = GetComponent<CharacterManager>();

        if (driveManager == null)
        {
            Debug.LogError($"EnemyDriveManager on {gameObject.name} requires a DriveManager component!");
        }

        if (characterManager == null)
        {
            Debug.LogError($"EnemyDriveManager on {gameObject.name} requires a CharacterManager component!");
            return;
        }

        if (driveConfig == null)
        {
            Debug.LogWarning($"EnemyDriveManager on {gameObject.name} initialized with null config, drive usage disabled");
            driveConfig = new EnemyDriveConfig(); // Create default config
        }
        
        Debug.Log($"EnemyDriveManager initialized for {characterManager.characterData.characterName} with strategy: {driveConfig.strategy}");
    }

    /// <summary>
    /// Called during enemy turn to evaluate and potentially use drive
    /// </summary>
    public void EvaluateDriveUsage()
    {
        // Safety checks
        if (driveConfig == null || driveManager == null || characterManager == null)
        {
            Debug.LogWarning($"EvaluateDriveUsage: Safety check failed - driveConfig: {driveConfig != null}, driveManager: {driveManager != null}, characterManager: {characterManager != null}");
            return;
        }

        Debug.Log($"[{characterManager.characterData.characterName}] EvaluateDriveUsage called. Strategy: {driveConfig.strategy}, Available Segments: {driveManager.Drive.AvailableSegments}");

        if (driveConfig.strategy == DriveUsageStrategy.Never)
        {
            Debug.Log($"[{characterManager.characterData.characterName}] Strategy is Never, skipping drive usage");
            return;
        }

        switch (driveConfig.strategy)
        {
            case DriveUsageStrategy.InstantOnSegment:
                Debug.Log($"[{characterManager.characterData.characterName}] Handling InstantOnSegment");
                HandleInstantUsage();
                break;

            case DriveUsageStrategy.SaveUntilLowHealth:
                Debug.Log($"[{characterManager.characterData.characterName}] Handling SaveUntilLowHealth");
                HandleLowHealthUsage();
                break;

            case DriveUsageStrategy.AlternateUsage:
                Debug.Log($"[{characterManager.characterData.characterName}] Handling AlternateUsage");
                HandleAlternateUsage();
                break;

            case DriveUsageStrategy.RandomChance:
                Debug.Log($"[{characterManager.characterData.characterName}] Handling RandomChance");
                HandleRandomUsage();
                break;
        }
    }

    private void HandleInstantUsage()
    {
        // Use drive as soon as we have enough segments
        Debug.Log($"[{characterManager.characterData.characterName}] InstantOnSegment: Checking segments. Have {driveManager.Drive.AvailableSegments}, need {driveConfig.minimumSegmentsToUse}");
        
        if (driveManager.Drive.AvailableSegments >= driveConfig.minimumSegmentsToUse)
        {
            Debug.Log($"[{characterManager.characterData.characterName}] InstantOnSegment: Attempting to use {driveConfig.preferredAction}");
            AttemptUseDriveAction(driveConfig.preferredAction);
        }
        else
        {
            Debug.Log($"[{characterManager.characterData.characterName}] InstantOnSegment: Not enough segments");
        }
    }

    private void HandleLowHealthUsage()
    {
        // Only use drive when health is below threshold
        float healthPercentage = characterManager.CurrentHealth / characterManager.MaxHealth;
        Debug.Log($"[{characterManager.characterData.characterName}] LowHealth check: {characterManager.CurrentHealth}/{characterManager.MaxHealth} = {healthPercentage:P0}, threshold: {driveConfig.healthThreshold:P0}");

        if (healthPercentage < driveConfig.healthThreshold && 
            driveManager.Drive.AvailableSegments >= driveConfig.minimumSegmentsToUse)
        {
            // Use healing if available and health is low, otherwise use attack buff
            if (driveManager.Drive.CanUseSegments(2)) // Assume heal costs 2
            {
                AttemptUseDriveAction(DriveAction.HealSelf);
            }
            else if (driveManager.Drive.CanUseSegments(1))
            {
                AttemptUseDriveAction(DriveAction.BuffNextAttack);
            }
        }
    }

    private void HandleAlternateUsage()
    {
        // Alternate between saving and using drive every other turn
        turnsSinceLastUsage++;

        if (turnsSinceLastUsage % 2 == 0 && 
            driveManager.Drive.AvailableSegments >= driveConfig.minimumSegmentsToUse)
        {
            AttemptUseDriveAction(driveConfig.preferredAction);
            turnsSinceLastUsage = 0;
        }
    }

    private void HandleRandomUsage()
    {
        // Random chance to use drive each turn
        float randomValue = Random.value;
        Debug.Log($"[{characterManager.characterData.characterName}] RandomChance: Random value {randomValue:P0}, threshold {driveConfig.usageChance:P0}");
        
        if (randomValue < driveConfig.usageChance && 
            driveManager.Drive.AvailableSegments >= driveConfig.minimumSegmentsToUse)
        {
            AttemptUseDriveAction(driveConfig.preferredAction);
        }
    }

    /// <summary>
    /// Only suggest using BuffNextAttack if we're about to actually attack
    /// </summary>
    private void AttemptUseDriveAction(DriveAction preferredAction)
    {
        Debug.Log($"[{characterManager.characterData.characterName}] AttemptUseDriveAction: Attempting {preferredAction}");

        // The attack buff is now the shared stacking mechanic — commit multiple stacks
        // (diminishing returns) instead of a single one-shot flag.
        if (preferredAction == DriveAction.BuffNextAttack)
        {
            ApplyAttackStacks();
            return;
        }

        List<DriveAction> availableActions = driveManager.GetAvailableActions();
        Debug.Log($"[{characterManager.characterData.characterName}] Available drive actions: {availableActions.Count}");
        
        foreach (var action in availableActions)
        {
            Debug.Log($"  - {action}");
        }

        if (availableActions.Count == 0)
        {
            Debug.Log($"[{characterManager.characterData.characterName}] No available drive actions");
            return;
        }

        // Try to use the preferred action first
        if (availableActions.Contains(preferredAction))
        {
            Debug.Log($"[{characterManager.characterData.characterName}] Preferred action {preferredAction} is available, using it");
            if (driveManager.TryUseDriveAction(preferredAction))
            {
                Debug.Log($"[{characterManager.characterData.characterName}] Successfully used drive action: {preferredAction}");
            }
            else
            {
                Debug.LogWarning($"[{characterManager.characterData.characterName}] Failed to use drive action: {preferredAction}");
            }
        }
        else if (availableActions.Count > 0)
        {
            // Fall back to the first available action
            Debug.Log($"[{characterManager.characterData.characterName}] Preferred action {preferredAction} not available, using fallback: {availableActions[0]}");
            if (driveManager.TryUseDriveAction(availableActions[0]))
            {
                Debug.Log($"[{characterManager.characterData.characterName}] Successfully used fallback drive action: {availableActions[0]}");
            }
            else
            {
                Debug.LogWarning($"[{characterManager.characterData.characterName}] Failed to use fallback drive action: {availableActions[0]}");
            }
        }
    }

    /// <summary>
    /// Commits drive stacks toward the enemy's next attack, up to the configured stacksPerBuff.
    /// Uses the same stacking + diminishing-returns system as the player. Bounded by available
    /// segments and the DriveManager's max-stack cap.
    /// </summary>
    private void ApplyAttackStacks()
    {
        int target = Mathf.Max(1, driveConfig.stacksPerBuff);
        int added = 0;

        while (driveManager.DriveStacks < target && driveManager.TryAddDriveStack())
        {
            added++;
        }

        if (added > 0)
        {
            Debug.Log($"[{characterManager.characterData.characterName}] Committed {added} drive stack(s), now at {driveManager.DriveStacks}");
        }
        else
        {
            Debug.Log($"[{characterManager.characterData.characterName}] Could not add drive stacks (not enough segments or already at max)");
        }
    }

    /// <summary>
    /// Called at the start of the enemy's turn to check if they already have a buffed attack waiting
    /// </summary>
    public bool HasPendingAttackBuff()
    {
        return driveManager != null && driveManager.NextAttackBuffed;
    }

    public EnemyDriveConfig GetDriveConfig() => driveConfig;

    public void ResetTurnCounter()
    {
        turnsSinceLastUsage = 0;
    }
}