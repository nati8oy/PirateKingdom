using UnityEngine;
using System.Collections.Generic;

public class CombatController : MonoBehaviour
{

    [SerializeField] private Character playerCharacter;
    [SerializeField] private Character enemyCharacter;
    
    [SerializeField] private GameObject playerCharacterGameObject; 
    [SerializeField] private GameObject enemyCharacterGameObject;  
    
    private CharacterManager playerCharacterManager;  // These will be found automatically
    private CharacterManager enemyCharacterManager;   // These will be found automatically
    private GameObject[] enemyGameObjects;           // Array to store all enemy objects

	public Character currentTarget;
    public Action selectedAction;
    private bool currentPlayerTurn;

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
    }
    
    public bool IsPlayerTurn()
    {
        int randomBonus = Random.Range(1, 9);
        float playerInitiative = playerCharacter.speed + randomBonus;

        randomBonus = Random.Range(1, 9);
        float enemyInitiative = enemyCharacter.speed + randomBonus;

        bool isPlayerTurn = playerInitiative >= enemyInitiative;
        Debug.Log($"Turn Order - Player Initiative: {playerInitiative}, Enemy Initiative: {enemyInitiative}. It's {(isPlayerTurn ? "Player's" : "Enemy's")} turn!");

        return isPlayerTurn;
    }

    void Start()
    {
    enemyGameObjects = GameObject.FindGameObjectsWithTag("Enemy");
    StartNewRound();
    }

    public void StartNewRound()
    {
        currentPlayerTurn = IsPlayerTurn();
        
        if (currentPlayerTurn)
        {
            HandlePlayerTurn();
        }
        else
        {
            HandleEnemyTurn();
        }
    }

    private void HandlePlayerTurn()
    {
        // Enable player input, show UI, etc.
        Debug.Log("Player's turn - waiting for input");
    }

    private void HandleEnemyTurn()
    {
        // Execute enemy AI logic
        Debug.Log("Enemy's turn");
        // After enemy action, switch to player
        currentPlayerTurn = true;
        HandlePlayerTurn();
    }

    // Call this when player completes their action
    public void OnPlayerActionComplete()
    {
        currentPlayerTurn = false;
        HandleEnemyTurn();
    }


    public void SelectAction(Action action)
    {
        selectedAction = action;
        Debug.Log(selectedAction);
    }

    public bool TryExecuteActionOnTarget(Character target)
    {
        if (selectedAction == null || !IsValidTarget(target))
            return false;

        PerformAction(target);
        selectedAction = null;
        OnPlayerActionComplete();
        return true;
    }

    private bool IsValidTarget(Character target)
    {
        if (selectedAction == null)
            return false;

        switch (selectedAction.targetType)
        {
            case Action.TargetType.SingleEnemy:
                return target == enemyCharacter;
            case Action.TargetType.SingleAlly:
                return target == playerCharacter;
            case Action.TargetType.AllAllies:
                return target == playerCharacter;
            case Action.TargetType.AllEnemies:
                return target == enemyCharacter;
            default:
                return false;
        }
    }

    public void PerformAction(Character target)
    {
        CharacterManager targetManager = GetCharacterManager(target);
        
        if (targetManager == null)
        {
            Debug.LogError($"Could not find CharacterManager for target character!");
            return;
        }
        
        switch (selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                targetManager.TakeDamage(selectedAction.baseDamage);
                break;
            case Action.ActionType.Heal:
                targetManager.Heal(selectedAction.baseValue); 
                break;
            case Action.ActionType.Buff:
                targetManager.AddBuff(selectedAction.buffType, selectedAction.baseValue, selectedAction.duration);
                break;
            case Action.ActionType.Debuff:
                targetManager.AddBuff(selectedAction.buffType, -selectedAction.baseValue, selectedAction.duration);
                break;
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
            Debug.LogError($"Unknown character target - not player or enemy character!");
            return null;
        }
    }
}

	
    

