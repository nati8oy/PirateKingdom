using UnityEngine;
using UnityEngine.InputSystem;

public class ParrySystem : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowDuration = 0.3f;
    [SerializeField] private bool requireDriveModeToParry = true;
    private float parryDriveBonus;

    [Header("Audio")]
    [SerializeField] private AudioClip parrySuccessSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private CharacterManager characterManager;
    private bool isParryWindowActive = false;
    private float parryWindowEndTime = 0f;
    private bool isActiveParryTarget = false;
    private float incomingDamage = 0f; // NEW: Store the incoming attack damage

    public float ParryWindowDuration => parryWindowDuration;
    public float ParryDriveBonus => parryDriveBonus;
    public bool IsParryWindowActive => isParryWindowActive;
    public float IncomingDamage => incomingDamage; // NEW: Public getter

    // Events
    public System.Action OnParryWindowOpened;
    public System.Action OnParryWindowClosed;
    public System.Action OnParrySuccess;
    public System.Action OnParryFailed;

    private void Awake()
    {
        //set the bonus for the parrying of the attack based on the character's stats
        parryDriveBonus = GetComponent<CharacterManager>().characterData.parryBonusDriveMultiplier;
        
        
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
    /// Called by EnemyAttack to notify of incoming damage when parry window opens
    /// </summary>
    public void SetIncomingDamage(float damage)
    {
        incomingDamage = damage;
        
        
        
        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Incoming damage set to: {incomingDamage}");
        }
    }

    /// <summary>
    /// Clears the stored incoming damage
    /// </summary>
    private void ClearIncomingDamage()
    {
        incomingDamage = 0f;
    }

    /// <summary>
    /// Closes the parry window
    /// </summary>
    public void CloseParryWindow()
    {
        if (!isParryWindowActive) return;

        isParryWindowActive = false;
        ClearIncomingDamage(); // NEW: Clear damage when window closes

        // Clear active target if this was the active system
        if (isActiveParryTarget && ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.ClearActiveParrySystem();
        }


        OnParryWindowClosed?.Invoke();
    }

    /// <summary>
    /// Forces a parry window to close immediately (for successful parries)
    /// </summary>
    public void ForceCloseParryWindow()
    {
        isParryWindowActive = false;
        ClearIncomingDamage(); // NEW: Clear damage when forced closed
    
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
            return;
        }

        // Check drive mode requirement if enabled
        if (requireDriveModeToParry)
        {
            DriveManager driveManager = characterManager?.GetComponent<DriveManager>();
            if (driveManager == null || !driveManager.IsDriveMode)
            {
                Debug.Log($"[ParrySystem] {characterManager?.characterData?.characterName ?? gameObject.name} cannot parry without active drive mode!");
                OnParryFailed?.Invoke();
                return;
            }
        }

        // Only allow parry during active window
        if (!isParryWindowActive)
        {
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
        // Play parry success sound
        PlayParrySound();

        // Store the damage BEFORE closing the window (which clears it)
        float damageParried = incomingDamage;

        // Close parry window immediately
        ForceCloseParryWindow();

        // Grant drive bonus based on incoming damage
        if (characterManager != null)
        {
            var driveManager = characterManager.GetDriveManager();
            if (driveManager?.Drive != null)
            {
                // Calculate drive bonus: incoming damage × parry multiplier
                var driveIncrease = damageParried * characterManager.characterData.parryBonusDriveMultiplier;
                driveManager.Drive.AddDrive(driveIncrease);
                
                Debug.Log($"[ParrySystem] {characterManager.characterData.characterName} parried incoming damage of {damageParried}! " +
                          $"Drive increased by {driveIncrease} ({damageParried} × {characterManager.characterData.parryBonusDriveMultiplier})");
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
    
    public void OpenParryWindow()
    {
        if (isParryWindowActive) return;

        // Don't open parry window if character is dead/inactive
        if (!IsCharacterAlive())
        {
            return;
        }

        isParryWindowActive = true;
        parryWindowEndTime = Time.time + parryWindowDuration;

        // Register as active parry target with the manager
        if (ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.SetActiveParrySystem(this);
        }

        OnParryWindowOpened?.Invoke();
    }
}