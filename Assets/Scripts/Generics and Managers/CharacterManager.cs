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

    [Header("Art")]
    [Tooltip("The body SpriteRenderer — img_character on the prefab. Left empty, the first " +
             "SpriteRenderer in children is used.")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("UI Elements")]
    [SerializeField] private Slider healthBar;
    [SerializeField] TMP_Text characterName;
    //[SerializeField] private TMP_Text hp;
    [FormerlySerializedAs("healthModifier")] [SerializeField] private TMP_Text actionStatusText;

    [Tooltip("Seconds the '+N Drive' / '-N Drive' number stays up when NO drive feedback is wired " +
             "above. Every other status number is cleared by its own MMF_Player (an MMF_Events " +
             "calling HideHealthUI), so this is only the safety net for the unwired case — assign " +
             "the feedback slots and this is never used.")]
    [SerializeField] private float driveStatusTextDuration = 0.9f;
    
    public Image turnMarker;

    [Tooltip("img_target_indicator on the prefab. Shown while an enemy is telegraphing an attack on " +
             "this character, and while the player hovers this character as a legal target.")]
    [SerializeField] private GameObject targetIndicator;

    // Two independent reasons the indicator can be lit, tracked separately so neither source can
    // switch off the other's. A single bool would mean whichever finished last hid the marker —
    // hovering a crew member mid-telegraph and moving away would cancel the enemy's "you are about
    // to be hit" cue, and the enemy's attack resolving would drop a live hover highlight.
    //
    // The two happen to be disjoint in time today (hover targeting needs a selected action, which
    // only exists on a player's turn; telegraphs only run on an enemy's), but that's a consequence
    // of TurnManager clearing the selection on every turn change — an invariant living in another
    // class. Not worth coupling this to.
    private bool telegraphTargeted;
    private bool hoverTargeted;

    /// <summary>
    /// Marks this character as the victim of an incoming attack. Called by the attacking
    /// <see cref="EnemyManager"/> before its windup, and cleared when the attack resolves.
    /// </summary>
    public void SetTargeted(bool targeted)
    {
        telegraphTargeted = targeted;
        RefreshTargetIndicator();
    }

    /// <summary>
    /// Marks this character as the target the player is currently hovering for their selected
    /// action. Driven by <see cref="ActionTargetingLine"/>, which re-evaluates every frame.
    /// </summary>
    /// <remarks>
    /// Reuses the enemy's telegraph indicator deliberately: "this character is about to be acted
    /// on" is one idea, and giving it one visual keeps the player reading a single symbol rather
    /// than learning two.
    /// </remarks>
    public void SetHoverTargeted(bool targeted)
    {
        hoverTargeted = targeted;
        RefreshTargetIndicator();
    }

    private void RefreshTargetIndicator()
    {
        if (targetIndicator != null) targetIndicator.SetActive(telegraphTargeted || hoverTargeted);
    }

    [Header("Feedback Players")]
    [Tooltip("Feeback player for damage")]
    public MMF_Player dealDamageFeedback;
    [Tooltip("Feedback player for healing")]
    public MMF_Player healFeedback;
    [Tooltip("Feedback player for missing or dodges")]
    public MMF_Player missFeedback;
    [SerializeField] private MMF_Player parryFeedback;
    [Tooltip("Feedback player for a buff landing on this character — the one-shot cast burst.")]
    public MMF_Player buffFeedback;

    [Tooltip("Optional. Played when a DriveGrant action fills this character's drive meter.")]
    public MMF_Player driveGrantedFeedback;

    [Tooltip("Optional. Played when a DriveDrain action strips this character's drive meter.")]
    public MMF_Player driveDrainedFeedback;

    public MMF_Player feedbackPlayer;


    public ParticleImage driveParticles;

    [Tooltip("Particle shown continuously while any buff is active on this character, and stopped " +
             "when the last one expires. Same Play/Stop pattern as driveParticles.")]
    public ParticleImage buffParticles;
    
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

    /// <summary>
    /// Assigns the run record this character is playing as. Call this while the instance is still
    /// parented to the spawner's inactive staging object — i.e. before <c>Awake</c>/<c>Start</c>.
    /// </summary>
    /// <remarks>
    /// This is the authoritative way to bind a spawned crew member. The id-matching fallback in
    /// <see cref="BindToRunState"/> exists only for hand-placed crew and can't tell two crew
    /// sharing one Character asset apart.
    /// </remarks>
    public void AssignCrewState(CrewMemberState state)
    {
        crewState = state;
    }

    void Awake()
    {
        driveMeter = new DriveMeter();
        driveMeter.Initialize();

        // Resolved in Awake, not Start: TurnManager.BeginBattle() runs during the Start phase and
        // reaches driveManager via UpdateBuffsForTurn(). If this were assigned in Start, whichever
        // Start happened to run second would silently skip the first turn's drive regen.
        driveManager = GetComponent<DriveManager>();

        ApplyCharacterArt();
    }

    /// <summary>
    /// Copies <see cref="Character.characterSprite"/> and <see cref="Character.characterTint"/> onto
    /// the body renderer, so one prefab can serve every character. Safe in <c>Awake</c> because
    /// <see cref="EncounterBootstrapper"/> assigns <c>characterData</c> under an inactive staging
    /// parent *before* the object wakes.
    /// </summary>
    /// <remarks>
    /// A null sprite deliberately leaves the renderer's art alone rather than blanking it — an
    /// unassigned Character asset should look wrong in the Inspector, not turn the combatant
    /// invisible mid-fight.
    ///
    /// The tint is written **unconditionally**, white included. That's what makes the asset the
    /// single source of truth: a colour override left on a prefab variant no longer wins. Stop-gap
    /// until characters are animated.
    /// </remarks>
    public void ApplyCharacterArt()
    {
        if (characterData == null) return;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();

        if (bodyRenderer == null)
        {
            if (characterData.characterSprite != null)
            {
                Debug.LogWarning($"[CharacterManager] '{gameObject.name}' has character art but no " +
                                 "SpriteRenderer to put it on — assign Body Renderer, or leave the sprite empty.", this);
            }

            return;
        }

        if (characterData.characterSprite != null)
        {
            bodyRenderer.sprite = characterData.characterSprite;
        }

        bodyRenderer.color = characterData.characterTint;
    }


    void Start()
    {
        actionStatusText.enabled = false;

        if (characterData != null)
        {
            RefreshStats();
            BindToRunState();

            // Named after the run record where there is one, so generated crew keep their rolled
            // name rather than showing the archetype's authored name.
            characterName.text = crewState != null && !string.IsNullOrEmpty(crewState.displayName)
                ? crewState.displayName
                : characterData.characterName;

            UpdateHealthBar();
        }
        else
        {
            Debug.LogError($"Character Data is missing on '{gameObject.name}'!");
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
            // would come back next encounter as though nothing happened. Reported to RunManager
            // rather than written to disk here — the save happens once, at the encounter boundary,
            // in RunManager.CompleteEncounter().
            if (crewState != null)
            {
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.ReportCrewDeath(crewState);
                }
                else
                {
                    // No manager to report to, so there's nothing that could persist this anyway.
                    crewState.isDead = true;
                    crewState.currentHealth = 0f;
                }

                Debug.Log($"[CharacterManager] {crewState.displayName} died — removed from the run's crew for good.");
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
            // Played on a detached object, NOT an AudioSource on this one: the caller destroys this
            // GameObject on the next line, which would kill the clip a few milliseconds in. Delaying
            // the destroy instead is not an option — battle-end polling counts living combatants by
            // tag, so a lingering corpse would read as still alive.
            PlayDetachedOneShot(characterData.deathSoundEffect, transform.position);
            Debug.Log($"Playing death sound from scriptable object for {characterData.characterName}");
        }
        // Fallback to MMF_Player if no AudioClip is set in scriptable object
        else if (deathAudioFeedback != null)
        {
            // Known limitation: this player lives on the dying object, so it has the same problem
            // the branch above solves and will usually be cut off. Assign deathSoundEffect on the
            // Character asset to get a reliable death sound.
            deathAudioFeedback.PlayFeedbacks();
            Debug.Log($"Playing death sound from MMF_Player for {characterData?.characterName ?? gameObject.name} " +
                      "— this is cut short by the object being destroyed; prefer deathSoundEffect on the asset.");
        }
        else
        {
            Debug.LogWarning($"No death sound configured for {characterData?.characterName ?? gameObject.name}");
        }
    }

    /// <summary>
    /// Plays a clip on a throwaway GameObject that outlives the caller, then cleans itself up.
    /// For sounds that fire as their own object is destroyed.
    /// </summary>
    /// <remarks>
    /// Not <c>AudioSource.PlayClipAtPoint</c>, which spawns a 3D source: the camera sits 10 units
    /// off the combat plane, so a positional source would be attenuated by distance for no reason.
    /// This matches the character prefab's own audio, which is 2D (<c>SpatialBlend: 0</c>).
    /// </remarks>
    private static void PlayDetachedOneShot(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        GameObject holder = new GameObject($"~OneShot_{clip.name}");
        holder.transform.position = position;

        AudioSource source = holder.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.Play();

        Destroy(holder, clip.length + 0.1f);
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

        // Preferred path: the spawner already told us exactly which record we are.
        if (crewState == null)
        {
            // Fallback for crew placed in the scene by hand, which have no spawner to bind them.
            // Ambiguous if two crew share one Character asset — spawned crew never take this path.
            if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun) return;

            crewState = RunManager.Instance.GetCrewMember(characterData.Id);

            if (crewState == null)
            {
                Debug.LogWarning($"[CharacterManager] {characterData.characterName} (id '{characterData.Id}') " +
                                 "isn't in the active run's crew — starting at full health.");
                return;
            }
        }

        if (crewState.isDead)
        {
            Debug.LogWarning($"[CharacterManager] {crewState.displayName} is dead in the current run but is in " +
                             "the scene anyway. Hand-placed crew aren't filtered by the run — remove them from " +
                             "the scene and let the bootstrapper spawn the crew instead.");
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
            //
            // BuffType.DamageReduction is absent on purpose — it isn't a stat, so there's nothing
            // here for it to modify. It's read in TakeDamage() at the moment damage lands. See the
            // remarks on the enum member.
            MaxHealth = Mathf.Max(1f, characterData.maxHealth + SumBuffValue(Character.BuffType.Health));
            AttackPower = Mathf.Max(0f, characterData.attackPower + SumBuffValue(Character.BuffType.Accuracy));
            DefenseValue = Mathf.Max(0f, characterData.defenseValue + SumBuffValue(Character.BuffType.Defense));
            Speed = Mathf.Max(0.1f, characterData.speed + SumBuffValue(Character.BuffType.Speed));
            UpdateBuffDisplay();
        }
    }

    // Ceiling on stacked protection. Without it two 50% buffs reach 1.0 and the character is simply
    // immune, which is never a state a turn-based fight should be able to enter. The floor allows a
    // vulnerability debuff to at most double incoming damage.
    private const float MinDamageReduction = -1f;
    private const float MaxDamageReduction = 0.9f;

    /// <summary>
    /// Net protection against incoming damage, as a fraction. 0.25 means take 25% less; a negative
    /// value is a vulnerability and means take more. Clamped so stacked buffs can't reach immunity.
    /// </summary>
    /// <remarks>
    /// Buffs and debuffs of this type sum before clamping, so a +40% protection and a -25%
    /// vulnerability net out to +15% rather than cancelling to nothing — the same additive rule
    /// every other buff type follows.
    /// </remarks>
    public float DamageReductionFraction => Mathf.Clamp(
        SumBuffValue(Character.BuffType.DamageReduction), MinDamageReduction, MaxDamageReduction);

    // The unbuffed crit rule: a natural 20 and nothing else, i.e. 5%.
    private const int BaseCritThreshold = 20;

    // Floor of 11 caps buffed crit chance at 50% — past that "critical" stops meaning anything and
    // damage variance swamps every other dial. 21 is reachable and deliberate: no d20 face satisfies
    // it, so a negative CritChance buff switches crits off entirely rather than wrapping around.
    private const int MinCritThreshold = 11;
    private const int MaxCritThreshold = 21;

    /// <summary>
    /// Lowest d20 roll that crits for this character. 20 unbuffed; each point of
    /// <see cref="Character.BuffType.CritChance"/> lowers it by one, widening the window by 5%.
    /// </summary>
    /// <remarks>
    /// Compare with <c>roll &gt;= CritThreshold</c>. This is separate from the "natural 20 always
    /// hits" rule, which stays pinned at 20 — a buffed 18 can crit, but it still has to beat the
    /// target's defense to land at all. You cannot crit on a miss.
    /// </remarks>
    public int CritThreshold => Mathf.Clamp(
        BaseCritThreshold - Mathf.RoundToInt(SumBuffValue(Character.BuffType.CritChance)),
        MinCritThreshold, MaxCritThreshold);

    /// <summary>Crit chance as a percentage, for tooltips. 5 unbuffed, 0 when crits are switched off.</summary>
    public int CritChancePercent => Mathf.Max(0, (21 - CritThreshold) * 5);

    /// <summary>
    /// Applies protection to an incoming damage figure. Returns what actually gets through.
    /// </summary>
    private float ApplyDamageReduction(float damage)
    {
        float reduction = DamageReductionFraction;

        if (Mathf.Approximately(reduction, 0f) || damage <= 0f) return damage;

        float reduced = damage * (1f - reduction);

        Debug.Log($"{characterData?.characterName ?? gameObject.name} protection {reduction:P0}: " +
                  $"incoming {damage:F1} reduced to {reduced:F1}");

        return reduced;
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

    /// <summary>
    /// Raised whenever this character's buffs change — applied, expired, or refreshed at a turn
    /// boundary. Driven from <see cref="UpdateBuffDisplay"/>, which <see cref="RefreshStats"/>
    /// already calls on every one of those paths, so displays never poll.
    /// </summary>
    public event System.Action BuffsChanged;

    /// <summary>
    /// Net effect of every buff and debuff of one type. Positive is a buff, negative a debuff,
    /// zero means none active — a +2 and a -2 of the same type cancel and read as nothing.
    /// </summary>
    public float GetNetBuffValue(Character.BuffType type)
    {
        return SumBuffValue(type);
    }

    // Tracks whether buffParticles is currently running, so the sustained effect isn't restarted
    // on every buff refresh — UpdateBuffDisplay runs on each turn boundary, not only on change.
    private bool buffParticlesPlaying;

    /// <summary>
    /// Starts or stops the sustained buff particle to match the character's current state.
    ///
    /// Driven from <see cref="UpdateBuffDisplay"/> rather than from the cast site, so it stays
    /// correct however a buff arrives or leaves — applied, expired at a turn boundary, or stripped
    /// by drive's RemoveDebuff. Only positive buffs light it; a debuff shouldn't glow.
    /// </summary>
    private void RefreshBuffParticles()
    {
        if (buffParticles == null) return;

        bool shouldPlay = false;

        foreach (var buff in activeBuffs)
        {
            if (buff.Value > 0f)
            {
                shouldPlay = true;
                break;
            }
        }

        if (shouldPlay == buffParticlesPlaying) return;

        buffParticlesPlaying = shouldPlay;

        if (shouldPlay) buffParticles.Play();
        else buffParticles.Stop();
    }

    /// <summary>Longest remaining duration among buffs of one type, or 0 if none are active.</summary>
    public int GetBuffTurnsRemaining(Character.BuffType type)
    {
        int longest = 0;

        foreach (var buff in activeBuffs)
        {
            if (buff.Type == type && buff.TurnsRemaining > longest) longest = buff.TurnsRemaining;
        }

        return longest;
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

    
    /// <summary>
    /// Applies damage. Returns the health this character <b>actually lost</b>, which is not the
    /// same as <paramref name="damage"/>: it's rounded, and it's capped by whatever health was left.
    /// </summary>
    /// <remarks>
    /// The return value exists for HealthDrain actions, which heal the attacker by a share of what
    /// the target lost. Deriving it from health genuinely removed rather than from the damage roll
    /// is what stops a killing blow on a 3hp target from paying out as though it hit for 20 —
    /// health should move between the two, not be created.
    ///
    /// Every other caller ignores it, which is why widening this from <c>void</c> was safe.
    /// </remarks>
    public float TakeDamage(float damage, bool wasParried = false)
    {
        // First thing, before anything reads `damage`: the floating number, the health loss, the
        // drive gain and the return value must all agree on one figure — what actually got through.
        //
        // Deliberately multiplicative with a parry rather than special-cased. A parried hit arrives
        // here already reduced to its 25% chip, so protection then applies to that; the two are
        // independent mitigations and stacking them is the expected reading.
        //
        // It also intentionally reduces the drive earned from being hit, because the block below
        // scales off this same figure. Protection that cut the damage but paid full drive would be
        // a pure win, and the parry path already sets the precedent of paying drive on the damage
        // actually taken rather than the damage swung.
        damage = ApplyDamageReduction(damage);

        // Only update the status text and play feedback if it wasn't a parry
        if (!wasParried)
        {
            actionStatusText.enabled = true;
            actionStatusText.text = "-" + Mathf.Round(damage);
            dealDamageFeedback.PlayFeedbacks();
            feedbackPlayer.PlayFeedbacks();
        }

        float roundedDamage = Mathf.Round(damage);
        float healthBefore = CurrentHealth;
        CurrentHealth = Mathf.Max(0, CurrentHealth - roundedDamage);
        float healthLost = healthBefore - CurrentHealth;
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

        return healthLost;
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

    /// <summary>
    /// Heals this character. <paramref name="driveMultiplier"/> is the <b>caster's</b> drive-stack
    /// multiplier, supplied by whoever resolved the action.
    /// </summary>
    /// <remarks>
    /// This used to read <c>driveManager.GetNextHealingMultiplier()</c> off <i>this</i> character —
    /// i.e. the heal's <b>target</b>. A healer who committed drive stacks and healed a crewmate
    /// therefore spent the segments for nothing while the target's idle stacks were consumed
    /// instead; self-heals only worked because healer and target were the same object. The caster's
    /// multiplier is not something the receiver can look up, so it has to be passed in.
    /// </remarks>
    public void Heal(float amount, float driveMultiplier = 1f)
    {
        if (driveMultiplier > 1f)
        {
            amount *= driveMultiplier;
            Debug.Log($"Drive stacks boosted healing on {characterData?.characterName ?? gameObject.name} " +
                      $"to {amount} (multiplier: {driveMultiplier}x)");
        }


        actionStatusText.enabled = true;
        actionStatusText.text = "+" + Mathf.Round(amount);
        healFeedback.PlayFeedbacks();
        
        float roundedHeal = Mathf.Round(amount);
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + roundedHeal);
        UpdateHealthBar();
        SyncHealthToRunState();
    }

    /// <summary>
    /// Adds raw drive to this character's meter, from a <c>DriveGrant</c> action cast on them.
    /// Returns how much actually landed — 0 if their meter was already full.
    /// </summary>
    /// <remarks>
    /// The caster's own drive stacks deliberately do <b>not</b> multiply this. Letting a drive
    /// spend produce more drive is a feedback loop, and it's the same reasoning that keeps
    /// accuracy buffs out of damage: two multipliers that feed each other stop being tunable.
    /// </remarks>
    public float GainDrive(float amount)
    {
        if (driveManager == null)
        {
            Debug.LogWarning($"[CharacterManager] {characterData?.characterName ?? gameObject.name} has no " +
                             "DriveManager, so a drive grant did nothing.", this);
            return 0f;
        }

        float granted = driveManager.GrantDrive(amount);

        ShowDriveStatus($"+{Mathf.Round(granted)} Drive", driveGrantedFeedback);

        return granted;
    }

    /// <summary>
    /// Strips raw drive from this character's meter, from a <c>DriveDrain</c> action cast on them.
    /// Returns how much was actually removed, which is less than asked for on a near-empty meter.
    /// </summary>
    /// <remarks>
    /// Reports the real number rather than the authored one, so draining an already-empty enemy
    /// reads as the wasted turn it was instead of implying an effect that didn't happen.
    /// </remarks>
    public float LoseDrive(float amount)
    {
        if (driveManager == null)
        {
            Debug.LogWarning($"[CharacterManager] {characterData?.characterName ?? gameObject.name} has no " +
                             "DriveManager, so a drive drain did nothing.", this);
            return 0f;
        }

        float removed = driveManager.DrainDrive(amount);

        ShowDriveStatus($"-{Mathf.Round(removed)} Drive", driveDrainedFeedback);

        return removed;
    }

    /// <summary>
    /// Writes a drive number to the floating status text and plays its feedback.
    /// </summary>
    /// <remarks>
    /// Every other status number (damage, heal, miss) is paired with an MMF_Player that clears the
    /// text again on its way out. These two slots are new and start unassigned on the prefab, so
    /// without the timed fallback the first drive action would leave a number stuck on the
    /// character forever. Wire the feedback and the fallback never runs.
    /// </remarks>
    private void ShowDriveStatus(string text, MMF_Player feedback)
    {
        actionStatusText.enabled = true;
        actionStatusText.text = text;

        if (feedback != null)
        {
            feedback.PlayFeedbacks();
            return;
        }

        // Deliberately not driveParticles: DriveManager owns that one as a sustained effect it
        // starts per stack and stops in ClearStacks(), so playing it here would leave it running.
        CancelInvoke(nameof(HideHealthUI));
        Invoke(nameof(HideHealthUI), driveStatusTextDuration);
    }

    public void AddBuff(Character.BuffType type, float amount, float duration)
    {
        // Convert float duration to turns (assuming 1 duration = 1 turn)
        int turns = Mathf.RoundToInt(duration);
        activeBuffs.Add(new Character.ActiveBuff(type, amount, turns));

        // One-shot cast burst, mirroring DriveManager's driveFeedbacks. Buffs only — a debuff
        // landing shouldn't play the "you got stronger" flourish. The sustained particle is not
        // started here: RefreshStats below reaches UpdateBuffDisplay, which owns that state.
        if (amount > 0f && buffFeedback != null)
        {
            buffFeedback.PlayFeedbacks();
        }
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

    /// <summary>
    /// Tells buff displays this character's buffs changed. Called from <see cref="RefreshStats"/>,
    /// which runs on every path that adds, expires or strips one — so displays never poll.
    /// </summary>
    /// <remarks>
    /// This used to write a text label listing each buff and its remaining turns. That was removed
    /// in favour of <see cref="BuffIconDisplay"/>, which shows the same information as icons and
    /// can carry the turn count next to each one via its optional turnsText.
    /// Fully qualified System.Action — a bare Action is the game's own ability ScriptableObject.
    /// </remarks>
    public void UpdateBuffDisplay()
    {
        BuffsChanged?.Invoke();
        RefreshBuffParticles();
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