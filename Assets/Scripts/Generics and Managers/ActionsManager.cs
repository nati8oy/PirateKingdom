using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionsManager : MonoBehaviour
{
    [SerializeField] private GameObject actionsGrid;
    [SerializeField] private GameObject actionSlotPrefab;

    public void LoadCharacterActions(Character character)
    {
        // Clear existing actions
        foreach (Transform child in actionsGrid.transform)
        {
            Destroy(child.gameObject);
        }

        // Create new action slots
        for (int i = 0; i < character.actionSlots.Length; i++)
        {
            Action action = character.actionSlots[i];
            
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
            
            // Check if action is available (not on cooldown)
            bool isAvailable = character.IsActionAvailable(action);
            int cooldownRemaining = character.GetActionCooldownRemaining(action);
            
            if (button != null)
            {
                // Enable/disable button based on availability
                button.interactable = isAvailable;
                
                button.onClick.AddListener(() => {
                    Debug.Log($"Button clicked for action: {action.actionName}");
                    FindObjectOfType<CombatController>().SelectAction(action);
                });
            }
            else
            {
                Debug.LogWarning($"No Button component found on actionSlotPrefab!");
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
    }
}