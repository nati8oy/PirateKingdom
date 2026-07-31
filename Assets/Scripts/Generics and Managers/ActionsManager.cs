
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ActionsManager : MonoBehaviour
{
    [SerializeField] private GameObject actionsGrid;
    [SerializeField] private GameObject actionSlotPrefab;
    [SerializeField] private GameObject endTurnButtonPrefab; // New field for End Turn button prefab
    
    /// <summary>
    /// Checks if an action is available and valid for selection
    /// Returns false if the action is on cooldown
    /// This method can be used by both player and enemy AI
    /// </summary>
    /// <remarks>
    /// Takes the runtime <see cref="CharacterManager"/>, not the <see cref="Character"/> asset —
    /// cooldowns are per scene instance, so two enemies sharing one asset track them separately.
    /// </remarks>
    public static bool IsActionAvailable(Action action, CharacterManager character)
    {
        if (action == null || character == null)
            return false;

        return character.IsActionAvailable(action);
    }

    public void LoadCharacterActions(CharacterManager character)
    {
        // Clear existing actions
        foreach (Transform child in actionsGrid.transform)
        {
            Destroy(child.gameObject);
        }

        if (character == null || character.characterData == null)
        {
            Debug.LogWarning("[ActionsManager] LoadCharacterActions called with no character.");
            return;
        }

        Action[] actionSlots = character.characterData.actionSlots;

        // Create new action slots
        for (int i = 0; i < actionSlots.Length; i++)
        {
            Action action = actionSlots[i];
            
            if (action == null || string.IsNullOrEmpty(action.actionName)) 
            {
                continue; // Skip null or invalid actions
            }

            GameObject actionSlot = Instantiate(actionSlotPrefab, actionsGrid.transform);
            
            if (actionSlot == null)
            {
                Debug.LogError("Failed to instantiate actionSlotPrefab!");
                continue;
            }

            Button button = actionSlot.GetComponent<Button>();
            TMP_Text actionText = actionSlot.GetComponentInChildren<TMP_Text>();
            
            // Find the specific image component named "img_icon"
            Transform iconTransform = actionSlot.transform.Find("action_icon/img_icon");
            Image actionIconImage = iconTransform?.GetComponent<Image>();
            
            // Add hover handler for tooltip with character context
            ActionButtonHover hoverHandler = actionSlot.GetComponent<ActionButtonHover>();
            if (hoverHandler == null)
            {
                hoverHandler = actionSlot.AddComponent<ActionButtonHover>();
            }
            // Pass both action and character so hover can show cooldown status
            hoverHandler.SetActionWithCharacter(action, character);
            
            // Check if action is available (not on cooldown)
            bool isAvailable = IsActionAvailable(action, character);
            int cooldownRemaining = character.GetActionCooldownRemaining(action);
            
            if (button != null)
            {
                // Enable/disable button based on availability
                button.interactable = isAvailable;
                
                button.onClick.AddListener(() => {
                    Debug.Log($"Button clicked for action: {action.actionName}");
                    FindObjectOfType<CombatController>().SelectAction(action);
                    
                    // Show the same detailed tooltip that was shown on hover
                    if (TooltipUI.Instance != null)
                    {
                        TooltipUI.Instance.ShowActionTooltipWithCharacter(action, character);
                    }
                });
            }
            else
            {
                Debug.LogWarning($"No Button component found on actionSlotPrefab!");
            }
            
            // Set the action icon
            if (actionIconImage != null && action.actionIcon != null)
            {
                actionIconImage.sprite = action.actionIcon;
                
                // If action is on cooldown, gray out the icon
                if (!isAvailable)
                {
                    actionIconImage.color = Color.gray;
                }
                else
                {
                    actionIconImage.color = Color.white; // Reset to normal color
                }
            }
            else
            {
                if (actionIconImage == null)
                {
                    Debug.LogWarning($"No Image component found on 'img_icon' in actionSlotPrefab for {action.actionName}!");
                }
                if (action.actionIcon == null)
                {
                    Debug.LogWarning($"Action {action.actionName} has no actionIcon sprite assigned!");
                }
            }
            
            if (actionText != null)
            {
                if (isAvailable)
                {
                    actionText.text = action.actionName;
                    // Don't change color - keep whatever was set in the editor
                }
                else
                {
                    // Show cooldown remaining
                    actionText.text = $"{action.actionName} ({cooldownRemaining})";
                    actionText.color = Color.gray; // Only change color when on cooldown
                }
            }
            else
            {
                Debug.LogWarning($"No TMP_Text component found in children of actionSlotPrefab!");
            }
        }
        
        // Add the End Turn button after all action slots
        CreateEndTurnButton();
    }
    
    private void CreateEndTurnButton()
    {
        // If no prefab is assigned, try to create a simple button dynamically
        if (endTurnButtonPrefab == null)
        {
            CreateDynamicEndTurnButton();
            return;
        }
        
        GameObject endTurnSlot = Instantiate(endTurnButtonPrefab, actionsGrid.transform);
        if (endTurnSlot == null)
        {
            Debug.LogError("Failed to instantiate endTurnButtonPrefab!");
            return;
        }
        
        Button button = endTurnSlot.GetComponent<Button>();
        TMP_Text buttonText = endTurnSlot.GetComponentInChildren<TMP_Text>();
        
        if (button != null)
        {
            button.interactable = true;
            button.onClick.AddListener(() => {
                Debug.Log("End Turn button clicked!");
                TurnManager turnManager = FindObjectOfType<TurnManager>();
                if (turnManager != null)
                {
                    turnManager.CompleteTurn();
                }
                else
                {
                    Debug.LogError("TurnManager not found in scene!");
                }
            });
        }
        else
        {
            Debug.LogWarning("No Button component found on endTurnButtonPrefab!");
        }
        
        if (buttonText != null)
        {
            buttonText.text = "End Turn";
        }
    }
    
    private void CreateDynamicEndTurnButton()
    {
        // Create a simple end turn button if no prefab is assigned
        GameObject endTurnSlot = new GameObject("EndTurnButton");
        endTurnSlot.transform.SetParent(actionsGrid.transform, false);
        
        // Try to copy the layout from an action slot if one exists
        if (actionsGrid.transform.childCount > 1)
        {
            RectTransform template = actionsGrid.transform.GetChild(0).GetComponent<RectTransform>();
            if (template != null)
            {
                RectTransform rectTransform = endTurnSlot.AddComponent<RectTransform>();
                rectTransform.sizeDelta = template.sizeDelta;
            }
        }
        
        Button button = endTurnSlot.AddComponent<Button>();
        Image image = endTurnSlot.AddComponent<Image>();
        image.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Red color to distinguish it
        
        button.targetGraphic = image;
        button.interactable = true;
        button.onClick.AddListener(() => {
            Debug.Log("End Turn button clicked!");
            TurnManager turnManager = FindObjectOfType<TurnManager>();
            if (turnManager != null)
            {
                turnManager.CompleteTurn();
            }
            else
            {
                Debug.LogError("TurnManager not found in scene!");
            }
        });
        
        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(endTurnSlot.transform, false);
        TMP_Text buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "End Turn";
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontSize = 36;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}