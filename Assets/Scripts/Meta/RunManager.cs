using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// NOTE: deliberately no `using System;` — it would make `System.Action` collide with the game's
// own `Action` ScriptableObject. System types are fully qualified instead.

/// <summary>
/// Owns the live <see cref="RunState"/> and its persistence. Survives scene loads, so the voyage
/// map and the encounter scene are looking at the same run.
/// </summary>
/// <remarks>
/// Put one of these on a GameObject in the encounter scene for now; once there's a boot scene it
/// should live there instead. Everything reads run state through <c>RunManager.Instance</c>, and
/// every consumer must tolerate there being no active run — pressing Play straight into a fight
/// with no save is a supported case, and just means default full-health characters.
/// </remarks>
public class RunManager : MonoBehaviour
{
    private const string SaveFileName = "run.json";

    [Header("Debug / Bootstrap")]
    [Tooltip("If no save file exists, start a run from the crew below. Lets encounters be played " +
             "before the voyage map exists. Turn off once runs are started from the map.")]
    [SerializeField] private bool startDebugRunIfNoSave = true;

    [Tooltip("Crew the debug run begins with.")]
    [SerializeField] private List<Character> debugStartingCrew = new List<Character>();

    [Header("Run State — live, read-only")]
    [Tooltip("Serialized purely so you can watch it change in the Inspector during play. Don't hand-edit.")]
    [SerializeField] private RunState current;

    private static RunManager _instance;

    /// <summary>
    /// The live RunManager, or null if there isn't one in the scene.
    /// </summary>
    /// <remarks>
    /// Resolved lazily, and the run is loaded on first access rather than only in <c>Awake</c>.
    /// <see cref="EncounterBootstrapper"/> spawns the crew from its own <c>Awake</c>, and Unity
    /// gives no ordering guarantee between GameObjects — so whichever of the two happened to wake
    /// first would decide whether the crew existed at all. Same reasoning as
    /// <c>ParryInputManager.Instance</c>: script execution order must not be load-bearing.
    /// </remarks>
    public static RunManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RunManager>();
                if (_instance != null) _instance.EnsureRunLoaded();
            }

            return _instance;
        }
    }

    public RunState Current => current;
    public bool HasActiveRun => current != null;

    private bool runLoaded;

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // No-op if an earlier Instance access already loaded the run.
        EnsureRunLoaded();
    }

    /// <summary>
    /// Loads the run, or starts the debug one. Runs at most once, from whichever comes first:
    /// this component's <c>Awake</c>, or the first <see cref="Instance"/> access.
    /// </summary>
    private void EnsureRunLoaded()
    {
        if (runLoaded) return;
        runLoaded = true;

        if (LoadRun())
        {
            // Loud on purpose: an existing save wins over Debug Starting Crew, so edits to that
            // list appear to do nothing until the save is cleared.
            Debug.Log("[RunManager] Resumed the saved run — 'Debug Starting Crew' was NOT used. " +
                      "To start over from that list, use 'Delete Save' or 'Start Debug Run Now' " +
                      "on the RunManager's ⋮ menu.");
            return;
        }

        if (startDebugRunIfNoSave)
        {
            Debug.Log("[RunManager] No save found — starting a debug run from 'Debug Starting Crew'.");
            StartNewRun(debugStartingCrew);
        }
    }

    /// <summary>
    /// Editor convenience: throw away the current run and rebuild it from
    /// <see cref="debugStartingCrew"/>, so edits to that list take effect without hunting for the
    /// save file. Overwrites the save.
    /// </summary>
    [ContextMenu("Start Debug Run Now")]
    public void StartDebugRunNow()
    {
        Debug.Log("[RunManager] Restarting the run from 'Debug Starting Crew'.");
        StartNewRun(debugStartingCrew);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Discards any current run and builds a fresh one from the given crew.</summary>
    public void StartNewRun(IEnumerable<Character> startingCrew)
    {
        current = new RunState();

        if (startingCrew != null)
        {
            foreach (Character character in startingCrew)
            {
                CrewMemberState member = CrewMemberState.CreateFrom(character);
                if (member != null) current.crew.Add(member);
            }
        }

        if (current.crew.Count == 0)
        {
            Debug.LogWarning("[RunManager] Started a run with no crew. Populate 'Debug Starting Crew' on the RunManager.");
        }

        SaveRun();
        Debug.Log($"[RunManager] New run started with {current.crew.Count} crew.");
    }

    public CrewMemberState GetCrewMember(string characterId)
    {
        return current?.FindCrewMember(characterId);
    }

    // --- Encounter boundary ---
    // Crew who died during the encounter currently being fought. Tracked here because the dying
    // GameObject is destroyed on the spot, so the battle's end can't read casualties back off the
    // scene — but the run record survives, and this is the list of which records are new losses.
    private readonly List<CrewMemberState> deathsThisEncounter = new List<CrewMemberState>();
    private bool encounterInProgress;

    /// <summary>
    /// Opens an encounter. Called from <c>TurnManager.BeginBattle()</c>, so it runs once per fight
    /// regardless of whether the bootstrapper or <c>autoStartBattle</c> owns the start.
    /// </summary>
    public void BeginEncounter()
    {
        deathsThisEncounter.Clear();
        encounterInProgress = true;
    }

    /// <summary>
    /// Records a permadeath in memory. Deliberately does <b>not</b> save — see
    /// <see cref="CompleteEncounter"/> for why the write waits for the encounter boundary.
    /// </summary>
    public void ReportCrewDeath(CrewMemberState member)
    {
        if (member == null) return;

        member.isDead = true;
        member.currentHealth = 0f;

        if (!deathsThisEncounter.Contains(member)) deathsThisEncounter.Add(member);
    }

    /// <summary>
    /// Closes the encounter: awards plunder, builds the <see cref="EncounterResult"/>, and writes
    /// the run to disk <b>once</b>. Returns the result, or null if there's no active run.
    /// </summary>
    /// <remarks>
    /// <para>This is the single persistence boundary. Health syncs into <see cref="RunState"/>
    /// continuously as damage lands, and deaths are recorded by
    /// <see cref="ReportCrewDeath"/> — but neither touches the disk. Only this does.</para>
    ///
    /// <para>Consequence, and it's intended: quitting mid-fight rewinds cleanly to the start of the
    /// encounter, losing both the damage <i>and</i> the deaths. Previously a death wrote through
    /// immediately while damage didn't, so quitting after a casualty kept the corpse and refunded
    /// everyone else's wounds.</para>
    /// </remarks>
    public EncounterResult CompleteEncounter(bool playerVictory, EncounterDefinition encounter)
    {
        if (!encounterInProgress)
        {
            Debug.LogWarning("[RunManager] CompleteEncounter() called without a matching BeginEncounter() — " +
                             "ignoring, so the run isn't written twice.");
            return null;
        }

        encounterInProgress = false;

        if (current == null)
        {
            Debug.Log("[RunManager] Encounter finished with no active run — nothing to persist.");
            return null;
        }

        var result = new EncounterResult
        {
            encounterName = encounter != null ? encounter.displayName : "hand-placed encounter",
            playerVictory = playerVictory
        };

        // Counts survived encounters, so it only advances on a win — a wipe ends the run.
        if (playerVictory)
        {
            current.encountersSurvived++;
        }

        result.encountersSurvived = current.encountersSurvived;

        // Rewards are for winning only. A missing definition just means no payout — the scene is
        // still playable with enemies placed by hand.
        if (playerVictory && encounter != null && encounter.plunderReward > 0)
        {
            current.plunder += encounter.plunderReward;
            result.plunderAwarded = encounter.plunderReward;
        }

        result.plunderTotal = current.plunder;

        foreach (CrewMemberState member in current.crew)
        {
            if (member == null) continue;

            bool diedHere = deathsThisEncounter.Contains(member);

            // Crew lost in an earlier encounter took no part in this one, so they're not in the report.
            if (member.isDead && !diedHere) continue;

            result.crew.Add(new EncounterResult.CrewOutcome
            {
                characterId = member.characterId,
                displayName = member.displayName,
                endHealth = member.currentHealth,
                died = diedHere
            });
        }

        deathsThisEncounter.Clear();

        SaveRun();

        return result;
    }

    /// <summary>
    /// Writes the run to disk. In normal play the only caller is
    /// <see cref="CompleteEncounter"/> — deliberately NOT on every health change or death: we can't
    /// serialise a battle in progress, so a mid-fight save would reload with the crew damaged and
    /// the enemies restored. Saving per encounter means quitting mid-fight cleanly rewinds to the
    /// start of that encounter.
    /// </summary>
    [ContextMenu("Save Run")]
    public void SaveRun()
    {
        if (current == null) return;

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(current, true));
            Debug.Log($"[RunManager] Run saved — {current.LivingCrewCount}/{current.crew.Count} crew alive.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RunManager] Failed to save run to {SavePath}: {e.Message}");
        }
    }

    /// <summary>Loads the run from disk. Returns false if there was nothing to load.</summary>
    [ContextMenu("Load Run")]
    public bool LoadRun()
    {
        try
        {
            if (!File.Exists(SavePath)) return false;

            RunState loaded = JsonUtility.FromJson<RunState>(File.ReadAllText(SavePath));
            if (loaded == null) return false;

            if (loaded.saveVersion != RunState.CurrentSaveVersion)
            {
                Debug.LogWarning($"[RunManager] Save is version {loaded.saveVersion}, expected " +
                                 $"{RunState.CurrentSaveVersion}. Discarding it and starting fresh.");
                return false;
            }

            current = loaded;
            Debug.Log($"[RunManager] Loaded run: {current.LivingCrewCount}/{current.crew.Count} crew alive.");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RunManager] Failed to load run from {SavePath}: {e.Message}");
            return false;
        }
    }

    /// <summary>Wipes the save and the in-memory run. Handy for testing a fresh start.</summary>
    [ContextMenu("Delete Save")]
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Debug.Log($"[RunManager] Deleted save at {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RunManager] Failed to delete save: {e.Message}");
        }

        current = null;
    }

    [ContextMenu("Log Save Path")]
    private void LogSavePath()
    {
        Debug.Log($"[RunManager] Save path: {SavePath}");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only. Deletes the run save from the top menu — no RunManager instance needed, so it
    /// works while the game isn't playing. The next Play then rebuilds the run from
    /// 'Debug Starting Crew'.
    /// </summary>
    /// <remarks>
    /// This exists because a save on disk always beats the Debug Starting Crew list, which makes
    /// edits to that list look like they do nothing. Deleting the save is the reset.
    /// </remarks>
    [MenuItem("Tools/Pirate Kingdom/Delete Run Save")]
    private static void DeleteRunSaveFromMenu()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log($"[RunManager] No save to delete at {SavePath} — the next Play will start a fresh debug run.");
            return;
        }

        // Summarise before destroying it, so a mis-click doesn't lose the run silently.
        try
        {
            RunState existing = JsonUtility.FromJson<RunState>(File.ReadAllText(SavePath));
            if (existing != null)
            {
                var names = new List<string>();
                foreach (CrewMemberState member in existing.crew)
                {
                    if (member != null) names.Add(member.isDead ? $"{member.displayName} (dead)" : $"{member.displayName} {member.currentHealth:0}hp");
                }
                Debug.Log($"[RunManager] Deleting save containing: {string.Join(", ", names)}");
            }
        }
        catch (System.Exception)
        {
            // Unreadable save — deleting it is exactly what we want anyway.
        }

        File.Delete(SavePath);
        Debug.Log($"[RunManager] Save deleted. Press Play to start a fresh run from 'Debug Starting Crew'.");
    }

    [MenuItem("Tools/Pirate Kingdom/Log Run Save Path")]
    private static void LogSavePathFromMenu()
    {
        Debug.Log($"[RunManager] Save path: {SavePath}\nExists: {File.Exists(SavePath)}");
    }
#endif
}
