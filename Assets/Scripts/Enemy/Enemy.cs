using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Scriptable Objects/Enemy")]
public class Enemy : Character
{
    
    private EnemyDriveManager enemyDriveManager;

    [Header("Rewards")]
    [Min(0)]
    [Tooltip("Plunder this enemy is worth. EncounterDefinition sums these across everything it " +
             "spawns, so a fight's payout follows from what's in it rather than being set by hand.")]
    public int plunderReward = 5;

    [Header("AI Behavior Settings")]
    [SerializeField, Range(0.1f, 1.0f)]
    public float healThreshold = 0.3f;
    
    [Header("Drive Usage")]
    [SerializeField]
    public EnemyDriveConfig driveConfig = new EnemyDriveConfig();
   
    [SerializeField] 
    public bool showDebugInfo = false;
    
    public enum TargetingStrategy
    {
        Random,
        LowestHealth,
        HighestHealth,
        ClosestToDefeating
    }

    [Header("AI Targeting")]
    public TargetingStrategy targetingStrategy = TargetingStrategy.Random;
    
    [Header("Action Weights (Higher = More Likely)")]
    [SerializeField, Range(0f, 1f)] 
    public float attackWeight = 0.7f;
    
    [SerializeField, Range(0f, 1f)] 
    public float healWeight = 0.9f; // High priority when needed
    
    [SerializeField, Range(0f, 1f)] 
    public float buffWeight = 0.15f;
    
    [SerializeField, Range(0f, 1f)] 
    public float debuffWeight = 0.2f;
    
    [Header("Randomization")]
    [SerializeField, Range(0f, 0.5f)] 
    public float randomnessAmount = 0.1f; // Add some unpredictability
    
    
}