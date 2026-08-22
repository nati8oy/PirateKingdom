using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    // Cooldowns live on CharacterManager, shared with the player path — this class used to keep a
    // second, name-keyed cooldown dictionary that nothing ever called.
    [SerializeField] private Enemy enemyData;
    [SerializeField] private float enemyActionDelay = 1.25f;

    [Tooltip("Seconds the target indicator shows on the victim before this enemy starts its attack. " +
             "0 skips the telegraph entirely.")]
    [SerializeField] private float targetHighlightDuration = 0.6f;

    [Header("Attack System")]
    [SerializeField] private bool useParrySystem = true;
    private readonly List<GameObject> _playerCharacters = new List<GameObject>();
    private Action _selectedAction;
    private TurnManager _turnManager;
    private EnemyAttack _enemyAttack;
    private CharacterManager _characterManager;

    // Who this enemy is currently telegraphing at, so the indicator can be cleared on every exit.
    private CharacterManager _highlightedTarget;

    // Add flag to track if we're currently showing enemy tooltip
    private bool _isShowingEnemyTooltip = false;
    
    // Add this field at the top of the EnemyManager class
    private EnemyDriveManager enemyDriveManager;
    
    private void Awake()
    {
        // Initialize drive manager as early as possible
        CharacterManager characterManager = GetComponent<CharacterManager>();
        if (characterManager != null)
        {
            InitializeEnemyDriveManager(characterManager);
        }
        
        
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
        enemyDriveManager = GetComponent<EnemyDriveManager>();
    
        // Initialize with the config from enemy data
        if (enemyData != null)
        {
            enemyDriveManager.Initialize(enemyData.driveConfig);
        }
        
        // Get enemy data from CharacterManager
        enemyData = GetComponent<CharacterManager>().characterData as Enemy;
        if (enemyData == null)
        {
            Debug.LogError($"[EnemyManager] Enemy data not found on {gameObject.name}! Make sure the Character assigned is an Enemy scriptable object.");
        }
        
        RefreshTargetList();
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

    private void ExecuteEnemyAction()
    {
    
        // Evaluate drive usage AFTER turn buffs have been updated
        if (enemyDriveManager != null)
        {
            Debug.Log($"[EnemyManager] About to call EvaluateDriveUsage, enemyDriveManager is not null");
            enemyDriveManager.EvaluateDriveUsage();
        }
        else
        {
            Debug.LogWarning($"[EnemyManager] enemyDriveManager is NULL in ExecuteEnemyAction!");
        }
        
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
            // Put the action on cooldown. ChooseBestAction() has always filtered on
            // IsActionAvailable, but nothing ever marked an enemy action as used, so enemy
            // cooldowns never actually started — masked only because no enemy action has a
            // cooldown above 0 yet. Marked here rather than after resolution because the
            // parryable path finishes asynchronously.
            _characterManager.UseAction(_selectedAction);

            StartCoroutine(TelegraphThenAct(target));
        }
        else
        {
            Debug.LogWarning($"[EnemyManager] No valid target found for action {_selectedAction.actionName}, completing turn");
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
        }
    }
    
    /// <summary>
    /// Marks the victim, holds for <see cref="targetHighlightDuration"/> so the player can read who
    /// is about to be hit, then runs the action. The pause sits before the attack rather than
    /// inside it so it reads as a separate beat from the windup — the windup is the parry cue, this
    /// is the "who" cue.
    /// </summary>
    private System.Collections.IEnumerator TelegraphThenAct(CharacterManager target)
    {
        HighlightTarget(target);

        if (targetHighlightDuration > 0f)
        {
            yield return new WaitForSeconds(targetHighlightDuration);
        }

        // The target can die, or the battle can end, during the pause — this is the first place in
        // the enemy turn that yields, so it's the first place that has to cope with it.
        if (target == null || target.gameObject == null)
        {
            Debug.Log($"[EnemyManager] {gameObject.name}'s target vanished during the telegraph — completing turn.");
            ClearHighlight();
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            yield break;
        }

        PerformAction(target);
    }

    private void HighlightTarget(CharacterManager target)
    {
        ClearHighlight();

        _highlightedTarget = target;
        if (target != null) target.SetTargeted(true);
    }

    /// <summary>Clears this enemy's highlight. Safe to call when nothing is highlighted.</summary>
    private void ClearHighlight()
    {
        if (_highlightedTarget != null) _highlightedTarget.SetTargeted(false);
        _highlightedTarget = null;
    }

    // An enemy killed mid-telegraph would otherwise leave its victim highlighted for good.
    private void OnDisable() => ClearHighlight();

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
                // Use the same availability check as the player UI
                if (action != null && ActionsManager.IsActionAvailable(action, _characterManager))
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
                if (action != null && ActionsManager.IsActionAvailable(action, _characterManager) && !availableActions.Contains(action))
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

            // Lean on the drain once hurt — it's the only sustain that doesn't cost a turn of
            // damage. Deliberately not folded into the emergency-heal branch above: that one
            // guarantees a heal, and a drain only heals if it lands, so it's a poor panic button.
            if (action.actionType == Action.ActionType.HealthDrain && healthPercentage < 0.5f)
            {
                situationalModifier *= 1.4f;
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
            Action.ActionType.DriveGrant => enemyData.driveGrantWeight,
            Action.ActionType.DriveDrain => enemyData.driveDrainWeight,
            Action.ActionType.HealthDrain => enemyData.healthDrainWeight,
            Action.ActionType.ApplyStatus => enemyData.applyStatusWeight,
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

                validTargets = FilterHiddenTargets(action, validTargets);

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
    
    /// <summary>
    /// Drops hidden characters from a hostile action's candidate list — <b>unless every candidate is
    /// hidden</b>, in which case the list is returned untouched.
    /// </summary>
    /// <remarks>
    /// <b>The fallback is the point.</b> Without it, hiding the whole party means every enemy finds
    /// no target and forfeits its turn, which stalls the fight rather than adding tension. Stealth
    /// protects an individual by making someone else the better target; it was never meant to be a
    /// party-wide "skip the enemy's turn" button.
    ///
    /// Single-ally-only stealth makes an all-hidden party hard to reach, but not impossible — three
    /// crew stealthing each other in sequence gets there, and a fight that quietly stops working
    /// would be a miserable thing to diagnose. Three lines is cheap insurance.
    /// </remarks>
    private List<CharacterManager> FilterHiddenTargets(Action action, List<CharacterManager> candidates)
    {
        if (action == null || !action.IsHostile) return candidates;

        var visible = candidates.Where(c => c != null && !c.IsHiddenFromHostileActions).ToList();

        if (visible.Count > 0) return visible;

        Debug.Log($"[EnemyManager] Every target of {action.actionName} is hidden — attacking anyway, " +
                  "since forfeiting the turn would stall the fight.");

        return candidates;
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

                case Enemy.TargetingStrategy.HighestDrive:
                    return GetHighestDriveTarget(validTargets);

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

    /// <summary>
    /// Whoever has the most drive banked — the strategy that makes a DriveDrain or HealthDrain feel
    /// aimed. Falls back to a random pick when nobody has any drive at all.
    /// </summary>
    /// <remarks>
    /// The fallback matters more than it looks: with every meter at zero an ordered pick is
    /// deterministic, so the enemy would open every single fight by draining whichever crew member
    /// happened to sort first. Random keeps the opening turn from being scripted.
    /// </remarks>
    private CharacterManager GetHighestDriveTarget(List<CharacterManager> targets)
    {
        CharacterManager best = null;
        float bestDrive = 0f;

        foreach (CharacterManager candidate in targets)
        {
            DriveManager drive = candidate != null ? candidate.GetDriveManager() : null;
            if (drive?.Drive == null) continue;

            if (best == null || drive.Drive.CurrentDrive > bestDrive)
            {
                best = candidate;
                bestDrive = drive.Drive.CurrentDrive;
            }
        }

        if (best == null || bestDrive <= 0f)
        {
            return targets[Random.Range(0, targets.Count)];
        }

        return best;
    }
    
    private void PerformAction(CharacterManager targetManager)
    {
        if (_selectedAction == null || targetManager == null) return;

        // Same rule as the player: swinging gives away your position, healing an ally doesn't.
        if (_selectedAction.IsHostile) _characterManager?.BreakStealthFromHostileAction();
        
        // HealthDrain goes down the parryable path alongside Attack. It's a damaging swing with a
        // windup like any other, and an enemy attack the player can't parry reads as a bug. It also
        // falls out well: a parry cuts the damage to 25%, and since the enemy's healing is derived
        // from health actually lost, it cuts their heal by the same amount.
        bool isDamagingSwing = _selectedAction.actionType == Action.ActionType.Attack
                               || _selectedAction.actionType == Action.ActionType.HealthDrain;

        if (useParrySystem && isDamagingSwing && _enemyAttack != null)
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
    
        // To-hit is resolved before the attack sequence starts. A miss skips the parry window
        // entirely rather than opening one over nothing — a parry attempt is spent whether or not
        // it lands, so making the player burn theirs on an attack that was never going to connect
        // would be a straight loss for no decision.
        if (!RollToHit(targetManager, out int toHitRoll))
        {
            ResolveMiss(targetManager, toHitRoll);

            ClearHighlight();
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            return;
        }

        // Calculate damage first (before potential parry)
        float damage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);

        // Check for critical hit
        if (RollForCritical())
        {
            damage *= 2;
            Debug.Log($"[EnemyManager] {gameObject.name} scored a critical hit!");
        }

        // Apply drive mode multiplier if active
        DriveManager enemyDriveManager = _characterManager.GetComponent<DriveManager>();
        if (enemyDriveManager != null)
        {
            float driveMultiplier = enemyDriveManager.GetNextAttackDamageMultiplier();
            damage *= driveMultiplier;
            
            if (driveMultiplier > 1f)
            {
                Debug.Log($"[EnemyManager] {gameObject.name} applied {driveMultiplier}x drive multiplier! Damage: {damage}");
            }
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
        
            // Pass the already-rolled damage so the parry window reports the damage that will
            // actually land, and the target's parry drive bonus is scaled off the real number.
            _enemyAttack.StartAttack(targetManager.gameObject, _selectedAction, _pendingDamage);
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
            // HealthDrain shares this label — resolution is identical, and the siphon at the end is
            // a no-op for a plain Attack. This path only runs with the parry system off or missing;
            // normally both reach PerformParryableAttack instead.
            case Action.ActionType.HealthDrain:
            case Action.ActionType.Attack:
                if (!RollToHit(targetManager, out int toHitRoll))
                {
                    ResolveMiss(targetManager, toHitRoll);
                    break;
                }

                float damage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);

                // Check for critical hit
                if (RollForCritical())
                {
                    damage *= 2;
                    Debug.Log($"[EnemyManager] {gameObject.name} scored a critical hit!");
                }

                // Apply drive mode multiplier if active
                DriveManager enemyDriveManager = _characterManager.GetComponent<DriveManager>();
                if (enemyDriveManager != null)
                {
                    float driveMultiplier = enemyDriveManager.GetNextAttackDamageMultiplier();
                    damage *= driveMultiplier;
                    
                    if (driveMultiplier > 1f)
                    {
                        Debug.Log($"[EnemyManager] {gameObject.name} applied {driveMultiplier}x drive multiplier! Damage: {damage}");
                    }
                    
                    // Consume the drive buff after applying it
                    enemyDriveManager.OnAttackPerformed();
                }
                
                float healthTaken = targetManager.TakeDamage(damage);
                GrantDriveForDamageDealt(damage);

                // Landed hit, so riders roll. This path only runs with the parry system off or
                // missing; normally an enemy Attack or Health Drain resolves in OnAttackComplete.
                targetManager.ApplyStatusEffectsFrom(_selectedAction);

                ApplyHealthDrain(healthTaken);
                break;

            case Action.ActionType.Heal:
                float healing = Random.Range(_selectedAction.minHeal, _selectedAction.maxHeal);
                
                // Pass the healer's drive multiplier in rather than pre-multiplying: Heal() applies
                // it, and can't derive it itself because its own DriveManager is the target's.
                DriveManager healerDriveManager = _characterManager.GetComponent<DriveManager>();
                float healingMultiplier = healerDriveManager != null
                    ? healerDriveManager.GetNextHealingMultiplier()
                    : 1f;

                if (healingMultiplier > 1f)
                {
                    Debug.Log($"[EnemyManager] {gameObject.name} applied {healingMultiplier}x drive multiplier to healing!");
                }

                _characterManager.Heal(healing, healingMultiplier);

                // Consume the drive buff after applying it
                healerDriveManager?.OnAttackPerformed();
                break;
                
            // Drive actions call the target's CharacterManager directly — the same path the player
            // takes, and now the same path the buff branch below takes.
            //
            // targetManager is used as-is rather than being overridden to self for SingleAlly the
            // way the buff branch does, because SelectTarget already resolves SingleAlly to another
            // living enemy and only falls back to self when this one is alone. An enemy feeding its
            // ally's meter is the interesting case; forcing self would throw it away.
            case Action.ActionType.DriveGrant:
                float driveGranted = targetManager.GainDrive(_selectedAction.driveAmount);
                Debug.Log($"[EnemyManager] {gameObject.name} granted {driveGranted} drive to " +
                          $"{targetManager.characterData?.characterName}");
                break;

            case Action.ActionType.DriveDrain:
                float driveDrained = targetManager.LoseDrive(_selectedAction.driveAmount);
                Debug.Log($"[EnemyManager] {gameObject.name} drained {driveDrained} drive from " +
                          $"{targetManager.characterData?.characterName}");
                break;

            // Applies its riders and nothing else. SingleAlly resolves to another enemy (or this one
            // when alone) via SelectTarget, so an enemy hiding a wounded ally already works.
            case Action.ActionType.ApplyStatus:
                CharacterManager statusTarget = targetManager != null ? targetManager : _characterManager;

                statusTarget.ApplyStatusEffectsFrom(_selectedAction);
                Debug.Log($"[EnemyManager] {gameObject.name} applied {_selectedAction.actionName} to " +
                          $"{statusTarget.characterData?.characterName ?? statusTarget.name}");
                break;

            case Action.ActionType.Buff:
            case Action.ActionType.Debuff:
                // Called directly, matching the player path in CombatController. This used to go
                // through SendMessage with an object[] of three or four arguments — SendMessage only
                // ever passes ONE, so it matched no overload, and the bare catch below swallowed the
                // failure without even a log line. Every enemy buff and debuff silently did nothing.
                // Don't reintroduce reflection here.
                //
                // SingleAlly resolves to this enemy rather than SelectTarget's pick, so a buff is a
                // self-buff. That differs from the DriveGrant branch above, which honours the pick —
                // deliberately: feeding an ally's meter is the interesting case for drive, whereas a
                // protection or accuracy buff is almost always meant for the caster, and there is no
                // Self target type to say so explicitly.
                CharacterManager buffTarget = _selectedAction.targetType == Action.TargetType.SingleAlly
                    ? _characterManager
                    : targetManager;

                if (buffTarget != null)
                {
                    // Debuff negates, exactly as CombatController does — buffValue is authored
                    // positive for both.
                    float buffAmount = _selectedAction.actionType == Action.ActionType.Buff
                        ? _selectedAction.buffValue
                        : -_selectedAction.buffValue;

                    buffTarget.AddBuff(_selectedAction.buffType, buffAmount, _selectedAction.duration);
                }
                else
                {
                    Debug.LogWarning($"[EnemyManager] {gameObject.name} had no target for {_selectedAction.actionName}.");
                }
                break;
        }
        
        // Clear tooltip flag and complete turn after action
        ClearHighlight();
        _isShowingEnemyTooltip = false;
        _turnManager.CompleteTurn();
    }

    // Store pending damage and target for parryable attacks
    private float _pendingDamage;
    private CharacterManager _pendingTarget;
    
    
    private void OnAttackComplete(bool wasParried)
    {
        if (_pendingTarget == null)
        {
            Debug.LogWarning("[EnemyManager] OnAttackComplete called but no pending target!");
            ClearHighlight();
            _isShowingEnemyTooltip = false;
            _turnManager.CompleteTurn();
            return;
        }
        
        float healthLost;

        if (!wasParried)
        {
            // Attack was not parried, apply full damage
            healthLost = _pendingTarget.TakeDamage(_pendingDamage);
            GrantDriveForDamageDealt(_pendingDamage);
            Debug.Log($"[EnemyManager] {gameObject.name} hit {_pendingTarget.characterData.characterName} for {_pendingDamage:F1} damage");

            // Status riders land only on an unparried hit. A parry still eats the 25% chip but
            // stops the bleed outright — that's what makes parrying a status attack meaningfully
            // better than absorbing it, and parry is already the game's skill expression.
            _pendingTarget.ApplyStatusEffectsFrom(_selectedAction);
        }
        else
        {
            // Attack was parried, apply reduced damage (adjust multiplier as needed)
            float parryDamageReduction = 0.25f; // 25% damage gets through on parry

            float reducedDamage = _pendingDamage * parryDamageReduction;
            healthLost = _pendingTarget.TakeDamage(reducedDamage, wasParried: true);  // <-- Pass wasParried = true!

            // The chip still counts. Drive follows damage actually dealt, and the parried player
            // is already paid for that same chip via damageTakenDriveMultiplier.
            GrantDriveForDamageDealt(reducedDamage);
            Debug.Log($"[EnemyManager] {gameObject.name}'s attack was parried! Reduced damage: {reducedDamage:F1} ({parryDamageReduction * 100}% of {_pendingDamage:F1})");
        }

        // Siphon happens here rather than at the roll, so it's scaled by what the parry let through.
        ApplyHealthDrain(healthLost);

        // Clear pending data, the target highlight and the tooltip flag
        ClearHighlight();
        _pendingDamage = 0f;
        _pendingTarget = null;
        _isShowingEnemyTooltip = false;
        
        // Complete turn
        _turnManager.CompleteTurn();
    }
    
    /// <summary>
    /// Heals this enemy for its share of the health a HealthDrain just took. No-ops for every other
    /// action type, so both the parryable and direct paths can call it unconditionally.
    /// </summary>
    /// <remarks>
    /// Takes health <i>actually lost</i> rather than damage rolled, which is what makes the parry
    /// interaction work without a special case: a parried drain removed 25% of the health, so it
    /// heals the enemy 25% as much.
    ///
    /// The 1x multiplier on Heal() is deliberate — drive already multiplied the damage this is
    /// derived from, and applying it again would let one drive spend pay out twice.
    /// </remarks>
    private void ApplyHealthDrain(float healthLost)
    {
        if (_selectedAction == null || _selectedAction.actionType != Action.ActionType.HealthDrain) return;
        if (_characterManager == null || healthLost <= 0f) return;

        float siphoned = healthLost * _selectedAction.healthDrainRatio;
        if (siphoned <= 0f) return;

        _characterManager.Heal(siphoned);

        Debug.Log($"[EnemyManager] {gameObject.name} siphoned {siphoned:F1} health " +
                  $"({_selectedAction.healthDrainRatio:P0} of {healthLost:F1} taken)");
    }

    /// <summary>
    /// Rolls this enemy's critical hit, honouring any <see cref="Character.BuffType.CritChance"/>
    /// buff via <c>CharacterManager.CritThreshold</c>. Returns true on a crit.
    /// </summary>
    /// <remarks>
    /// Returns the verdict rather than the raw roll, so the threshold is applied in one place
    /// instead of every call site re-testing <c>== 20</c> — which is exactly how the base crit rule
    /// came to be duplicated across two branches in the first place.
    ///
    /// <b>Still a separate roll from the to-hit check</b>, unlike the player, whose single d20
    /// decides both. That asymmetry predates this and is logged in <c>TODO.md</c>; a CritChance buff
    /// makes it slightly more visible, because an enemy's crit is gated behind landing a separate
    /// to-hit roll first while the player's buffed high roll both hits and crits by construction.
    /// </remarks>
    /// <summary>
    /// Pays this enemy the drive it earned for dealing damage, the same way <c>CombatController</c>
    /// pays the player.
    /// </summary>
    /// <remarks>
    /// <b>This was simply missing.</b> Every enemy damage path called <c>TakeDamage</c> and stopped
    /// there, so <c>damageInflictedDriveMultiplier</c> was authored on every Enemy asset and never
    /// read — enemies could only build drive from being hit and from turn regeneration, while the
    /// player's path had called <c>OnDamageDealt</c> all along. The two sides had quietly different
    /// drive economies.
    ///
    /// Passes the damage DEALT rather than health actually lost, matching the player exactly. So
    /// overkill pays in full on both sides — consistent, and logged in <c>TODO.md</c> as its own
    /// question rather than silently changed for one side here.
    /// </remarks>
    private void GrantDriveForDamageDealt(float damage)
    {
        if (damage <= 0f) return;

        _characterManager?.OnDamageDealt(damage);
    }

    private bool RollForCritical()
    {
        int roll = Random.Range(1, 21); // d20 roll
        int threshold = _characterManager != null ? _characterManager.CritThreshold : 20;

        bool crit = roll >= threshold;

        if (crit && enemyData != null && enemyData.showDebugInfo)
        {
            Debug.Log($"[EnemyManager] {gameObject.name} crit roll {roll} met threshold {threshold}");
        }

        return crit;
    }

    /// <summary>
    /// Enemy to-hit roll. Mirrors <see cref="CombatController"/>'s player-side rule exactly:
    /// natural 1 always misses, natural 20 always hits, otherwise
    /// <c>roll + attackPower >= target defense</c>.
    /// </summary>
    /// <remarks>
    /// Enemies previously could not miss — they rolled a d20 for crits only, so every attack landed
    /// unless parried. That left <c>attackPower</c> on enemy assets and <c>defenseValue</c> on crew
    /// authored but never read.
    /// </remarks>
    private bool RollToHit(CharacterManager targetManager, out int attackRoll)
    {
        attackRoll = Random.Range(1, 21);

        if (attackRoll == 1) return false;
        if (attackRoll == 20) return true;

        return attackRoll + _characterManager.AttackPower >= targetManager.DefenseValue;
    }

    /// <summary>
    /// Shared miss resolution: tells the target, and spends any drive the enemy had committed —
    /// a miss consumes stacks on the player's side too, so whiffing has the same cost either way.
    /// </summary>
    private void ResolveMiss(CharacterManager targetManager, int attackRoll)
    {
        Debug.Log($"[EnemyManager] {gameObject.name} rolled {attackRoll} and missed " +
                  $"{targetManager.characterData?.characterName} (defense {targetManager.DefenseValue}).");

        targetManager.Miss();

        DriveManager enemyDriveManager = _characterManager.GetComponent<DriveManager>();
        enemyDriveManager?.OnAttackPerformed();
    }
    
    // Add this method to initialize the drive manager (call it in Awake or Start)
    
    
    private void InitializeEnemyDriveManager(CharacterManager characterManager)
    {
        Debug.Log($"[EnemyManager] InitializeEnemyDriveManager called for {characterManager.characterData.characterName}");
    
        enemyDriveManager = characterManager.GetComponent<EnemyDriveManager>();
        Debug.Log($"[EnemyManager] GetComponent<EnemyDriveManager> returned: {enemyDriveManager}");

        if (enemyDriveManager == null)
        {
            Debug.Log($"[EnemyManager] EnemyDriveManager not found, adding component");
            enemyDriveManager = characterManager.gameObject.AddComponent<EnemyDriveManager>();
            Debug.Log($"[EnemyManager] AddComponent returned: {enemyDriveManager}");
        }

        // Get the enemy data
        Enemy enemyData = characterManager.characterData as Enemy;
        Debug.Log($"[EnemyManager] Cast to Enemy returned: {enemyData}");
    
        if (enemyData != null && enemyData.driveConfig != null)
        {
            Debug.Log($"[EnemyManager] Found Enemy scriptable object, initializing with driveConfig");
            enemyDriveManager.Initialize(enemyData.driveConfig);
        }
        else
        {
            Debug.LogWarning($"[EnemyManager] {characterManager.characterData.characterName} has no driveConfig, using default");
            enemyDriveManager.Initialize(new EnemyDriveConfig());
        }
    
        Debug.Log($"[EnemyManager] InitializeEnemyDriveManager complete. enemyDriveManager is now: {enemyDriveManager}");
    }
    
}