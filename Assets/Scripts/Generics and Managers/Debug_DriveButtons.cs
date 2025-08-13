using UnityEngine;

public class Debug_DriveButtons : MonoBehaviour
{
    [Header("Debug Actions")]
    [Tooltip("Fill all player character drive meters to maximum")]
    public bool fillPlayerDrives;
    
    [Tooltip("Fill all enemy character drive meters to maximum")]
    public bool fillEnemyDrives;
    
    [Tooltip("Empty all drive meters")]
    public bool emptyAllDrives;
    
    [Tooltip("Reset all drive meters to starting state")]
    public bool resetAllDrives;
    
    [Header("Individual Character Debug")]
    [Tooltip("Target character for individual debug actions")]
    public CharacterManager targetCharacter;
    
    [Tooltip("Fill target character's drive meter")]
    public bool fillTargetDrive;
    
    [Tooltip("Empty target character's drive meter")]
    public bool emptyTargetDrive;
    
    [Tooltip("Force target character into drive mode")]
    public bool enterTargetDriveMode;
    
    [Tooltip("Force target character out of drive mode")]
    public bool exitTargetDriveMode;
    
    void Update()
    {
        // Only process debug commands during play mode
        if (!Application.isPlaying) return;
        
        // Global actions
        if (fillPlayerDrives)
        {
            fillPlayerDrives = false;
            FillDriveMeters(Character.Allegiance.Player);
        }
        
        if (fillEnemyDrives)
        {
            fillEnemyDrives = false;
            FillDriveMeters(Character.Allegiance.Enemy);
        }
        
        if (emptyAllDrives)
        {
            emptyAllDrives = false;
            EmptyAllDriveMeters();
        }
        
        if (resetAllDrives)
        {
            resetAllDrives = false;
            ResetAllDriveMeters();
        }
        
        // Individual character actions
        if (targetCharacter != null)
        {
            if (fillTargetDrive)
            {
                fillTargetDrive = false;
                FillCharacterDrive(targetCharacter);
            }
            
            if (emptyTargetDrive)
            {
                emptyTargetDrive = false;
                EmptyCharacterDrive(targetCharacter);
            }
            
            if (enterTargetDriveMode)
            {
                enterTargetDriveMode = false;
                EnterDriveMode(targetCharacter);
            }
            
            if (exitTargetDriveMode)
            {
                exitTargetDriveMode = false;
                ExitDriveMode(targetCharacter);
            }
        }
    }
    
    [ContextMenu("Fill All Player Drives")]
    public void FillPlayerDrivesMenu()
    {
        FillDriveMeters(Character.Allegiance.Player);
    }
    
    [ContextMenu("Fill All Enemy Drives")]
    public void FillEnemyDrivesMenu()
    {
        FillDriveMeters(Character.Allegiance.Enemy);
    }
    
    [ContextMenu("Empty All Drives")]
    public void EmptyAllDrivesMenu()
    {
        EmptyAllDriveMeters();
    }
    
    [ContextMenu("Reset All Drives")]
    public void ResetAllDrivesMenu()
    {
        ResetAllDriveMeters();
    }
    
    private void FillDriveMeters(Character.Allegiance allegiance)
    {
        CharacterManager[] allCharacters = FindObjectsOfType<CharacterManager>();
        int count = 0;
        
        foreach (CharacterManager character in allCharacters)
        {
            if (character?.characterData != null && character.characterData.allegiance == allegiance)
            {
                DriveManager driveManager = character.GetDriveManager();
                if (driveManager?.Drive != null)
                {
                    driveManager.Drive.AddDrive(driveManager.Drive.MaxDriveValue);
                    count++;
                }
            }
        }
        
        Debug.Log($"[DEBUG] Filled drive meters for {count} {allegiance} characters");
    }
    
    private void EmptyAllDriveMeters()
    {
        CharacterManager[] allCharacters = FindObjectsOfType<CharacterManager>();
        int count = 0;
        
        foreach (CharacterManager character in allCharacters)
        {
            DriveManager driveManager = character?.GetDriveManager();
            if (driveManager?.Drive != null)
            {
                // Reset drive to 0
                float currentDrive = driveManager.Drive.CurrentDrive;
                driveManager.Drive.AddDrive(-currentDrive);
                count++;
            }
        }
        
        Debug.Log($"[DEBUG] Emptied drive meters for {count} characters");
    }
    
    private void ResetAllDriveMeters()
    {
        CharacterManager[] allCharacters = FindObjectsOfType<CharacterManager>();
        int count = 0;
        
        foreach (CharacterManager character in allCharacters)
        {
            DriveManager driveManager = character?.GetDriveManager();
            if (driveManager?.Drive != null)
            {
                driveManager.Drive.Initialize();
                count++;
            }
        }
        
        Debug.Log($"[DEBUG] Reset drive meters for {count} characters");
    }
    
    private void FillCharacterDrive(CharacterManager character)
    {
        DriveManager driveManager = character?.GetDriveManager();
        if (driveManager?.Drive != null)
        {
            driveManager.Drive.AddDrive(driveManager.Drive.MaxDriveValue);
            Debug.Log($"[DEBUG] Filled drive meter for {character.characterData.characterName}");
        }
    }
    
    private void EmptyCharacterDrive(CharacterManager character)
    {
        DriveManager driveManager = character?.GetDriveManager();
        if (driveManager?.Drive != null)
        {
            float currentDrive = driveManager.Drive.CurrentDrive;
            driveManager.Drive.AddDrive(-currentDrive);
            Debug.Log($"[DEBUG] Emptied drive meter for {character.characterData.characterName}");
        }
    }
    
    private void EnterDriveMode(CharacterManager character)
    {
        DriveManager driveManager = character?.GetDriveManager();
        if (driveManager != null)
        {
            // Force enter drive mode by giving enough drive first
            if (!driveManager.Drive.CanUseSegments(3))
            {
                driveManager.Drive.AddDrive(driveManager.Drive.MaxDriveValue);
            }
            driveManager.EnterDriveMode();
            Debug.Log($"[DEBUG] Entered drive mode for {character.characterData.characterName}");
        }
    }
    
    private void ExitDriveMode(CharacterManager character)
    {
        DriveManager driveManager = character?.GetDriveManager();
        if (driveManager != null)
        {
            driveManager.ExitDriveMode();
            Debug.Log($"[DEBUG] Exited drive mode for {character.characterData.characterName}");
        }
    }
}