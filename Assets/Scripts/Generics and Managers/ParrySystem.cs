
using UnityEngine;
using UnityEngine.InputSystem;

public class ParrySystem : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowDuration = 0.3f;
    [SerializeField] private float parryDriveBonus = 50f;

    [Header("Audio")]
    [SerializeField] private AudioClip parrySuccessSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private CharacterManager characterManager;
    private bool isParryWindowActive = false;
    private float parryWindowEndTime = 0f;
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

        // Get or create AudioSource component
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void OnEnable()
    {
        // Register with ParryInputManager instead of directly subscribing to input
        if (ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.RegisterParrySystem(this);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning("[ParrySystem] ParryInputManager not found!");
        }
    }

    private void OnDisable()
    {
        // Unregister from ParryInputManager
        if (ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.UnregisterParrySystem(this);
        }
    }

    private void OnDestroy()
    {
        // Ensure cleanup when destroyed
        if (ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.UnregisterParrySystem(this);
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
        parryWindowEndTime = Time.time + parryWindowDuration;

        // Register as active parry target with the manager
        if (ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.SetActiveParrySystem(this);
        }

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

        // Clear active target if this was the active system
        if (isActiveParryTarget && ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.ClearActiveParrySystem();
        }

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
        
        if (isActiveParryTarget && ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.ClearActiveParrySystem();
        }
        
        OnParryWindowClosed?.Invoke();
    }

    /// <summary>
    /// Called by ParryInputManager when this system is set as active target
    /// </summary>
    public void SetActiveTarget(bool active)
    {
        isActiveParryTarget = active;
    }

    /// <summary>
    /// Handle parry input forwarded from ParryInputManager
    /// </summary>
    public void HandleParryInput(InputAction.CallbackContext context)
    {
        // Early safety checks
        if (this == null || gameObject == null || !isActiveParryTarget)
        {
            return;
        }

        // Check if character is still alive
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
    /// </summary>
    public bool IsCharacterAlive()
    {
        return characterManager != null && 
               characterManager.gameObject != null && 
               characterManager.characterData != null &&
               characterManager.GetCurrentHealth() > 0;
    }

    private void PerformSuccessfulParry()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Successful parry on {gameObject.name}!");
        }

        // Play parry success sound
        PlayParrySound();

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
    /// Plays the parry success sound effect
    /// </summary>
    private void PlayParrySound()
    {
        if (parrySuccessSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(parrySuccessSound);
        }
        else if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Parry sound not configured on {gameObject.name}");
        }
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