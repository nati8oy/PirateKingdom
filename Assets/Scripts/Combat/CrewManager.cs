using UnityEngine;

public class CrewManager : MonoBehaviour
{
    private Character[] crewMembers;
    private const int MAX_CREW_SIZE = 4;
    private CharacterStats[] crewStats;

    private class CharacterStats
    {
        public float maxHealth;
        public float attackPower;
        public float defenseValue;
        public float currentHealth;
    }

    public Character crewSlot1;
    public Character crewSlot2;
    public Character crewSlot3;
    public Character crewSlot4;
    
    private void Awake()
    {
        InitializeCrew();
    }

    private void InitializeCrew()
    {
        crewMembers = new Character[MAX_CREW_SIZE];
        crewStats = new CharacterStats[MAX_CREW_SIZE];

        Character[] tempSlots = { crewSlot1, crewSlot2, crewSlot3, crewSlot4 };

        for (int i = 0; i < tempSlots.Length; i++)
        {
            if (tempSlots[i] != null)
            {
                int position = FindNextAvailablePosition();
                tempSlots[i].Position = position;
                crewMembers[position - 1] = tempSlots[i];
                crewMembers[position - 1].Initialize();
                StoreCharacterStats(position - 1);
            }
        }

        UpdateCrewSlots();
    }

    private void StoreCharacterStats(int index)
    {
        crewStats[index] = new CharacterStats
        {
            maxHealth = crewMembers[index].maxHealth,
            attackPower = crewMembers[index].attackPower,
            defenseValue = crewMembers[index].defenseValue,
            //currentHealth = crewMembers[index].currentHealth
        };
    }
    
    public Character[] GetCrewMembers()
    {
        return crewMembers;
    }
    
    private bool IsValidPosition(int position)
    {
        return position >= 0 && position < MAX_CREW_SIZE;
    }

    private bool IsPositionTaken(int position)
    {
        return crewMembers[position - 1] != null;
    }

    private int FindNextAvailablePosition()
    {
        for (int i = 0; i < MAX_CREW_SIZE; i++)
        {
            if (crewMembers[i] == null)
                return i + 1;
        }
        return MAX_CREW_SIZE;
    }

    private void UpdateCrewSlots()
    {
        crewSlot1 = crewMembers[0];
        crewSlot2 = crewMembers[1];
        crewSlot3 = crewMembers[2];
        crewSlot4 = crewMembers[3];
    }

    public bool SwapCrewPositions(int position1, int position2)
    {
        if (!IsValidPosition(position1) || !IsValidPosition(position2))
            return false;

        Character temp = crewMembers[position1];
        crewMembers[position1] = crewMembers[position2];
        crewMembers[position2] = temp;

        if (crewMembers[position1] != null)
            crewMembers[position1].Position = position1 + 1;
        if (crewMembers[position2] != null)
            crewMembers[position2].Position = position2 + 1;

        UpdateCrewSlots();
        return true;
    }
    
    
    
}
