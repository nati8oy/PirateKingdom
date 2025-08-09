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
    
    [Header("Battle State")]
    [SerializeField] private GameObject victoryUI;
    [SerializeField] private GameObject defeatUI;
    [SerializeField] private TMP_Text battleResultText;
    private bool battleEnded = false;
    
    public List<GameObject> turnOrder = new List<GameObject>();
    private List<(GameObject obj, float initiative)> initiativeList = new List<(GameObject obj, float initiative)>();

    void Start()
    {
        GetTurnOrder();
        SetCharacterTurn();
    }

    private void Update()
    {
        // Don't process turns if battle has ended
        if (battleEnded) return;
        
        // Check for battle end conditions
        CheckBattleEndConditions();
        
        // Check if the current character is still alive
        if (currentCharacterTurn == null || currentCharacterTurn.gameObject == null)
        {
            Debug.Log("Current character died, advancing turn...");
            CompleteTurn();
            return;
        }

        if (currentCharacterTurn != null && currentCharacterTurn.characterData != null)
        {
            playerTurn.text = currentCharacterTurn.characterData.name + "'s" + " Turn";
        }
        roundCounter.text = "Round " + roundCounterInt;
    }

    private void CheckBattleEndConditions()
    {
        if (battleEnded) return;
        
        // Count alive players and enemies
        int alivePlayersCount = CountAliveCharacters(Character.Allegiance.Player);
        int aliveEnemiesCount = CountAliveCharacters(Character.Allegiance.Enemy);
        
        if (alivePlayersCount == 0)
        {
            // All players are dead - Enemy victory
            EndBattle(false);
        }
        else if (aliveEnemiesCount == 0)
        {
            // All enemies are dead - Player victory
            EndBattle(true);
        }
    }
    
    private int CountAliveCharacters(Character.Allegiance allegiance)
    {
        var allCharacters = FindObjectsOfType<CharacterManager>();
        int count = 0;
        
        foreach (var character in allCharacters)
        {
            if (character != null && 
                character.gameObject != null && 
                character.characterData != null && 
                character.characterData.allegiance == allegiance)
            {
                count++;
            }
        }
        
        return count;
    }
    
    private void EndBattle(bool playerVictory)
    {
        battleEnded = true;
        
        // Stop all turn processing
        CancelInvoke();
        
        // Hide turn marker if current character exists
        if (currentCharacterTurn != null && currentCharacterTurn.turnMarker != null)
        {
            currentCharacterTurn.turnMarker.gameObject.SetActive(false);
        }
        
        // SHOW THE UI FIRST, BEFORE DISABLING OTHER THINGS
        if (playerVictory)
        {
            Debug.Log("BATTLE WON! All enemies defeated!");
            ShowVictoryScreen();
        }
        else
        {
            Debug.Log("BATTLE LOST! All players defeated!");
            ShowDefeatScreen();
        }
        
        // THEN disable action UI (after showing victory/defeat UI)
        if (actionsManager != null)
        {
            actionsManager.gameObject.SetActive(false);
        }
    }
    
    private void ShowVictoryScreen()
    {
        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }
        
        if (battleResultText != null)
        {
            if (battleResultText.gameObject.activeSelf == false)
            {
                battleResultText.gameObject.SetActive(true);
            }
            battleResultText.text = "VICTORY!";
        }
        
        // Hide turn UI
        if (playerTurn != null) playerTurn.gameObject.SetActive(false);
        if (roundCounter != null) roundCounter.gameObject.SetActive(false);
    }
    
    private void ShowDefeatScreen()
    {
        if (defeatUI != null)
        {
            defeatUI.SetActive(true);
        }
        
        if (battleResultText != null)
        {
            battleResultText.text = "DEFEAT!";
        }
        
        // Hide turn UI
        if (playerTurn != null) playerTurn.gameObject.SetActive(false);
        if (roundCounter != null) roundCounter.gameObject.SetActive(false);
    }

    private void GetTurnOrder()
    {
        if (battleEnded) return;
        
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

    private void SetCharacterTurn()
    {
        if (battleEnded) return;
        
        if (turnOrder.Count > 0)
        {
            // Skip any destroyed objects in the turn order
            while (currentTurnIndex < turnOrder.Count && 
                   (turnOrder[currentTurnIndex] == null || 
                    turnOrder[currentTurnIndex].GetComponent<CharacterManager>() == null))
            {
                currentTurnIndex++;
            }
            
            // If we've gone through all characters, complete the round
            if (currentTurnIndex >= turnOrder.Count)
            {
                RoundComplete();
                currentTurnIndex = 0;
                GetTurnOrder();
                return;
            }
            
            currentCharacterTurn = turnOrder[currentTurnIndex].GetComponent<CharacterManager>();
            
            // Update buffs and stats at the start of the character's turn
            currentCharacterTurn.UpdateBuffsForTurn();
            
            if (currentCharacterTurn.turnMarker != null)
            {
                currentCharacterTurn.turnMarker.gameObject.SetActive(true);
            }
        
            if (currentCharacterTurn != null && currentCharacterTurn.characterData != null)
            {
                actionsManager.LoadCharacterActions(currentCharacterTurn.characterData); 

                if (currentCharacterTurn.characterData.allegiance == Character.Allegiance.Enemy)
                {
                    var enemyManager = turnOrder[currentTurnIndex].GetComponent<EnemyManager>();
                    if (enemyManager != null)
                    {
                        enemyManager.EnemyTurnAction();
                    }
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
        if (battleEnded) return;
        
        // Check if currentCharacterTurn is still valid before accessing its components
        if (currentCharacterTurn != null && currentCharacterTurn.turnMarker != null)
        {
            // Update the current character's buffs when they complete their turn
            currentCharacterTurn.OnTurnComplete();
            
            currentCharacterTurn.turnMarker.gameObject.SetActive(false);
        }
        
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
        if (battleEnded) return;
        
        roundCounterInt += 1;
        Debug.Log($"Round {roundCounterInt - 1} complete! Starting Round {roundCounterInt}");
        
        // Note: We no longer update buffs here since they're now turn-based
        // Buffs are updated individually when each character completes their turn
    }
    
    // Public method to restart battle (can be called from UI buttons)
    public void RestartBattle()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
    
    // Public method to return to main menu (can be called from UI buttons)
    public void ReturnToMainMenu()
    {
        // You can implement this based on your game's structure
        // For example: UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        Debug.Log("Return to Main Menu - Implement scene loading here");
    }
    
    [ContextMenu("Test Victory UI")]
    public void TestVictoryUI()
    {
        if (victoryUI != null)
        {
            victoryUI.SetActive(!victoryUI.activeSelf);
            Debug.Log($"Victory UI is now: {(victoryUI.activeSelf ? "ACTIVE" : "INACTIVE")}");
        }
        else
        {
            Debug.LogError("Victory UI is NULL!");
        }
    }
}