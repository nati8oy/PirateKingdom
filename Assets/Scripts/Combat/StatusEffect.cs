using UnityEngine;

// NOTE: no `using System;` — a bare `Action` here would collide with the game's own ability
// ScriptableObject. Same rule as the Meta/ and Settings/ files.

/// <summary>
/// The kinds of lasting effect an action can leave on a character.
/// </summary>
/// <remarks>
/// <b>Append-only.</b> Unity serializes enums by integer value, so reordering silently repoints every
/// authored rider at a different effect. Same rule as <see cref="Character.BuffType"/>,
/// <see cref="Action.ActionType"/> and <see cref="Enemy.TargetingStrategy"/>.
///
/// <para>The damage-over-time kinds are <b>mechanically identical</b> and differ only in
/// presentation — poison and fire are bleed with another icon and colour. That's deliberate: adding
/// a kind is an enum member plus Editor wiring, not a system. If one ever needs genuinely different
/// mechanics, branch on the kind then rather than designing for it now.</para>
/// </remarks>
public enum StatusEffectKind
{
    Bleed,
    Poison,
    Burn,

    /// <summary>
    /// Skips the affected character's turn. <b>Inert until Phase D2</b> — the substrate carries it so
    /// the enum doesn't shift later, but nothing reads it yet and applying one currently does
    /// nothing but tick down. <see cref="CharacterManager.TryApplyStatus"/> warns if you author one.
    /// </summary>
    Stun
}

/// <summary>
/// Where a hit came from. Decides which rules apply to it on the way in.
/// </summary>
/// <remarks>
/// An enum rather than another bool on <c>TakeDamage</c> — <c>TakeDamage(5f, false, true)</c> is
/// unreadable, and this distinction is likely to grow more rules.
/// </remarks>
public enum DamageSource
{
    /// <summary>A landed attack. Reduced by protection, and earns drive.</summary>
    Direct,

    /// <summary>
    /// A damage-over-time tick. **Ignores protection entirely and earns no drive.** Armour stops
    /// blades, not poison — so DoT is the designated counter to a heavy-protection build. And drive
    /// is paid on the initial hit alone, so an attack that deals 0 and only applies a bleed
    /// generates no drive at all; that is the authored trade-off for it.
    /// </summary>
    OverTime
}

/// <summary>
/// A live effect on a character. Runtime state — this is the mutable counterpart to the authored
/// <see cref="StatusApplication"/>, exactly as <c>CharacterManager</c> is to <c>Character</c>.
/// </summary>
[System.Serializable]
public class StatusEffect
{
    [SerializeField] private StatusEffectKind kind;
    [SerializeField] private float damagePerTurn;
    [SerializeField] private int turnsRemaining;

    public StatusEffectKind Kind => kind;
    public float DamagePerTurn => damagePerTurn;
    public int TurnsRemaining => turnsRemaining;

    public StatusEffect(StatusEffectKind kind, float damagePerTurn, int turns)
    {
        this.kind = kind;
        this.damagePerTurn = Mathf.Max(0f, damagePerTurn);
        this.turnsRemaining = Mathf.Max(1, turns);
    }

    /// <summary>
    /// Re-application of the same kind. Takes the <b>higher</b> of each value rather than adding —
    /// effects refresh, they never stack.
    /// </summary>
    /// <remarks>
    /// Max on duration as well as damage, so a weak late application can't cut short a strong one
    /// that still had turns to run. "Refresh" that shortened an effect would be a nasty surprise.
    /// </remarks>
    public void Refresh(float newDamagePerTurn, int newTurns)
    {
        damagePerTurn = Mathf.Max(damagePerTurn, Mathf.Max(0f, newDamagePerTurn));
        turnsRemaining = Mathf.Max(turnsRemaining, Mathf.Max(1, newTurns));
    }

    public void ReduceTurns() => turnsRemaining--;

    public bool IsExpired => turnsRemaining <= 0;

    /// <summary>True for the kinds that deal recurring damage, i.e. anything but pure control.</summary>
    public bool IsDamageOverTime => damagePerTurn > 0f;
}

/// <summary>
/// Shared presentation for status effects, so the floating damage number, the icons and the
/// tooltips all describe an effect the same way.
/// </summary>
/// <remarks>
/// Colours are returned as TMP rich-text hex rather than applied to a component's <c>color</c>.
/// <c>actionStatusText</c> is shared with the normal damage and heal numbers, so tinting the
/// component would leave the next "-5" still coloured for bleed. An inline tag can't leak.
/// </remarks>
public static class StatusEffectStyle
{
    /// <summary>
    /// The one definition of each kind's colour. Everything else derives from it, so the floating
    /// damage number and the status icon can never drift apart.
    /// </summary>
    public static Color Tint(StatusEffectKind kind)
    {
        return kind switch
        {
            StatusEffectKind.Bleed => new Color(0.878f, 0.278f, 0.235f),
            StatusEffectKind.Poison => new Color(0.435f, 0.749f, 0.278f),
            StatusEffectKind.Burn => new Color(0.941f, 0.549f, 0.157f),
            StatusEffectKind.Stun => new Color(0.910f, 0.831f, 0.302f),
            _ => Color.white
        };
    }

    /// <summary>The same colour as a TMP rich-text hex, e.g. "#e0473c".</summary>
    public static string HexColour(StatusEffectKind kind)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(Tint(kind));
    }

    /// <summary>"Bleed", and the colour it should read in.</summary>
    public static string Rich(StatusEffectKind kind)
    {
        return $"<color={HexColour(kind)}>{kind}</color>";
    }
}

/// <summary>
/// A character's authored resistance to one effect kind. Reduces the CHANCE of being afflicted —
/// never the damage or the duration.
/// </summary>
[System.Serializable]
public class StatusResistance
{
    public StatusEffectKind kind;

    [Tooltip("0 = no resistance, 1 = immune. Subtracted from the effect's Chance To Apply.")]
    [Range(0f, 1f)]
    public float value;
}

/// <summary>
/// An effect an <see cref="Action"/> tries to apply on a landed hit. <b>Authored data — never mutated
/// at runtime</b>; the live copy is a <see cref="StatusEffect"/>.
/// </summary>
[System.Serializable]
public class StatusApplication
{
    [Tooltip("Which effect to apply. Bleed, Poison and Burn are identical damage-over-time and differ " +
             "only in presentation. Stun is inert until Phase D2.")]
    public StatusEffectKind kind = StatusEffectKind.Bleed;

    [Tooltip("Damage dealt at the start of each of the victim's turns. Leave at 0 for a control " +
             "effect like Stun.\n\n" +
             "This damage IGNORES the target's Damage Reduction and grants them NO drive. It is also " +
             "capped: the total from all effects can't exceed a fraction of the victim's max health " +
             "per turn (see CharacterManager.MaxDoTFractionPerTurn).\n\n" +
             "Rounded like every other damage number, so values under 1 vanish entirely.")]
    [Min(0f)]
    public float damagePerTurn = 2f;

    [Tooltip("How many of the victim's turns it lasts. The first tick lands at the start of their " +
             "next turn.")]
    [Min(1)]
    public int turns = 2;

    [Tooltip("Base chance before resistance. The target's resistance for this kind is SUBTRACTED " +
             "from it, so 1.0 against a 40%-resistant target lands 60% of the time.\n\n" +
             "Only rolled when the attack itself lands — a miss applies nothing, and a successful " +
             "parry prevents it too.")]
    [Range(0f, 1f)]
    public float chanceToApply = 1f;
}
