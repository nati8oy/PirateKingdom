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
        if (encountersSurvivedText == null || result == null) return;

        encountersSurvivedText.text = string.Format(encountersSurvivedFormat, result.encountersSurvived);
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
