using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class ActionButtonHover : MonoBehaviour
{
    private Action associatedAction;
    private bool isHovering = false;
    
    public void SetAction(Action action)
    {
        associatedAction = action;
    }
    
    void Update()
    {
        // Check if mouse is over this UI element using new Input System
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        
        RectTransform rectTransform = GetComponent<RectTransform>();
        bool isMouseOver = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePosition);
        
        // Handle hover enter
        if (isMouseOver && !isHovering)
        {
            isHovering = true;
            OnHoverEnter();
        }
        // Handle hover exit
        else if (!isMouseOver && isHovering)
        {
            isHovering = false;
            OnHoverExit();
        }
    }
    
    private void OnHoverEnter()
    {
        if (associatedAction != null && TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowActionTooltip(associatedAction);
        }
    }
    
    private void OnHoverExit()
    {
        // Check if we should show target info or default text
        CombatController combatController = FindObjectOfType<CombatController>();
        if (combatController != null && combatController.selectedAction != null)
        {
            // If an action is selected, show that info instead of default
            TooltipUI.Instance.ShowCustomText($"<b>{combatController.selectedAction.actionName}</b> selected\nHover over a target to see combat details");
        }
        else if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
        }
    }
    
    void OnDestroy()
    {
        if (isHovering && TooltipUI.Instance != null)
        {
            TooltipUI.Instance.ShowDefaultText();
        }
    }
}