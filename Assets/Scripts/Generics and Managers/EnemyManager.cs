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

    public void RefreshTargetList()
    {
        _playerCharacters.Clear();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        // Filter out null/destroyed objects
        foreach (GameObject player in players)
        {
            if (player != null && player.GetComponent<CharacterManager>() != null)
            {
                _playerCharacters.Add(player);
            }
        }
    }
    
    public void EnemyTurnAction()
    {
        Invoke(nameof(ExecuteEnemyAction), enemyActionDelay);
    }

    private void ExecuteEnemyAction()
    {
        // Refresh the target list to remove any destroyed characters
        RefreshTargetList();
        
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
    /// Selects the appropriate target based on the action type
    /// </summary>
    private CharacterManager SelectTarget(Action action)
    {
        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                // Target random player
                RefreshTargetList();
                if (_playerCharacters.Count > 0)
                {
                    var validTargets = _playerCharacters.Where(p => p != null && p.GetComponent<CharacterManager>() != null).ToList();
                    if (validTargets.Count > 0)
                    {
                        return validTargets[Random.Range(0, validTargets.Count)].GetComponent<CharacterManager>();
                    }
                }
                break;
                
            case Action.TargetType.SingleAlly:
                // Target self or ally (for now just target self)
                return _characterManager;
                
            case Action.TargetType.AllAllies:
            case Action.TargetType.AllEnemies:
                // For AoE actions, we can still return the primary target
                // The action execution logic can handle multiple targets
                return _characterManager; // or a primary enemy target
        }
        
        return null;
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
            // No damage dealt
        }
        else
        {
            Debug.Log($"[EnemyManager] {gameObject.name}'s attack hit {_pendingTarget.name} for {_pendingDamage} damage!");
            // Apply the calculated damage
            if (_pendingTarget != null)
            {
                _pendingTarget.TakeDamage(_pendingDamage);
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