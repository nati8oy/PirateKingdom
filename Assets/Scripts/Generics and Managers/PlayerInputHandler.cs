using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    private PlayerControls playerControls;
    private CharacterManager selectedCharacter;
    
    private void Awake()
    {
        playerControls = new PlayerControls();
        
        // Subscribe to the Drive action
        playerControls.Player.Drive.performed += OnDrivePressed;
    }
    
    private void OnEnable()
    {
        playerControls.Enable();
    }
    
    private void OnDisable()
    {
        playerControls.Disable();
    }
    
    private void OnDestroy()
    {
        if (playerControls != null)
        {
            playerControls.Player.Drive.performed -= OnDrivePressed;
            playerControls.Dispose();
        }
    }
    
    private void OnDrivePressed(InputAction.CallbackContext context)
    {
        if (selectedCharacter != null)
        {
            DriveManager driveManager = selectedCharacter.GetDriveManager();
            if (driveManager != null)
            {
                // Enter drive mode - you'll need to implement this method in DriveManager
                driveManager.EnterDriveMode();
                Debug.Log($"{selectedCharacter.characterData.characterName} entered drive mode!");
            }
        }
        else
        {
            Debug.LogWarning("No character selected for drive mode!");
        }
    }
    
    // Method to set the currently selected character
    public void SetSelectedCharacter(CharacterManager character)
    {
        selectedCharacter = character;
        Debug.Log($"Selected character: {character.characterData.characterName}");
    }
    
    // Method to clear selection
    public void ClearSelectedCharacter()
    {
        selectedCharacter = null;
    }
}