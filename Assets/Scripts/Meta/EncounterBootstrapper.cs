using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the encounter when the scene loads, then hands control to <see cref="TurnManager"/>.
/// </summary>
/// <remarks>
/// <para>Phase 2a spawns enemies from an <see cref="EncounterDefinition"/>; crew are still placed
/// by hand in the scene until Phase 2b spawns them from run state.</para>
///
/// <para><b>Why spawning happens in Awake:</b> <see cref="TurnManager.BeginBattle"/> snapshots the
/// turn order, so every combatant must exist before it runs. Unity guarantees all
/// <c>Awake()</c> calls finish before any <c>Start()</c>, so spawning in Awake and starting the
/// battle in Start is ordered by the engine rather than by luck. Don't move spawning to Start and
/// try to fix it with Script Execution Order.</para>
///
/// <para><b>Why the inactive staging parent:</b> <c>DriveManager.Awake()</c>,
/// <c>ParrySystem.Awake()</c> and <c>EnemyManager.Awake()</c> all dereference
/// <c>CharacterManager.characterData</c>, and the enemy prefab has no default assigned. A plain
/// <c>Instantiate</c> would run those Awakes — and throw — before the data could be set.
/// Instantiating under an inactive parent defers Awake; reparenting to the (active) spawn point
/// is what actually wakes the object, by which time its data is in place.</para>
/// </remarks>
public class EncounterBootstrapper : MonoBehaviour
{
    [Header("What to fight")]
    [Tooltip("Leave empty to use whatever enemies are already placed in the scene by hand.")]
    [SerializeField] private EncounterDefinition encounter;

    [Header("Enemy spawning")]
    [Tooltip("Prefab used for every spawned enemy — normally EnemyCharacterVariant.prefab.")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("One spawn point per enemy, filled in order. Must be under the same Canvas as the other combatants.")]
    [SerializeField] private Transform[] enemySpawnPoints;

    [Header("Crew spawning")]
    [Tooltip("Prefab used for every spawned crew member — normally PlayerCharacter.prefab. " +
             "Leave empty to use crew placed in the scene by hand instead.")]
    [SerializeField] private GameObject crewPrefab;

    [Tooltip("One spawn point per crew berth, filled in order of the run's living crew.")]
    [SerializeField] private Transform[] crewSpawnPoints;

    [Header("Wiring")]
    [Tooltip("Left empty, this is found automatically.")]
    [SerializeField] private TurnManager turnManager;

    private readonly List<CharacterManager> spawnedEnemies = new List<CharacterManager>();
    private readonly List<CharacterManager> spawnedCrew = new List<CharacterManager>();

    /// <summary>Enemies this bootstrapper spawned, in spawn order.</summary>
    public IReadOnlyList<CharacterManager> SpawnedEnemies => spawnedEnemies;

    /// <summary>Crew this bootstrapper spawned, in spawn order.</summary>
    public IReadOnlyList<CharacterManager> SpawnedCrew => spawnedCrew;

    private void Awake()
    {
        if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();

        if (turnManager == null)
        {
            Debug.LogError("[EncounterBootstrapper] No TurnManager in the scene — can't start a battle.");
            return;
        }

        // One staging area for both: combatant components read characterData in Awake, so nothing
        // may wake until its data is assigned. See the class remarks.
        GameObject staging = new GameObject("~SpawnStaging");
        staging.transform.SetParent(transform, false);
        staging.SetActive(false);

        SpawnCrew(staging.transform);
        SpawnEnemies(staging.transform);

        Destroy(staging);

        // GameManager caches its Player/Enemy lists by tag and would otherwise keep the snapshot it
        // took in its own Awake — empty or partial, since these combatants were built during Awake
        // too and component order is arbitrary.
        GameManager.Instance.FindCharacters();
    }

    private void SpawnCrew(Transform staging)
    {
        if (crewPrefab == null)
        {
            Debug.Log("[EncounterBootstrapper] No crew prefab assigned — using the crew already in the scene.");
            return;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            Debug.LogWarning("[EncounterBootstrapper] No active run, so there's no crew to spawn. " +
                             "Add a RunManager to the scene, or place crew by hand and clear the crew prefab.");
            return;
        }

        if (crewSpawnPoints == null || crewSpawnPoints.Length == 0)
        {
            Debug.LogError("[EncounterBootstrapper] No crew spawn points assigned — cannot place the crew.");
            return;
        }

        // Only the living sail on. Dead crew keep their record for the run summary but never spawn.
        var living = new List<CrewMemberState>(RunManager.Instance.Current.LivingCrew);

        if (living.Count == 0)
        {
            Debug.LogWarning("[EncounterBootstrapper] Every crew member is dead — the run should have ended.");
            return;
        }

        if (living.Count > crewSpawnPoints.Length)
        {
            Debug.LogWarning($"[EncounterBootstrapper] {living.Count} living crew but only " +
                             $"{crewSpawnPoints.Length} spawn points — the extras won't appear.");
        }

        int spawnCount = Mathf.Min(living.Count, crewSpawnPoints.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform spawnPoint = crewSpawnPoints[i];

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[EncounterBootstrapper] Crew spawn point {i} is empty — skipping.");
                continue;
            }

            SpawnCrewMember(living[i], spawnPoint, staging);
        }

        Debug.Log($"[EncounterBootstrapper] Spawned {spawnedCrew.Count} crew.");
    }

    private void SpawnCrewMember(CrewMemberState member, Transform spawnPoint, Transform staging)
    {
        if (ContentDatabase.Instance == null) return;

        Character characterData = ContentDatabase.Instance.GetCharacter(member.characterId);

        if (characterData == null)
        {
            // GetCharacter already logged the specifics.
            Debug.LogError($"[EncounterBootstrapper] Can't spawn '{member.displayName}' — unresolved character id.");
            return;
        }

        GameObject instance = Instantiate(crewPrefab, staging);

        CharacterManager manager = instance.GetComponent<CharacterManager>();

        if (manager == null)
        {
            Debug.LogError($"[EncounterBootstrapper] Crew prefab '{crewPrefab.name}' has no CharacterManager.");
            Destroy(instance);
            return;
        }

        // Both must be set before the object wakes. characterData in particular: DriveManager.Awake
        // caches buffNextActionMultiplier off it, so waking with the prefab's default and swapping
        // afterwards would leave the wrong character's drive multiplier cached.
        manager.characterData = characterData;
        manager.AssignCrewState(member);
        instance.name = member.displayName;

        instance.transform.SetParent(spawnPoint, false);
        ResetLocalTransform(instance.transform);

        if (!instance.CompareTag("Player"))
        {
            Debug.LogError($"[EncounterBootstrapper] Spawned crew '{instance.name}' is tagged '{instance.tag}', " +
                           "not 'Player'. Target-finding and win/lose checks rely on that tag — fix it on the prefab.");
        }

        spawnedCrew.Add(manager);
    }

    private void Start()
    {
        if (turnManager == null) return;

        // Set before BeginBattle so the reward is known when the battle ends.
        turnManager.SetEncounter(encounter);

        // Every combatant exists by now (spawning happened in Awake), so the turn-order snapshot
        // is complete. BeginBattle is idempotent: if TurnManager.autoStartBattle is still ticked
        // it will already have run, and this call is ignored with a warning.
        turnManager.BeginBattle();
    }

    private void SpawnEnemies(Transform staging)
    {
        if (encounter == null)
        {
            Debug.Log("[EncounterBootstrapper] No EncounterDefinition assigned — using the enemies already in the scene.");
            return;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError("[EncounterBootstrapper] No enemy prefab assigned — cannot spawn the encounter.");
            return;
        }

        List<Enemy> toSpawn = encounter.BuildSpawnList();

        if (toSpawn.Count == 0)
        {
            Debug.LogWarning($"[EncounterBootstrapper] '{encounter.displayName}' defines no enemies.");
            return;
        }

        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            Debug.LogError("[EncounterBootstrapper] No enemy spawn points assigned — cannot place the encounter.");
            return;
        }

        if (toSpawn.Count > enemySpawnPoints.Length)
        {
            Debug.LogWarning($"[EncounterBootstrapper] '{encounter.displayName}' wants {toSpawn.Count} enemies but " +
                             $"only {enemySpawnPoints.Length} spawn points exist. The extras won't appear.");
        }

        int spawnCount = Mathf.Min(toSpawn.Count, enemySpawnPoints.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform spawnPoint = enemySpawnPoints[i];

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[EncounterBootstrapper] Enemy spawn point {i} is empty — skipping.");
                continue;
            }

            SpawnEnemy(toSpawn[i], spawnPoint, staging);
        }

        Debug.Log($"[EncounterBootstrapper] Spawned {spawnedEnemies.Count} enemies for '{encounter.displayName}'.");
    }

    private void SpawnEnemy(Enemy enemyData, Transform spawnPoint, Transform staging)
    {
        // Instantiated under the inactive staging object, so Awake hasn't run yet.
        GameObject instance = Instantiate(enemyPrefab, staging);

        CharacterManager manager = instance.GetComponent<CharacterManager>();

        if (manager == null)
        {
            Debug.LogError($"[EncounterBootstrapper] Enemy prefab '{enemyPrefab.name}' has no CharacterManager.");
            Destroy(instance);
            return;
        }

        manager.characterData = enemyData;
        instance.name = enemyData.characterName;

        // Reparenting onto an active spawn point is what wakes the object — data is set by now.
        instance.transform.SetParent(spawnPoint, false);
        ResetLocalTransform(instance.transform);

        if (!instance.CompareTag("Enemy"))
        {
            Debug.LogError($"[EncounterBootstrapper] Spawned '{instance.name}' is tagged '{instance.tag}', not 'Enemy'. " +
                           "Target-finding and win/lose checks rely on that tag — fix it on the prefab.");
        }

        spawnedEnemies.Add(manager);
    }

    /// <summary>Drops the instance exactly onto its spawn point, RectTransform or not.</summary>
    private static void ResetLocalTransform(Transform instanceTransform)
    {
        if (instanceTransform is RectTransform rect)
        {
            rect.anchoredPosition3D = Vector3.zero;
        }
        else
        {
            instanceTransform.localPosition = Vector3.zero;
        }

        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;
    }
}
