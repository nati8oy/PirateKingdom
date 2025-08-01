using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public Character playerCharacter;
    public Character currentCharacterTurn;
    private GameObject[] enemyCharacters;
    private CharacterManager[] enemyCharacterManagers;
    
    public List<GameObject> turnOrder = new List<GameObject>();
    public List<(GameObject obj, float initiative)> initiativeList = new List<(GameObject obj, float initiative)>(); // Made public and moved to class level

    void Start()
    {

        GetTurnOrder();
        //StartNewRound();
        
    }

    
    public void GetTurnOrder()
    {
        var characterManagers = FindObjectsOfType<CharacterManager>();
        initiativeList.Clear(); // Clear previous data

        foreach (var characterManager in characterManagers)
        {
            float initiative = characterManager.Speed + Random.Range(1, 9);
            initiativeList.Add((characterManager.gameObject, initiative));
        }

        initiativeList.Sort((a, b) => b.initiative.CompareTo(a.initiative));
        turnOrder = initiativeList.Select(x => x.obj).ToList();

        // Display turn order with initiatives
        Debug.Log("=== TURN ORDER ===");
        for (int i = 0; i < initiativeList.Count; i++)
        {
            var entry = initiativeList[i];
            var characterManager = entry.obj.GetComponent<CharacterManager>();
            string characterName = characterManager?.characterData?.characterName ?? entry.obj.name;
        
            Debug.Log($"Turn {i + 1}: {characterName} - Initiative: {entry.initiative:F1}");
        }
        Debug.Log($"Total characters: {initiativeList.Count}");
    }


    public void SetCharacterTurn()
    {
        Debug.Log(turnOrder.Count);
    }
    
    
    public bool IsPlayerTurn()
    {
        int randomBonus = Random.Range(1, 9);
        float playerInitiative = playerCharacter.speed + randomBonus;
        float highestEnemyInitiative = 0;

        foreach(CharacterManager enemy in enemyCharacterManagers)
        {
            randomBonus = Random.Range(1, 9);
            float enemyInitiative = enemy.Speed + randomBonus;
            highestEnemyInitiative = Mathf.Max(highestEnemyInitiative, enemyInitiative);
        }

        bool isPlayerTurn = playerInitiative >= highestEnemyInitiative;
        Debug.Log($"Turn Order - Player Initiative: {playerInitiative}, Highest Enemy Initiative: {highestEnemyInitiative}. It's {(isPlayerTurn ? "Player's" : "Enemy's")} turn!");

        return isPlayerTurn;
        
    }
    
    
}
