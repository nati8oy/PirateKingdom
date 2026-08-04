
using UnityEngine;
using UnityEngine.EventSystems;

public class ActionButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Action associatedAction;
    // The runtime character who owns this action — cooldowns are per instance, so this has to be
    // the CharacterManager rather than the shared Character asset.
    private CharacterManager ownerCharacter;

    /// <summary>Read-only accessor used by ActionTargetingLine to find the button for an action.</summary>
    public Action AssociatedAction => associatedAction;

    public void SetAction(Action action)
    {
        associatedAction = action;

        // Try to find the character that owns this action
        // This could be from ActionsManager context or current character
        CombatController combatController = FindObjectOfType<CombatController>();
        if (combatController != null)
        {
            ownerCharacter = combatController.GetCurrentCharacter();
        }
    }

    public void SetActionWithCharacter(Action action, CharacterManager character)
    {
        associatedAction = action;
        ownerCharacter = character;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Don't show hover tooltip if an enemy is currently showing their action
        if (IsEnemyShowingTooltip())
        {
            return;
        }
        
        if (associatedAction != null && TooltipUI.Instance != null)
        {
            // Show detailed action tooltip with cooldown information
            TooltipUI.Instance.ShowActionTooltipWithCharacter(associatedAction, ownerCharacter);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        // Don't change tooltip if an enemy is currently showing their action
        if (IsEnemyShowingTooltip())
        {
            return;
        }
        
        // Check if we should show target info or default text
        CombatController combatController = FindObjectOfType<CombatController>();
        if (combatController != null && combatController.selectedAction != null)
        {
            // If an action is selected, show the same detailed info as when hovering
            TooltipUI.Instance.ShowActionTooltipWithCharacter(combatController.selectedAction, combatController.GetCurrentCharacter());
        }
        else if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
        }
    }
    
    private bool IsEnemyShowingTooltip()
    {
        // Check if any enemy is currently showing their tooltip
        EnemyManager[] enemies = FindObjectsOfType<EnemyManager>();
        foreach (var enemy in enemies)
        {
            if (enemy.IsShowingEnemyTooltip())
            {
                return true;
            }
        }
        return false;
    }
    
    void OnDestroy()
    {
        if (TooltipUI.Instance != null && !IsEnemyShowingTooltip())
        {
            TooltipUI.Instance.ShowDefaultText();
        }
    }
}