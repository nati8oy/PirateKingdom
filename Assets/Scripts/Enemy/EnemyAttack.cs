using UnityEngine;
using System.Collections;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Timing")]
    [SerializeField] private float attackHitTime = 0.1f;
    [SerializeField] private float attackRecoveryTime = 0.5f;
    
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowStartOffset = 0.2f; // How long before hit the parry window opens
    
    [Header("Attack Properties")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask targetLayers = 1;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private CharacterManager characterManager;
    private bool isAttacking = false;
    private ParrySystem targetParrySystem;
    private ParryVisualFeedback targetVisualFeedback;
    
    // Current action being executed
    private Action currentAction;
    
    public float AttackHitTime => attackHitTime;
    public float AttackRecoveryTime => attackRecoveryTime;
    public float AttackDamage => attackDamage;
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
    /// Starts an attack sequence against the specified target using the given action
    /// </summary>
    public void StartAttack(GameObject target, Action action)
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
    /// Starts an attack sequence against the specified target
    /// </summary>
    public void StartAttack(GameObject target)
    {
        // Use a default action if none provided
        StartAttack(target, null);
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
        
        // The old problematic code is commented out:
        /*
        if (isAttacking)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[EnemyAttack] Already attacking!");
            }
            return;
        }
        
        currentAction = action;
        
        // Find player target (assuming player has specific tag or component)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            targetParrySystem = player.GetComponent<ParrySystem>();
            targetVisualFeedback = player.GetComponent<ParryVisualFeedback>();
            if (targetVisualFeedback == null)
            {
                targetVisualFeedback = player.GetComponentInChildren<ParryVisualFeedback>();
            }
        }
        
        StartCoroutine(ExecuteAttackSequence());
        */
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
        
        // Get the windup time from the current action
        float windupTime = AttackWindupTime;
        
        if (showDebugInfo)
        {
            string actionName = currentAction != null ? currentAction.actionName : "Default Attack";
            Debug.Log($"[EnemyAttack] {actionName} started - windup time: {windupTime}s");
            Debug.Log($"[EnemyAttack] Calling StartAttackCountdown on: {targetVisualFeedback?.name}");
        }
        
        OnAttackStart?.Invoke();
        
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
        
        // Open parry window for target
        if (targetParrySystem != null)
        {
            targetParrySystem.OpenParryWindow();
            
            if (showDebugInfo)
            {
                Debug.Log("[EnemyAttack] Parry window opened for target");
            }
        }
        
        // Wait for remaining windup time
        yield return new WaitForSeconds(parryWindowStartOffset);
        
        // Check if attack was parried before hitting
        if (targetParrySystem != null && !targetParrySystem.IsParryWindowActive)
        {
            // Parry window closed early - attack was parried
            wasParried = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[EnemyAttack] Attack was parried!");
            }
        }
        else
        {
            // Attack hits
            if (showDebugInfo)
            {
                Debug.Log("[EnemyAttack] Attack hit!");
            }
            
            OnAttackHit?.Invoke();
            
            // Close parry window since attack landed
            if (targetParrySystem != null)
            {
                targetParrySystem.CloseParryWindow();
            }
            
            // Deal damage to target
            DealDamageToTarget();
        }
        
        // Attack hit duration
        yield return new WaitForSeconds(attackHitTime);
        
        // Recovery phase
        yield return new WaitForSeconds(attackRecoveryTime);
        
        // Attack complete - CLEAR target references
        isAttacking = false;
        currentAction = null;
        
        // IMPORTANT: Clear target references to prevent reuse
        targetParrySystem = null;
        targetVisualFeedback = null;
        
        if (showDebugInfo)
        {
            Debug.Log($"[EnemyAttack] Attack complete - Was parried: {wasParried}");
            Debug.Log("[EnemyAttack] Cleared target references");
        }
        
        OnAttackEnd?.Invoke();
        OnAttackComplete?.Invoke(wasParried);
    }
    
    private void DealDamageToTarget()
    {
        if (targetParrySystem != null)
        {
            // Calculate damage from action if available
            float damage = attackDamage;
            if (currentAction != null)
            {
                damage = Random.Range(currentAction.minDamage, currentAction.maxDamage);
            }
            
            // Try to find a HealthManager component on the target
            var healthManager = targetParrySystem.GetComponent<HealthManager>();
            if (healthManager != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[EnemyAttack] Dealing {damage} damage to {targetParrySystem.name}");
                }
                
                // If HealthManager has a TakeDamage method, use it:
                // healthManager.TakeDamage(damage);
            }
            else
            {
                // Alternative: try to find CharacterManager and deal damage through it
                var targetCharacterManager = targetParrySystem.GetComponent<CharacterManager>();
                if (targetCharacterManager != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[EnemyAttack] Would deal {damage} damage to {targetCharacterManager.name}");
                    }
                }
            }
        }
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
        
        // Close any open parry windows
        if (targetParrySystem != null && targetParrySystem.IsParryWindowActive)
        {
            targetParrySystem.CloseParryWindow();
        }
        
        if (showDebugInfo)
        {
            Debug.Log("[EnemyAttack] Attack cancelled");
        }
        
        OnAttackEnd?.Invoke();
    }
}