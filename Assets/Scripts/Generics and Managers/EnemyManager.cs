using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] private Enemy enemyData;
    [SerializeField] private float enemyActionDelay = 1.25f;
    
    [Header("Attack System")]
    [SerializeField] private bool useParrySystem = true;
    
    private readonly List<GameObject> _playerCharacters = new List<GameObject>();
    private Action _selectedAction;
    private TurnManager _turnManager;
    private EnemyAttack _enemyAttack;
    private CharacterManager _characterManager;

    private void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        if (_turnManager == null)
        {
            Debug.LogError("No _turnManager found in the scene!");
        }
        
        _characterManager = GetComponent<CharacterManager>();
        if (_characterManager == null)
        {
            Debug.LogError("No CharacterManager found on this GameObject!");
        }
        
        // Get or add EnemyAttack component
        _enemyAttack = GetComponent<EnemyAttack>();
        if (_enemyAttack == null && useParrySystem)
        {
            _enemyAttack = gameObject.AddComponent<EnemyAttack>();
            Debug.Log($"[EnemyManager] Added EnemyAttack component to {gameObject.name}");
        }
        
        // Subscribe to attack completion event if using parry system
        if (_enemyAttack != null)
        {
            _enemyAttack.OnAttackComplete += OnAttackComplete;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_enemyAttack != null)
        {
            _enemyAttack.OnAttackComplete -= OnAttackComplete;
        }
    }

    void Start()
    {
        // Get enemy data from CharacterManager
        enemyData = GetComponent<CharacterManager>().characterData as Enemy;
        if (enemyData == null)
        {
            Debug.LogError($"[EnemyManager] No Enemy scriptable object found on {gameObject.name}! Make sure to assign an Enemy (not Character) to CharacterManager.");
        }
        
        RefreshTargetList();
    }

    private void ExecuteEnemyAction()
    {
        // Check if this enemy is still alive before executing action
        if (_characterManager == null || _characterManager.gameObject == null)
        {
            Debug.Log($"[EnemyManager] Enemy {gameObject.name} is dead, skipping turn...");
            _turnManager.CompleteTurn();
            return;
        }
        
        // Refresh the target list to remove any destroyed characters
        RefreshTargetList();
        
        // Check if there are any valid targets left
        if (_playerCharacters.Count == 0)
        {
            Debug.Log($"[EnemyManager] No valid targets for {gameObject.name}, completing turn...");
            _turnManager.CompleteTurn();
            return;
        }
        
        // Choose the best action for this turn
        _selectedAction = ChooseBestAction();
        
        if (_selectedAction == null)
        {
            Debug.LogWarning($"[EnemyManager] No valid actions available for {gameObject.name}, skipping turn...");
            _turnManager.CompleteTurn();
            return;
        }
        
        if (enemyData != null && enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} chose action: {_selectedAction.actionName}");
        }
        
        // Execute the chosen action
        CharacterManager target = SelectTarget(_selectedAction);
        if (target != null)
        {
            PerformAction(target);
        }
        else
        {
            Debug.LogWarning($"[EnemyManager] No valid target found for action {_selectedAction.actionName}, completing turn...");
            _turnManager.CompleteTurn();
        }
    }

    public void RefreshTargetList()
    {
        _playerCharacters.Clear();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        // Filter out null/destroyed objects and ensure they have valid CharacterManager components
        foreach (GameObject player in players)
        {
            if (player != null && 
                player.GetComponent<CharacterManager>() != null &&
                player.GetComponent<CharacterManager>().gameObject != null &&
                player.GetComponent<CharacterManager>().characterData != null)
            {
                _playerCharacters.Add(player);
            }
        }
        
        if (enemyData != null && enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} found {_playerCharacters.Count} valid player targets");
        }
    }
    
    public void EnemyTurnAction()
    {
        // Check if this enemy is still alive before starting turn actions
        if (_characterManager == null || _characterManager.gameObject == null)
        {
            Debug.Log($"[EnemyManager] Enemy {gameObject.name} is dead, cannot take turn");
            _turnManager.CompleteTurn();
            return;
        }
        
        Invoke(nameof(ExecuteEnemyAction), enemyActionDelay);
    }

    
    
    /// <summary>
    /// Chooses the best action based on current battle conditions and Enemy scriptable object settings
    /// </summary>
    private Action ChooseBestAction()
    {
        if (enemyData == null) return null;
        
        // Get all available actions (non-null action slots that are NOT on cooldown)
        List<Action> availableActions = new List<Action>();
        
        // Get actions from action slots
        if (enemyData.actionSlots != null)
        {
            foreach (Action action in enemyData.actionSlots)
            {
                if (action != null && enemyData.IsActionAvailable(action))
                {
                    availableActions.Add(action);
                }
                else if (action != null && enemyData.showDebugInfo)
                {
                    int cooldownRemaining = enemyData.GetActionCooldownRemaining(action);
                    Debug.Log($"[EnemyManager] {gameObject.name} - {action.actionName} on cooldown ({cooldownRemaining} turns remaining)");
                }
            }
        }
        
        // Also get actions from Character.GetActions() if available
        if (enemyData.GetActions() != null)
        {
            foreach (Action action in enemyData.GetActions())
            {
                if (action != null && 
                    !availableActions.Contains(action) && 
                    enemyData.IsActionAvailable(action))
                {
                    availableActions.Add(action);
                }
            }
        }
        
        if (availableActions.Count == 0)
        {
            Debug.LogWarning($"[EnemyManager] No available actions for {gameObject.name} (all on cooldown or null)!");
            return null;
        }
        
        if (enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} has {availableActions.Count} available actions: {string.Join(", ", availableActions.ConvertAll(a => a.actionName))}");
        }
        
        // Get current health percentage
        float healthPercentage = _characterManager != null ? 
            _characterManager.GetCurrentHealth() / _characterManager.MaxHealth : 1f;
        
        // Check for emergency healing
        if (healthPercentage <= enemyData.healThreshold)
        {
            var healActions = availableActions.Where(a => a.actionType == Action.ActionType.Heal).ToList();
            if (healActions.Count > 0)
            {
                if (enemyData.showDebugInfo)
                {
                    Debug.Log($"[EnemyManager] {gameObject.name} needs healing ({healthPercentage:P}) - choosing from {healActions.Count} available heal actions");
                }
                return healActions[Random.Range(0, healActions.Count)];
            }
        }
        
        // Calculate weights for each action type
        List<(Action action, float weight)> actionWeights = new List<(Action, float)>();
        
        foreach (var action in availableActions)
        {
            float baseWeight = GetBaseWeight(action.actionType);
            
            // Add some randomness to make AI less predictable
            float randomModifier = 1f + Random.Range(-enemyData.randomnessAmount, enemyData.randomnessAmount);
            float finalWeight = baseWeight * randomModifier;
            
            actionWeights.Add((action, Mathf.Max(0.01f, finalWeight))); // Ensure minimum weight
            
            if (enemyData.showDebugInfo)
            {
                Debug.Log($"[EnemyManager] {action.actionName} - Base: {baseWeight:F2}, Final: {finalWeight:F2}");
            }
        }
        
        // Choose action based on weights
        return ChooseWeightedAction(actionWeights);
    }
    
    /// <summary>
    /// Gets the base weight for an action type from the Enemy scriptable object
    /// </summary>
    private float GetBaseWeight(Action.ActionType actionType)
    {
        if (enemyData == null) return 0.1f;
        
        return actionType switch
        {
            Action.ActionType.Attack => enemyData.attackWeight,
            Action.ActionType.Heal => enemyData.healWeight,
            Action.ActionType.Buff => enemyData.buffWeight,
            Action.ActionType.Debuff => enemyData.debuffWeight,
            _ => 0.1f
        };
    }
    
    /// <summary>
    /// Chooses an action based on weighted probabilities
    /// </summary>
    private Action ChooseWeightedAction(List<(Action action, float weight)> actionWeights)
    {
        float totalWeight = actionWeights.Sum(aw => aw.weight);
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        foreach (var (action, weight) in actionWeights)
        {
            currentWeight += weight;
            if (randomValue <= currentWeight)
            {
                return action;
            }
        }
        
        // Fallback to first action
        return actionWeights[0].action;
    }
    
    /// <summary>
    /// Selects the appropriate target based on the action type and strategic considerations
    /// </summary>
    private CharacterManager SelectTarget(Action action)
    {
        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                // Target player based on strategy
                RefreshTargetList(); // Refresh again to be extra sure
                if (_playerCharacters.Count > 0)
                {
                    var validTargets = _playerCharacters
                        .Where(p => p != null && 
                                   p.GetComponent<CharacterManager>() != null &&
                                   p.GetComponent<CharacterManager>().gameObject != null &&
                                   p.GetComponent<CharacterManager>().characterData != null)
                        .Select(p => p.GetComponent<CharacterManager>())
                        .ToList();
                
                    if (validTargets.Count > 0)
                    {
                        return SelectPlayerTarget(validTargets);
                    }
                    else
                    {
                        Debug.LogWarning($"[EnemyManager] No valid player targets found for {gameObject.name}");
                    }
                }
                break;
            
            case Action.TargetType.SingleAlly:
                // Target self or ally (for now just target self)
                if (_characterManager != null && _characterManager.gameObject != null)
                {
                    return _characterManager;
                }
                break;
            
            case Action.TargetType.AllAllies:
            case Action.TargetType.AllEnemies:
                // For AoE actions, we can still return the primary target
                // The action execution logic can handle multiple targets
                return _characterManager;
        }
    
        return null;
    }

    /// <summary>
    /// Selects a player target based on strategic AI behavior
    /// </summary>
    private CharacterManager SelectPlayerTarget(List<CharacterManager> validTargets)
    {
        if (validTargets.Count == 0) return null;
        if (validTargets.Count == 1) return validTargets[0];
    
        // If enemyData has targeting preferences, use them
        if (enemyData != null)
        {
            switch (enemyData.targetingStrategy)
            {
                case Enemy.TargetingStrategy.LowestHealth:
                    return GetLowestHealthTarget(validTargets);
                
                case Enemy.TargetingStrategy.HighestHealth:
                    return GetHighestHealthTarget(validTargets);
                
                case Enemy.TargetingStrategy.Random:
                    return validTargets[Random.Range(0, validTargets.Count)];
                
                case Enemy.TargetingStrategy.ClosestToDefeating:
                    return GetClosestToDefeatingTarget(validTargets);
                
                default:
                    return validTargets[Random.Range(0, validTargets.Count)];
            }
        }
    
        // Default to random if no strategy specified
        return validTargets[Random.Range(0, validTargets.Count)];
    }

    /// <summary>
    /// Gets the player with the lowest current health
    /// </summary>
    private CharacterManager GetLowestHealthTarget(List<CharacterManager> targets)
    {
        CharacterManager lowestHealthTarget = targets[0];
        float lowestHealth = lowestHealthTarget.GetCurrentHealth();
    
        foreach (var target in targets)
        {
            float currentHealth = target.GetCurrentHealth();
            if (currentHealth < lowestHealth)
            {
                lowestHealth = currentHealth;
                lowestHealthTarget = target;
            }
        }
    
        if (enemyData != null && enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} targeting {lowestHealthTarget.name} with lowest health: {lowestHealth}");
        }
    
        return lowestHealthTarget;
    }

    /// <summary>
    /// Gets the player with the highest current health
    /// </summary>
    private CharacterManager GetHighestHealthTarget(List<CharacterManager> targets)
    {
        CharacterManager highestHealthTarget = targets[0];
        float highestHealth = highestHealthTarget.GetCurrentHealth();
    
        foreach (var target in targets)
        {
            float currentHealth = target.GetCurrentHealth();
            if (currentHealth > highestHealth)
            {
                highestHealth = currentHealth;
                highestHealthTarget = target;
            }
        }
    
        if (enemyData != null && enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} targeting {highestHealthTarget.name} with highest health: {highestHealth}");
        }
    
        return highestHealthTarget;
    }

    /// <summary>
    /// Gets the player who is closest to being defeated (lowest health percentage)
    /// </summary>
    private CharacterManager GetClosestToDefeatingTarget(List<CharacterManager> targets)
    {
        CharacterManager closestToDeathTarget = targets[0];
        float lowestHealthPercentage = closestToDeathTarget.GetCurrentHealth() / closestToDeathTarget.MaxHealth;
    
        foreach (var target in targets)
        {
            float healthPercentage = target.GetCurrentHealth() / target.MaxHealth;
            if (healthPercentage < lowestHealthPercentage)
            {
                lowestHealthPercentage = healthPercentage;
                closestToDeathTarget = target;
            }
        }
    
        if (enemyData != null && enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} targeting {closestToDeathTarget.name} closest to defeat: {lowestHealthPercentage:P}");
        }
    
        return closestToDeathTarget;
    }

    private void PerformAction(CharacterManager targetManager)
    {
        if (targetManager == null)
        {
            Debug.LogError($"Target CharacterManager is null!");
            return;
        }

        // Get the current character's modified stats
        CharacterManager currentCharacter = GetComponent<CharacterManager>();
        if (currentCharacter == null)
        {
            Debug.LogError("Current CharacterManager is null!");
            return;
        }
        
        currentCharacter.RefreshStats(); // Make sure we have current stats

        switch (_selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                if (useParrySystem && _enemyAttack != null)
                {
                    // Use the parry system for attacks
                    PerformParryableAttack(targetManager);
                }
                else
                {
                    // Use the old direct damage system
                    PerformDirectAttack(targetManager);
                }
                break;
                
            case Action.ActionType.Heal:
                int healRoll = RollForCritical();
                float healAmount = Random.Range(_selectedAction.minHeal, _selectedAction.maxHeal);
                if (healRoll == 20)
                {
                    Debug.Log("Critical Heal! Double healing!");
                    healAmount *= 2;
                }
                targetManager.Heal(healAmount);
                _turnManager.CompleteTurn();
                break;
                
            case Action.ActionType.Buff:
                targetManager.AddBuff(_selectedAction.buffType, _selectedAction.buffValue, _selectedAction.duration);
                _turnManager.CompleteTurn();
                break;
                
            case Action.ActionType.Debuff:
                targetManager.AddBuff(_selectedAction.buffType, -_selectedAction.buffValue, _selectedAction.duration);
                _turnManager.CompleteTurn();
                break;
        }
    }
    
    private void PerformParryableAttack(CharacterManager targetManager)
    {
        // Calculate damage that will be applied if attack hits
        int attackRoll = RollForCritical();
        if (attackRoll == 1)
        {
            Debug.Log($"[EnemyManager] {gameObject.name}'s attack missed!");
            _turnManager.CompleteTurn();
            return;
        }
        
        CharacterManager currentCharacter = GetComponent<CharacterManager>();
        float baseDamage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
        float modifiedDamage = baseDamage + (currentCharacter.AttackPower * 0.1f);
        
        if (attackRoll == 20)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} scored a critical hit!");
            modifiedDamage *= 2;
        }
        
        // Store damage and target for completion callback
        _pendingDamage = modifiedDamage;
        _pendingTarget = targetManager;
        
        // Start the parryable attack sequence with the selected action
        _enemyAttack.StartAttack(targetManager.gameObject, _selectedAction);
    }
    
    private void PerformDirectAttack(CharacterManager targetManager)
    {
        int attackRoll = RollForCritical();
        if (attackRoll == 1)
        {
            Debug.Log("Critical Fail! Attack missed.");
            _turnManager.CompleteTurn();
            return;
        }
        
        // Get the current character's modified stats
        CharacterManager currentCharacter = GetComponent<CharacterManager>();
        
        // Use modified attack power for damage calculation
        float baseDamage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
        float modifiedDamage = baseDamage + (currentCharacter.AttackPower * 0.1f);
        
        if (attackRoll == 20)
        {
            Debug.Log("Critical Hit! Double damage!");
            modifiedDamage *= 2;
        }
        
        targetManager.TakeDamage(modifiedDamage);
        _turnManager.CompleteTurn();
    }
    
    // Store pending damage and target for parryable attacks
    private float _pendingDamage;
    private CharacterManager _pendingTarget;
    
    /// <summary>
    /// Called when an EnemyAttack completes (either hits or is parried)
    /// </summary>
    private void OnAttackComplete(bool wasParried)
    {
        if (wasParried)
        {
            Debug.Log($"[EnemyManager] {gameObject.name}'s attack was parried by {_pendingTarget.name}!");
            
            // Call the parry method instead of TakeDamage
            if (_pendingTarget != null)
            {
                _pendingTarget.OnAttackParried();
            }
        }
        else
        {
            Debug.Log($"[EnemyManager] {gameObject.name}'s attack hit {_pendingTarget.name} for {_pendingDamage} damage!");
            
            // Apply the calculated damage with wasParried = false (default)
            if (_pendingTarget != null)
            {
                _pendingTarget.TakeDamage(_pendingDamage, false);
            }
        }
        
        // Clear pending values
        _pendingDamage = 0f;
        _pendingTarget = null;
        
        // Complete the turn
        _turnManager.CompleteTurn();
    }
    
    private int RollForCritical()
    {
        return Random.Range(1, 21); // Returns 1-20
    }
}