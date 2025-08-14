using UnityEngine;
using System.Collections;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Timing")]
    [SerializeField] private float attackWindupTime = 1.0f;
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
    
    public float AttackWindupTime => attackWindupTime;
    public float AttackHitTime => attackHitTime;
    public float AttackRecoveryTime => attackRecoveryTime;
    public float AttackDamage => attackDamage;
    public bool IsAttacking => isAttacking;
    
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
    /// Starts an attack sequence against the specified target
    /// </summary>
    public void StartAttack(GameObject target)
    {
        if (isAttacking)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[EnemyAttack] Already attacking!");
            }
            return;
        }
        
        // Find target's parry system and visual feedback
        targetParrySystem = target.GetComponent<ParrySystem>();
        targetVisualFeedback = target.GetComponent<ParryVisualFeedback>();
        if (targetVisualFeedback == null)
        {
            targetVisualFeedback = target.GetComponentInChildren<ParryVisualFeedback>();
        }
        
        StartCoroutine(ExecuteAttackSequence());
    }
    
    /// <summary>
    /// Starts an attack sequence (use this when target is determined by other means)
    /// </summary>
    public void StartAttack()
    {
        if (isAttacking)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[EnemyAttack] Already attacking!");
            }
            return;
        }
        
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
    }
    
    private IEnumerator ExecuteAttackSequence()
    {
        isAttacking = true;
        bool wasParried = false;
        
        if (showDebugInfo)
        {
            Debug.Log("[EnemyAttack] Attack started - beginning windup");
        }
        
        OnAttackStart?.Invoke();
        
        // Start the visual countdown immediately when attack begins
        if (targetVisualFeedback != null)
        {
            targetVisualFeedback.StartAttackCountdown(attackWindupTime);
        }
        
        // Attack windup phase
        float windupEndTime = attackWindupTime - parryWindowStartOffset;
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
        
        // Attack complete
        isAttacking = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"[EnemyAttack] Attack complete - Was parried: {wasParried}");
        }
        
        OnAttackEnd?.Invoke();
        OnAttackComplete?.Invoke(wasParried);
    }
    
    private void DealDamageToTarget()
    {
        if (targetParrySystem != null)
        {
            // Try to find a HealthManager component on the target
            var healthManager = targetParrySystem.GetComponent<HealthManager>();
            if (healthManager != null)
            {
                // Assuming HealthManager has a method to take damage
                // You may need to adjust this based on your HealthManager's API
                if (showDebugInfo)
                {
                    Debug.Log($"[EnemyAttack] Dealing {attackDamage} damage to {targetParrySystem.name}");
                }
                
                // If HealthManager has a TakeDamage method, use it:
                // healthManager.TakeDamage(attackDamage);
                
                // Or if it has a different method, adjust accordingly
            }
            else
            {
                // Alternative: try to find CharacterManager and deal damage through it
                var targetCharacterManager = targetParrySystem.GetComponent<CharacterManager>();
                if (targetCharacterManager != null)
                {
                    // Implement damage dealing through your character system
                    if (showDebugInfo)
                    {
                        Debug.Log($"[EnemyAttack] Would deal {attackDamage} damage to {targetCharacterManager.name}");
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