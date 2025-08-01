using UnityEngine;

public class ActionSet : MonoBehaviour
{
    [Header("Action Slots")]
    [SerializeField] private Action actionSlot1;
    [SerializeField] private Action actionSlot2;
    [SerializeField] private Action actionSlot3;
    [SerializeField] private Action actionSlot4;
    [SerializeField] private Action actionSlot5;
    [SerializeField] private Action actionSlot6;

    public Action[] GetAllActions()
    {
        return new Action[] { actionSlot1, actionSlot2, actionSlot3, actionSlot4, actionSlot5, actionSlot6 };
    }
    
    public bool IsActionSlotFilled(int index)
    {
        if (index < 0 || index >= 6) return false;
        return GetAllActions()[index] != null;
    }

    public Action GetAction(int index)
    {
        if (index < 0 || index >= 6) return null;
        return GetAllActions()[index];
    }
}
