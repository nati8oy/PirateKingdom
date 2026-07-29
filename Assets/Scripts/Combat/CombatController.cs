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

    public void SelectAction(Action action)
    {
        // Set the selected action
        selectedAction = action;

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

    private bool IsValidTarget(CharacterManager targetManager)
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

    public void PerformAction(CharacterManager targetManager)
{
    if (targetManager == null)
    {
        Debug.LogError($"Target CharacterManager is null!");
        return;
    }

    // Get the current character performing the action
    CharacterManager currentCharacter = turnManager.currentCharacterTurn;

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
                
                if (attackRoll == 20)
                {
                    Debug.Log("Critical Hit! Double damage!");
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

        case Action.ActionType.Heal:
            int healRoll = HitChanceRoll();
            float healAmount = Random.Range(selectedAction.minHeal, selectedAction.maxHeal);
            
            if (healRoll == 20)
            {
                Debug.Log("Critical Heal! Double healing!");
                healAmount *= 2;
            }
            
            targetManager.Heal(healAmount); // This now applies drive mode multiplier automatically
            Debug.Log($"Healing {targetManager.gameObject.name} by {healAmount}");
            
            // IMPORTANT: Consume drive mode after the heal
            currentCharacter.OnAttackPerformed();
            break;

        case Action.ActionType.Buff:
            currentTargetManager.buffEffectText.text = selectedAction.buffType.ToString() + " +" + selectedAction.buffValue;
            targetManager.AddBuff(selectedAction.buffType, selectedAction.buffValue, selectedAction.duration);
            break;

        case Action.ActionType.Debuff:
            targetManager.AddBuff(selectedAction.buffType, -selectedAction.buffValue, selectedAction.duration);
            break;
    }
    
    // Mark action as used AFTER successful execution
    if (turnManager.currentCharacterTurn != null && turnManager.currentCharacterTurn.characterData != null)
    {
        turnManager.currentCharacterTurn.UseAction(selectedAction);

        // Refresh the actions UI to show updated cooldown states
        FindObjectOfType<ActionsManager>().LoadCharacterActions(turnManager.currentCharacterTurn);
    }
    
    turnManager.CompleteTurn();
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