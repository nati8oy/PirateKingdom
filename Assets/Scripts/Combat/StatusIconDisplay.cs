using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// NOTE: no `using System;` — it would make `System.Action` collide with the game's own `Action`
// ScriptableObject.

/// <summary>
/// Shows an icon per active status effect on a combatant — bleed, poison, burn. Event-driven off
/// <see cref="CharacterManager.StatusesChanged"/>, so it never polls and updates the frame an effect
/// lands or expires.
///
/// Put it on the combatant root, with the icon Images under its world-space <c>CharacterUI</c>
/// canvas. Each entry is one fixed slot rather than pooled instances — effects never stack, so there
/// can only ever be one row per kind.
/// </summary>
/// <remarks>
/// Deliberately a sibling of <see cref="BuffIconDisplay"/> rather than an extension of it: buffs are
/// keyed by <c>Character.BuffType</c> and show a signed magnitude, statuses are keyed by
/// <see cref="StatusEffectKind"/> and show damage per turn. Merging them would mean one component
/// switching on which kind of thing it was displaying.
/// </remarks>
public class StatusIconDisplay : MonoBehaviour
{
    /// <summary>One status kind's icon slot.</summary>
    [System.Serializable]
    public class StatusIconSlot
    {
        public StatusEffectKind kind;

        [Tooltip("Image toggled on and off. Its GameObject is what gets shown/hidden, so put each icon on its own object.")]
        public Image icon;

        [Tooltip("Fallback sprite for this kind, used when the applying action didn't supply one of " +
                 "its own.\n\n" +
                 "An action's Status Effect entry can carry an Icon, and that wins where set — so this " +
                 "is the per-kind default. Leave both empty to keep whatever the Image already has.")]
        public Sprite sprite;

        [Tooltip("Tint the icon with this kind's standard colour — the same one the floating damage " +
                 "number uses, so a red '-3 Bleed' and a red bleed icon can't drift apart.\n\n" +
                 "Turn off to use the Custom Tint below instead, or to leave the Image's own colour alone " +
                 "when the artwork is already coloured.")]
        public bool useKindColour = true;

        [Tooltip("Used only when Use Kind Colour is off.")]
        public Color customTint = Color.white;

        [Tooltip("Optional. Filled with the turns remaining.")]
        public TMP_Text turnsText;

        [Tooltip("Optional. Filled with the damage this effect deals per turn, and blanked for the " +
                 "kinds that deal none — stun and stealth.")]
        public TMP_Text damageText;
    }

    [Header("Icons")]
    [Tooltip("One entry per status kind you want shown. Kinds with no entry are simply not displayed.")]
    [SerializeField] private List<StatusIconSlot> iconSlots = new List<StatusIconSlot>();

    [Header("Formatting")]
    [Tooltip("Format for the turns label. {0} is turns remaining.")]
    [SerializeField] private string turnsFormat = "{0}";

    [Tooltip("Format for the damage label. {0} is damage per turn.")]
    [SerializeField] private string damageFormat = "{0}";

    private CharacterManager characterManager;

    private void Awake()
    {
        characterManager = GetComponentInParent<CharacterManager>();

        if (characterManager == null)
        {
            Debug.LogError($"[StatusIconDisplay] No CharacterManager on or above {gameObject.name} — icons disabled.", this);
            enabled = false;
            return;
        }

        HideAll();
    }

    private void OnEnable()
    {
        if (characterManager == null) return;

        characterManager.StatusesChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (characterManager != null) characterManager.StatusesChanged -= Refresh;
    }

    /// <summary>Rebuilds every slot from the character's current statuses.</summary>
    public void Refresh()
    {
        foreach (StatusIconSlot slot in iconSlots)
        {
            if (slot == null || slot.icon == null) continue;

            StatusEffect active = FindActive(slot.kind);

            if (active == null)
            {
                slot.icon.gameObject.SetActive(false);
                continue;
            }

            slot.icon.gameObject.SetActive(true);

            // The applying action's icon wins where it supplied one, so two actions of the same kind
            // can look different. Falling back to the slot's own sprite keeps per-kind icons working
            // with nothing authored on the action.
            Sprite sprite = active.Icon != null ? active.Icon : slot.sprite;
            if (sprite != null) slot.icon.sprite = sprite;

            slot.icon.color = slot.useKindColour ? StatusEffectStyle.Tint(slot.kind) : slot.customTint;

            if (slot.turnsText != null)
            {
                slot.turnsText.text = string.Format(turnsFormat, active.TurnsRemaining);
            }

            if (slot.damageText != null)
            {
                // Blanked for effects that deal no damage — stun and stealth would otherwise show a
                // meaningless "0" beside their icon.
                slot.damageText.text = active.IsDamageOverTime
                    ? string.Format(damageFormat, Mathf.Round(active.DamagePerTurn))
                    : string.Empty;
            }
        }
    }

    private StatusEffect FindActive(StatusEffectKind kind)
    {
        foreach (StatusEffect status in characterManager.ActiveStatuses)
        {
            if (status.Kind == kind) return status;
        }

        return null;
    }

    private void HideAll()
    {
        foreach (StatusIconSlot slot in iconSlots)
        {
            if (slot?.icon != null) slot.icon.gameObject.SetActive(false);
        }
    }
}
