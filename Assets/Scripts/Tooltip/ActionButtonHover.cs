using UnityEngine;
using UnityEngine.EventSystems;

public class ActionButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Action associatedAction;
    private Character ownerCharacter; // Reference to the character who owns this action
    
    public void SetAction(Action action)
    {
        associatedAction = action;
        
        // Try to find the character that owns this action
        // This could be from ActionsManager context or current character
        CombatController combatController = FindObjectOfType<CombatController>();
        if (combatController != null)
        {
            CharacterManager currentCharacter = combatController.GetCurrentCharacter();
            if (currentCharacter != null && currentCharacter.characterData != null)
            {
                ownerCharacter = currentCharacter.characterData;
            }
        }
    }
    
    public void SetActionWithCharacter(Action action, Character character)
    {
        associatedAction = action;
        ownerCharacter = character;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (associatedAction != null && TooltipUI.Instance != null)
        {
            // Show detailed action tooltip with cooldown information
            TooltipUI.Instance.ShowActionTooltipWithCharacter(associatedAction, ownerCharacter);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        // Check if we should show target info or default text
        CombatController combatController = FindObjectOfType<CombatController>();
        if (combatController != null && combatController.selectedAction != null)
        {
            // If an action is selected, show the same detailed info as when hovering
            CharacterManager currentCharacter = combatController.GetCurrentCharacter();
            Character currentCharacterData = currentCharacter != null ? currentCharacter.characterData : null;
            TooltipUI.Instance.ShowActionTooltipWithCharacter(combatController.selectedAction, currentCharacterData);
        }
        else if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
        }
    }
    
    void OnDestroy()
    {
        if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
        }
    }
}