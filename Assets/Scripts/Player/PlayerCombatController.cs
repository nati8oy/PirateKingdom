using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [SerializeField] private Character playerAttributes;
    [SerializeField] private ActionSet actionSet;

    /*
    private PlayerControls PlayerControls; // Use your generated Input Actions class
    private InputAction mouseLMBAction;
    
    private void Awake()
    {
        PlayerControls = new PlayerControls(); // Initialize the generated class
        mouseLMBAction = PlayerControls.Player.LMB; // Access the Player action map
        mouseLMBAction.performed += OnMouseLMB;
    }

    private void OnEnable()
    {
        PlayerControls.Enable(); // Enable all actions
    }

    private void OnDisable()
    {
        PlayerControls.Disable(); // Disable all actions
    }

    private void OnDestroy()
    {
        mouseLMBAction.performed -= OnMouseLMB;
        PlayerControls?.Dispose(); // Clean up resources
    }

    private void OnMouseLMB(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("Mouse LMBed!");
        }
    }*/
}