using UnityEngine;

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

        // Debug logging to see what's happening
        Debug.Log($"Loading actions for character: {character.characterName}");
        Debug.Log($"ActionSlots array length: {character.actionSlots.Length}");

        // Create new action slots
        int validActionCount = 0;
        for (int i = 0; i < character.actionSlots.Length; i++)
        {
            Action action = character.actionSlots[i];
            Debug.Log($"Action slot {i}: {(action != null ? action.actionName : "NULL")}");
            
            if (action == null || string.IsNullOrEmpty(action.actionName)) 
            {
                Debug.Log($"Skipping null or empty action at slot {i}");
                continue; // Skip null or invalid actions
            }
            
            validActionCount++;
            Debug.Log($"Creating action slot for: {action.actionName}");
            
            GameObject actionSlot = Instantiate(actionSlotPrefab, actionsGrid.transform);
            
            if (actionSlot == null)
            {
                Debug.LogError("Failed to instantiate actionSlotPrefab!");
                continue;
            }

            UnityEngine.UI.Button button = actionSlot.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => {
                    FindObjectOfType<CombatController>().SelectAction(action);
                });
                Debug.Log($"Button listener added for {action.actionName}");
            }
            else
            {
                Debug.LogWarning($"No Button component found on actionSlotPrefab!");
            }
            
            TMPro.TMP_Text actionText = actionSlot.GetComponentInChildren<TMPro.TMP_Text>();
            if (actionText != null)
            {
                actionText.text = action.actionName;
                Debug.Log($"Text set to: {action.actionName}");
            }
            else
            {
                Debug.LogWarning($"No TMP_Text component found in children of actionSlotPrefab!");
            }
        }
        
        Debug.Log($"Created {validActionCount} action slots");
    }
}