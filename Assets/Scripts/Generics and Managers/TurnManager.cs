
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class TurnManager : MonoBehaviour
{
    public Character playerCharacter;
    public CharacterManager currentCharacterTurn;
    private GameObject[] enemyCharacters;
    private CharacterManager[] enemyCharacterManagers;
    [SerializeField] private TMP_Text roundCounter;
    [SerializeField] private TMP_Text playerTurn;
    [SerializeField] private float actionDelay = 1f;
    private int currentTurnIndex;
    private int roundCounterInt = 1; // Initialize to 1 for the first round

    public ActionsManager actionsManager;
    
    public List<GameObject> turnOrder = new List<GameObject>();
    public List<(GameObject obj, float initiative)> initiativeList = new List<(GameObject obj, float initiative)>(); // Made public and moved to class level

    void Start()
    {
        GetTurnOrder();
        SetCharacterTurn();
    }

    private void Update()
    {
        playerTurn.text = currentCharacterTurn.characterData.name+"'s" + " Turn";
        roundCounter.text = "Round " + roundCounterInt; // Use the actual round counter
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
    }

    public void SetCharacterTurn()
    {
        if (turnOrder.Count > 0)
        {
            currentCharacterTurn = turnOrder[currentTurnIndex].GetComponent<CharacterManager>();
            
            currentCharacterTurn.turnMarker.gameObject.SetActive(true);
        
            if (currentCharacterTurn != null && currentCharacterTurn.characterData != null)
            {
                actionsManager.LoadCharacterActions(currentCharacterTurn.characterData); 

                if (currentCharacterTurn.characterData.allegiance == Character.Allegiance.Enemy)
                {
                    turnOrder[currentTurnIndex].GetComponent<EnemyManager>().EnemyTurnAction();
                }
            }
            else
            {
                Debug.Log($"Current turn: {turnOrder[currentTurnIndex].name}");
            }
        }
        else
        {
            Debug.LogWarning("Turn order is empty!");
        }
    }

    public void CompleteTurn()
    {
        currentCharacterTurn.turnMarker.gameObject.SetActive(false);
        currentTurnIndex++;
        
        // Check if we've completed a full round
        if (currentTurnIndex >= turnOrder.Count)
        {
            RoundComplete();
            currentTurnIndex = 0;
            GetTurnOrder();
        }

        SetCharacterTurn();
    }

    private void RoundComplete()
    {
        roundCounterInt += 1;
        Debug.Log($"Round {roundCounterInt - 1} complete! Starting Round {roundCounterInt}");
    }
}