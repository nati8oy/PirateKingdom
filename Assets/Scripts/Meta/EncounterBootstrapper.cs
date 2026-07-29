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

    [Header("Spawning")]
    [Tooltip("Prefab used for every spawned enemy — normally EnemyCharacterVariant.prefab.")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("One spawn point per enemy, filled in order. Must be under the same Canvas as the hand-placed combatants.")]
    [SerializeField] private Transform[] enemySpawnPoints;

    [Header("Wiring")]
    [Tooltip("Left empty, this is found automatically.")]
    [SerializeField] private TurnManager turnManager;

    private readonly List<CharacterManager> spawnedEnemies = new List<CharacterManager>();

    /// <summary>Enemies this bootstrapper spawned, in spawn order.</summary>
    public IReadOnlyList<CharacterManager> SpawnedEnemies => spawnedEnemies;

    private void Awake()
    {
        if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();

        if (turnManager == null)
        {
            Debug.LogError("[EncounterBootstrapper] No TurnManager in the scene — can't start a battle.");
            return;
        }

        SpawnEnemies();
    }

    private void Start()
    {
        // Every combatant exists by now (spawning happened in Awake), so the turn-order snapshot
        // is complete. BeginBattle is idempotent: if TurnManager.autoStartBattle is still ticked
        // it will already have run, and this call is ignored with a warning.
        turnManager?.BeginBattle();
    }

    private void SpawnEnemies()
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

        // See the class remarks: combatant components read characterData in Awake, so instances
        // must not wake until their data is assigned.
        GameObject staging = new GameObject("~SpawnStaging");
        staging.transform.SetParent(transform, false);
        staging.SetActive(false);

        int spawnCount = Mathf.Min(toSpawn.Count, enemySpawnPoints.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform spawnPoint = enemySpawnPoints[i];

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[EncounterBootstrapper] Enemy spawn point {i} is empty — skipping.");
                continue;
            }

            SpawnEnemy(toSpawn[i], spawnPoint, staging.transform);
        }

        Destroy(staging);

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
