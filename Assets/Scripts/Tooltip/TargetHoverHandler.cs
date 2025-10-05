using UnityEngine;
using UnityEngine.EventSystems;

public class TargetHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CharacterManager characterManager;
    private CombatController combatController;

    void Start()
    {
        characterManager = GetComponent<CharacterManager>();
        combatController = FindObjectOfType<CombatController>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null && combatController != null)
        {
            Action selectedAction = combatController.GetSelectedAction();
            CharacterManager attacker = combatController.GetCurrentCharacter();
            
            if (selectedAction != null && attacker != null && characterManager != null)
            {
                // Show target tooltip with combat calculations
                TooltipUI.Instance.ShowTargetTooltip(selectedAction, attacker, characterManager);
            }
            else if (characterManager != null)
            {
                // Show basic character info if no action is selected
                TooltipUI.Instance.ShowCustomText($"{characterManager.characterData.characterName}\nHP: {characterManager.CurrentHealth}/{characterManager.MaxHealth}");
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null)
        {
            Action selectedAction = combatController?.GetSelectedAction();
            if (selectedAction != null)
            {
                // Return to showing selected action info
                TooltipUI.Instance.ShowActionTooltip(selectedAction);
            }
            else
            {
                // Return to default text
                TooltipUI.Instance.ShowDefaultText();
            }
        }
    }
}