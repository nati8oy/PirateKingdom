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
    
    /// <remarks>
    /// Serialized by integer value like every other enum here — rename freely, but **append only**.
    /// Reordering silently repoints every authored Enemy asset at a different strategy.
    /// </remarks>
    public enum TargetingStrategy
    {
        Random,
        LowestHealth,
        HighestHealth,
        ClosestToDefeating,

        /// <summary>
        /// Whoever has the most drive banked. Pairs with DriveDrain and HealthDrain — picking by
        /// health would otherwise mean draining whoever happens to have the emptiest meter.
        /// Falls back to random when nobody has any drive, so it can't fixate on one crew member.
        /// </summary>
        HighestDrive
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

    [Tooltip("Weight for DriveGrant actions — feeding an ally's drive meter. SelectTarget resolves " +
             "SingleAlly to another living enemy, falling back to this one when it's alone.")]
    [SerializeField, Range(0f, 1f)]
    public float driveGrantWeight = 0.15f;

    [Tooltip("Weight for DriveDrain actions — stripping a crew member's drive meter. Note this " +
             "only ever hits banked drive: the crew commit stacks on their own turn and spend them " +
             "the same turn, so there is nothing committed to steal by the time an enemy acts.")]
    [SerializeField, Range(0f, 1f)]
    public float driveDrainWeight = 0.25f;

    [Tooltip("Weight for ApplyStatus actions — stealth, and any pure status application. Note a " +
             "hostile one (a poison dart) is weighted by the same number as a friendly one (hiding a " +
             "wounded ally), so an enemy carrying both will pick between them at random.")]
    [SerializeField, Range(0f, 1f)]
    public float applyStatusWeight = 0.2f;

    [Tooltip("Weight for HealthDrain actions. Scored separately from Attack because it doubles as " +
             "this enemy's sustain — a high weight here is what makes an attrition fight, since the " +
             "crew have to out-damage the healing rather than just survive it.")]
    [SerializeField, Range(0f, 1f)]
    public float healthDrainWeight = 0.4f;

    [Header("Randomization")]
    [SerializeField, Range(0f, 0.5f)] 
    public float randomnessAmount = 0.1f; // Add some unpredictability
    
    
}