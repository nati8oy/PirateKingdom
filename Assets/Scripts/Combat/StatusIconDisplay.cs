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

        [Tooltip("Optional. Sprite to show for this kind. Leave empty to keep whatever the Image already has, " +
                 "which is what you want if each icon object already has its own artwork.")]
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

        [Tooltip("Optional. Filled with the damage this effect deals per turn.")]
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

            if (slot.sprite != null) slot.icon.sprite = slot.sprite;
            slot.icon.color = slot.useKindColour ? StatusEffectStyle.Tint(slot.kind) : slot.customTint;

            if (slot.turnsText != null)
            {
                slot.turnsText.text = string.Format(turnsFormat, active.TurnsRemaining);
            }

            if (slot.damageText != null)
            {
                // Rounded to match what TakeDamage will actually apply, so the icon and the floating
                // number can't disagree.
                slot.damageText.text = string.Format(damageFormat, Mathf.Round(active.DamagePerTurn));
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
