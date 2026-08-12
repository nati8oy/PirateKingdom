using UnityEngine;
using System.Collections.Generic;

public enum TargetingMode
{
    Automatic,
    Manual
}

public class CombatController : MonoBehaviour
{
    private TurnManager turnManager;
    public CharacterManager currentTargetManager;
    public Action selectedAction;
    
    [Header("Targeting System")]
    [SerializeField] private bool useManualTargeting = true;

    void Awake()
    {
        turnManager = FindObjectOfType<TurnManager>();
        if (turnManager == null)
        {
            Debug.LogError("No TurnManager found in the scene!");
        }
    }

    public void OnPlayerActionComplete()
    {
        if (turnManager != null)
        {
            // Handle any additional logic needed after player action
            // Currently empty but kept for ClickableCharacter compatibility
        }
        else
        {
            Debug.LogError("Cannot handle action complete: TurnManager is null!");
        }
    }

    /// <summary>
    /// True only while a Player-allegiance character holds the turn. The action buttons are
    /// already disabled outside that window, but this is the authority — it also covers a click
    /// that lands in the same frame the turn changes, and a selection left over from an earlier
    /// turn.
    /// </summary>
    private bool IsPlayerControlledTurn()
    {
        CharacterManager current = turnManager != null ? turnManager.currentCharacterTurn : null;

        return current != null
               && current.characterData != null
               && current.characterData.allegiance == Character.Allegiance.Player;
    }

    /// <summary>Drops any pending action/target. Called by TurnManager whenever the turn changes.</summary>
    public void ClearSelection()
    {
        selectedAction = null;
        currentTargetManager = null;
    }

    public void SelectAction(Action action)
    {
        if (!IsPlayerControlledTurn())
        {
            Debug.Log($"Ignoring selection of {action?.actionName} — it is not a player character's turn.");
            return;
        }

        // Set the selected action
        selectedAction = action;

        // Self-cast actions skip target selection entirely: one click on the button resolves them
        // on the caster and ends the turn. Handled here rather than at the click site so it holds
        // for every route into SelectAction, and checked before the automatic-targeting branch
        // below because "cast on me" is a stronger statement than "pick something at random".
        if (action != null && action.autoTargetSelf)
        {
            CharacterManager caster = turnManager != null ? turnManager.currentCharacterTurn : null;

            // IsValidTarget reads selectedAction, which is assigned above.
            if (caster != null && IsValidTarget(caster))
            {
                Debug.Log($"[CombatController] {action.actionName} is self-cast — resolving on " +
                          $"{caster.characterData?.characterName ?? caster.name} without target selection.");

                // Resolves, ends the turn and clears the selection, all inside PerformAction.
                TryExecuteActionOnTarget(caster);
                return;
            }

            // Degrade to manual targeting rather than erroring: an unusable action that eats the
            // turn is worse than one that simply asks for a target the normal way.
            Debug.LogWarning($"[CombatController] '{action.actionName}' has Auto Target Self ticked, but " +
                             $"its Target Type ({action.targetType}) doesn't allow the caster as a target. " +
                             "Falling back to manual targeting — set Target Type to Single Ally.", action);
        }

        if (!useManualTargeting)
        {
            // Automatic targeting mode - find and execute on a random target immediately
            CharacterManager randomTarget = FindRandomValidTarget(action);
    
            if (randomTarget != null)
            {
                // Execute the action immediately on the random target
                bool success = TryExecuteActionOnTarget(randomTarget);
        
                if (success)
                {
                    Debug.Log($"Action {action.actionName} automatically executed on random target {randomTarget.characterData.characterName}");
                    OnPlayerActionComplete();
                }
                else
                {
                    Debug.Log($"Failed to execute {action.actionName} on random target {randomTarget.characterData.characterName}");
                }
            }
            else
            {
                Debug.Log($"No valid targets found for action {action.actionName}");
            }
    
            // Clear the selected action since we've used it
            selectedAction = null;
        }
        else
        {
            // Manual targeting mode - just store the action and wait for target selection
            Debug.Log($"Action {action.actionName} selected. Click on a target to execute.");
        }
    }

    private CharacterManager FindRandomValidTarget(Action action)
    {
        List<CharacterManager> validTargets = new List<CharacterManager>();
    
        // Use GameManager to get character lists if available
        GameManager.Instance.FindCharacters();
    
        switch (action.targetType)
        {
            case Action.TargetType.SingleEnemy:
                // Target enemies - get from GameManager's enemy list
                foreach (GameObject enemy in GameManager.EnemyCharacters)
                {
                    if (enemy != null)
                    {
                        CharacterManager enemyManager = enemy.GetComponent<CharacterManager>();
                        if (enemyManager != null && enemyManager.GetCurrentHealth() > 0)
                        {
                            validTargets.Add(enemyManager);
                        }
                    }
                }
                break;
            
            case Action.TargetType.SingleAlly:
                // Target player allies - get from GameManager's player list
                foreach (GameObject player in GameManager.PlayerCharacters)
                {
                    if (player != null)
                    {
                        CharacterManager playerManager = player.GetComponent<CharacterManager>();
                        if (playerManager != null && playerManager.GetCurrentHealth() > 0)
                        {
                            validTargets.Add(playerManager);
                        }
                    }
                }
                break;
            
            case Action.TargetType.AllEnemies:
                // For AoE enemy targeting, return any valid enemy (the action system will handle hitting all)
                foreach (GameObject enemy in GameManager.EnemyCharacters)
                {
                    if (enemy != null)
                    {
                        CharacterManager enemyManager = enemy.GetComponent<CharacterManager>();
                        if (enemyManager != null && enemyManager.GetCurrentHealth() > 0)
                        {
                            return enemyManager; // Return first valid enemy for AoE
                        }
                    }
                }
                break;
            
            case Action.TargetType.AllAllies:
                // For AoE ally targeting, return any valid ally (the action system will handle hitting all)
                foreach (GameObject player in GameManager.PlayerCharacters)
                {
                    if (player != null)
                    {
                        CharacterManager playerManager = player.GetComponent<CharacterManager>();
                        if (playerManager != null && playerManager.GetCurrentHealth() > 0)
                        {
                            return playerManager; // Return first valid ally for AoE
                        }
                    }
                }
                break;
        }
    
        // Return random target from valid targets
        if (validTargets.Count > 0)
        {
            int randomIndex = Random.Range(0, validTargets.Count);
            return validTargets[randomIndex];
        }
    
        return null;
    }

    public void SetCurrentTarget(CharacterManager targetManager)
    {
        currentTargetManager = targetManager;
    }

    public bool TryExecuteActionOnTarget(CharacterManager targetManager)
    {
        SetCurrentTarget(targetManager);
        return TryExecuteActionOnCurrentTarget();
    }

    // Keep this for backward compatibility with ClickableCharacter
    public bool TryExecuteActionOnTarget(Character target)
    {
        // Since we no longer have hardcoded references, we need to find the CharacterManager
        // by searching through all CharacterManagers in the scene
        CharacterManager[] allManagers = FindObjectsOfType<CharacterManager>();
        CharacterManager targetManager = null;
        
        foreach (CharacterManager manager in allManagers)
        {
            if (manager.characterData == target)
            {
                targetManager = manager;
                break;
            }
        }
        
        if (targetManager != null)
        {
            return TryExecuteActionOnTarget(targetManager);
        }
        else
        {
            Debug.LogError($"Could not find CharacterManager for character: {target.name}");
            return false;
        }
    }

    public bool TryExecuteActionOnCurrentTarget()
    {
        if (selectedAction == null || currentTargetManager == null)
        {
            Debug.LogError("No action selected or no target set!");
            return false;
        }

        if (!IsPlayerControlledTurn())
        {
            Debug.Log("Ignoring action — it is not a player character's turn.");
            ClearSelection();
            return false;
        }

        if (!IsValidTarget(currentTargetManager))
        {
            Debug.LogError($"Invalid target for action {selectedAction.name}");
            return false;
        }

        PerformAction(currentTargetManager);
        selectedAction = null;
        currentTargetManager = null;
        return true;
    }

    /// <summary>
    /// Whether the selected action may be used on this target. Public so UI feedback
    /// (ActionTargetingLine) asks the same authority that execution does, rather than keeping a
    /// second copy of the targeting rules that could drift out of step.
    /// </summary>
    public bool IsValidTarget(CharacterManager targetManager)
    {
        if (selectedAction == null)
            return false;

        switch (selectedAction.targetType)
        {
            case Action.TargetType.SingleEnemy:
                return targetManager.gameObject.CompareTag("Enemy");
            case Action.TargetType.SingleAlly:
                return targetManager.gameObject.CompareTag("Player");
            case Action.TargetType.AllAllies:
                return targetManager.gameObject.CompareTag("Player");
            case Action.TargetType.AllEnemies:
                return targetManager.gameObject.CompareTag("Enemy");
            default:
                return false;
        }
    }

    private int HitChanceRoll()
    {
        return Random.Range(1, 21);
    }

    /// <summary>
    /// Resolves the selected action on a target and ends the acting character's turn.
    /// </summary>
    /// <remarks>
    /// <b>Turn completion is in a finally, and that is load-bearing.</b> Resolution and turn
    /// handover used to be a plain sequence, so anything that threw while applying an effect
    /// skipped <c>CompleteTurn()</c> — and a stranded turn is not a stalled game, it's a free extra
    /// action: the selection is still live, the buttons are still interactable, and the player
    /// simply picks something else. That turns a fault in one action into a balance exploit and
    /// hides the original error behind it.
    ///
    /// Acting costs the turn whether or not the effect resolved cleanly. Note the null-target guard
    /// deliberately sits *above* the try — that's a click that never became an action, so it must
    /// not cost anything.
    ///
    /// This also closes the selection leak for free: <c>CompleteTurn()</c> reaches
    /// <c>TurnManager.SetCharacterTurn()</c>, which calls <see cref="ClearSelection"/> on every turn
    /// change, so a throw can't leave a live action for the next character to resolve.
    /// </remarks>
    public void PerformAction(CharacterManager targetManager)
{
    if (targetManager == null)
    {
        Debug.LogError($"Target CharacterManager is null!");
        return;
    }

    // Get the current character performing the action
    CharacterManager currentCharacter = turnManager.currentCharacterTurn;

    try
    {
        switch (selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                // Play attack audio immediately when attack action starts
                currentCharacter.PlayAttackAudio();
            
                int attackRoll = HitChanceRoll();
            
                if (attackRoll == 1)
                {
                    Debug.Log("Critical Fail! Attack missed.");
                    targetManager.Miss();
                
                    // Even on a miss, if drive mode was active, it should be consumed
                    DriveManager attackerDriveManager = currentCharacter.GetDriveManager();
                    attackerDriveManager?.OnAttackPerformed();
                
                    break;
                }

                if (attackRoll == 20 || (attackRoll + currentCharacter.AttackPower >= targetManager.DefenseValue))
                {
                    float damage = Random.Range(selectedAction.minDamage, selectedAction.maxDamage);

                    // Damage bonuses come from drive alone — Accuracy buffs deliberately do not touch
                    // damage, so the two systems don't stack into one another. See CLAUDE.md.

                    // Apply drive mode multiplier if the attacker is in drive mode
                    DriveManager attackerDriveManager = currentCharacter.GetDriveManager();
                    if (attackerDriveManager != null)
                    {
                  
                    
                        float driveMultiplier = attackerDriveManager.GetNextAttackDamageMultiplier();
                        damage *= driveMultiplier;
                    
                    
                        if (driveMultiplier > 1f)
                        {
                            Debug.Log($"Drive system applied {driveMultiplier}x damage multiplier!");
                        }
                    
                        // IMPORTANT: Call OnAttackPerformed to deactivate drive mode after the attack
                        attackerDriveManager.OnAttackPerformed();
                    }
                    else
                    {
                        Debug.Log($"No drive manager found for {currentCharacter.characterData.characterName}");
                    }
                
                    // >= threshold rather than == 20, so a CritChance buff widens the window.
                    // Note this is a different test from the "natural 20 always hits" check above,
                    // which stays pinned at 20 — a buffed 18 can crit, but it still has to beat the
                    // target's defense to land, so you can never crit on a miss.
                    if (attackRoll >= currentCharacter.CritThreshold)
                    {
                        Debug.Log($"Critical Hit! Double damage! (rolled {attackRoll} vs threshold {currentCharacter.CritThreshold})");
                        damage *= 2;
                    }

                    Debug.Log($"Final damage dealt: {damage}");
                    Debug.Log("Attack value of " + (attackRoll + currentCharacter.AttackPower) + " hit enemy with defense value of: " + targetManager.DefenseValue);
                    targetManager.TakeDamage(damage);
                    currentCharacter.OnDamageDealt(damage);
                }
                else
                {
                    Debug.Log("Attack value of " + (attackRoll + currentCharacter.AttackPower) + " Missed enemy with defense value of: " + targetManager.DefenseValue);
                    targetManager.Miss();
                
                    // Even on a miss, if drive mode was active, it should be consumed
                    DriveManager attackerDriveManager = currentCharacter.GetDriveManager();
                    attackerDriveManager?.OnAttackPerformed();
                }
                break;

            // Resolves exactly like Attack — same to-hit rule, same crit, same drive multiplier — and
            // then siphons a share of it back. Kept as its own case rather than a flag on Attack so the
            // d20 rule stays written out in full and stays comparable to EnemyManager.RollToHit().
            case Action.ActionType.HealthDrain:
                currentCharacter.PlayAttackAudio();

                int drainRoll = HitChanceRoll();

                if (drainRoll == 1)
                {
                    Debug.Log("Critical Fail! Drain missed.");
                    targetManager.Miss();
                    currentCharacter.GetDriveManager()?.OnAttackPerformed();
                    break;
                }

                if (drainRoll == 20 || (drainRoll + currentCharacter.AttackPower >= targetManager.DefenseValue))
                {
                    float drainDamage = Random.Range(selectedAction.minDamage, selectedAction.maxDamage);

                    DriveManager drainDriveManager = currentCharacter.GetDriveManager();
                    if (drainDriveManager != null)
                    {
                        drainDamage *= drainDriveManager.GetNextAttackDamageMultiplier();
                        drainDriveManager.OnAttackPerformed();
                    }

                    if (drainRoll >= currentCharacter.CritThreshold)
                    {
                        Debug.Log($"Critical Hit! Double drain! (rolled {drainRoll} vs threshold {currentCharacter.CritThreshold})");
                        drainDamage *= 2;
                    }

                    // The heal is derived from the damage, so the drive multiplier has already flowed
                    // into it once. Heal() is called with the default 1x multiplier deliberately —
                    // passing GetNextHealingMultiplier() as well would apply drive twice to one action.
                    float healthTaken = targetManager.TakeDamage(drainDamage);
                    currentCharacter.OnDamageDealt(drainDamage);

                    float siphoned = healthTaken * selectedAction.healthDrainRatio;

                    if (siphoned > 0f)
                    {
                        currentCharacter.Heal(siphoned);
                    }

                    Debug.Log($"{currentCharacter.characterData.characterName} drained {healthTaken} health from " +
                              $"{targetManager.characterData.characterName}, healing {siphoned}");
                }
                else
                {
                    Debug.Log("Drain value of " + (drainRoll + currentCharacter.AttackPower) + " missed enemy with defense value of: " + targetManager.DefenseValue);
                    targetManager.Miss();
                    currentCharacter.GetDriveManager()?.OnAttackPerformed();
                }
                break;

            case Action.ActionType.Heal:
                int healRoll = HitChanceRoll();
                float healAmount = Random.Range(selectedAction.minHeal, selectedAction.maxHeal);
            
                // Crit chance applies to healing too — a healer who buffs their crit should see it
                // in their heals, or the buff silently means nothing in a support character's hands.
                if (healRoll >= currentCharacter.CritThreshold)
                {
                    Debug.Log($"Critical Heal! Double healing! (rolled {healRoll} vs threshold {currentCharacter.CritThreshold})");
                    healAmount *= 2;
                }
            
                // The healer's drive stacks, read before they're consumed below. Heal() can't look this
                // up itself — its own DriveManager belongs to the target, not the caster.
                DriveManager healerDriveManager = currentCharacter.GetDriveManager();
                float healDriveMultiplier = healerDriveManager != null
                    ? healerDriveManager.GetNextHealingMultiplier()
                    : 1f;

                targetManager.Heal(healAmount, healDriveMultiplier);
                Debug.Log($"Healing {targetManager.gameObject.name} by {healAmount} (drive x{healDriveMultiplier})");

                // IMPORTANT: Consume drive mode after the heal
                currentCharacter.OnAttackPerformed();
                break;

            case Action.ActionType.Buff:
                targetManager.AddBuff(selectedAction.buffType, selectedAction.buffValue, selectedAction.duration);
                break;

            case Action.ActionType.Debuff:
                targetManager.AddBuff(selectedAction.buffType, -selectedAction.buffValue, selectedAction.duration);
                break;

            // Drive actions move raw meter and nothing else — no roll, no crit, and deliberately no
            // drive multiplier off the caster's own stacks. Spending drive to make more drive is a
            // feedback loop, and the amount is authored on the Action rather than rolled so a support
            // player can plan around exactly how much of a segment they're handing over.
            case Action.ActionType.DriveGrant:
                targetManager.GainDrive(selectedAction.driveAmount);
                Debug.Log($"{currentCharacter?.characterData?.characterName ?? "Someone"} granted " +
                          $"{selectedAction.driveAmount} drive to {targetManager.characterData?.characterName ?? targetManager.name}");
                break;

            case Action.ActionType.DriveDrain:
                targetManager.LoseDrive(selectedAction.driveAmount);
                Debug.Log($"{currentCharacter?.characterData?.characterName ?? "Someone"} drained " +
                          $"{selectedAction.driveAmount} drive from {targetManager.characterData?.characterName ?? targetManager.name}");
                break;
        }

    }
    finally
    {
        // Everything here runs even if resolving the effect above threw — see the method remarks.
        // Using an action costs its cooldown and the turn together; splitting them means a fault
        // mid-resolution hands back a free, off-cooldown action.
        if (turnManager.currentCharacterTurn != null && turnManager.currentCharacterTurn.characterData != null)
        {
            turnManager.currentCharacterTurn.UseAction(selectedAction);

            // Null-guarded: this sits between the cooldown and turn handover, so an unguarded miss
            // here would strand the turn all over again.
            ActionsManager actionsManager = FindObjectOfType<ActionsManager>();

            if (actionsManager != null)
            {
                actionsManager.LoadCharacterActions(turnManager.currentCharacterTurn);
            }
            else
            {
                Debug.LogWarning("[CombatController] No ActionsManager in the scene — action buttons won't refresh.");
            }
        }

        turnManager.CompleteTurn();
    }
}
    public Action GetSelectedAction()
    {
        return selectedAction; // assuming you have a selectedAction field
    }

    public CharacterManager GetCurrentCharacter()
    {
        if (turnManager != null)
        {
            return turnManager.currentCharacterTurn;
        }
        return null;
    }
    
}