using UnityEngine;

public class CombatController : MonoBehaviour
{
    private TurnManager turnManager;
    public CharacterManager currentTargetManager;
    public Action selectedAction;

    void Awake()
    {
        turnManager = FindObjectOfType<TurnManager>();
        if (turnManager == null)
        {
            Debug.LogError("No TurnManager found in the scene!");
        }
    }

    public void OnPlayerActionComplete()
    {
        if (turnManager != null)
        {
            // Handle any additional logic needed after player action
            // Currently empty but kept for ClickableCharacter compatibility
        }
        else
        {
            Debug.LogError("Cannot handle action complete: TurnManager is null!");
        }
    }

    public void SelectAction(Action action)
    {
        selectedAction = action;
        
        // Your existing action selection logic here...
        
        // After successfully performing the action, mark it as used
        if (turnManager.currentCharacterTurn != null && turnManager.currentCharacterTurn.characterData != null)
        {
            turnManager.currentCharacterTurn.characterData.UseAction(action);
            
            // Refresh the actions UI to show updated cooldown states
            FindObjectOfType<ActionsManager>().LoadCharacterActions(turnManager.currentCharacterTurn.characterData);
        }
    }

    public void SetCurrentTarget(CharacterManager targetManager)
    {
        currentTargetManager = targetManager;
    }

    public bool TryExecuteActionOnTarget(CharacterManager targetManager)
    {
        SetCurrentTarget(targetManager);
        return TryExecuteActionOnCurrentTarget();
    }

    // Keep this for backward compatibility with ClickableCharacter
    public bool TryExecuteActionOnTarget(Character target)
    {
        // Since we no longer have hardcoded references, we need to find the CharacterManager
        // by searching through all CharacterManagers in the scene
        CharacterManager[] allManagers = FindObjectsOfType<CharacterManager>();
        CharacterManager targetManager = null;
        
        foreach (CharacterManager manager in allManagers)
        {
            if (manager.characterData == target)
            {
                targetManager = manager;
                break;
            }
        }
        
        if (targetManager != null)
        {
            return TryExecuteActionOnTarget(targetManager);
        }
        else
        {
            Debug.LogError($"Could not find CharacterManager for character: {target.name}");
            return false;
        }
    }

    public bool TryExecuteActionOnCurrentTarget()
    {
        if (selectedAction == null || currentTargetManager == null)
        {
            Debug.LogError("No action selected or no target set!");
            return false;
        }

        if (!IsValidTarget(currentTargetManager))
        {
            Debug.LogError($"Invalid target for action {selectedAction.name}");
            return false;
        }

        PerformAction(currentTargetManager);
        selectedAction = null;
        currentTargetManager = null;
        return true;
    }

    private bool IsValidTarget(CharacterManager targetManager)
    {
        if (selectedAction == null)
            return false;

        switch (selectedAction.targetType)
        {
            case Action.TargetType.SingleEnemy:
                return targetManager.gameObject.CompareTag("Enemy");
            case Action.TargetType.SingleAlly:
                return targetManager.gameObject.CompareTag("Player");
            case Action.TargetType.AllAllies:
                return targetManager.gameObject.CompareTag("Player");
            case Action.TargetType.AllEnemies:
                return targetManager.gameObject.CompareTag("Enemy");
            default:
                return false;
        }
    }

    private int HitChanceRoll()
    {
        return Random.Range(1, 21);
    }

    public void PerformAction(CharacterManager targetManager)
    {
        if (targetManager == null)
        {
            Debug.LogError($"Target CharacterManager is null!");
            return;
        }

        // Get the current character performing the action
        CharacterManager currentCharacter = turnManager.currentCharacterTurn;

        switch (selectedAction.actionType)
        {
            case Action.ActionType.Attack:
                int attackRoll = HitChanceRoll();
                if (attackRoll == 1)
                {
                    Debug.Log("Critical Fail! Attack missed.");
                    targetManager.Miss();
                    break;
                }

                if (attackRoll == 20 || (attackRoll + currentCharacter.AttackPower >= targetManager.DefenseValue))
                {
                    float damage = Random.Range(selectedAction.minDamage, selectedAction.maxDamage);
                    if (attackRoll == 20)
                    {
                        Debug.Log("Critical Hit! Double damage!");
                        damage *= 2;
                    }
                    Debug.Log("Attack value of " + (attackRoll + currentCharacter.AttackPower) + " hit enemy with defense value of: " + targetManager.DefenseValue);
                    targetManager.TakeDamage(damage);
                }
                else
                {
                    Debug.Log("Attack value of " + (attackRoll + currentCharacter.AttackPower) + " Missed enemy with defense value of: " + targetManager.DefenseValue);
                    targetManager.Miss();
                }
                break;

            case Action.ActionType.Heal:
                int healRoll = HitChanceRoll();
                float healAmount = Random.Range(selectedAction.minHeal, selectedAction.maxHeal);
                if (healRoll == 20)
                {
                    Debug.Log("Critical Heal! Double healing!");
                    healAmount *= 2;
                }
                targetManager.Heal(healAmount);
                Debug.Log($"Healing {targetManager.gameObject.name} by {healAmount}");
                break;

            case Action.ActionType.Buff:
                currentTargetManager.buffEffectText.text = selectedAction.buffType.ToString() + " +" + selectedAction.buffValue;
                targetManager.AddBuff(selectedAction.buffType, selectedAction.buffValue, selectedAction.duration);
                break;

            case Action.ActionType.Debuff:
                targetManager.AddBuff(selectedAction.buffType, -selectedAction.buffValue, selectedAction.duration);
                break;
        }
        
        turnManager.CompleteTurn();
    }
    
    
}