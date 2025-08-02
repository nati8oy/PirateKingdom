using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] private Character mainCharacterData;
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

        _selectedAction = mainCharacterData.actionSlots[0];
        
        
        _playerCharacters.Clear();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        _playerCharacters.AddRange(players);
    }

    public void EnemyTurnAction()
    {
        PerformAction(_playerCharacters[0].GetComponent<CharacterManager>());
        _turnManager.CompleteTurn();
    }

    private void PerformAction(CharacterManager targetManager)
    {
        if (targetManager == null)
        {
            
            Debug.LogError($"Target CharacterManager is null!");
            return;
        }

        //Debug.Log($"Performing {_selectedAction.actionType} on {targetManager.gameObject.name}");

        switch (_selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                int attackRoll = RollForCritical();
                if (attackRoll == 1)
                {
                    Debug.Log("Critical Fail! Attack missed.");
                    break;
                }
                float damage = Random.Range(_selectedAction.minDamage, _selectedAction.maxDamage);
                if (attackRoll == 20)
                {
                    Debug.Log("Critical Hit! Double damage!");
                    damage *= 2;
                }
                if (_playerCharacters.Count > 0)
                {
                    int randomIndex = Random.Range(0, _playerCharacters.Count);
                    var targetCharacter = _playerCharacters[randomIndex].GetComponent<CharacterManager>();
                    targetCharacter.TakeDamage(damage);
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
                targetManager.AddBuff(_selectedAction.buffType, _selectedAction.baseValue, _selectedAction.duration);
                break;
            case Action.ActionType.Debuff:
                targetManager.AddBuff(_selectedAction.buffType, -_selectedAction.baseValue, _selectedAction.duration);
                break;
        }
        
        //_turnManager.CompleteTurn();

    }
    
    private int RollForCritical()
    {
        return Random.Range(1, 21); // Returns 1-20
    }

}
