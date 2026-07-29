using UnityEngine;
using System.Collections;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Timing")]
    [SerializeField] private float attackHitTime = 0.1f;
    [SerializeField] private float attackRecoveryTime = 0.5f;
    
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowStartOffset = 0.2f; // How long before hit the parry window opens
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private CharacterManager characterManager;
    private bool isAttacking = false;
    private ParrySystem targetParrySystem;
    private ParryVisualFeedback targetVisualFeedback;

    // Current action being executed
    private Action currentAction;

    // Damage rolled by EnemyManager for this attack. This is the same value that will actually be
    // applied (full on hit, reduced on parry), so the target's parry drive bonus is calculated
    // from the damage genuinely prevented rather than from a second, unrelated roll.
    private float incomingDamage;

    public float AttackHitTime => attackHitTime;
    public float AttackRecoveryTime => attackRecoveryTime;
    public bool IsAttacking => isAttacking;
    
    // Property to get windup time from current action
    public float AttackWindupTime => currentAction != null ? currentAction.AttackWindupTime : 1.0f;
    
    // Events
    public System.Action OnAttackStart;
    public System.Action OnAttackHit;
    public System.Action OnAttackEnd;
    public System.Action<bool> OnAttackComplete; // bool indicates if attack was parried
    
    private void Awake()
    {
        characterManager = GetComponent<CharacterManager>();
    }
    
    /// <summary>
    /// Starts an attack sequence against the specified target using the given action.
    /// <paramref name="damage"/> is the already-rolled damage for this attack (including crit and
    /// drive multipliers) and is what the target's parry window will report as incoming.
    /// </summary>
    public void StartAttack(GameObject target, Action action, float damage)
    {
        if (isAttacking)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[EnemyAttack] Already attacking!");
            }
            return;
        }
        
        // CLEAR previous target references to prevent issues
        targetParrySystem = null;
        targetVisualFeedback = null;
        
        currentAction = action;
        incomingDamage = damage;

        // Add debug logging to track targeting
        if (showDebugInfo)
        {
            Debug.Log($"[EnemyAttack] Starting attack against target: {target?.name}");
        }
        
        if (target == null)
        {
            Debug.LogError("[EnemyAttack] Target is null!");
            return;
        }
        
        // Find target's parry system and visual feedback
        targetParrySystem = target.GetComponent<ParrySystem>();
        targetVisualFeedback = target.GetComponent<ParryVisualFeedback>();
        if (targetVisualFeedback == null)
        {
            targetVisualFeedback = target.GetComponentInChildren<ParryVisualFeedback>();
        }
        
        // Add more debug logging
        if (showDebugInfo)
        {
            Debug.Log($"[EnemyAttack] Target ParrySystem: {targetParrySystem?.name}");
            Debug.Log($"[EnemyAttack] Target VisualFeedback: {targetVisualFeedback?.name}");
        }
        
        // Verify we found the correct components
        if (targetParrySystem == null)
        {
            Debug.LogWarning($"[EnemyAttack] No ParrySystem found on target {target.name}!");
        }
        
        if (targetVisualFeedback == null)
        {
            Debug.LogWarning($"[EnemyAttack] No ParryVisualFeedback found on target {target.name}!");
        }
        
        StartCoroutine(ExecuteAttackSequence());
    }
    
    /// <summary>
    /// DEPRECATED: Use StartAttack(GameObject target, Action action) instead
    /// This method should not be used as it doesn't specify which player to target
    /// </summary>
    [System.Obsolete("Use StartAttack(GameObject target, Action action) to specify the exact target")]
    public void StartAttack(Action action)
    {
        Debug.LogError("[EnemyAttack] StartAttack(Action) is deprecated and should not be used! Use StartAttack(GameObject target, Action action) instead.");
        
        // Don't execute the attack to force proper usage
        return;
        
       
    }
    
    /// <summary>
    /// DEPRECATED: Use StartAttack(GameObject target, Action action) instead
    /// </summary>
    [System.Obsolete("Use StartAttack(GameObject target, Action action) to specify the exact target")]
    public void StartAttack()
    {
        Debug.LogError("[EnemyAttack] StartAttack() is deprecated and should not be used! Use StartAttack(GameObject target, Action action) instead.");
        return;
    }
    
    private IEnumerator ExecuteAttackSequence()
    {
        isAttacking = true;
        bool wasParried = false;

        // Damage was rolled by EnemyManager and handed to us via StartAttack, so the number the
        // parry window reports is the number that will actually be applied.
        float damage = incomingDamage;

        if (showDebugInfo)
        {
            Debug.Log($"[EnemyAttack] Damage for this attack: {damage}");
        }

        // Get the windup time from the current action
        float windupTime = AttackWindupTime;
        
        if (showDebugInfo)
        {
            string actionName = currentAction != null ? currentAction.actionName : "Default Attack";
            Debug.Log($"[EnemyAttack] {actionName} started - windup time: {windupTime}s");
            Debug.Log($"[EnemyAttack] Calling StartAttackCountdown on: {targetVisualFeedback?.name}");
        }
        
        OnAttackStart?.Invoke();

        // Arm the target's single parry attempt for the whole windup, not just the timing window.
        // Claiming the input target this early is what lets an over-eager press be caught and
        // spent rather than silently ignored.
        if (targetParrySystem != null)
        {
            targetParrySystem.BeginParrySequence(damage);
        }

        // Start the visual countdown immediately when attack begins
        if (targetVisualFeedback != null)
        {
            targetVisualFeedback.StartAttackCountdown(windupTime);
        }
        else
        {
            Debug.LogWarning("[EnemyAttack] targetVisualFeedback is null, cannot start countdown!");
        }

        // Attack windup phase
        float windupEndTime = windupTime - parryWindowStartOffset;
        yield return new WaitForSeconds(windupEndTime);

        // Open the timing window — from here a press counts as a hit rather than a miss
        if (targetParrySystem != null)
        {
            targetParrySystem.OpenParryWindow();

            if (showDebugInfo)
            {
                Debug.Log($"[EnemyAttack] Parry window opened - incoming damage: {damage}");
            }
        }

        // Wait for remaining windup time
        yield return new WaitForSeconds(parryWindowStartOffset);

        // Check if attack was parried before hitting. Read the outcome directly rather than
        // inferring it from the window being shut — the window also shuts on timeout, so that
        // inference only held while parryWindowDuration happened to exceed parryWindowStartOffset.
        if (targetParrySystem != null && targetParrySystem.ParrySucceeded)
        {
            wasParried = true;

            if (showDebugInfo)
            {
                Debug.Log("[EnemyAttack] Attack was parried!");
            }

            targetParrySystem.EndParrySequence();

            // Early exit - skip all hit feedback and damage
            isAttacking = false;
            currentAction = null;
            targetParrySystem = null;
            targetVisualFeedback = null;

            OnAttackEnd?.Invoke();
            OnAttackComplete?.Invoke(wasParried);
            yield break;  // <-- EXIT EARLY, skip the rest of the coroutine
        }
        
        // Attack hits (only reached if NOT parried)
        if (showDebugInfo)
        {
            Debug.Log("[EnemyAttack] Attack hit!");
        }
        
        OnAttackHit?.Invoke();

        // Attack landed — close the window and release the target's parry attempt.
        if (targetParrySystem != null)
        {
            targetParrySystem.CloseParryWindow();
            targetParrySystem.EndParrySequence();
        }

        // Damage itself is applied by EnemyManager.OnAttackComplete once parry state is known.

        // Attack hit duration
        yield return new WaitForSeconds(attackHitTime);
        
        // Recovery phase
        yield return new WaitForSeconds(attackRecoveryTime);
        
        // Attack complete
        isAttacking = false;
        currentAction = null;
        targetParrySystem = null;
        targetVisualFeedback = null;
        
        if (showDebugInfo)
        {
            Debug.Log("[EnemyAttack] Attack complete - Was parried: false");
            Debug.Log("[EnemyAttack] Cleared target references");
        }
        
        OnAttackEnd?.Invoke();
        OnAttackComplete?.Invoke(wasParried);
    }
    
    /// <summary>
    /// Cancels the current attack (useful for interruptions)
    /// </summary>
    public void CancelAttack()
    {
        if (!isAttacking) return;
    
        StopAllCoroutines();
        isAttacking = false;
        currentAction = null;

        // Close any open parry window and release the target's attempt — otherwise an interrupted
        // attack would leave them armed and holding the input target indefinitely.
        if (targetParrySystem != null)
        {
            targetParrySystem.CloseParryWindow();
            targetParrySystem.EndParrySequence();
        }

        OnAttackEnd?.Invoke();
    }
}