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
    private int roundCounterInt = 1;

    public ActionsManager actionsManager;
    
    public List<GameObject> turnOrder = new List<GameObject>();
    public List<(GameObject obj, float initiative)> initiativeList = new List<(GameObject obj, float initiative)>();

    void Start()
    {
        GetTurnOrder();
        SetCharacterTurn();
    }

    private void Update()
    {
        playerTurn.text = currentCharacterTurn.characterData.name+"'s" + " Turn";
        roundCounter.text = "Round " + roundCounterInt;
    }

    public void GetTurnOrder()
    {
        var characterManagers = FindObjectsOfType<CharacterManager>();
        initiativeList.Clear();

        foreach (var characterManager in characterManagers)
        {
            // Update stats before calculating initiative
            characterManager.RefreshStats();
            float initiative = characterManager.Speed + Random.Range(1, 9);
            initiativeList.Add((characterManager.gameObject, initiative));
        }

        initiativeList.Sort((a, b) => b.initiative.CompareTo(a.initiative));
        turnOrder = initiativeList.Select(x => x.obj).ToList();

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
            
            // Update buffs and stats at the start of the character's turn
            currentCharacterTurn.UpdateBuffsForTurn();
            
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
        // Update the current character's buffs when they complete their turn
        currentCharacterTurn.OnTurnComplete();
        
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
        
        // Note: We no longer update buffs here since they're now turn-based
        // Buffs are updated individually when each character completes their turn
    }
}