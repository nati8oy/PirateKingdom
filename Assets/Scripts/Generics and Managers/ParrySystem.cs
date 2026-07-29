using UnityEngine;
using UnityEngine.InputSystem;

public class ParrySystem : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowDuration = 0.3f;
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

    // --- One attempt per incoming attack ---
    // The sequence spans the whole attack (windup -> resolution), which is wider than the timing
    // window. That's deliberate: a press during the windup has to be catchable so it can be
    // consumed, otherwise mashing through the windup lands a parry on every attack.
    private bool parrySequenceActive = false;
    private bool parryAttemptSpent = false;
    private bool parrySucceeded = false;

    public float ParryWindowDuration => parryWindowDuration;
    public float ParryDriveBonus => parryDriveBonus;
    public bool IsParryWindowActive => isParryWindowActive;
    public float IncomingDamage => incomingDamage; // NEW: Public getter

    /// <summary>True once this attack's single parry attempt has been used, hit or miss.</summary>
    public bool ParryAttemptSpent => parryAttemptSpent;

    /// <summary>Whether the current attack was successfully parried.</summary>
    public bool ParrySucceeded => parrySucceeded;

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

    private void OnDisable()
    {
        // No matching registration step: the manager only ever dispatches to whichever system is
        // the current active target, which OpenParryWindow() sets. This just makes sure we aren't
        // left as that target after going away.
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
    /// Called by EnemyAttack when an attack begins its windup. Arms this character's single parry
    /// attempt for the incoming attack and makes them the input target for the whole sequence —
    /// not just the timing window — so an early press is caught and spent rather than ignored.
    /// </summary>
    public void BeginParrySequence(float damage)
    {
        parrySequenceActive = true;
        parryAttemptSpent = false;
        parrySucceeded = false;
        incomingDamage = damage;

        if (ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.SetActiveParrySystem(this);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Parry sequence begun, incoming damage: {incomingDamage}");
        }
    }

    /// <summary>
    /// Called by EnemyAttack once the attack has resolved. Closes out the sequence and releases
    /// the input target so presses between attacks are ignored entirely.
    /// </summary>
    public void EndParrySequence()
    {
        if (!parrySequenceActive) return;

        parrySequenceActive = false;
        isParryWindowActive = false;
        ClearIncomingDamage();

        if (isActiveParryTarget && ParryInputManager.Instance != null)
        {
            ParryInputManager.Instance.ClearActiveParrySystem();
        }

        if (showDebugInfo)
        {
            Debug.Log($"[ParrySystem] Parry sequence ended. Attempt spent: {parryAttemptSpent}, succeeded: {parrySucceeded}");
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
    /// Closes the timing window. Note this does NOT end the parry sequence — the attack hasn't
    /// resolved yet, and a late press still has to be caught so it can be spent and reported as a
    /// miss. Releasing the input target is EndParrySequence()'s job.
    /// </summary>
    public void CloseParryWindow()
    {
        if (!isParryWindowActive) return;

        isParryWindowActive = false;

        OnParryWindowClosed?.Invoke();
    }

    /// <summary>
    /// Forces the timing window shut immediately (for successful parries). Same as above: the
    /// sequence stays open until the attack resolves.
    /// </summary>
    public void ForceCloseParryWindow()
    {
        isParryWindowActive = false;

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

        // No attack inbound — there's nothing to parry, so don't burn anything.
        if (!parrySequenceActive)
        {
            return;
        }

        // One attempt per attack: they've already had their go at this one.
        if (parryAttemptSpent)
        {
            if (showDebugInfo)
            {
                Debug.Log("[ParrySystem] Parry input ignored — attempt already spent for this attack");
            }
            return;
        }

        // Consume the attempt up front. Whether this turns out to be a hit or a miss, there is no
        // second try against this attack — that's what stops mashing from being a valid strategy.
        parryAttemptSpent = true;

        if (!isParryWindowActive)
        {
            // Mistimed: either still in windup, or the window has already elapsed.
            if (showDebugInfo)
            {
                Debug.Log("[ParrySystem] Parry attempt mistimed — window not open");
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
        // Play parry success sound
        PlayParrySound();

        // Store the damage BEFORE closing the window
        float damageParried = incomingDamage;

        // Record the outcome so EnemyAttack can read it directly rather than inferring a parry
        // from the window having shut — the window also shuts on timeout.
        parrySucceeded = true;

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
    /// Check if a parry attempt would be successful at this moment — the window is open AND the
    /// one attempt for this attack hasn't already been spent.
    /// </summary>
    public bool CanParryNow()
    {
        return isParryWindowActive && !parryAttemptSpent && IsCharacterAlive();
    }

    /// <summary>
    /// Opens the timing window partway through the attack's windup. The input target was already
    /// claimed by BeginParrySequence(), so this only controls whether a press counts as a hit.
    /// </summary>
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

        OnParryWindowOpened?.Invoke();
    }
}