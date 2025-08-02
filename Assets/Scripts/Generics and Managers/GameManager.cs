using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static List<GameObject> PlayerCharacters = new List<GameObject>();
    public static  List<GameObject> EnemyCharacters = new List<GameObject>();

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
        EnemyCharacters.AddRange(enemies);
        
        PlayerCharacters.Clear();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        PlayerCharacters.AddRange(players);
    
    }

    private void Initialize()
    {
        // Add initialization logic here
        FindCharacters();
    }
}
