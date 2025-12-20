using AssetKits.ParticleImage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Feedbacks;
using UnityEngine.Serialization;
using static AssetKits.ParticleImage.ParticleImage;

public class CharacterManager : MonoBehaviour
{
    [Header("Character Stats")]
    [SerializeField]
    [Tooltip("Scriptable object containing the character's base stats and information")]
    public Character characterData;
    [Tooltip("Current health value of the character")]
    public float CurrentHealth;
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
    //[SerializeField] private TMP_Text hp;
    [FormerlySerializedAs("healthModifier")] [SerializeField] private TMP_Text actionStatusText;
    
    public Image turnMarker;

    [Header("Feedback Players")]
    [Tooltip("Feeback player for damage")]
    public MMF_Player dealDamageFeedback;
    [Tooltip("Feedback player for healing")]
    public MMF_Player healFeedback;
    [Tooltip("Feedback player for missing or dodges")]
    public MMF_Player missFeedback;
    [SerializeField] private MMF_Player parryFeedback;
    public MMF_Player feedbackPlayer;

    
    public ParticleImage driveParticles;
    
    [Header("Character-Specific Audio")]
    [Tooltip("MMF_Player for this character's attack audio (configure with MMF_AudioSource)")]
    public MMF_Player attackAudioFeedback;
    [Tooltip("MMF_Player for this character's death audio (configure with MMF_AudioSource)")]
    public MMF_Player deathAudioFeedback;
    
    public delegate void OnDeathHandler();
    public event OnDeathHandler OnDeath;

    private bool isDead = false;
    private DriveManager driveManager;
    
    private DriveMeter driveMeter;
    
    void Awake()
    {
        driveMeter = new DriveMeter();
        driveMeter.Initialize();
    }

    
    void Start()
    {
        driveManager = GetComponent<DriveManager>();
        
        actionStatusText.enabled = false;
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
        
        //hp.text = CurrentHealth + "/" + MaxHealth;
    }

    void Update()
    {
        //hp.text = CurrentHealth + "/" + MaxHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth);
        
        if (!isDead && CurrentHealth <= 0)
        {
            isDead = true;
            
            // Play character-specific death audio from scriptable object
            PlayDeathAudio();
            
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Plays the death audio from the character's scriptable object data
    /// Falls back to the MMF_Player if no AudioClip is assigned in the scriptable object
    /// </summary>
    private void PlayDeathAudio()
    {
        // First try to use the AudioClip from the Character scriptable object
        if (characterData != null && characterData.deathSoundEffect != null)
        {
            // Get or create an AudioSource component
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.PlayOneShot(characterData.deathSoundEffect);
            Debug.Log($"Playing death sound from scriptable object for {characterData.characterName}");
        }
        // Fallback to MMF_Player if no AudioClip is set in scriptable object
        else if (deathAudioFeedback != null)
        {
            deathAudioFeedback.PlayFeedbacks();
            Debug.Log($"Playing death sound from MMF_Player for {characterData?.characterName ?? gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"No death sound configured for {characterData?.characterName ?? gameObject.name}");
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
        driveManager?.OnTurnStart(); // Regenerate drive at turn start
        RefreshStats(); // Make sure we have the current modified stats
    }

    // Call this when the character completes their turn
    public void OnTurnComplete()
    {
        characterData?.UpdateBuffsForCharacterTurn();
        characterData?.UpdateActionCooldowns(); 
        driveManager?.OnTurnEnd(); // Handle drive turn end effects
        RefreshStats(); // Update stats after buffs are reduced
        if (characterData != null) Debug.Log($"{characterData.characterName} completed their turn, buffs updated");
    }

    // Deprecated method - kept for backwards compatibility
    public void OnRoundComplete()
    {
        // This method is now deprecated since we use turn-based buffs
        // The method is kept to avoid breaking existing code, but does nothing
        Debug.LogWarning($"OnRoundComplete() is deprecated for {characterData.characterName}. Buffs are now updated per turn.");
    }

    
    public void TakeDamage(float damage, bool wasParried = false)
    {
        // Only update the status text and play feedback if it wasn't a parry
        if (!wasParried)
        {
            actionStatusText.enabled = true;
            actionStatusText.text = "-" + Mathf.Round(damage);
            dealDamageFeedback.PlayFeedbacks();
            feedbackPlayer.PlayFeedbacks();
        }
    
        float roundedDamage = Mathf.Round(damage);
        CurrentHealth = Mathf.Max(0, CurrentHealth - roundedDamage);
        UpdateHealthBar();

        if (CurrentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    
        // Add damage taken to drive meter through DriveManager
        if (driveManager != null)
        {
            var driveIncrease = damage * characterData.damageTakenDriveMultiplier;
            //drive gained is multiplied by the damage in the damageTakenDriveMultiplier character data
            driveManager.Drive.AddDrive(driveIncrease);
            Debug.Log($"{characterData.characterName} took {damage} damage, drive increased by {driveIncrease}");
        }
    }
    
    /// <summary>
    /// Handles when an attack is successfully parried
    /// </summary>
    public void OnAttackParried()
    {
        actionStatusText.enabled = true;
        actionStatusText.text = "Parry!";
        
        // Play parry feedback instead of damage feedback
        if (parryFeedback != null)
        {
            parryFeedback.PlayFeedbacks();
        }
        
        // Add drive bonus for successful parry (already handled in ParrySystem)
        // No health reduction since attack was parried
    }

    public void DealDamage(CharacterManager target, float damage)
    {
        // Deal damage to target
        target.TakeDamage(damage);

        // Add damage dealt to attacker's drive meter through DriveManager
        if (driveManager != null)
        {
           var driveIncrease = damage * characterData.damageInflictedDriveMultiplier;
            
            //drive gained is multiplied by the damageInflictedDriveMultiplier in the character data
            driveManager.Drive.AddDrive(driveIncrease);
            Debug.Log($"{characterData.characterName} inflicted {damage} damage on {target.characterData.characterName}, drive increased by {driveIncrease}");
        }
    }
    
    public void OnDamageDealt(float damage)
    {
        // Add damage dealt to drive meter
        if (driveManager != null)
        {
            var driveIncrease = damage * characterData.damageInflictedDriveMultiplier;
            
            //drive gained is multiplied by the damageInflictedDriveMultiplier in the character data
            driveManager.Drive.AddDrive(driveIncrease);
            Debug.Log($"{characterData.characterName} dealt {damage} damage, drive increased by {driveIncrease}");
        }
    }

    // Modified to consider drive attack buffs
    public float GetModifiedAttackPower()
    {
        float baseAttack = AttackPower;
        
        // Apply drive system attack buff if active
        if (driveManager != null)
        {
            baseAttack *= driveManager.GetNextAttackDamageMultiplier();
        }
        
        return baseAttack;
    }
    
    // Separate method for just playing attack audio without drive effects
    public void PlayAttackAudio()
    {
        if (attackAudioFeedback != null)
        {
            attackAudioFeedback.PlayFeedbacks();
        }
    }

    // Method that handles drive mode consumption and buffs (no audio)
    public void OnAttackPerformed()
    {
        driveManager?.ConsumeAttackBuff();
        // Also deactivate drive mode for any action, not just attacks
        if (driveManager?.IsDriveMode == true)
        {
            driveManager.OnAttackPerformed(); // This will deactivate drive mode
        }
    }

    public void Heal(float amount)
    {   
        // Apply drive mode healing multiplier if active
        if (driveManager != null)
        {
            float healingMultiplier = driveManager.GetNextHealingMultiplier();
            amount *= healingMultiplier;
            
            if (healingMultiplier > 1f)
            {
                Debug.Log($"Drive mode boosted healing from base amount to {amount} (multiplier: {healingMultiplier}x)");
            }
        }
        
        actionStatusText.enabled = true;
        actionStatusText.text = "+" + Mathf.Round(amount);
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
        //Debug.Log($"{buffName} applied to {characterData.characterName}: {type} {amount:+0;-0} for {turns} turns");
    }

    public void Miss()
    {
        actionStatusText.enabled = true;
        actionStatusText.text = "Missed!";
        missFeedback.PlayFeedbacks();
    }
    
    public void Parry()
    {
        actionStatusText.enabled = true;
        actionStatusText.text = "Parry!";
        parryFeedback.PlayFeedbacks();
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
        actionStatusText.enabled = false;
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
    
    // Public getter for the drive manager
    public DriveManager GetDriveManager()
    {
        return driveManager;
    }
    // Add this method to access current health
    public float GetCurrentHealth() 
    { 
        return CurrentHealth; 
    }

 
}