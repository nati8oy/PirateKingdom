# TODO / Known Issues

Running backlog of unresolved issues found while working in this repo. Newest investigations at
the top of each section. See `CLAUDE.md` for how the systems actually work.

---

## Outstanding right now

### Parry drive retune — Inspector values not yet applied
The code side is done (`Character.cs` default is now `5f`), but **existing assets keep their
serialized values**, so these must be set by hand in the Inspector:

| Asset | `parryBonusDriveMultiplier` | From → To |
|---|---|---|
| `Assets/Scripts/Characters/Tephi.asset` | was the untouched `40f` default | 40 → **5** |
| `Assets/Scripts/Characters/Black Sam.asset` | 2× over target | 10 → **5** |
| `Assets/Scripts/Characters/Kolo Aka.asset` | offsets his `damageTakenDriveMultiplier` of 10 | 5 → **2.5** |
| `Assets/Scripts/Characters/Biku Bale.asset` | already correct | 5 → no change |

Target: a clean parry pays ~25 drive (a quarter segment) for a typical ~5-damage hit.

---

## Tuning & game feel

Notes on values that are *correct in code* but read as broken in play. Worth checking before
debugging a system that "doesn't work".

### A low `buffNextActionMultiplier` reads as a broken drive system — resolved for Black Sam
Black Sam was authored at **1.25** against **2.0** on Kolo Aka, Biku Bale, Skeleton and Skeleton
Elite. Since `baseBonus = buffNextActionMultiplier - 1`, that gave him **1.25×** on the first stack
where a Skeleton got **2.0×**, and stack 2 took him only to 1.375× (capping at 1.47× on four stacks
against the roster's 1.9375×). Raised in the Inspector, which fixed the feel.

**The generalizable trap:** drive multipliers are applied to small integer damage ranges and then
put through `Mathf.Round` in `TakeDamage`. On Black Sam's loadout — Stab 2–4, Slash 3–7 — a 1.25×
multiplier frequently moved the damage number by **0 or 1**. So committing a whole drive segment,
which is an expensive and deliberate player choice, produced no visible feedback. The system was
working perfectly; the reward was rounding away.

Consequences worth keeping in mind:

- **Any `buffNextActionMultiplier` below ~1.5 is invisible at current damage scales.** Tephi is
  still at 1.5, which is the weakest case left on the roster.
- **Diminishing returns compress this further.** Stacks 3 and 4 add `baseBonus·d²` and
  `baseBonus·d³` — at `decayRate` 0.5 and a low base bonus those are fractions of a point, i.e.
  segments spent for literally nothing after rounding.
- **A spender needs to be legible at the smallest damage roll it can apply to**, not just on
  average. Sanity-check new drive tuning against `minDamage`, not the midpoint.
- The same rounding applies to heals (`Heal` rounds too), so low heal ranges have the same problem.

---

## Bugs

### ~~Player could act during the enemy's turn~~ — DONE
`ActionsManager.LoadCharacterActions()` is called for enemies too, and buttons were enabled purely
on cooldown (`button.interactable = isAvailable`), so the **enemy's own actions were clickable
during their turn** — pick one, click a target, and it resolved.

Fixed in three layers, all needed:
- `ActionsManager` gates `interactable` on the loaded character's allegiance, **including the End
  Turn button** (otherwise the player could skip the enemy's turn). The panel still *shows* enemy
  actions, deliberately, so the player can read what's coming.
- `CombatController.IsPlayerControlledTurn()` guards `SelectAction()` and
  `TryExecuteActionOnCurrentTarget()`. UI state alone can't close a click that lands on the frame
  the turn flips.
- `TurnManager.SetCharacterTurn()` calls `CombatController.ClearSelection()` on every turn change.
  This closed a second, quieter version: an action selected but never targeted stayed live, so the
  next character to act could resolve an action that wasn't theirs — `PerformAction` attributes it
  to `currentCharacterTurn`, so a crew member could use an action outside their own loadout.

### ~~Drive stacks don't boost heals cast on an ally~~ — DONE
`CharacterManager.Heal()` applied `driveManager.GetNextHealingMultiplier()` using **its own**
`driveManager` field — i.e. the **target's** DriveManager, not the healer's. So a player healer who
committed drive stacks and healed a crewmate spent the segments for nothing while the target's idle
stacks were consumed instead; self-heals only worked by coincidence (healer and target are the same
object). Enemies were unaffected, because `EnemyManager`'s heal branch multiplied explicitly with
the healer's own manager — which is why drive visibly worked for enemies and not for the player.

Now `Heal(float amount, float driveMultiplier = 1f)` takes the **caster's** multiplier, supplied by
whoever resolves the action (`CombatController`, `EnemyManager`, `DriveManager.ExecuteHealSelf`).
The receiving `CharacterManager` no longer looks one up — it can't, the caster's stacks aren't its
to read. `TooltipUI.ShowTargetTooltip()` already read the attacker's multiplier, so the preview and
the result now agree.

### ~~Cooldowns tick twice for whoever ends a round~~ — DONE (Phase 1a)
The redundant `UpdateActionCooldowns()` call in `TurnManager.RoundComplete()` is gone; the
per-turn call in `CharacterManager.OnTurnComplete()` is the only one.

### ~~Enemy action cooldowns never started~~ — DONE (Phase 1a)
`EnemyManager.ChooseBestAction()` has always filtered on `IsActionAvailable`, but nothing ever
marked an enemy action as *used*, so enemy cooldowns never began — every action was permanently
available. `EnemyManager` also carried a second, name-keyed cooldown dictionary with its own
`UseAction` / `IsActionAvailable` / `UpdateActionCooldowns` that no caller ever touched.

That duplicate system is deleted, and enemies now call `CharacterManager.UseAction()` on the same
path as the player. Masked until now because no enemy action has a `cooldown` above 0.

### `EnemyManager.Start()` initializes the drive manager off a stale field
`EnemyManager.cs:75-84` calls `enemyDriveManager.Initialize(enemyData.driveConfig)` *before*
`enemyData` is reassigned from `GetComponent<CharacterManager>().characterData`. It reads the
serialized `enemyData` Inspector field, which for scene enemies is generally null — so the branch
is skipped and the real initialization is the one already done in `Awake()` via
`InitializeEnemyDriveManager()`.

Harmless today, but it silently applies the wrong `driveConfig` the moment anyone populates that
Inspector field. It also dereferences `enemyDriveManager` with no null check.

**Fix direction:** reorder so `enemyData` is resolved from `CharacterManager` first, or drop the
redundant `Start()` init entirely and rely on `Awake()`.

### ~~Duplicate scene instances share ScriptableObject state~~ — DONE (Phase 1a)
`activeBuffs` and `actionCooldowns` now live on the runtime `CharacterManager`, so two scene
objects sharing a `Character` asset (both Skeletons reference `Skeleton.asset`) no longer share
buffs, cooldowns, or double-tick their durations. `Character` is authored data only — **do not
reintroduce mutable fields on it**, both for this reason and because Editor play sessions write
ScriptableObject changes into the project asset on disk.

`ActionsManager.IsActionAvailable()` and `LoadCharacterActions()` now take a `CharacterManager`
rather than a `Character`, as do `TooltipUI.ShowActionTooltipWithCharacter()` and
`ActionButtonHover.SetActionWithCharacter()`.

Still on the ScriptableObject: `reputation`. It's authored, zeroed by `Initialize()`, and never
read — see the "what is `reputation` for?" open question in `META.md`. It should either move with
the rest or be deleted once it has a purpose.

### `ParrySystem.Awake()` dereferences `characterData` before its null check
`ParrySystem.cs:51` does
`GetComponent<CharacterManager>().characterData.parryBonusDriveMultiplier` with no guard, and
*then* null-checks `characterManager` immediately after. Throws a `NullReferenceException` if the
component or its data is missing. Reorder the guard above the read — or just delete the field, see
"Dead code and data" below, which removes the dereference entirely.

### A parry pays out drive twice
`ParrySystem.PerformSuccessfulParry()` grants the parry bonus, and then
`EnemyManager.cs:656` calls `TakeDamage(25% chip, wasParried: true)` — whose drive block
(`CharacterManager.cs:370-378`) runs unconditionally and adds
`chip × damageTakenDriveMultiplier` on top.

Deliberately left in for now and compensated for in Kolo Aka's tuning. If you'd rather it pay
once, gate that block behind `if (!wasParried)` — then Kolo Aka goes back to **5** and the whole
roster is uniform. It's a feel change to his "tank builds drive by getting hit" identity, so it
should be its own decision.

---

## Architecture / cleanup

### ~~Move `ParryInputManager` off `PlayerCharacter.prefab`~~ — DONE
A single `Parry Input Manager` GameObject now lives in `Encounter.unity` with the component
removed from the prefab, so exactly one instance exists. `DontDestroyOnLoad` is gone (parry input
is only meaningful inside an encounter, and an instance surviving into the map scene would still
hold the shared input action when the next encounter loads its own), `Instance` resolves lazily so
script execution order can't strand a `ParrySystem`, and the dead registration list is deleted.
The duplicate branch in `Awake()` is kept but now logs an error instead of swallowing it, since
nothing should produce a second instance any more. The `OnEnable`/`OnDisable` guards stay — see
`CLAUDE.md`.

### Leftovers from the 2.5D conversion
- `txt_hit_text` and `img_glow` are still direct children of the `PlayerCharacter.prefab` root,
  outside `CharacterUI`. Both are inactive, and **`txt_hit_text` is referenced by no script** —
  the floating damage number is `actionStatusText` (`UI_action_text`, under `CharacterUI`). Delete
  `txt_hit_text`; decide whether `img_glow` is wanted and either move it under `CharacterUI` or
  delete it. Anything left outside a Canvas will simply never render.
- `EnemyCharacterVariant.prefab` still carries **dangling overrides** pointing at the `Image` and
  `Button` that were deleted from the base during the conversion. They can never apply. The `Image`
  one held the enemy colour tint, which is why enemies briefly rendered untinted — the tint now
  lives on the variant's `SpriteRenderer.Color`. Harmless, but worth clearing.
- `Players characters` (the old `GridLayoutGroup` container in `Encounter.unity`) is disabled rather
  than deleted, and `Player Crew` is an empty leftover RectTransform root. Both can go.

### Dead code and data
- `ParrySystem.parryDriveBonus` / `ParryDriveBonus` (`ParrySystem.cs:8,32,51`) is cached in
  `Awake` but never used — `PerformSuccessfulParry` re-reads `characterData` directly. Delete.
  (Deleting it also removes the unguarded `characterData` dereference logged as a bug above.)
- `parryBonusDriveMultiplier` on **enemy** assets (Skeleton `2.5`, Slayer `10`) is never read.
  `OpenParryWindow()` is only called from `EnemyAttack`, and enemy attacks only target players,
  so enemies can never parry. Either wire up enemy parry or move the field off the shared
  `Character` base.
- `Assets/Prefabs/EnemyCharacter[DEPRECIATED].prefab` has **0 references** anywhere in
  `Encounter.unity` — enemies use `EnemyCharacterVariant.prefab`. Safe to delete.
- `HealthManager.cs` is an empty stub; damage flows through `CharacterManager.TakeDamage`.
  Implement it or delete it.
- `DriveManager.requiredSegments` (`DriveManager.cs:21`) is labelled legacy and kept only for
  Inspector compatibility.
- Deprecated back-compat methods: `CharacterManager.OnRoundComplete()` (`CharacterManager.cs:340`)
  and the two `[Obsolete]` `EnemyAttack.StartAttack()` error-stub overloads.
  (`Character.UpdateBuffsForNewRound()` was deleted in Phase 1a.)

---

## Content gaps

- `Assets/Scripts/Combat/Skelly Spear.asset` (5–10 dmg) is not in any enemy's `actionSlots` —
  orphaned.
- `Tephi`, `Witch Healer` (Witch), and `Skeleton Boss 2` (Killswitch) are authored but not placed
  in `Encounter.unity`. Note this is why Tephi's broken `40f` parry multiplier never showed up in
  play.
