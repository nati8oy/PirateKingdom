using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterManager : MonoBehaviour
{
    [SerializeField] public Character characterData;
    [SerializeField] private Slider healthBar;
    [SerializeField] TMP_Text characterName;
    [SerializeField] private TMP_Text hp;
    public Image turnMarker;
    public delegate void OnDeathHandler();
    public event OnDeathHandler OnDeath;
    
    private float CurrentHealth;

    public float MaxHealth;

    
    public float AttackPower;
    public float DefenseValue;
    public float Speed;
    public int Position;
    
    private bool isDead = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        characterName.text = characterData.characterName;
        hp.text = CurrentHealth.ToString() + "/" + MaxHealth.ToString();
        
        
        if (characterData != null)
        {
            MaxHealth = characterData.maxHealth;
            CurrentHealth = characterData.maxHealth;
            AttackPower = characterData.attackPower;
            DefenseValue = characterData.defenseValue;
            Speed = characterData.speed;
            UpdateHealthBar();
        }
        else
        {
            Debug.LogError("Character Data is missing!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        hp.text = CurrentHealth.ToString() + "/" + MaxHealth.ToString();
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth);
        
        if (!isDead && CurrentHealth <= 0)
        {
            isDead = true;
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }

    public void TakeDamage(float damage)
    {
        // Round damage using standard rounding (0.5 rounds up)
        float roundedDamage = Mathf.Round(damage);
        CurrentHealth = Mathf.Max(0, CurrentHealth - roundedDamage);
        UpdateHealthBar();

        if (CurrentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        // Round heal amount using standard rounding (0.5 rounds up)
        float roundedHeal = Mathf.Round(amount);
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + roundedHeal);
        UpdateHealthBar();
    }

    public void AddBuff(Character.BuffType Type, float amount, float duration)
    {
        //add debuff code here. Is used for both buff and debuff.
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.value = (CurrentHealth / MaxHealth) * 1f;
        }
    }

    private void OnValidate()
    {
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<Slider>();
            if (healthBar == null)
            {
                Debug.LogWarning("Health Bar Slider not assigned and not found in children!");
            }
        }
    }
    
}