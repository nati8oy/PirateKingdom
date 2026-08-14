using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static List<GameObject> PlayerCharacters = new List<GameObject>();
    public static List<GameObject> EnemyCharacters = new List<GameObject>();

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    instance = go.AddComponent<GameManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    // PlayerCharacters/EnemyCharacters are static and this object survives scene loads, so without
    // this the lists would keep the snapshot taken on the very first Awake — stale the moment a new
    // encounter loads its own combatants. Guarded on `instance != this` so a duplicate being
    // destroyed in Awake can't unsubscribe the real one, matching ParryInputManager's convention.
    private void OnEnable()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindCharacters();
    }

    /// <summary>
    /// Rebuilds the cached Player/Enemy lists from the current scene. Call after spawning
    /// combatants at runtime — <see cref="EncounterBootstrapper"/> does.
    /// </summary>
    public void FindCharacters()
    {
        EnemyCharacters.Clear();
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        // Filter out null/destroyed objects
        EnemyCharacters.AddRange(enemies.Where(enemy => enemy != null && enemy.GetComponent<CharacterManager>() != null));
        
        PlayerCharacters.Clear();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        // Filter out null/destroyed objects
        PlayerCharacters.AddRange(players.Where(player => player != null && player.GetComponent<CharacterManager>() != null));
    }

    private void Initialize()
    {
        // Add initialization logic here
        FindCharacters();
    }
    
    // Both refresh first: they're static entry points, so a caller has no way to know whether the
    // cached lists are current, and answering from a stale snapshot is worse than the lookup cost.
    public static int GetAlivePlayerCount()
    {
        Instance.FindCharacters();
        return CountAlive(PlayerCharacters);
    }

    public static int GetAliveEnemyCount()
    {
        Instance.FindCharacters();
        return CountAlive(EnemyCharacters);
    }

    /// <summary>
    /// Counts combatants that are actually still in the fight.
    /// </summary>
    /// <remarks>
    /// Both helpers above used to count anything with a <c>CharacterManager</c> attached, which meant
    /// they answered "exists" while being named "alive" — a character at 0 health still counted until
    /// Unity destroyed it on its next <c>Update()</c>. Neither has a caller yet, so nothing was
    /// getting a wrong answer, but the first one to use them would have.
    ///
    /// <c>IsAlive</c> rather than a health comparison: it treats a not-yet-initialised character as
    /// alive, which is the safe direction here.
    /// </remarks>
    private static int CountAlive(List<GameObject> characters)
    {
        int count = 0;

        foreach (GameObject character in characters)
        {
            if (character == null) continue;

            CharacterManager manager = character.GetComponent<CharacterManager>();
            if (manager != null && manager.IsAlive) count++;
        }

        return count;
    }
}