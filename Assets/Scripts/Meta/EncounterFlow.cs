using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// NOTE: deliberately no `using System;` — it would make `System.Action` collide with the game's
// own `Action` ScriptableObject. System types are fully qualified below.

/// <summary>
/// Restarts the encounter from the victory/defeat screen, giving a repeatable attrition loop:
/// fight → win → fight the same encounter again with the crew's carried damage → until they wipe.
/// The point is to see how many encounters a crew can actually survive.
///
/// Reloading the scene is the whole mechanism. It works because persistence already happened:
/// <see cref="TurnManager.EndBattle"/> → <see cref="RunManager.CompleteEncounter"/> writes the run
/// once, and <see cref="RunManager"/> is <c>DontDestroyOnLoad</c>, so the live run survives the
/// reload while the scene rebuilds fresh enemies and respawns the surviving crew at their carried
/// health.
///
/// **This is a stand-in for Phase 3's real scene transitions**, not a design. When the voyage map
/// exists, moving between nodes replaces it.
/// </summary>
public class EncounterFlow : MonoBehaviour
{
    [Header("Optional UI")]
    [Tooltip("Filled with the run's survived-encounter count when an encounter ends. Leave empty if you don't want a label.")]
    [SerializeField] private TMP_Text encountersSurvivedText;

    [Tooltip("Format for the label above. {0} is the count.")]
    [SerializeField] private string encountersSurvivedFormat = "Encounters survived: {0}";

    [Tooltip("Filled with the run's total plunder when an encounter ends. Assign labels on the " +
             "defeat and/or victory screens — each is updated before either panel is shown.")]
    [SerializeField] private TMP_Text[] plunderTotalTexts;

    [Tooltip("Format for the plunder labels. {0} is the run total, {1} is the amount awarded this encounter.")]
    [SerializeField] private string plunderTotalFormat = "Plunder collected: {0}";

    [Header("Balancing")]
    [Tooltip("Log the crew's end-of-encounter health to the console. The attrition curve is what " +
             "tells you whether a gauntlet is tuned, rather than just where the run ended.")]
    [SerializeField] private bool logAttrition = true;

    [Tooltip("Crew below this fraction of max health are flagged in the log, so a party that only " +
             "just survived is visible at a glance.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthWarning = 0.3f;

    private TurnManager turnManager;

    private void Start()
    {
        turnManager = FindObjectOfType<TurnManager>();

        if (turnManager != null)
        {
            turnManager.OnEncounterComplete += HandleEncounterComplete;

            // The event may already have fired if this object was enabled late.
            if (turnManager.LastResult != null) HandleEncounterComplete(turnManager.LastResult);
        }
        else
        {
            Debug.LogWarning("[EncounterFlow] No TurnManager found — the survived-encounters label won't update.", this);
        }
    }

    private void OnDestroy()
    {
        if (turnManager != null) turnManager.OnEncounterComplete -= HandleEncounterComplete;
    }

    private void HandleEncounterComplete(EncounterResult result)
    {
        if (result == null) return;

        if (encountersSurvivedText != null)
        {
            encountersSurvivedText.text = string.Format(encountersSurvivedFormat, result.encountersSurvived);
        }

        // Set on every encounter end, not just defeat: TurnManager fires this before it activates
        // either panel, so whichever screen these labels live on is already correct when it opens.
        if (plunderTotalTexts != null)
        {
            string plunderLabel = string.Format(plunderTotalFormat, result.plunderTotal, result.plunderAwarded);

            foreach (TMP_Text label in plunderTotalTexts)
            {
                if (label != null) label.text = plunderLabel;
            }
        }

        if (logAttrition) LogAttrition(result);
    }

    /// <summary>
    /// One block per encounter: where the run is, and what state it left the crew in. Health is
    /// shown as a fraction of the max carried on the report, because the raw
    /// number means nothing across crew with different pools.
    /// </summary>
    private void LogAttrition(EncounterResult result)
    {
        var log = new System.Text.StringBuilder();

        string position = RunManager.Instance != null ? RunManager.Instance.DescribeSequencePosition() : string.Empty;
        string header = string.IsNullOrEmpty(position) ? $"#{result.encountersSurvived}" : position;

        log.Append($"[Balance] Encounter {header} '{result.encounterName}' — ")
           .Append(result.playerVictory ? "VICTORY" : "DEFEAT")
           .Append($" | survivors {result.SurvivorCount}, deaths {result.DeathCount}")
           .Append($" | plunder +{result.plunderAwarded} (total {result.plunderTotal})");

        foreach (EncounterResult.CrewOutcome outcome in result.crew)
        {
            if (outcome == null) continue;

            log.Append($"\n           {outcome.displayName,-14} ");

            if (outcome.died)
            {
                log.Append("DIED");
                continue;
            }

            // Read off the report rather than looked up: max health is resolved from the character's
            // class at their level, and only RunManager had the level to hand when this was built.
            if (outcome.maxHealth <= 0f)
            {
                log.Append($"{outcome.endHealth:0} hp");
                continue;
            }

            float fraction = outcome.endHealth / outcome.maxHealth;

            log.Append($"{outcome.endHealth:0}/{outcome.maxHealth:0} ({fraction:P0})");
            if (fraction <= lowHealthWarning) log.Append("  <-- low");
        }

        Debug.Log(log.ToString());
    }

    /// <summary>
    /// Reloads the encounter scene. Wire to the victory screen's "Next Fight" button.
    /// Crew return at their carried health, minus anyone who died; enemies are rebuilt from the
    /// <c>EncounterDefinition</c>.
    /// </summary>
    public void RestartEncounter()
    {
        int survived = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.Current.encountersSurvived
            : 0;

        Debug.Log($"[EncounterFlow] Restarting the encounter — {survived} survived so far.");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Rebuilds the run from the RunManager's Debug Starting Crew and reloads, so a wiped crew can
    /// start over. Wire to the defeat screen's "New Run" button.
    /// </summary>
    public void StartNewRun()
    {
        if (RunManager.Instance != null)
        {
            Debug.Log($"[EncounterFlow] Starting a new run after {RunManager.Instance.Current?.encountersSurvived ?? 0} " +
                      "survived encounter(s).");

            // Rebuilds in place and overwrites the save. Deliberately not destroying the
            // DontDestroyOnLoad instance — its static Instance would dangle until end of frame,
            // racing the scene load queued below.
            RunManager.Instance.StartDebugRunNow();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
