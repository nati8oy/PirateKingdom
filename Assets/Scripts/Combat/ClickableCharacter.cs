using UnityEngine;
using UnityEngine.InputSystem;

public class ClickableCharacter : MonoBehaviour
{
    private Character character;
    [SerializeField] private CombatController combatController;

    
    private PlayerControls PlayerControls;
    private InputAction clickAction;
 
    public void CharacterClicked()
    {
    if (combatController != null)
    {
        
        // Get CharacterManager from this GameObject
        CharacterManager characterManager = GetComponent<CharacterManager>();
        if (characterManager != null)
        {
            combatController.TryExecuteActionOnTarget(characterManager);
        }
        else
        {
            Debug.LogError($"No CharacterManager component found on {gameObject.name}!");
            // Fallback to the old Character-based method if needed
            combatController.TryExecuteActionOnTarget(character);
        }

        

        if (combatController.selectedAction != null)
        {
            bool success = combatController.TryExecuteActionOnTarget(character);

            if (success)
            {
                Debug.Log($"Action executed on {character.name}");
                combatController.OnPlayerActionComplete();
            }
            else
            {
                Debug.Log($"Cannot target {character.name} with selected action");
            }
        }
    
    }
    }
    
    private void Awake()
    {
        CharacterManager characterManager = GetComponentInParent<CharacterManager>();
        if (characterManager != null)
        {
            character = characterManager.characterData;
        }
        else
        {
            Debug.LogError($"No CharacterManager found in parent of {gameObject.name}!");
        }
    }

    private void Start()
    {
        // Find the CombatController if not assigned
        if (combatController == null)
            combatController = FindObjectOfType<CombatController>();
    }
}