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
    
    private GameObject[] playerCharacters;

    private void Awake()
    {
        playerCharacters = GameObject.FindGameObjectsWithTag("Player");
        InitializeCrew();
    }

    private void InitializeCrew()
    {
        crewMembers = new Character[MAX_CREW_SIZE];
        crewStats = new CharacterStats[MAX_CREW_SIZE];

        foreach (GameObject playerChar in playerCharacters)
        {
            Character character = playerChar.GetComponent<Character>();
            if (character != null)
            {
                int position = FindNextAvailablePosition();
                if (position <= MAX_CREW_SIZE)
                {
                    crewMembers[position - 1] = character;
                    crewMembers[position - 1].Initialize();
                    StoreCharacterStats(position - 1);
                }
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
    
}
