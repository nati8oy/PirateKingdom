using UnityEngine;
using System.Collections.Generic;

public class CombatController : MonoBehaviour
{
    [SerializeField] private Character playerCharacter;
    [SerializeField] private Character enemyCharacter;
    [SerializeField] private GameObject playerCharacterGameObject; 
    [SerializeField] private GameObject enemyCharacterGameObject;  
    
    private CharacterManager playerCharacterManager;
    private CharacterManager enemyCharacterManager;
    private GameObject[] enemyGameObjects;
    private TurnManager turnManager;

    public CharacterManager currentTargetManager; // Changed to CharacterManager instead of Character
    public Action selectedAction;

    void Awake()
    {
        // Automatically find and cache CharacterManager components
        if (playerCharacterGameObject != null)
        {
            playerCharacterManager = playerCharacterGameObject.GetComponent<CharacterManager>();
            if (playerCharacterManager == null)
            {
                Debug.LogError($"No CharacterManager component found on player character GameObject: {playerCharacterGameObject.name}");
            }
        }
        else
        {
            Debug.LogError("Player Character GameObject is not assigned in CombatController!");
        }

        if (enemyCharacterGameObject != null)
        {
            enemyCharacterManager = enemyCharacterGameObject.GetComponent<CharacterManager>();
            if (enemyCharacterManager == null)
            {
                Debug.LogError($"No CharacterManager component found on enemy character GameObject: {enemyCharacterGameObject.name}");
            }
        }
        else
        {
            Debug.LogError("Enemy Character GameObject is not assigned in CombatController!");
        }

        turnManager = FindObjectOfType<TurnManager>();
        if (turnManager == null)
        {
            Debug.LogError("No TurnManager found in the scene!");
        }
    }
    

    void Start()
    {
        enemyGameObjects = GameObject.FindGameObjectsWithTag("Enemy");
        
    }
    public void OnPlayerActionComplete()
    {
        
        if (turnManager != null)
        {
            //turnManager.HandleEnemyTurn();
        }
        else
        {
            Debug.LogError("Cannot handle enemy turn: TurnManager is null!");
        }
    }
   

    public void SelectAction(Action action)
    {
        selectedAction = action;
        Debug.Log(selectedAction);
    }

    // Method to set the current target when a character manager is clicked
    public void SetCurrentTarget(CharacterManager targetManager)
    {
        currentTargetManager = targetManager;
        //Debug.Log($"Current target set to: {targetManager.gameObject.name}");
    }

    // Method to set target by Character (for backward compatibility)
    public void SetCurrentTarget(Character target)
    {
        CharacterManager manager = GetCharacterManager(target);
        if (manager != null)
        {
            SetCurrentTarget(manager);
        }
        else
        {
            Debug.LogError($"Could not find CharacterManager for character: {target.name}");
        }
    }

    // Execute action on the current target
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
        currentTargetManager = null; // Clear target after action
        OnPlayerActionComplete();
        return true;
    }

    // Keep the old method for backward compatibility
    public bool TryExecuteActionOnTarget(Character target)
    {
        SetCurrentTarget(target);
        return TryExecuteActionOnCurrentTarget();
    }

    // New method that works directly with CharacterManager
    public bool TryExecuteActionOnTarget(CharacterManager targetManager)
    {
        SetCurrentTarget(targetManager);
        return TryExecuteActionOnCurrentTarget();
    }

    private bool IsValidTarget(CharacterManager targetManager)
    {
        if (selectedAction == null)
            return false;

        switch (selectedAction.targetType)
        {
            case Action.TargetType.SingleEnemy:
                return IsEnemyCharacterManager(targetManager);
            case Action.TargetType.SingleAlly:
                return IsPlayerCharacterManager(targetManager);
            case Action.TargetType.AllAllies:
                return IsPlayerCharacterManager(targetManager);
            case Action.TargetType.AllEnemies:
                return IsEnemyCharacterManager(targetManager);
            default:
                return false;
        }
    }

    // Helper method to check if a CharacterManager is an enemy
    private bool IsEnemyCharacterManager(CharacterManager characterManager)
    {
        return characterManager.gameObject.CompareTag("Enemy");
    }

    // Helper method to check if a CharacterManager is a player character
    private bool IsPlayerCharacterManager(CharacterManager characterManager)
    {
        return characterManager == playerCharacterManager;
    }

    private int RollForCritical()
    {
        return Random.Range(1, 21); // Returns 1-20
    }

    public void PerformAction(CharacterManager targetManager)
    {
        if (targetManager == null)
        {
            Debug.LogError($"Target CharacterManager is null!");
            return;
        }

        //Debug.Log($"Performing {selectedAction.actionType} on {targetManager.gameObject.name}");

        switch (selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                int attackRoll = RollForCritical();
                if (attackRoll == 1)
                {
                    Debug.Log("Critical Fail! Attack missed.");
                    break;
                }
                float damage = Random.Range(selectedAction.minDamage, selectedAction.maxDamage);
                if (attackRoll == 20)
                {
                    Debug.Log("Critical Hit! Double damage!");
                    damage *= 2;
                }
                targetManager.TakeDamage(damage);
                break;
            case Action.ActionType.Heal:
                int healRoll = RollForCritical();
                float healAmount = Random.Range(selectedAction.minHeal, selectedAction.maxHeal);
                if (healRoll == 20)
                {
                    Debug.Log("Critical Heal! Double healing!");
                    healAmount *= 2;
                }
                targetManager.Heal(healAmount);
                break;
            case Action.ActionType.Buff:
                targetManager.AddBuff(selectedAction.buffType, selectedAction.baseValue, selectedAction.duration);
                break;
            case Action.ActionType.Debuff:
                targetManager.AddBuff(selectedAction.buffType, -selectedAction.baseValue, selectedAction.duration);
                break;
        }
        
        turnManager.CompleteTurn();

    }

    
    // Overload for backward compatibility
    public void PerformAction(Character target)
    {
        CharacterManager targetManager = GetCharacterManager(target);
        if (targetManager != null)
        {
            PerformAction(targetManager);
        }
        else
        {
            Debug.LogError($"Could not find CharacterManager for target character!");
        }
        
    }
    
    private CharacterManager GetCharacterManager(Character target)
    {
        if (target == playerCharacter)
            return playerCharacterManager;
        else if (target == enemyCharacter)
            return enemyCharacterManager;
        else
        {
            // This method still has the same issue, but it's kept for backward compatibility
            // It's better to use the CharacterManager-based methods instead
            foreach (GameObject enemyGO in enemyGameObjects)
            {
                CharacterManager cm = enemyGO.GetComponent<CharacterManager>();
                if (cm != null && cm.characterData == target)
                {
                    return cm;
                }
            }
            
            Debug.LogError($"Unknown character target - could not find CharacterManager!");
            return null;
        }
    }
}