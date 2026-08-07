using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Describes one fight: which enemies show up and what it pays out. Authored per encounter, so a
/// single <c>Encounter.unity</c> can serve every combat node on the voyage.
/// </summary>
/// <remarks>
/// Enemies fill the bootstrapper's spawn points in list order, so ordering here is positioning.
/// </remarks>
[CreateAssetMenu(fileName = "Encounter", menuName = "Scriptable Objects/Encounter Definition")]
public class EncounterDefinition : ScriptableObject
{
    /// <summary>One enemy type and how many of them.</summary>
    [System.Serializable]
    public class EnemyGroup
    {
        public Enemy enemy;

        [Min(1)]
        [Tooltip("How many of this enemy. Each one takes its own spawn point.")]
        public int count = 1;
    }

    [Header("Presentation")]
    [Tooltip("Shown to the player when the encounter starts. Also handy for identifying the asset.")]
    public string displayName = "Encounter";

    [Header("Enemies")]
    [Tooltip("Filled into the bootstrapper's enemy spawn points in order — first group takes the first points.")]
    public List<EnemyGroup> enemies = new List<EnemyGroup>();

    [Header("Rewards")]
    [Min(0)]
    [Tooltip("Flat bonus added on top of what the enemies are individually worth — for an elite or " +
             "boss fight that should pay above the sum of its parts. Leave at 0 for a normal fight; " +
             "the payout then follows from the enemies alone.")]
    [FormerlySerializedAs("plunderReward")]
    public int bonusPlunder;

    /// <summary>
    /// What victory pays: every spawned enemy's own <see cref="Enemy.plunderReward"/>, plus
    /// <see cref="bonusPlunder"/>. Derived rather than authored, so adding an enemy to a fight
    /// raises its payout automatically and two encounters with the same enemies can't drift apart.
    /// </summary>
    public int TotalPlunderReward
    {
        get
        {
            int total = bonusPlunder;

            foreach (EnemyGroup group in enemies)
            {
                if (group == null || group.enemy == null) continue;

                total += group.enemy.plunderReward * Mathf.Max(1, group.count);
            }

            return total;
        }
    }

    /// <summary>
    /// Flattens the groups into one entry per spawn slot, in order. Skips empty rows so a
    /// half-filled list in the Inspector doesn't spawn nulls.
    /// </summary>
    public List<Enemy> BuildSpawnList()
    {
        var spawnList = new List<Enemy>();

        foreach (EnemyGroup group in enemies)
        {
            if (group == null || group.enemy == null) continue;

            for (int i = 0; i < Mathf.Max(1, group.count); i++)
            {
                spawnList.Add(group.enemy);
            }
        }

        return spawnList;
    }

    /// <summary>Total enemies this encounter wants to spawn.</summary>
    public int TotalEnemyCount
    {
        get
        {
            int total = 0;
            foreach (EnemyGroup group in enemies)
            {
                if (group != null && group.enemy != null) total += Mathf.Max(1, group.count);
            }
            return total;
        }
    }
}
