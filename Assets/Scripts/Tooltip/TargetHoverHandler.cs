

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
        // Don't show hover tooltip if an enemy is currently showing their action
        if (IsEnemyShowingTooltip())
        {
            return;
        }
        
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
        // Don't change tooltip if an enemy is currently showing their action
        if (IsEnemyShowingTooltip())
        {
            return;
        }
        
        if (TooltipUI.Instance != null)
        {
            Action selectedAction = combatController?.GetSelectedAction();
            if (selectedAction != null)
            {
                // Return to showing detailed selected action info (same as hover)
                CharacterManager currentCharacter = combatController.GetCurrentCharacter();
                Character currentCharacterData = currentCharacter != null ? currentCharacter.characterData : null;
                TooltipUI.Instance.ShowActionTooltipWithCharacter(selectedAction, currentCharacterData);
            }
            else
            {
                // Return to default text
                TooltipUI.Instance.ShowDefaultText();
            }
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
}