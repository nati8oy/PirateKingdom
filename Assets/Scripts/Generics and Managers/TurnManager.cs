
using System.Collections;
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

    [Header("Round Pacing")]
    [Tooltip("Seconds between a round ending and damage-over-time ticking. Gives the tick its own " +
             "beat instead of it landing on the tail of the previous character's turn. 0 skips the wait.")]
    [SerializeField] private float statusTickDelay = 0.6f;

    [Tooltip("Seconds between the damage-over-time tick and the first character acting. Without this " +
             "the next turn takes the screen back before the damage numbers can be read.")]
    [SerializeField] private float statusTickSettleDelay = 0.8f;

    // True while the round-transition coroutine is mid-flight. Load-bearing: see Update().
    private bool roundTransitionInProgress;
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
        
        // Check if the current character is still alive.
        //
        // Suppressed while a round transition is in flight. During that pause currentCharacterTurn
        // still points at the LAST character of the previous round, who may well have just died —
        // so without this guard the branch fires every frame, each one calling CompleteTurn() and
        // kicking off another round transition. That is the log-spam death spiral again, just via a
        // different door.
        if (!roundTransitionInProgress &&
            (currentCharacterTurn == null || currentCharacterTurn.gameObject == null))
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
            // Anyone who bled out in this round's status tick is excluded here, which is the point
            // of ticking before the roll — they're still in the scene (Unity destroys them on their
            // next Update, which can't run inside this chain) but they must not be handed a turn.
            //
            // IsAlive rather than a health comparison: an uninitialised character also reads 0
            // health, and filtering those out would empty the turn order at battle start.
            if (!characterManager.IsAlive) continue;

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
                    AdvanceToNextRound();
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

                // Status effects are NOT ticked here — they resolve for everyone at once at the top
                // of the round, in AdvanceToNextRound(). See the remarks there for why.
                //
                // D2 inserts the stun check here, since that IS per-character: read IsStunned, then
                // skip via AdvancePastSkippedTurn() — never by calling CompleteTurn() directly.

                // Nobody has been skipped on the way to acting, so the consecutive-skip guard resets.
                turnSkipDepth = 0;

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
    /// Hands the turn on because the current character can't take it. Refuses to recurse forever if
    /// that turns out to be true of everyone.
    /// </summary>
    /// <remarks>
    /// <b>Currently uncalled, and deliberately kept.</b> Its original caller — the mid-turn
    /// bleed-out skip — is gone now that status effects tick for everyone at the top of the round
    /// and the dead are filtered out of the turn order before it's built. Phase D2's stun is the
    /// next skip and must route through here; the warning below is the reason it exists at all.
    /// </remarks>
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
            AdvanceToNextRound();
            return;
        }

        SetCharacterTurn();
    }

    /// <summary>
    /// Closes the round and opens the next one: status effects tick for everybody, then initiative
    /// is re-rolled, then the first character acts.
    /// </summary>
    /// <remarks>
    /// <b>The order here is the whole point.</b> Status effects resolve <i>before</i>
    /// <see cref="GetTurnOrder"/> builds the new order, so anyone who bleeds out is already excluded
    /// from it and never has to be skipped mid-round. That's why <c>GetTurnOrder</c> filters on
    /// <c>CharacterManager.IsAlive</c> — the corpse is still in the scene at this point, because
    /// Unity doesn't destroy it until the dying object's next <c>Update()</c>, which can't run
    /// inside this synchronous chain.
    ///
    /// Extracted because the round boundary is reached from two places (a turn completing, and the
    /// skip-destroyed loop in <see cref="SetCharacterTurn"/> running off the end) and they must not
    /// drift apart.
    /// </remarks>
    private void AdvanceToNextRound()
    {
        // Two coroutines running this at once would double-advance the round and roll initiative
        // twice. The Update() guard should already prevent the only path that could do it, but this
        // is the cheap belt to that braces.
        if (roundTransitionInProgress) return;

        StartCoroutine(AdvanceToNextRoundRoutine());
    }

    private IEnumerator AdvanceToNextRoundRoutine()
    {
        roundTransitionInProgress = true;

        try
        {
            RoundComplete();

            // Beat before anything bleeds, so the tick reads as its own event rather than something
            // that happened during the previous character's turn.
            if (statusTickDelay > 0f) yield return new WaitForSeconds(statusTickDelay);

            if (battleEnded) yield break;

            TickRoundStatusEffects();

            // Beat after, so the damage numbers are readable before the next character's turn takes
            // the screen back. Without this the tick is technically visible and practically not.
            if (statusTickSettleDelay > 0f) yield return new WaitForSeconds(statusTickSettleDelay);

            if (battleEnded) yield break;

            currentTurnIndex = 0;
            GetTurnOrder();
            SetCharacterTurn();
        }
        finally
        {
            // Must clear on every exit, including the battleEnded breaks above — a stuck flag would
            // freeze the turn system with no error.
            roundTransitionInProgress = false;
        }
    }

    /// <summary>
    /// Ticks damage over time on every living combatant at once, at the top of the round.
    /// </summary>
    /// <remarks>
    /// Everyone bleeds together rather than each at the start of their own turn. Two reasons: the
    /// whole board resolves before initiative is re-rolled, so a bleed-out never has to be handled
    /// as a mid-round skip; and simultaneous ticks read as one clear beat instead of dribbling out
    /// across the round.
    ///
    /// Safe to iterate while characters die — <c>Destroy</c> is deferred to end of frame, so the
    /// array stays valid for the whole loop.
    /// </remarks>
    private void TickRoundStatusEffects()
    {
        int ticked = 0;
        int died = 0;

        foreach (CharacterManager character in FindObjectsOfType<CharacterManager>())
        {
            if (character == null || !character.IsAlive) continue;

            // The damage-over-time cap is a fraction of MaxHealth, and this now runs BEFORE
            // GetTurnOrder() (which used to be what refreshed stats each round), so refresh here or
            // the cap could be computed against a stale maximum.
            character.RefreshStats();

            float damage = character.TickDamageOverTime();

            // Expiry counted in the same pass the damage fires, so a 2-round bleed ticks twice.
            character.AdvanceStatuses();

            if (damage <= 0f) continue;

            ticked++;
            if (!character.IsAlive) died++;
        }

        if (ticked > 0)
        {
            Debug.Log($"[TurnManager] Round {roundCounterInt} status tick: {ticked} combatant(s) took " +
                      $"damage over time, {died} died. Turn order is rolled after this, so the dead " +
                      "never get a turn.");
        }
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