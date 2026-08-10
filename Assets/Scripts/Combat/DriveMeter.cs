using System;
using UnityEngine;

[System.Serializable]
public class DriveMeter
{
    [Header("Drive Meter Settings")]
    [SerializeField] private float maxDriveValue = 400f;
    [SerializeField] private int totalSegments = 4;
    [SerializeField] private float regenerationPerTurn = 50f;
    
    [Header("Current State")]
    [SerializeField] private float currentDrive = 0f;
    [SerializeField] private int availableSegments = 0;
    
    public float MaxDriveValue => maxDriveValue;
    public int TotalSegments => totalSegments;
    public float CurrentDrive => currentDrive;
    public int AvailableSegments => availableSegments;
    public float SegmentValue => maxDriveValue / totalSegments;
    public float RegenerationPerTurn => regenerationPerTurn;
    
    public event Action<int> OnSegmentsChanged;
    public event Action<float> OnDriveChanged;
    
    public void Initialize()
    {
        currentDrive = 0f;
        availableSegments = 0;
    }
    
    public void RegenerateDrive()
    {
        float previousDrive = currentDrive;
        int previousSegments = availableSegments;
        
        currentDrive = Mathf.Min(maxDriveValue, currentDrive + regenerationPerTurn);
        availableSegments = Mathf.FloorToInt(currentDrive / SegmentValue);
        
        if (currentDrive != previousDrive)
            OnDriveChanged?.Invoke(currentDrive);
            
        if (availableSegments != previousSegments)
            OnSegmentsChanged?.Invoke(availableSegments);
    }
    
    public bool CanUseSegments(int segmentCount)
    {
        return availableSegments >= segmentCount;
    }
    
    public bool UseSegments(int segmentCount)
    {
        if (!CanUseSegments(segmentCount))
            return false;
            
        float driveToConsume = segmentCount * SegmentValue;
        currentDrive = Mathf.Max(0, currentDrive - driveToConsume);
        
        int previousSegments = availableSegments;
        availableSegments = Mathf.FloorToInt(currentDrive / SegmentValue);
        
        OnDriveChanged?.Invoke(currentDrive);
        
        if (availableSegments != previousSegments)
            OnSegmentsChanged?.Invoke(availableSegments);
            
        return true;
    }
    
    public void AddDrive(float amount)
    {
        float previousDrive = currentDrive;
        int previousSegments = availableSegments;

        currentDrive = Mathf.Min(maxDriveValue, currentDrive + amount);
        availableSegments = Mathf.FloorToInt(currentDrive / SegmentValue);

        if (currentDrive != previousDrive)
            OnDriveChanged?.Invoke(currentDrive);

        if (availableSegments != previousSegments)
            OnSegmentsChanged?.Invoke(availableSegments);
    }

    /// <summary>
    /// Removes raw drive, floored at zero. Returns how much was <b>actually</b> removed, which is
    /// less than <paramref name="amount"/> when the meter was already low — callers report the real
    /// number rather than the one they asked for.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="AddDrive"/>, and deliberately not <see cref="UseSegments"/>:
    /// that spends whole segments as a cost, this strips raw meter as an effect.
    ///
    /// Drive stacks already committed are <b>not</b> touched. <c>TryAddDriveStack()</c> spends the
    /// drive at the moment a stack is committed, so a stack is already paid for and draining the
    /// meter afterwards can't retroactively take it back.
    /// </remarks>
    public float RemoveDrive(float amount)
    {
        if (amount <= 0f) return 0f;

        float previousDrive = currentDrive;
        int previousSegments = availableSegments;

        currentDrive = Mathf.Max(0f, currentDrive - amount);
        availableSegments = Mathf.FloorToInt(currentDrive / SegmentValue);

        float removed = previousDrive - currentDrive;

        if (removed > 0f)
            OnDriveChanged?.Invoke(currentDrive);

        if (availableSegments != previousSegments)
            OnSegmentsChanged?.Invoke(availableSegments);

        return removed;
    }
    
    public float GetDrivePercentage()
    {
        return currentDrive / maxDriveValue;
    }
    
    public float GetSegmentPercentage()
    {
        float currentSegmentProgress = currentDrive % SegmentValue;
        return currentSegmentProgress / SegmentValue;
    }
}