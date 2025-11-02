
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Centralized manager for parry input that coordinates between all ParrySystem instances
/// </summary>
public class ParryInputManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference parryInputAction;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private static ParryInputManager _instance;
    public static ParryInputManager Instance => _instance;
    
    private readonly List<ParrySystem> registeredParrySystems = new List<ParrySystem>();
    private ParrySystem activeParrySystem = null;
    
    private void Awake()
    {
        // Singleton pattern
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
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
    
    /// <summary>
    /// Register a ParrySystem with the manager
    /// </summary>
    public void RegisterParrySystem(ParrySystem parrySystem)
    {
        if (!registeredParrySystems.Contains(parrySystem))
        {
            registeredParrySystems.Add(parrySystem);
            if (showDebugInfo)
            {
                Debug.Log($"[ParryInputManager] Registered ParrySystem for {parrySystem.gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// Unregister a ParrySystem from the manager
    /// </summary>
    public void UnregisterParrySystem(ParrySystem parrySystem)
    {
        if (registeredParrySystems.Contains(parrySystem))
        {
            registeredParrySystems.Remove(parrySystem);
            
            // Clear active system if it's being unregistered
            if (activeParrySystem == parrySystem)
            {
                activeParrySystem = null;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[ParryInputManager] Unregistered ParrySystem for {parrySystem?.gameObject?.name ?? "Unknown"}");
            }
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
    
    /// <summary>
    /// Clean up any null references in the registered systems list
    /// </summary>
    public void CleanupNullReferences()
    {
        for (int i = registeredParrySystems.Count - 1; i >= 0; i--)
        {
            if (registeredParrySystems[i] == null || registeredParrySystems[i].gameObject == null)
            {
                registeredParrySystems.RemoveAt(i);
            }
        }
        
        // Clear active system if it's null
        if (activeParrySystem == null || activeParrySystem.gameObject == null)
        {
            activeParrySystem = null;
        }
    }
}