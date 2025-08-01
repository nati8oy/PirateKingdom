using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public Character playerCharacter;
    public Character currentPlayerCharacter;
    private GameObject[] enemyCharacters;
    private CharacterManager[] enemyCharacterManagers;
    
    public bool currentPlayerTurn;
	public List<GameObject> turnOrder = new List<GameObject>();

    void Start()
    {
        currentPlayerCharacter = playerCharacter;

        GetTurnOrder();
        //StartNewRound();
        
        
    }

    public void GetTurnOrder()
    {
    var characters = FindObjectsOfType<Character>();
    var initiativeList = new List<(GameObject obj, float initiative)>();

    foreach (var character in characters)
    {
        float initiative = character.speed + Random.Range(1, 9);
        Debug.Log($"{character.name} - Initiative: {initiative}");
        
        initiativeList.Add((character.GameObject(), initiative));
    }

    initiativeList.Sort((a, b) => b.initiative.CompareTo(a.initiative));
    turnOrder = initiativeList.Select(x => x.obj).ToList();
    Debug.Log(initiativeList.Count);
    
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
