using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] private Character mainCharacterData;
    [SerializeField] private float enemyActionDelay = 1.25f;
    private readonly List<GameObject> _playerCharacters = new List<GameObject>();
    private Action _selectedAction;
    private TurnManager _turnManager;


    private void Awake()
    {
        _turnManager = FindObjectOfType<TurnManager>();
        if (_turnManager == null)
        {
            Debug.LogError("No _turnManager found in the scene!");
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
        _playerCharacters.AddRange(players);
    }
    public void EnemyTurnAction()
    {
        Invoke(nameof(ExecuteEnemyAction), enemyActionDelay);
    }

    private void ExecuteEnemyAction()
    {
        if (_playerCharacters[0] != null)
        {
            PerformAction(_playerCharacters[0].GetComponent<CharacterManager>());
            _turnManager.CompleteTurn(); 
        }

        else
        {
            RefreshTargetList();
            ExecuteEnemyAction();
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
    currentCharacter.RefreshStats(); // Make sure we have current stats

    switch (_selectedAction.actionType)
    {
        case Action.ActionType.Attack:
            int attackRoll = RollForCritical();
            if (attackRoll == 1)
            {
                Debug.Log("Critical Fail! Attack missed.");
                break;
            }
            
            // Use modified attack power for damage calculation
            float baseDamage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
            float modifiedDamage = baseDamage + (currentCharacter.AttackPower * 0.1f); // Add 10% of attack power
            
            if (attackRoll == 20)
            {
                Debug.Log("Critical Hit! Double damage!");
                modifiedDamage *= 2;
            }
            
            if (_playerCharacters.Count > 0)
            {
                int randomIndex = Random.Range(0, _playerCharacters.Count);
                var targetCharacter = _playerCharacters[randomIndex].GetComponent<CharacterManager>();
                targetCharacter.TakeDamage(modifiedDamage);
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
            break;
            
        case Action.ActionType.Buff:
            targetManager.AddBuff(_selectedAction.buffType, _selectedAction.buffValue, _selectedAction.duration);
            break;
            
        case Action.ActionType.Debuff:
            targetManager.AddBuff(_selectedAction.buffType, -_selectedAction.buffValue, _selectedAction.duration);
            break;
    }
}
    
    private int RollForCritical()
    {
        return Random.Range(1, 21); // Returns 1-20
    }

}
