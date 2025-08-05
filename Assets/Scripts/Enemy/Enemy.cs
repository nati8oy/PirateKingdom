using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Scriptable Objects/Enemy")]
public class Enemy : Character
{
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss,
        MiniBoss
    }

    public enum DifficultyLevel
    {
        Easy,
        Medium,
        Hard,
        Expert
    }


    public enum ItemRarity
    {
        Common,
        Rare,
        Unique
    }

    public enum TargetingStrategy
    {
        LowestHealth,
        HighestHealth,
        LowestDefense,
        HighestAttack,
        HighestSpeed,
        SpecificClass
    }

    private const float NORMAL_MULT = 1.0f;
    private const float ELITE_MULT = 1.5f;
    private const float MINIBOSS_MULT = 2.0f;
    private const float BOSS_MULT = 3.0f;

    [SerializeField] private EnemyType enemyType = EnemyType.Normal;
    [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Medium;

    [Header("Drop Rates")]
    [SerializeField] [Range(0f, 1f)] private float commonDropRate = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float rareDropRate = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float uniqueDropRate = 0.1f;

    [Header("Targeting")]
    [SerializeField] private TargetingStrategy targetingStrategy = TargetingStrategy.LowestHealth;
    //[SerializeField] private CharacterClass targetClass = CharacterClass.Duelist;

    public float GetStatMultiplier()
    {
        switch (enemyType)
        {
            case EnemyType.Elite:
                return ELITE_MULT;
            case EnemyType.MiniBoss:
                return MINIBOSS_MULT;
            case EnemyType.Boss:
                return BOSS_MULT;
            default:
                return NORMAL_MULT;
        }
    }
    
    
}
