using System.Collections.Generic;
using System.Linq;
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

    // --- Per-battle runtime state ---
    // These used to live on the Character ScriptableObject, which meant two scene objects sharing
    // an asset (both Skeletons share Skeleton.asset) shared one set of buffs and cooldowns, and
    // that Editor play sessions wrote into the project asset. They belong to the instance.
    private readonly List<Character.ActiveBuff> activeBuffs = new List<Character.ActiveBuff>();
    private readonly Dictionary<Action, int> actionCooldowns = new Dictionary<Action, int>();

    // The run-state record this character is playing as, when a run is in progress. Null for
    // enemies (per-encounter, nothing persists) and whenever there's no active run.
    private CrewMemberState crewState;

    /// <summary>The run-state record backing this character, or null if not part of a run.</summary>
    public CrewMemberState CrewState => crewState;

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
            BindToRunState();
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

            // Permadeath: record it in the run before this object goes away, or the crew member
            // would come back next encounter as though nothing happened.
            if (crewState != null)
            {
                crewState.isDead = true;
                crewState.currentHealth = 0f;
                Debug.Log($"[CharacterManager] {crewState.displayName} died — removed from the run's crew for good.");
                RunManager.Instance?.SaveRun();
            }

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

    /// <summary>
    /// Picks up this character's carried-over health from the active run, falling back to full
    /// health when there's no run (playing the encounter scene directly), when this is an enemy,
    /// or when no matching crew record exists.
    /// </summary>
    /// <remarks>
    /// Binding by <c>characterData.Id</c> is a temporary bridge for scene-placed crew. Once
    /// encounters spawn their combatants from run state, the spawner should assign the
    /// <see cref="CrewMemberState"/> explicitly — matching on id can't distinguish two crew
    /// sharing one Character asset.
    /// </remarks>
    private void BindToRunState()
    {
        CurrentHealth = MaxHealth;

        // Enemies are per-encounter; nothing about them persists between fights.
        if (characterData.allegiance != Character.Allegiance.Player) return;

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun) return;

        crewState = RunManager.Instance.GetCrewMember(characterData.Id);

        if (crewState == null)
        {
            Debug.LogWarning($"[CharacterManager] {characterData.characterName} (id '{characterData.Id}') " +
                             "isn't in the active run's crew — starting at full health.");
            return;
        }

        if (crewState.isDead)
        {
            Debug.LogWarning($"[CharacterManager] {crewState.displayName} is dead in the current run but is " +
                             "still placed in the scene. They'll be spawned out once encounters build their " +
                             "own crew from run state.");
        }

        CurrentHealth = Mathf.Clamp(crewState.currentHealth, 0f, MaxHealth);
        Debug.Log($"[CharacterManager] {crewState.displayName} joined the fight at {CurrentHealth}/{MaxHealth}.");
    }

    /// <summary>Pushes current health back into the run so it carries to the next encounter.</summary>
    public void SyncHealthToRunState()
    {
        if (crewState == null) return;
        crewState.currentHealth = CurrentHealth;
    }

    // Refresh all stats from character data, including buffs
    public void RefreshStats()
    {
        if (characterData != null)
        {
            // Authored base from the ScriptableObject + this instance's own buffs. Clamps match
            // the ones the Character asset used to apply.
            MaxHealth = Mathf.Max(1f, characterData.maxHealth + SumBuffValue(Character.BuffType.Health));
            AttackPower = Mathf.Max(0f, characterData.attackPower + SumBuffValue(Character.BuffType.Attack));
            DefenseValue = Mathf.Max(0f, characterData.defenseValue + SumBuffValue(Character.BuffType.Defense));
            Speed = Mathf.Max(0.1f, characterData.speed + SumBuffValue(Character.BuffType.Speed));
            UpdateBuffDisplay();
        }
    }

    private float SumBuffValue(Character.BuffType buffType)
    {
        float total = 0f;
        foreach (var buff in activeBuffs)
        {
            if (buff.Type == buffType)
            {
                total += buff.Value;
            }
        }
        return total;
    }

    /// <summary>Ticks down this character's buffs at the end of their turn, dropping expired ones.</summary>
    private void UpdateBuffsForCharacterTurn()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].ReduceTurns();
            if (activeBuffs[i].IsExpired())
            {
                Debug.Log($"Buff/Debuff expired on {characterData?.characterName}: {activeBuffs[i].Type} after completing turn");
                activeBuffs.RemoveAt(i);
            }
        }
    }

    /// <summary>Ticks down this character's action cooldowns at the end of their turn.</summary>
    public void UpdateActionCooldowns()
    {
        foreach (var action in actionCooldowns.Keys.ToList())
        {
            int turnsRemaining = actionCooldowns[action] - 1;

            if (turnsRemaining <= 0)
            {
                actionCooldowns.Remove(action);
                Debug.Log($"{characterData?.characterName}: {action.actionName} cooldown expired and is now available");
            }
            else
            {
                actionCooldowns[action] = turnsRemaining;
            }
        }
    }

    /// <summary>Marks an action used, putting it on cooldown for this character only.</summary>
    public void UseAction(Action action)
    {
        if (action != null && action.cooldown > 0)
        {
            int cooldownTurns = Mathf.RoundToInt(action.cooldown);
            actionCooldowns[action] = cooldownTurns;
            Debug.Log($"{characterData?.characterName} used {action.actionName}, cooldown: {cooldownTurns} turns");
        }
    }

    /// <summary>Whether this character can use the action right now (i.e. it isn't on cooldown).</summary>
    public bool IsActionAvailable(Action action)
    {
        if (action == null) return false;
        if (action.cooldown <= 0) return true;

        return !actionCooldowns.ContainsKey(action);
    }

    public int GetActionCooldownRemaining(Action action)
    {
        if (action == null || !actionCooldowns.ContainsKey(action))
            return 0;

        return actionCooldowns[action];
    }

    /// <summary>Removes the first negative-valued buff. Used by the Drive "remove debuff" action.</summary>
    public bool RemoveFirstDebuff()
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].Value < 0)
            {
                activeBuffs.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public List<Character.ActiveBuff> GetActiveBuffs()
    {
        return new List<Character.ActiveBuff>(activeBuffs);
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
        UpdateBuffsForCharacterTurn();
        UpdateActionCooldowns();
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
        SyncHealthToRunState();

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
        SyncHealthToRunState();
    }

    public void AddBuff(Character.BuffType type, float amount, float duration)
    {
        // Convert float duration to turns (assuming 1 duration = 1 turn)
        int turns = Mathf.RoundToInt(duration);
        activeBuffs.Add(new Character.ActiveBuff(type, amount, turns));
        Debug.Log($"Added {(amount > 0 ? "buff" : "debuff")} to {characterData?.characterName}: {type} {amount:+0;-0} for {turns} turns");
        RefreshStats(); // Immediately update stats to reflect the new buff
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