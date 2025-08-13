
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] private Character mainCharacterData;
    [SerializeField] private float enemyActionDelay = 1.25f;
    
    [Header("Attack System")]
    [SerializeField] private bool useParrySystem = true;
    
    private readonly List<GameObject> _playerCharacters = new List<GameObject>();
    private Action _selectedAction;
    private TurnManager _turnManager;
    private EnemyAttack _enemyAttack;

    private void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        if (_turnManager == null)
        {
            Debug.LogError("No _turnManager found in the scene!");
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCharacterData = GetComponent<CharacterManager>().characterData;
        RefreshTargetList();
    }

    public void RefreshTargetList()
    {
        _selectedAction = mainCharacterData.actionSlots[0];
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
        
        if (_playerCharacters.Count > 0 && _playerCharacters[0] != null)
        {
            PerformAction(_playerCharacters[0].GetComponent<CharacterManager>());
        }
        else
        {
            Debug.Log("No valid player targets found, completing turn...");
            _turnManager.CompleteTurn();
        }
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
            Debug.Log("Critical Fail! Attack missed.");
            _turnManager.CompleteTurn();
            return;
        }
        
        CharacterManager currentCharacter = GetComponent<CharacterManager>();
        float baseDamage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
        float modifiedDamage = baseDamage + (currentCharacter.AttackPower * 0.1f);
        
        if (attackRoll == 20)
        {
            Debug.Log("Critical Hit! Double damage!");
            modifiedDamage *= 2;
        }
        
        // Set the damage amount on the EnemyAttack component
        // Note: You may need to add a SetDamage method to EnemyAttack or make attackDamage public
        // For now, we'll store it and apply it in the completion callback
        _pendingDamage = modifiedDamage;
        _pendingTarget = targetManager;
        
        // Start the parryable attack sequence
        _enemyAttack.StartAttack(targetManager.gameObject);
        
        // Note: Don't call _turnManager.CompleteTurn() here - it will be called in OnAttackComplete
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
        float modifiedDamage = baseDamage + (currentCharacter.AttackPower * 0.1f); // Add 10% of attack power
        
        if (attackRoll == 20)
        {
            Debug.Log("Critical Hit! Double damage!");
            modifiedDamage *= 2;
        }
        
        // Refresh target list and filter out destroyed objects
        RefreshTargetList();
        var validTargets = _playerCharacters.Where(p => p != null && p.GetComponent<CharacterManager>() != null).ToList();
        
        if (validTargets.Count > 0)
        {
            int randomIndex = Random.Range(0, validTargets.Count);
            var targetCharacter = validTargets[randomIndex].GetComponent<CharacterManager>();
            if (targetCharacter != null)
            {
                targetCharacter.TakeDamage(modifiedDamage);
            }
        }
        else
        {
            Debug.Log("No valid targets for attack!");
        }
        
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
            Debug.Log($"[EnemyManager] Attack was parried by {_pendingTarget.name}!");
            // No damage dealt
        }
        else
        {
            Debug.Log($"[EnemyManager] Attack hit {_pendingTarget.name} for {_pendingDamage} damage!");
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