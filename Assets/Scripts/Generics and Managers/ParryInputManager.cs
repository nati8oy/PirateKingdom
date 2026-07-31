
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes the shared Parry input action to whichever ParrySystem currently has an open parry
/// window. Lives on a single GameObject in the encounter scene.
/// </summary>
public class ParryInputManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference parryInputAction;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private static ParryInputManager _instance;

    /// <summary>
    /// The active manager. Resolves lazily so that a ParrySystem registering from its own
    /// OnEnable can't miss us purely because of script execution order — which matters once
    /// crew are spawned at runtime rather than placed in the scene.
    /// </summary>
    public static ParryInputManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ParryInputManager>();
            }
            return _instance;
        }
    }

    private ParrySystem activeParrySystem = null;

    private void Awake()
    {
        // Deliberately NOT DontDestroyOnLoad. Parry input is only meaningful inside an encounter,
        // and a manager that survived into the map scene would still be alive when the next
        // encounter loads its own — leaving a stale instance holding the shared input action.
        // This component belongs on a single GameObject in the encounter scene.
        if (_instance != null && _instance != this)
        {
            // Should not happen now that this lives in the scene rather than on the character
            // prefab — so shout rather than swallow it. Destroy only this component, not the whole
            // GameObject. The OnEnable/OnDisable guards below are what keep the duplicate from
            // calling Disable() on the *shared* project-wide Parry action on its way out, which
            // would silently kill parry input for the surviving instance.
            Debug.LogError($"[ParryInputManager] A second ParryInputManager was found on '{gameObject.name}'. " +
                           "There must be exactly one, in the encounter scene. Destroying the duplicate component.");
            Destroy(this);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        // Guard: duplicates must never touch the shared input action's enabled state.
        if (_instance != this) return;

        if (parryInputAction != null)
        {
            parryInputAction.action.performed += OnParryInput;
            parryInputAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (_instance != this) return;

        if (parryInputAction != null)
        {
            parryInputAction.action.performed -= OnParryInput;
            parryInputAction.action.Disable();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
    
    /// <summary>
    /// Called by a ParrySystem that is being disabled or destroyed. Input dispatch only ever goes
    /// to <see cref="activeParrySystem"/>, so the only thing that matters here is dropping the
    /// active target if it's the one going away — otherwise a character who dies mid-parry-window
    /// leaves a dangling active reference.
    /// </summary>
    public void UnregisterParrySystem(ParrySystem parrySystem)
    {
        if (activeParrySystem != parrySystem) return;

        activeParrySystem = null;

        if (showDebugInfo)
        {
            Debug.Log($"[ParryInputManager] Active ParrySystem for {parrySystem?.gameObject?.name ?? "Unknown"} went away, cleared");
        }
    }

    /// <summary>
    /// Set which ParrySystem should respond to input
    /// </summary>
    public void SetActiveParrySystem(ParrySystem parrySystem)
    {
        // Clear previous active system
        if (activeParrySystem != null && activeParrySystem != parrySystem)
        {
            activeParrySystem.SetActiveTarget(false);
        }
        
        activeParrySystem = parrySystem;
        
        if (activeParrySystem != null)
        {
            activeParrySystem.SetActiveTarget(true);
            if (showDebugInfo)
            {
                Debug.Log($"[ParryInputManager] Set active parry system: {activeParrySystem.gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// Clear the active ParrySystem
    /// </summary>
    public void ClearActiveParrySystem()
    {
        if (activeParrySystem != null)
        {
            activeParrySystem.SetActiveTarget(false);
            activeParrySystem = null;
        }
    }
    
    private void OnParryInput(InputAction.CallbackContext context)
    {
        if (activeParrySystem == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("[ParryInputManager] Parry input received but no active ParrySystem");
            }
            return;
        }
        
        // Validate the active system is still valid
        if (activeParrySystem.gameObject == null || !activeParrySystem.IsCharacterAlive())
        {
            if (showDebugInfo)
            {
                Debug.Log("[ParryInputManager] Active ParrySystem is invalid, clearing");
            }
            ClearActiveParrySystem();
            return;
        }
        
        // Forward input to active ParrySystem
        activeParrySystem.HandleParryInput(context);
    }
}