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
    [Tooltip("Current buff attribute and it's amount")]
    public TMP_Text buffEffectText;
    
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
    
    public delegate void OnDeathHandler();
    public event OnDeathHandler OnDeath;

    private bool isDead = false;
    
    void Start()
    {
        healthModifier.enabled = false;
        characterName.text = characterData.characterName;
        
        if (characterData != null)
        {
            RefreshStats();
            CurrentHealth = characterData.maxHealth;
            UpdateHealthBar();
        }
        else
        {
            Debug.LogError("Character Data is missing!");
        }
        
        hp.text = CurrentHealth.ToString() + "/" + MaxHealth.ToString();
    }

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

    // Refresh all stats from character data, including buffs
    public void RefreshStats()
    {
        if (characterData != null)
        {
            MaxHealth = characterData.GetModifiedMaxHealth();
            AttackPower = characterData.GetModifiedAttackPower();
            DefenseValue = characterData.GetModifiedDefenseValue();
            Speed = characterData.GetModifiedSpeed();
            UpdateBuffDisplay();
        }
    }

    // Call this at the beginning of each character's turn to update buffs and refresh stats
    public void UpdateBuffsForTurn()
    {
        RefreshStats(); // Make sure we have the current modified stats
    }

    // Call this when the character completes their turn
    public void OnTurnComplete()
    {
        characterData?.UpdateBuffsForCharacterTurn();
        RefreshStats(); // Update stats after buffs are reduced
        Debug.Log($"{characterData.characterName} completed their turn, buffs updated");
    }

    // Deprecated method - kept for backwards compatibility
    public void OnRoundComplete()
    {
        // This method is now deprecated since we use turn-based buffs
        // The method is kept to avoid breaking existing code, but does nothing
        Debug.LogWarning($"OnRoundComplete() is deprecated for {characterData.characterName}. Buffs are now updated per turn.");
    }

    public void TakeDamage(float damage)
    {
        healthModifier.enabled = true;
        healthModifier.text = "-" + Mathf.Round(damage).ToString();
        damageFeedback.PlayFeedbacks();
        
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
        
        float roundedHeal = Mathf.Round(amount);
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + roundedHeal);
        UpdateHealthBar();
    }

    public void AddBuff(Character.BuffType type, float amount, float duration)
    {
        // Convert float duration to turns (assuming 1 duration = 1 turn)
        int turns = Mathf.RoundToInt(duration);
        characterData?.AddBuff(type, amount, turns);
        RefreshStats(); // Immediately update stats to reflect the new buff
        
        string buffName = amount > 0 ? "Buff" : "Debuff";
        Debug.Log($"{buffName} applied to {characterData.characterName}: {type} {amount:+0;-0} for {turns} turns");
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
    
    
    public bool HasActiveBuff(Character.BuffType buffType)
    {
        if (characterData == null) return false;
    
        var activeBuffs = characterData.GetActiveBuffs();
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == buffType)
                return true;
        }
        return false;
    }
    
    public void UpdateBuffDisplay()
    {
        if (buffEffectText == null || characterData == null) return;
    
        var activeBuffs = characterData.GetActiveBuffs();
    
        if (activeBuffs.Count == 0)
        {
            buffEffectText.text = "";
            return;
        }
    
        string buffText = "";
        foreach (var buff in activeBuffs)
        {
            string buffName = buff.Type.ToString();
            string sign = buff.Value > 0 ? "+" : "";
            buffText += $"{buffName}: {sign}{buff.Value} ({buff.TurnsRemaining}t)\n";
        }
    
        buffEffectText.text = buffText.TrimEnd('\n');
    }
}