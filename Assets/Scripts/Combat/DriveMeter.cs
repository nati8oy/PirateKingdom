using System;
using UnityEngine;

[System.Serializable]
public class DriveMeter
{
    [Header("Drive Meter Settings")]
    [SerializeField] private float maxDriveValue = 600f;
    [SerializeField] private int totalSegments = 6;
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