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

    public static RunManager Instance { get; private set; }

    public RunState Current => current;
    public bool HasActiveRun => current != null;

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Runs in Awake so the run is ready before any CharacterManager.Start() tries to bind.
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
        if (Instance == this) Instance = null;
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

    /// <summary>
    /// Writes the run to disk. Called at encounter boundaries and on death — deliberately NOT on
    /// every health change: we can't serialise a battle in progress, so a mid-fight save would
    /// reload with the crew damaged and the enemies restored. Saving per encounter means quitting
    /// mid-fight cleanly rewinds to the start of that encounter.
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
