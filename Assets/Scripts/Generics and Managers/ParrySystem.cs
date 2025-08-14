using UnityEngine;
using UnityEngine.InputSystem;

public class ParrySystem : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowDuration = 0.3f;
    [SerializeField] private float parryDriveBonus = 50f;
    
    [Header("Input")]
    [SerializeField] private InputActionReference parryInputAction;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private CharacterManager characterManager;
    private bool isParryWindowActive = false;
    private float parryWindowEndTime = 0f;
    
    // Add this to track if this parry system should respond to input
    private bool isActiveParryTarget = false;
    
    public float ParryWindowDuration => parryWindowDuration;
    public float ParryDriveBonus => parryDriveBonus;
    public bool IsParryWindowActive => isParryWindowActive;
    
    // Events
    public System.Action OnParryWindowOpened;
    public System.Action OnParryWindowClosed;
    public System.Action OnParrySuccess;
    public System.Action OnParryFailed;
    
    private void Awake()
    {
        characterManager = GetComponent<CharacterManager>();
        if (characterManager == null)
        {
            Debug.LogError("[ParrySystem] No CharacterManager found on this GameObject!");
        }
    }
    
    private void OnEnable()
    {
        if (parryInputAction != null)
        {
            parryInputAction.action.performed += OnParryInput;
            parryInputAction.action.Enable();
        }
    }
    
    private void OnDisable()
    {
        if (parryInputAction != null)
        {
            parryInputAction.action.performed -= OnParryInput;
            parryInputAction.action.Disable();
        }
    }
    
    private void Update()
    {
        // Check if parry window should close
        if (isParryWindowActive && Time.time >= parryWindowEndTime)
        {
            CloseParryWindow();
        }
    }
    
    /// <summary>
    /// Opens the parry window for the specified duration
    /// </summary>
    public void OpenParryWindow()
    {
        if (isParryWindowActive) return;
        
        // Don't open parry window if character is dead/inactive
        if (!IsCharacterAlive())
        {
            if (showDebugInfo)
            {
                Debug.Log($"[ParrySystem] Cannot open parry window - character is dead on {gameObject.name}");
            }
            return;
        }
        
        isParryWindowActive = true;
        isActiveParryTarget = true; // Mark this as the active parry target
        parryWindowEndTime = Time.time + parryWindowDuration;
        
        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Parry window opened for {parryWindowDuration}s on {gameObject.name}");
        }
        
        OnParryWindowOpened?.Invoke();
    }
    
    /// <summary>
    /// Closes the parry window
    /// </summary>
    public void CloseParryWindow()
    {
        if (!isParryWindowActive) return;
        
        isParryWindowActive = false;
        isActiveParryTarget = false; // No longer the active target
        
        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Parry window closed on {gameObject.name}");
        }
        
        OnParryWindowClosed?.Invoke();
    }
    
    /// <summary>
    /// Forces a parry window to close immediately (for successful parries)
    /// </summary>
    public void ForceCloseParryWindow()
    {
        isParryWindowActive = false;
        isActiveParryTarget = false; // No longer the active target
        OnParryWindowClosed?.Invoke();
    }
    
    private void OnParryInput(InputAction.CallbackContext context)
    {
        // Only respond to input if this ParrySystem is the active target
        if (!isActiveParryTarget)
        {
            return; // Silently ignore input for non-active parry systems
        }
        
        // Check if character is still alive (using your game's logic)
        if (!IsCharacterAlive())
        {
            if (showDebugInfo)
            {
                Debug.Log($"[ParrySystem] Parry input ignored - character is dead on {gameObject.name}");
            }
            return;
        }
        
        // Only allow parry during active window
        if (!isParryWindowActive)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[ParrySystem] Parry attempted outside window - failed on {gameObject.name}");
            }
            OnParryFailed?.Invoke();
            return;
        }
        
        // Successful parry
        PerformSuccessfulParry();
    }
    
    /// <summary>
    /// Checks if the character is alive using the same logic as TurnManager
    /// In your system, alive = GameObject exists and CharacterManager exists
    /// </summary>
    private bool IsCharacterAlive()
    {
        // Same logic as your TurnManager uses to check if characters are alive
        return characterManager != null && 
               characterManager.gameObject != null && 
               characterManager.characterData != null;
    }
    
    private void PerformSuccessfulParry()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Successful parry on {gameObject.name}!");
        }
        
        // Close parry window immediately
        ForceCloseParryWindow();
        
        // Grant drive bonus
        if (characterManager != null)
        {
            var driveManager = characterManager.GetDriveManager();
            if (driveManager?.Drive != null)
            {
                driveManager.Drive.AddDrive(parryDriveBonus);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[ParrySystem] Added {parryDriveBonus} drive for successful parry on {gameObject.name}");
                }
            }
        }
        
        OnParrySuccess?.Invoke();
    }
    
    /// <summary>
    /// Get the remaining time in the parry window
    /// </summary>
    public float GetRemainingParryTime()
    {
        if (!isParryWindowActive) return 0f;
        return Mathf.Max(0f, parryWindowEndTime - Time.time);
    }
    
    /// <summary>
    /// Check if a parry attempt would be successful at this moment
    /// </summary>
    public bool CanParryNow()
    {
        return isParryWindowActive && IsCharacterAlive();
    }
}