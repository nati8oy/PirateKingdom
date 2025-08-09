using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    
    public static int GetAlivePlayerCount()
    {
        return PlayerCharacters.Count(player => player != null && player.GetComponent<CharacterManager>() != null);
    }
    
    public static int GetAliveEnemyCount()
    {
        return EnemyCharacters.Count(enemy => enemy != null && enemy.GetComponent<CharacterManager>() != null);
    }
}