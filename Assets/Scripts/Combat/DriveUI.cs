using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DriveUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider driveBar;
    [SerializeField] private Image driveBarFill;
    [SerializeField] private Transform segmentContainer;
    [SerializeField] private GameObject segmentPrefab;
    
    [Header("Visual Settings")]
    [SerializeField] private Color availableSegmentColor = Color.green;
    [SerializeField] private Color unavailableSegmentColor = Color.gray;
    [SerializeField] private Gradient driveBarGradient;
    
    private DriveManager driveManager;
    private List<Image> segmentImages = new List<Image>();
    
    void Start()
    {
        driveManager = GetComponentInParent<DriveManager>();
        if (driveManager != null)
        {
            driveManager.Drive.OnDriveChanged += UpdateDriveDisplay;
            driveManager.Drive.OnSegmentsChanged += UpdateSegmentDisplay;
            
            InitializeSegmentDisplay();
            UpdateDriveDisplay(driveManager.Drive.CurrentDrive);
            UpdateSegmentDisplay(driveManager.Drive.AvailableSegments);
        }
        else
        {
            Debug.LogError("DriveUI could not find DriveManager!");
        }
    }
    
    void OnDestroy()
    {
        if (driveManager != null)
        {
            driveManager.Drive.OnDriveChanged -= UpdateDriveDisplay;
            driveManager.Drive.OnSegmentsChanged -= UpdateSegmentDisplay;
        }
    }
    
    private void InitializeSegmentDisplay()
    {
        if (segmentContainer == null || segmentPrefab == null) return;
        
        // Clear existing segments
        foreach (Transform child in segmentContainer)
        {
            Destroy(child.gameObject);
        }
        segmentImages.Clear();
        
        // Create segment indicators
        for (int i = 0; i < driveManager.Drive.TotalSegments; i++)
        {
            GameObject segment = Instantiate(segmentPrefab, segmentContainer);
            Image segmentImage = segment.GetComponent<Image>();
            if (segmentImage != null)
            {
                segmentImages.Add(segmentImage);
                segmentImage.color = unavailableSegmentColor;
            }
        }
    }
    
    private void UpdateDriveDisplay(float currentDrive)
    {
        if (driveBar != null)
        {
            float percentage = currentDrive / driveManager.Drive.MaxDriveValue;
            driveBar.value = percentage;
            
            if (driveBarFill != null && driveBarGradient != null)
            {
                driveBarFill.color = driveBarGradient.Evaluate(percentage);
            }
        }
        
        
    }
    
    private void UpdateSegmentDisplay(int availableSegments)
    {
    
        
        // Update segment visual indicators
        for (int i = 0; i < segmentImages.Count; i++)
        {
            if (segmentImages[i] != null)
            {
                segmentImages[i].color = i < availableSegments ? availableSegmentColor : unavailableSegmentColor;
            }
        }
    }
    
    // Method to create drive action buttons dynamically
    public void CreateActionButtons(Transform buttonContainer, GameObject buttonPrefab)
    {
        if (driveManager == null || buttonContainer == null || buttonPrefab == null) return;
        
        var availableActions = driveManager.GetAvailableActions();
        
        // Clear existing buttons
        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Create buttons for available actions
        foreach (var action in availableActions)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);
            Button button = buttonObj.GetComponent<Button>();
            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            
            if (button != null && buttonText != null)
            {
                buttonText.text = GetActionDisplayName(action);
                button.onClick.AddListener(() => driveManager.TryUseDriveAction(action));
            }
        }
    }
    
    private string GetActionDisplayName(DriveAction action)
    {
        return action switch
        {
            DriveAction.BuffNextAttack => "Buff Attack",
            DriveAction.HealSelf => "Heal Self",
            DriveAction.RemoveDebuff => "Remove Debuff",
            DriveAction.ReduceCooldown => "Reduce Cooldown",
            _ => action.ToString()
        };
    }
}