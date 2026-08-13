
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
    [Tooltip("Start the battle automatically on Play. Uncheck when something else spawns the combatants and calls BeginBattle() itself.")]
    [SerializeField] private bool autoStartBattle = true;
    private bool battleEnded = false;
    private bool battleStarted = false;

    public bool BattleStarted => battleStarted;

    // Which encounter is being fought, pushed in by EncounterBootstrapper before BeginBattle().
    // Null is fine and means a hand-placed fight — it just pays no plunder. Phase 3 will get this
    // from the map node instead.
    private EncounterDefinition encounterContext;

    /// <summary>The last finished encounter's report, or null until a battle has ended.</summary>
    public EncounterResult LastResult { get; private set; }

    /// <summary>
    /// Fires once, at battle end, with everything the encounter did to the run. Hook a results
    /// screen here. Fully-qualified System.Action on purpose — a bare `Action` is the game's own
    /// ability ScriptableObject.
    /// </summary>
    public event System.Action<EncounterResult> OnEncounterComplete;

    /// <summary>
    /// Tells the battle which encounter it is, so the reward can be paid at the end. Call before
    /// <see cref="BeginBattle"/>.
    /// </summary>
    public void SetEncounter(EncounterDefinition definition)
    {
        encounterContext = definition;
    }

    public List<GameObject> turnOrder = new List<GameObject>();
    private List<(GameObject obj, float initiative)> initiativeList = new List<(GameObject obj, float initiative)>();

    void Start()
    {
        // Legacy path: crew and enemies are placed directly in the scene, so there is nothing to
        // wait for. Once encounters spawn their combatants from run state, uncheck autoStartBattle
        // and let the spawner call BeginBattle() when it's finished.
        if (autoStartBattle)
        {
            BeginBattle();
        }
    }

    /// <summary>
    /// Starts the battle. Call this once every combatant exists in the scene — turn order is
    /// snapshotted here, so anything spawned afterwards will not take part in the first round.
    /// Safe to call only once; subsequent calls are ignored.
    /// </summary>
    public void BeginBattle()
    {
        if (battleStarted)
        {
            Debug.LogWarning("[TurnManager] BeginBattle() called but the battle has already started.");
            return;
        }

        battleStarted = true;
        battleEnded = false;

        // A fight starts with no end screen, always. Don't rely on the scene's authored state:
        // these panels are turned on at runtime, and anything that restarts a battle without a
        // fresh scene load (or reloads into a surviving Canvas) would otherwise begin with the
        // previous result still on screen.
        HideEndBattleUI();

        // Opened here rather than in the bootstrapper so it happens exactly once per fight whether
        // the bootstrapper or autoStartBattle owns the start.
        RunManager.Instance?.BeginEncounter();

        GetTurnOrder();
        SetCharacterTurn();
    }

    private void Update()
    {
        // Nothing to poll until the battle actually begins. Without this guard an encounter that
        // spawns its combatants would find zero living players on the first frame and immediately
        // declare a defeat.
        if (!battleStarted) return;

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

    /// <summary>
    /// Clears both end-of-battle panels and their shared result text, plus re-enables the action
    /// bar that <see cref="EndBattle"/> switches off.
    /// </summary>
    private void HideEndBattleUI()
    {
        if (victoryUI != null) victoryUI.SetActive(false);
        if (defeatUI != null) defeatUI.SetActive(false);
        if (battleResultText != null) battleResultText.gameObject.SetActive(false);

        // EndBattle() disables this and nothing turned it back on.
        if (actionsManager != null) actionsManager.gameObject.SetActive(true);
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

        // Commit surviving crew's health to the run before anything is torn down, so damage carries
        // into the next encounter. Casualties can't be collected here — a dying CharacterManager
        // destroys its GameObject on the spot — so they were reported to RunManager as they
        // happened. CompleteEncounter() then applies rewards and writes the run to disk once.
        foreach (var character in FindObjectsOfType<CharacterManager>())
        {
            character.SyncHealthToRunState();
        }

        LastResult = RunManager.Instance?.CompleteEncounter(playerVictory, encounterContext);

        if (LastResult != null)
        {
            Debug.Log($"[TurnManager] Encounter complete — {LastResult.Summary()}");
            OnEncounterComplete?.Invoke(LastResult);
        }

        // Hide turn marker if current character exists
        if (currentCharacterTurn != null && currentCharacterTurn.turnMarker != null)
        {
            currentCharacterTurn.turnMarker.gameObject.SetActive(false);
        }
        
        // Clear tooltip when battle ends
        if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
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
            if (battleResultText.gameObject.activeSelf == false)
            {
                battleResultText.gameObject.SetActive(true);
            }
            
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

        //Debug.Log("=== TURN ORDER ===");
        for (int i = 0; i < initiativeList.Count; i++)
        {
            var entry = initiativeList[i];
            var characterManager = entry.obj.GetComponent<CharacterManager>();
            string characterName = characterManager?.characterData?.characterName ?? entry.obj.name;
        
            //Debug.Log($"Turn {i + 1}: {characterName} - Initiative: {entry.initiative:F1}");
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

                // A selection belongs to the turn it was made in. Without this, an action picked
                // but never targeted stays live, and the next character to act — enemy or another
                // crew member — could resolve an action that isn't theirs.
                CombatController combatController = FindObjectOfType<CombatController>();
                if (combatController != null)
                {
                    combatController.ClearSelection();
                }


                // ADD THIS: Set the current character for input handling
                if (currentCharacterTurn.characterData.allegiance == Character.Allegiance.Player)
                {
                    PlayerInputHandler inputHandler = FindObjectOfType<PlayerInputHandler>();
                    if (inputHandler != null)
                    {
                        inputHandler.SetSelectedCharacter(currentCharacterTurn);
                    }
                    
                    // Clear tooltip when it's player turn (they'll select actions manually)
                    if (TooltipUI.Instance != null)
                    {
                        TooltipUI.Instance.ShowDefaultText();
                    }
                }
                else
                {
                    // Clear selection when it's an enemy's turn
                    PlayerInputHandler inputHandler = FindObjectOfType<PlayerInputHandler>();
                    if (inputHandler != null)
                    {
                        inputHandler.ClearSelectedCharacter();
                    }
                }
                
                // Update buffs and stats at the start of the character's turn
                currentCharacterTurn.UpdateBuffsForTurn();

                // --- Status effects, before the character gets to act ---
                // Runs after UpdateBuffsForTurn because that refreshes MaxHealth, which the
                // damage-over-time cap is a fraction of. Ticking here rather than at turn end is
                // deliberate: a wound has to be felt before you act or it reads as weightless.
                float damageOverTime = currentCharacterTurn.TickDamageOverTime();

                // The tick can kill. The GameObject isn't destroyed until CharacterManager's next
                // Update, so the reference is still valid — but a corpse must not be handed a turn.
                //
                // **Gated on the tick having actually dealt damage, and that is load-bearing.**
                // `CurrentHealth <= 0` does NOT mean "dead": it also means "Start() hasn't run yet",
                // because BindToRunState() is what fills CurrentHealth in and BeginBattle() runs
                // during the Start phase, where Unity's ordering between GameObjects is arbitrary.
                // Testing health alone read every not-yet-initialised character as a corpse, skipped
                // the whole roster, re-snapshotted the same uninitialised characters on the wrap and
                // recursed until the Editor died. TickDamageOverTime() returns 0 for them, so this
                // can now only fire for a character that genuinely just bled out.
                if (damageOverTime > 0f && (currentCharacterTurn == null || currentCharacterTurn.GetCurrentHealth() <= 0f))
                {
                    AdvancePastSkippedTurn("damage over time killed them");
                    return;
                }

                // Past every skip condition, so this character is genuinely taking a turn. Clearing
                // the counter here is what makes the guard measure *consecutive* skips.
                turnSkipDepth = 0;

                // Expiry counted in the same place the damage fires, so a 2-turn bleed ticks twice.
                // D2 inserts the stun check BETWEEN the tick and this, because a stun has to be read
                // before it's decremented or a 1-turn stun is consumed without ever being seen.
                currentCharacterTurn.AdvanceStatuses();

                if (currentCharacterTurn.turnMarker != null)
                {
                    currentCharacterTurn.turnMarker.gameObject.SetActive(true);
                }
            
                if (currentCharacterTurn != null && currentCharacterTurn.characterData != null)
                {
                    actionsManager.LoadCharacterActions(currentCharacterTurn);

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

    // Consecutive turns skipped without anyone actually acting. Reset the moment a character takes
    // a real turn, so this only ever counts an unbroken run.
    private int turnSkipDepth;

    /// <summary>
    /// Hands the turn on because the current character can't take it — bled out, and from D2,
    /// stunned. Refuses to recurse forever if that's true of everyone.
    /// </summary>
    /// <remarks>
    /// <b>This exists because it already happened.</b> <see cref="SetCharacterTurn"/> skipping via
    /// <see cref="CompleteTurn"/> is mutual recursion — <c>SetCharacterTurn → CompleteTurn →
    /// SetCharacterTurn</c> — and a skip condition true for every combatant recurses until the
    /// process dies. It ran inside a single frame, so nothing could ever clear the condition:
    /// no <c>Update</c>, no destruction of the dead, no battle-end poll. The Editor wrote a 1.1 GB
    /// log and crashed.
    ///
    /// The specific cause is fixed at the call site, but the shape is the hazard, not that one bug —
    /// D2's stun skip re-enters exactly the same way and would be far easier to trip, since a
    /// stunned character stays in the turn order. This turns any recurrence into one error line and
    /// a stalled turn rather than a dead Editor.
    /// </remarks>
    private void AdvancePastSkippedTurn(string reason)
    {
        string who = currentCharacterTurn != null
            ? currentCharacterTurn.characterData?.characterName ?? currentCharacterTurn.name
            : "someone";

        turnSkipDepth++;

        // +1 so a full lap of genuinely skippable combatants is allowed before we call it runaway.
        if (turnSkipDepth > turnOrder.Count + 1)
        {
            Debug.LogError($"[TurnManager] Every combatant skipped their turn in one pass ({turnSkipDepth} in a row) — " +
                           "stopping to avoid the infinite SetCharacterTurn/CompleteTurn recursion. " +
                           $"Last reason: {reason}. The battle-end check will pick this up next frame if everyone is dead.");
            turnSkipDepth = 0;
            return;
        }

        Debug.Log($"[TurnManager] {who} skips their turn — {reason}.");
        CompleteTurn();
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
        
        // Clear tooltip when turn ends
        if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
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
        //Debug.Log($"Round {roundCounterInt - 1} complete! Starting Round {roundCounterInt}");

        // Note: cooldowns and buffs are both ticked per character in CharacterManager.OnTurnComplete().
        // There used to be an UpdateActionCooldowns() call here as well, which double-ticked the
        // cooldowns of whichever character happened to end the round.
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
}