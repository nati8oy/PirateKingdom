using UnityEngine;

public class Debug_Crew : MonoBehaviour
{
    public CrewManager crewManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (crewManager != null)
        {
            Character[] crew = crewManager.GetCrewMembers();
            for (int i = 0; i < crew.Length; i++)
            {
                if (crew[i] != null)
                {
                    Debug.Log($"Crew Member {i + 1}: {crew[i].characterName} (Level {crew[i].level})");
                    //Debug.Log($"Stats - HP: {crew[i].currentHealth}/{crew[i].maxHealth}, ATK: {crew[i].attackPower}, DEF: {crew[i].defenseValue}, Position: {crew[i].position}");
                }
                else
                {
                    Debug.Log($"Crew Slot {i + 1}: Empty");
                }
            }
        }
        else
        {
            Debug.LogWarning("CrewManager reference is missing!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
