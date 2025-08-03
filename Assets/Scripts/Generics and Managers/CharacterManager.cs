using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Feedbacks;

public class CharacterManager : MonoBehaviour
{
    [Header("Character Stats")]
    [SerializeField]
    [Tooltip("Scriptable object containing the character's base stats and information")]
    public Character characterData;
    [Tooltip("Current health value of the character")]
    private float CurrentHealth;
    [Tooltip("Maximum health value the character can have")]
    public float MaxHealth;
    [Tooltip("Character's attack power used for damage calculations")]
    public float AttackPower;
    [Tooltip("Character's defense value used to determine if attacks hit")]
    public float DefenseValue;
    [Tooltip("Character's speed value used for turn order")]
    public float Speed;
    [Tooltip("Character's position in the battle formation")]
    public int Position;

    [Header("UI Elements")]
    [SerializeField] private Slider healthBar;
    [SerializeField] TMP_Text characterName;
    [SerializeField] private TMP_Text hp;
    [SerializeField] private TMP_Text healthModifier;
    public Image turnMarker;

    [Header("Feedback Players")]
    [Tooltip("Feeback player for damage")]
    public MMF_Player damageFeedback;
    [Tooltip("Feeback player for healing")]
    public MMF_Player healFeedback;
    [Tooltip("Feeback player for missing or dodges")]
    public MMF_Player missFeedback;
    public MMF_Player feedbackPlayer;
    
    
    //[SerializeField] private TurnManager _turnManager;
    public delegate void OnDeathHandler();
    public event OnDeathHandler OnDeath;

    private bool isDead = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        healthModifier.enabled = false;
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
        healthModifier.enabled = true;
        healthModifier.text = "-" + Mathf.Round(damage).ToString();
        damageFeedback.PlayFeedbacks();
        
        // Round damage using standard rounding (0.5 rounds up)
        float roundedDamage = Mathf.Round(damage);
        CurrentHealth = Mathf.Max(0, CurrentHealth - roundedDamage);
        UpdateHealthBar();

       feedbackPlayer.PlayFeedbacks();

        if (CurrentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {   
        healthModifier.enabled = true;
        healthModifier.text = "+" + Mathf.Round(amount).ToString();
        healFeedback.PlayFeedbacks();
        
        // Round heal amount using standard rounding (0.5 rounds up)
        float roundedHeal = Mathf.Round(amount);
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + roundedHeal);
        UpdateHealthBar();
    }

    public void AddBuff(Character.BuffType Type, float amount, float duration)
    {
        //add debuff code here. Is used for both buff and debuff.
    }

    public void Miss()
    {
        healthModifier.enabled = true;
        healthModifier.text = "Missed!";
        missFeedback.PlayFeedbacks();
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


    public void HideHealthUI()
    {
        
        healthModifier.enabled = false;
        
        
    }
}