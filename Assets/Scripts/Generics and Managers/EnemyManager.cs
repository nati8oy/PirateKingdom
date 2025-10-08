using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    
    // Add flag to track if we're currently showing enemy tooltip
    private bool _isShowingEnemyTooltip = false;
    
    private void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        if (_turnManager == null)
        {
            Debug.LogError("[EnemyManager] TurnManager not found in scene!");
        }
        
        _characterManager = GetComponent<CharacterManager>();
        if (_characterManager == null)
        {
            Debug.LogError("[EnemyManager] CharacterManager component not found on enemy!");
        }
        
        // Get or add EnemyAttack component
        _enemyAttack = GetComponent<EnemyAttack>();
        if (_enemyAttack == null && useParrySystem)
        {
            _enemyAttack = gameObject.AddComponent<EnemyAttack>();
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
        
        // Clear tooltip flag when destroyed
        _isShowingEnemyTooltip = false;
    }
    
    void Start()
    {
        // Get enemy data from CharacterManager
        enemyData = GetComponent<CharacterManager>().characterData as Enemy;
        if (enemyData == null)
        {
            Debug.LogError($"[EnemyManager] Enemy data not found on {gameObject.name}! Make sure the Character assigned is an Enemy scriptable object.");
        }
        
        RefreshTargetList();
    }

    private void ExecuteEnemyAction()
    {
        // Check if this enemy is still alive before executing action
        if (_characterManager == null || _characterManager.gameObject == null)
        {
            Debug.Log($"[EnemyManager] Enemy {gameObject.name} is dead, completing turn");
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            return;
        }
        
        // Refresh the target list to remove any destroyed characters
        RefreshTargetList();
        
        // Check if there are any valid targets left
        if (_playerCharacters.Count == 0)
        {
            Debug.Log($"[EnemyManager] No valid targets found for {gameObject.name}, completing turn");
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            return;
        }
        
        // Choose the best action for this turn
        _selectedAction = ChooseBestAction();
        
        if (_selectedAction == null)
        {
            Debug.LogWarning($"[EnemyManager] No valid action found for {gameObject.name}, completing turn");
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            return;
        }

        // Show the selected enemy action in the tooltip and mark that we're showing it
        if (TooltipUI.Instance != null && _characterManager != null && _characterManager.characterData != null)
        {
            _isShowingEnemyTooltip = true;
            TooltipUI.Instance.ShowEnemyActionTooltip(_selectedAction, _characterManager.characterData.characterName);
            Debug.Log($"[EnemyManager] Showing tooltip for {_characterManager.characterData.characterName}'s action: {_selectedAction.actionName}");
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
            Debug.LogWarning($"[EnemyManager] No valid target found for action {_selectedAction.actionName}, completing turn");
            _isShowingEnemyTooltip = false;
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
        
        // Start the enemy turn process
        Debug.Log($"[EnemyManager] Starting turn for {gameObject.name}");
        Invoke(nameof(ExecuteEnemyAction), enemyActionDelay);
    }
    
    // Public method to check if this enemy is currently showing tooltip
    public bool IsShowingEnemyTooltip()
    {
        return _isShowingEnemyTooltip;
    }
    
    // Public method to get the current selected action for tooltip purposes
    public Action GetSelectedAction()
    {
        return _selectedAction;
    }
    
    // Public method to get enemy name for tooltip
    public string GetEnemyName()
    {
        return _characterManager?.characterData?.characterName ?? gameObject.name;
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
            foreach (var action in enemyData.actionSlots)
            {
                if (action != null && enemyData.IsActionAvailable(action))
                {
                    availableActions.Add(action);
                }
            }
        }
        
        // Also get actions from Character.GetActions() if available
        if (enemyData.GetActions() != null)
        {
            foreach (var action in enemyData.GetActions())
            {
                if (action != null && enemyData.IsActionAvailable(action) && !availableActions.Contains(action))
                {
                    availableActions.Add(action);
                }
            }
        }
        
        if (availableActions.Count == 0)
        {
            Debug.LogWarning($"[EnemyManager] No available actions for {gameObject.name}!");
            return null;
        }
        
        if (enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} has {availableActions.Count} available actions");
        }
        
        // Get current health percentage
        float healthPercentage = _characterManager != null ? 
            _characterManager.GetCurrentHealth() / _characterManager.MaxHealth : 1f;
        
        // Check for emergency healing
        if (healthPercentage <= enemyData.healThreshold)
        {
            var healingActions = availableActions.Where(a => a.actionType == Action.ActionType.Heal).ToList();
            if (healingActions.Any())
            {
                if (enemyData.showDebugInfo)
                {
                    Debug.Log($"[EnemyManager] {gameObject.name} is low on health ({healthPercentage:P}), prioritizing healing");
                }
                return healingActions[Random.Range(0, healingActions.Count)];
            }
        }
        
        // Calculate weights for each action type
        List<(Action action, float weight)> actionWeights = new List<(Action, float)>();
        
        foreach (var action in availableActions)
        {
            float baseWeight = GetBaseWeight(action.actionType);
            
            // Apply situational modifiers
            float situationalModifier = 1f;
            
            // Prefer attacks when enemies are healthy
            if (action.actionType == Action.ActionType.Attack && healthPercentage > 0.5f)
            {
                situationalModifier *= 1.2f;
            }
            
            // Prefer buffs at the start of combat (high health)
            if (action.actionType == Action.ActionType.Buff && healthPercentage > 0.8f)
            {
                situationalModifier *= 1.3f;
            }
            
            float finalWeight = baseWeight * situationalModifier;
            actionWeights.Add((action, finalWeight));
            
            if (enemyData.showDebugInfo)
            {
                Debug.Log($"[EnemyManager] {action.actionName}: base={baseWeight:F2}, situational={situationalModifier:F2}, final={finalWeight:F2}");
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
    
    private CharacterManager SelectTarget(Action action)
    {
        List<CharacterManager> validTargets = new List<CharacterManager>();
        
        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                // Enemy targeting players
                foreach (var playerGO in _playerCharacters)
                {
                    var playerManager = playerGO.GetComponent<CharacterManager>();
                    if (playerManager != null)
                    {
                        validTargets.Add(playerManager);
                    }
                }
                return validTargets.Count > 0 ? SelectPlayerTarget(validTargets) : null;
                
            case Action.TargetType.SingleAlly:
                // Enemy targeting other enemies
                var enemyManagers = FindObjectsOfType<CharacterManager>()
                    .Where(cm => cm.gameObject.CompareTag("Enemy") && cm != _characterManager)
                    .ToList();
                return enemyManagers.Count > 0 ? enemyManagers[Random.Range(0, enemyManagers.Count)] : _characterManager;
                
            case Action.TargetType.AllEnemies:
                // This would target all players, but we return one for the targeting system
                return _playerCharacters.Count > 0 ? _playerCharacters[0].GetComponent<CharacterManager>() : null;
                
            case Action.TargetType.AllAllies:
                // This would target all enemies, but we return self for the targeting system
                return _characterManager;
                
            default:
                return null;
        }
    }
    
    private CharacterManager SelectPlayerTarget(List<CharacterManager> validTargets)
    {
        if (validTargets.Count == 0) return null;
        if (validTargets.Count == 1) return validTargets[0];
        
        // Check if we have Enemy data with targeting strategy
        if (enemyData is Enemy enemy)
        {
            // Use the Enemy's targeting strategy
            switch (enemy.targetingStrategy)
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
        
        // Fallback to random if no Enemy data or targeting strategy
        return validTargets[Random.Range(0, validTargets.Count)];
    }
    
    private CharacterManager GetLowestHealthTarget(List<CharacterManager> targets)
    {
        return targets.OrderBy(t => t.GetCurrentHealth()).First();
    }
    
    private CharacterManager GetHighestHealthTarget(List<CharacterManager> targets)
    {
        return targets.OrderByDescending(t => t.GetCurrentHealth()).First();
    }
    
    private CharacterManager GetClosestToDefeatingTarget(List<CharacterManager> targets)
    {
        if (_selectedAction == null) return GetLowestHealthTarget(targets);
        
        return targets.OrderBy(t => t.GetCurrentHealth() - _selectedAction.maxDamage).First();
    }
    
    private void PerformAction(CharacterManager targetManager)
    {
        if (_selectedAction == null || targetManager == null) return;
        
        if (useParrySystem && _selectedAction.actionType == Action.ActionType.Attack && _enemyAttack != null)
        {
            PerformParryableAttack(targetManager);
        }
        else
        {
            PerformDirectAttack(targetManager);
        }
    }
    
    
    
    
    private void PerformParryableAttack(CharacterManager targetManager)
    {
        if (_enemyAttack == null)
        {
            Debug.LogError("[EnemyManager] EnemyAttack component is null but trying to perform parryable attack!");
            PerformDirectAttack(targetManager);
            return;
        }
    
        // Calculate damage first (before potential parry)
        float damage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
    
        // Check for critical hit
        int critRoll = RollForCritical();
        if (critRoll == 20) // Natural 20 is a crit
        {
            damage *= 2;
            Debug.Log($"[EnemyManager] {gameObject.name} scored a critical hit!");
        }
    
        // Store damage and target for after parry resolution
        _pendingDamage = damage;
        _pendingTarget = targetManager;
    
        // FIXED: Use the targetManager's gameObject directly instead of selecting a new target
        if (targetManager != null && targetManager.gameObject != null)
        {
            if (enemyData != null && enemyData.showDebugInfo)
            {
                Debug.Log($"[EnemyManager] {gameObject.name} attacking {targetManager.characterData?.characterName} with parry system");
            }
        
            _enemyAttack.StartAttack(targetManager.gameObject, _selectedAction);
        }
        else
        {
            Debug.LogWarning("[EnemyManager] Target manager or gameObject is null!");
            PerformDirectAttack(targetManager);
        }
    }
    
    private void PerformDirectAttack(CharacterManager targetManager)
    {
        if (_selectedAction == null) return;
        
        switch (_selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                float damage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
                
                // Check for critical hit
                int critRoll = RollForCritical();
                if (critRoll == 20)
                {
                    damage *= 2;
                    Debug.Log($"[EnemyManager] {gameObject.name} scored a critical hit!");
                }
                
                targetManager.TakeDamage(damage);
                break;
                
            case Action.ActionType.Heal:
                float healing = Random.Range(_selectedAction.minHeal, _selectedAction.maxHeal);
                _characterManager.Heal(healing);
                break;
                
            case Action.ActionType.Buff:
            case Action.ActionType.Debuff:
                // Apply buff/debuff - try different possible method names
                CharacterManager buffTarget = _selectedAction.targetType == Action.TargetType.SingleAlly ? 
                    _characterManager : targetManager;
                
                try
                {
                    // Try different possible buff method names
                    if (HasMethod(buffTarget, "ApplyBuff"))
                    {
                        buffTarget.SendMessage("ApplyBuff", new object[] { 
                            _selectedAction.buffType, 
                            _selectedAction.buffValue, 
                            Mathf.RoundToInt(_selectedAction.duration), 
                            _selectedAction.actionType == Action.ActionType.Buff 
                        });
                    }
                    else if (HasMethod(buffTarget, "AddBuff"))
                    {
                        buffTarget.SendMessage("AddBuff", new object[] { 
                            _selectedAction.buffType, 
                            _selectedAction.buffValue, 
                            Mathf.RoundToInt(_selectedAction.duration) 
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"[EnemyManager] No suitable buff method found on {buffTarget.name}");
                    }
                }
                catch
                {
                    Debug.LogWarning($"[EnemyManager] Error applying buff to {buffTarget.name}");
                }
                break;
        }
        
        // Clear tooltip flag and complete turn after action
        _isShowingEnemyTooltip = false;
        _turnManager.CompleteTurn();
    }
    
    private bool HasMethod(object obj, string methodName)
    {
        return obj.GetType().GetMethod(methodName) != null;
    }
    
    // Store pending damage and target for parryable attacks
    private float _pendingDamage;
    private CharacterManager _pendingTarget;
    
    private void OnAttackComplete(bool wasParried)
    {
        if (_pendingTarget == null)
        {
            Debug.LogWarning("[EnemyManager] OnAttackComplete called but no pending target!");
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            return;
        }
        
        if (!wasParried)
        {
            // Attack was not parried, apply full damage
            _pendingTarget.TakeDamage(_pendingDamage);
            Debug.Log($"[EnemyManager] {gameObject.name} hit {_pendingTarget.characterData.characterName} for {_pendingDamage:F1} damage");
        }
        else
        {
            // Attack was parried, apply reduced damage
            float reducedDamage = _pendingDamage * 0.5f;
            _pendingTarget.TakeDamage(reducedDamage);
            Debug.Log($"[EnemyManager] {gameObject.name}'s attack was parried! Reduced damage: {reducedDamage:F1}");
        }
        
        // Clear pending data and tooltip flag
        _pendingDamage = 0f;
        _pendingTarget = null;
        _isShowingEnemyTooltip = false;
        
        // Complete turn
        _turnManager.CompleteTurn();
    }
    
    private int RollForCritical()
    {
        return Random.Range(1, 21); // d20 roll
    }
}