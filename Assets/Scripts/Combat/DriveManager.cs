
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
    [SerializeField] private int requiredSegments = 3; // Legacy: no longer used for entry (kept for Inspector compatibility)

    [Header("Drive Stacking")]
    [Tooltip("Segments spent to add one drive stack")]
    [SerializeField] private int stackCost = 1;
    [Tooltip("Maximum drive stacks that can be committed to a single action")]
    [SerializeField] private int maxDriveStacks = 4;
    [Tooltip("Each additional stack adds this fraction of the previous stack's bonus (diminishing returns). 0.5 = each stack is half as strong as the last.")]
    [Range(0f, 1f)]
    [SerializeField] private float decayRate = 0.5f;

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
    private int driveStacks = 0;
    private int previousSegmentCount = 0; // Track previous segments for audio

    public DriveMeter Drive => driveMeter;
    public int DriveStacks => driveStacks;
    public bool NextAttackBuffed => driveStacks > 0;
    public float BuffAttackMultiplier => buffAttackMultiplier;
    public bool IsDriveMode => driveStacks > 0;

    // Bonus contributed by the first stack, derived from the per-character multiplier (e.g. 1.5 -> 0.5)
    private float BaseBonus => Mathf.Max(0f, buffAttackMultiplier - 1f);
    
    public event System.Action<DriveAction, bool> OnDriveActionAttempted;
    public event System.Action OnDriveModeEntered;
    public event System.Action OnDriveModeExited;
    
    void Awake()
    {
        
        characterManager = GetComponent<CharacterManager>();

        buffAttackMultiplier = characterManager.BuffNextActionMultiplier;
        
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
        // Clear any unused drive stacks so a committed buff doesn't leak into the next turn.
        // Note: segments already spent are not refunded — committing meter is the cost.
        ClearStacks();
    }

    /// <summary>
    /// Adds one drive stack, spending <see cref="stackCost"/> segments. Shared entry point used by
    /// the player (one press per stack) and enemy AI (looped by strategy). Returns false if the
    /// meter is short or the character is already at max stacks.
    /// </summary>
    public bool TryAddDriveStack()
    {
        if (driveStacks >= maxDriveStacks)
        {
            Debug.Log($"{characterManager?.characterData?.characterName ?? gameObject.name} already at max drive stacks ({maxDriveStacks})");
            return false;
        }

        if (!driveMeter.CanUseSegments(stackCost))
        {
            Debug.Log($"Not enough drive segments to add a stack. Need {stackCost}, have {driveMeter.AvailableSegments}");
            return false;
        }

        driveMeter.UseSegments(stackCost);
        AddStack();

        Debug.Log($"{characterManager?.characterData?.characterName ?? gameObject.name} added a drive stack. Stacks: {driveStacks}, next-action multiplier now {GetStackMultiplier()}x");
        return true;
    }

    // Increments the stack counter and plays the drive juice. Feedbacks + particles fire on
    // EVERY stack; the OnDriveModeEntered event fires once, on the first stack (0 -> 1), for any
    // one-time "entered drive" scene hooks. Does NOT spend segments — callers that charge
    // segments do so separately.
    private void AddStack()
    {
        bool wasZero = driveStacks == 0;
        driveStacks = Mathf.Min(driveStacks + 1, maxDriveStacks);

        // Sound + feedbacks per stack
        if (driveFeedbacks != null)
        {
            driveFeedbacks.PlayFeedbacks();
        }

        // Particle burst per stack
        if (characterManager != null && characterManager.driveParticles != null)
        {
            characterManager.driveParticles.Play();
        }

        if (wasZero && driveStacks > 0)
        {
            OnDriveModeEntered?.Invoke();
        }
    }

    // Clears all stacks and stops the associated feedback/particles.
    private void ClearStacks()
    {
        if (driveStacks <= 0)
            return;

        if (characterManager != null && characterManager.driveParticles != null)
        {
            characterManager.driveParticles.Stop();
        }

        driveStacks = 0;
        OnDriveModeExited?.Invoke();
    }

    // Diminishing-returns multiplier for the current stack count:
    // 1 + baseBonus * (1 + decay + decay^2 + ... + decay^(n-1))
    private float GetStackMultiplier()
    {
        if (driveStacks <= 0)
            return 1f;

        float bonus = 0f;
        float increment = BaseBonus;
        for (int i = 0; i < driveStacks; i++)
        {
            bonus += increment;
            increment *= decayRate;
        }

        return 1f + bonus;
    }
    
    /// <summary>
    /// Adds raw drive to this character's meter from an outside source — a <c>DriveGrant</c> action
    /// cast on them. Returns how much was actually added (0 if the meter was already full).
    /// </summary>
    /// <remarks>
    /// Grants raw meter, never stacks: committing a stack is the owner's own decision, made on
    /// their turn. A support character fills the meter; what to spend it on stays with whoever
    /// owns it.
    /// </remarks>
    public float GrantDrive(float amount)
    {
        if (amount <= 0f) return 0f;

        float before = driveMeter.CurrentDrive;
        driveMeter.AddDrive(amount);
        float granted = driveMeter.CurrentDrive - before;

        Debug.Log($"{characterManager?.characterData?.characterName ?? gameObject.name} was granted {granted} drive " +
                  $"(asked for {amount}). Now {driveMeter.CurrentDrive}, {driveMeter.AvailableSegments} segment(s).");

        return granted;
    }

    /// <summary>
    /// Strips raw drive from this character's meter — a <c>DriveDrain</c> action cast on them.
    /// Returns how much was actually removed, which is less than asked for on a near-empty meter.
    /// </summary>
    /// <remarks>
    /// Stacks the character has already committed are not refunded or removed — see
    /// <see cref="DriveMeter.RemoveDrive"/>. In practice a drain never races a committed stack:
    /// enemies commit theirs at the start of their own turn and spend them in the same turn, so a
    /// drain on the player's turn can only ever hit banked meter.
    /// </remarks>
    public float DrainDrive(float amount)
    {
        if (amount <= 0f) return 0f;

        float removed = driveMeter.RemoveDrive(amount);

        Debug.Log($"{characterManager?.characterData?.characterName ?? gameObject.name} lost {removed} drive " +
                  $"(asked for {amount}). Now {driveMeter.CurrentDrive}, {driveMeter.AvailableSegments} segment(s).");

        return removed;
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
        // Utility-menu path. TryUseDriveAction charges buffAttackCost separately, so this only
        // adds the stack (no extra segment spend here).
        if (driveStacks >= maxDriveStacks)
            return false;

        AddStack();
        return true;
    }
    
   
    private bool ExecuteHealSelf()
    {
        if (characterManager != null)
        {
            // Self-heal, so caster and target are the same character — but pass the multiplier
            // explicitly anyway, since Heal() no longer looks one up on its own behalf.
            characterManager.Heal(healAmount, GetNextHealingMultiplier());
            return true;
        }
        return false;
    }
    
    private bool ExecuteRemoveDebuff()
    {
        if (characterManager?.characterData != null)
        {
            bool removed = characterManager.RemoveFirstDebuff();

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
        float multiplier = GetStackMultiplier();

        if (multiplier > 1f)
        {
            Debug.Log($"Drive stacks ({driveStacks}) applying {multiplier}x attack multiplier");
        }

        return multiplier;
    }


    public float GetNextHealingMultiplier()
    {
        float multiplier = GetStackMultiplier();

        if (multiplier > 1f)
        {
            Debug.Log($"Drive stacks ({driveStacks}) applying {multiplier}x healing multiplier");
        }

        return multiplier;
    }

    // Consumes any committed drive stacks after an action resolves.
    public void OnActionPerformed()
    {
        if (driveStacks > 0)
        {
            Debug.Log($"Action performed with {driveStacks} drive stack(s). Clearing stacks for {characterManager?.characterData?.characterName ?? gameObject.name}");
        }
        ClearStacks();
    }

    // Keep the old method for backward compatibility, but make it call the new general method
    public void OnAttackPerformed()
    {
        OnActionPerformed();
    }

    public void ConsumeAttackBuff()
    {
        ClearStacks();
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
        if (characterManager != null)
        {
            foreach (var buff in characterManager.GetActiveBuffs())
            {
                if (buff.Value < 0) return true;
            }
        }
        return false;
    }
    
    // Legacy shim: drive "mode" is now a stack. A single call adds one stack.
    public void EnterDriveMode()
    {
        TryAddDriveStack();
    }

    public void ExitDriveMode()
    {
        ClearStacks();
    }
}