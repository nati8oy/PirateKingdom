using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// NOTE: deliberately no `using System;` — it would make `System.Action` collide with the game's
// own `Action` ScriptableObject.

/// <summary>
/// Shows an icon per active buff type on a combatant — attack, defense, speed (and health, if you
/// ever author one). Event-driven off <see cref="CharacterManager.BuffsChanged"/>, so it never
/// polls and updates the frame a buff is applied or expires.
///
/// Put it on the combatant root, with the icon Images under its world-space <c>CharacterUI</c>
/// canvas. Each entry is one fixed slot rather than pooled instances — there are only four buff
/// types and they can't stack into more rows than that.
/// </summary>
public class BuffIconDisplay : MonoBehaviour
{
    /// <summary>One buff type's icon slot.</summary>
    [System.Serializable]
    public class BuffIconSlot
    {
        public Character.BuffType buffType;

        [Tooltip("Image toggled on and off. Its GameObject is what gets shown/hidden, so put each icon on its own object.")]
        public Image icon;

        [Tooltip("Shown while the net effect is positive.")]
        public Sprite buffSprite;

        [Tooltip("Shown while the net effect is negative. Leave empty to reuse the buff sprite tinted with Debuff Tint.")]
        public Sprite debuffSprite;

        [Tooltip("Optional. Filled with the turns remaining.")]
        public TMP_Text turnsText;
    }

    [Header("Icons")]
    [Tooltip("One entry per buff type you want shown. Types with no entry are simply not displayed.")]
    [SerializeField] private List<BuffIconSlot> iconSlots = new List<BuffIconSlot>();

    [Header("Appearance")]
    [Tooltip("Tint applied when a type is net-negative and no dedicated debuff sprite is set.")]
    [SerializeField] private Color debuffTint = new Color(1f, 0.45f, 0.45f, 1f);

    [Tooltip("Tint applied when a type is net-positive.")]
    [SerializeField] private Color buffTint = Color.white;

    [Tooltip("Format for the turns label. {0} is turns remaining.")]
    [SerializeField] private string turnsFormat = "{0}";

    private CharacterManager characterManager;

    private void Awake()
    {
        characterManager = GetComponentInParent<CharacterManager>();

        if (characterManager == null)
        {
            Debug.LogError($"[BuffIconDisplay] No CharacterManager on or above {gameObject.name} — icons disabled.", this);
            enabled = false;
            return;
        }

        HideAll();
    }

    private void OnEnable()
    {
        if (characterManager == null) return;

        characterManager.BuffsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (characterManager != null) characterManager.BuffsChanged -= Refresh;
    }

    /// <summary>Rebuilds every slot from the character's current buffs.</summary>
    public void Refresh()
    {
        foreach (BuffIconSlot slot in iconSlots)
        {
            if (slot == null || slot.icon == null) continue;

            float net = characterManager.GetNetBuffValue(slot.buffType);

            // Net zero means nothing to show — a +2 and a -2 of the same type genuinely cancel,
            // so displaying both would misrepresent the character's stats.
            if (Mathf.Approximately(net, 0f))
            {
                slot.icon.gameObject.SetActive(false);
                continue;
            }

            bool isBuff = net > 0f;

            slot.icon.gameObject.SetActive(true);
            slot.icon.sprite = isBuff || slot.debuffSprite == null ? slot.buffSprite : slot.debuffSprite;
            slot.icon.color = isBuff ? buffTint : debuffTint;

            if (slot.turnsText != null)
            {
                slot.turnsText.text = string.Format(turnsFormat, characterManager.GetBuffTurnsRemaining(slot.buffType));
            }
        }
    }

    private void HideAll()
    {
        foreach (BuffIconSlot slot in iconSlots)
        {
            if (slot?.icon != null) slot.icon.gameObject.SetActive(false);
        }
    }
}
