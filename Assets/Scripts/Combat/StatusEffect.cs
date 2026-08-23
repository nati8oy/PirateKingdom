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
    /// Skips the affected character's turn, and ramps their stun resistance each time so they can't
    /// be chained indefinitely.
    /// </summary>
    /// <remarks>
    /// <b>Turn-scoped, unlike every kind above.</b> The damage kinds tick and expire together at the
    /// top of the round; a stun is consumed by the affected character's own turn when it skips one.
    /// See <see cref="StatusEffect.IsTurnScoped"/> — round-boundary expiry would burn a 1-turn stun
    /// before its owner ever reached a turn to lose.
    /// </remarks>
    Stun,

    /// <summary>
    /// Hidden. Cannot be targeted by a hostile action while it lasts; friendly ones still reach them.
    /// </summary>
    /// <remarks>
    /// <b>The first BENEFICIAL kind</b>, which is why <see cref="StatusEffectRules.IsBeneficial"/>
    /// exists — a friendly effect must not be stopped by the target's own resistance.
    ///
    /// Also the first kind that changes what is *legal*, rather than dealing damage or skipping a
    /// turn. It's enforced in <c>CombatController.IsValidTarget</c> and
    /// <c>EnemyManager.SelectTarget</c>, which are the two places that decide who can be hit.
    /// </remarks>
    Stealth
}

/// <summary>
/// Rules about a kind that aren't presentation and aren't per-instance.
/// </summary>
public static class StatusEffectRules
{
    /// <summary>
    /// Whether this kind helps its bearer. Beneficial kinds skip the resistance roll — being good at
    /// shrugging off poison should not make an ally's stealth harder to cast on you.
    /// </summary>
    public static bool IsBeneficial(StatusEffectKind kind) => kind == StatusEffectKind.Stealth;

    /// <summary>
    /// Whether this kind stops its bearer being targeted by hostile actions.
    /// </summary>
    public static bool HidesFromHostileActions(StatusEffectKind kind) => kind == StatusEffectKind.Stealth;
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
    [SerializeField] private Sprite icon;

    public StatusEffectKind Kind => kind;
    public float DamagePerTurn => damagePerTurn;
    public int TurnsRemaining => turnsRemaining;

    /// <summary>
    /// Icon the applying action supplied, or null to let the display use its own per-kind sprite.
    /// </summary>
    /// <remarks>
    /// Carried per instance so two actions of the same kind can look different — a "Vanish" and a
    /// "Smoke Bomb" both apply Stealth but needn't share an icon.
    /// </remarks>
    public Sprite Icon => icon;

    public StatusEffect(StatusEffectKind kind, float damagePerTurn, int turns, Sprite icon = null)
    {
        this.kind = kind;
        this.damagePerTurn = Mathf.Max(0f, damagePerTurn);
        this.turnsRemaining = Mathf.Max(1, turns);
        this.icon = icon;
    }

    /// <summary>
    /// Re-application of the same kind. Takes the <b>higher</b> of each value rather than adding —
    /// effects refresh, they never stack.
    /// </summary>
    /// <remarks>
    /// Max on duration as well as damage, so a weak late application can't cut short a strong one
    /// that still had turns to run. "Refresh" that shortened an effect would be a nasty surprise.
    /// </remarks>
    public void Refresh(float newDamagePerTurn, int newTurns, Sprite newIcon = null)
    {
        damagePerTurn = Mathf.Max(damagePerTurn, Mathf.Max(0f, newDamagePerTurn));
        turnsRemaining = Mathf.Max(turnsRemaining, Mathf.Max(1, newTurns));

        // The most recent application wins the icon, unlike the values, which take the max. There's
        // no "higher" of two sprites, and showing what just landed is the less surprising of the two.
        if (newIcon != null) icon = newIcon;
    }

    public void ReduceTurns() => turnsRemaining--;

    public bool IsExpired => turnsRemaining <= 0;

    /// <summary>True for the kinds that deal recurring damage, i.e. anything but pure control.</summary>
    public bool IsDamageOverTime => damagePerTurn > 0f;

    /// <summary>
    /// True for effects measured in the BEARER'S OWN TURNS rather than in rounds. Consumed when one
    /// of their turns happens, not by the round tick.
    /// </summary>
    /// <remarks>
    /// Stun and Stealth. Both are applied part-way through a round, so expiring them on the round
    /// boundary measures something initiative-dependent rather than a turn:
    /// <list type="bullet">
    /// <item>A 1-turn <b>stun</b> would be gone before its owner ever reached a turn to lose.</item>
    /// <item>A 1-turn <b>stealth</b> would cover only the remainder of the current round — all of it
    /// if the caster acted first, and <i>nothing at all</i> if they acted last.</item>
    /// </list>
    /// <c>AdvanceStatuses()</c> leaves these alone. <c>ConsumeStunForSkippedTurn()</c> and
    /// <c>ConsumeStealthForCompletedTurn()</c> spend them at the turn that measures them.
    /// </remarks>
    public bool IsTurnScoped => kind == StatusEffectKind.Stun || kind == StatusEffectKind.Stealth;

    [SerializeField] private bool skipNextTurnConsumption;

    /// <summary>
    /// True while this effect should survive one turn-completion untouched.
    /// </summary>
    /// <remarks>
    /// <b>For an effect applied to a character during their OWN turn</b> — a self-cast stealth. That
    /// turn is already underway, so counting it would consume the effect at the end of the very turn
    /// it was cast in: applied and gone inside one frame, which is exactly what happened.
    ///
    /// Only granted when the bearer is the acting character, so an ally-cast stealth is unaffected
    /// and both routes end up protecting for about one round.
    /// </remarks>
    public bool SkipNextTurnConsumption => skipNextTurnConsumption;

    public void GrantTurnConsumptionGrace() => skipNextTurnConsumption = true;

    public void SpendTurnConsumptionGrace() => skipNextTurnConsumption = false;
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
            StatusEffectKind.Stealth => new Color(0.545f, 0.510f, 0.780f),
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
/// A class's resistance to one effect kind. <b>The only thing that can stop an effect landing</b> —
/// actions carry no chance of their own.
/// </summary>
/// <remarks>
/// Reduces the CHANCE of being afflicted, never the damage or the duration. So the outcome is binary:
/// the full effect, or nothing.
/// </remarks>
[System.Serializable]
public class StatusResistance
{
    public StatusEffectKind kind;

    [Tooltip("Chance to shrug the effect off entirely. The effect lands (1 - this) of the time, so " +
             "0.4 means it lands 60% of the time. 1 is genuine immunity.\n\n" +
             "BAND THIS BOLDLY. Below about 0.3 it is barely felt — at 0.2, across a fight with three " +
             "applications, it stops nothing at all about half the time. Use 0.4-0.6 for a class that " +
             "is MEANT to resist something, and 0.1-0.2 only for incidental resistance.\n\n" +
             "Reduces the chance only — never the damage or duration.")]
    [Range(0f, 1f)]
    public float value;
}

/// <summary>
/// An effect an <see cref="Action"/> applies on a landed hit.
/// </summary>
/// <remarks>
/// <b>It always applies unless the target resists it.</b> There is no per-action chance — the
/// target's <see cref="StatusResistance"/> is the only thing that can stop one. So two bleed actions
/// differ by damage and duration alone, and those ranges have to carry all the differentiation
/// between them.
///
/// <b>Authored data, never mutated at runtime</b>; the live copy is a <see cref="StatusEffect"/>.
/// </remarks>
[System.Serializable]
public class StatusApplication
{
    [Tooltip("Which effect to apply. Bleed, Poison and Burn are identical damage-over-time and differ " +
             "only in presentation.\n\n" +
             "Stun is different: leave Damage Per Turn at 0 and set Turns to how many of the " +
             "target's turns to skip. Each skip ramps their stun resistance by 50% until they " +
             "actually get a turn, so chaining one gets rapidly harder.\n\n" +
             "Stealth is BENEFICIAL — damage 0, and it ignores resistance entirely, since an " +
             "ally shouldn't be able to shrug off a friendly effect. Author it on an Apply " +
             "Status action aimed at Single Ally.\n\n" +
             "Turns for Stun and Stealth count the BEARER'S OWN TURNS, not rounds. Stealth 1 means " +
             "'hidden until the end of your next turn' — so it always covers a full round of enemy " +
             "turns regardless of initiative.")]
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

    [Tooltip("Icon shown on the afflicted character while this effect is on them.\n\n" +
             "Optional. Left empty, the Status Icon Display falls back to whatever sprite that slot " +
             "was set up with on the prefab — so a per-kind icon still works and this is only needed " +
             "when one action wants its own.\n\n" +
             "The character needs a matching slot for this kind on its Status Icon Display, or there " +
             "is nowhere to draw it.")]
    public Sprite icon;
}
