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

## Bugs

### Drive stacks don't boost heals cast on an ally
`CharacterManager.Heal()` applies `driveManager.GetNextHealingMultiplier()` using **its own**
`driveManager` field — i.e. the **target's** DriveManager, not the healer's. `CombatController`
(`CombatController.cs:337-341`) heals the target and *then* clears the **healer's** stacks via
`currentCharacter.OnAttackPerformed()`.

So a healer who commits drive stacks and heals a crewmate spends the segments for nothing, while
the target's idle stacks get consumed instead. Self-heals only work by coincidence (healer and
target are the same object). This contradicts the "next **attack _or_ heal**" contract in
`CLAUDE.md`.

`TooltipUI.ShowTargetTooltip()` reads the **attacker's** multiplier for the healing preview, so
the tooltip also disagrees with what actually happens.

**Fix direction:** pass the caster's multiplier into `Heal()` (e.g. `Heal(float amount, float
driveMultiplier = 1f)`) and have `CombatController` / `EnemyManager` supply it, rather than
letting the receiving `CharacterManager` look it up. Note `EnemyManager.PerformDirectAttack()`'s
heal branch already multiplies explicitly *and* consumes before calling `Heal()` — it avoids a
double-application only because the stacks are cleared first, which is fragile.

### Cooldowns tick twice for whoever ends a round
`CharacterManager.OnTurnComplete()` calls `characterData.UpdateActionCooldowns()`
(`CharacterManager.cs:165`), and `TurnManager.RoundComplete()` calls it again on the same
`currentCharacterTurn` (`TurnManager.cs:329`). The last character to act in a round therefore
burns two turns of cooldown for one turn of play.

The `RoundComplete()` call also only touches a single character rather than everyone, which looks
vestigial from the old round-based buff system. Masked today because nothing in `Encounter.unity`
has a `cooldown > 0`.

**Fix direction:** delete the `UpdateActionCooldowns()` line from `RoundComplete()` — the
per-turn call in `OnTurnComplete()` is the correct one.

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

### Duplicate scene instances share ScriptableObject state
`Character` holds `activeBuffs`, `actionCooldowns`, and `reputation` as instance fields on the
**ScriptableObject**, but multiple scene objects can point `characterData` at the same asset.
This is live today: both Skeleton enemies in `Encounter.unity` reference
`Assets/Scripts/Enemy/Skeleton.asset`.

Consequences: debuffing one Skeleton debuffs both; an action put on cooldown by one is on
cooldown for both; and `UpdateBuffsForCharacterTurn()` / `UpdateActionCooldowns()` run once per
skeleton turn, so durations tick down twice as fast as authored.

Currently masked because Skeleton's only action (`Slash_Attack`) has `cooldown: 0` and nothing
debuffs enemies yet — but it will bite as soon as either changes, or as soon as two crew members
share a `Character` asset.

**Fix direction:** move per-battle mutable state (`activeBuffs`, `actionCooldowns`, `reputation`)
off `Character` and onto the runtime `CharacterManager`, leaving the ScriptableObject as pure
authored data. Non-trivial — touches `TurnManager`, `ActionsManager.IsActionAvailable`,
`EnemyManager`, and every `GetModified*()` call site.

### `ParrySystem.Awake()` dereferences `characterData` before its null check
`ParrySystem.cs:37` does
`GetComponent<CharacterManager>().characterData.parryBonusDriveMultiplier` with no guard, and
*then* null-checks `characterManager` at line 41. Throws an `NullReferenceException` if the
component or its data is missing. Reorder the guard above the read.

### A parry pays out drive twice
`ParrySystem.PerformSuccessfulParry()` grants the parry bonus, and then
`EnemyManager.cs:647` calls `TakeDamage(25% chip, wasParried: true)` — whose drive block
(`CharacterManager.cs:201-207`) runs unconditionally and adds
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

### Dead code and data
- `ParrySystem.parryDriveBonus` / `ParryDriveBonus` (`ParrySystem.cs:8,24,37`) is cached in
  `Awake` but never used — `PerformSuccessfulParry` re-reads `characterData` directly. Delete.
  (Deleting it also removes the unguarded `characterData` dereference logged as a bug above.)
- `parryBonusDriveMultiplier` on **enemy** assets (Skeleton `2.5`, Slayer `10`) is never read.
  `OpenParryWindow()` is only called from `EnemyAttack`, and enemy attacks only target players,
  so enemies can never parry. Either wire up enemy parry or move the field off the shared
  `Character` base.
- `HealthManager.cs` is an empty stub; damage flows through `CharacterManager.TakeDamage`.
  Implement it or delete it.
- `DriveManager.requiredSegments` (`DriveManager.cs:21`) is labelled legacy and kept only for
  Inspector compatibility.
- Deprecated back-compat methods flagged in `CLAUDE.md`: `Character.UpdateBuffsForNewRound()`,
  `CharacterManager.OnRoundComplete()`, and the two `[Obsolete]` `EnemyAttack.StartAttack()`
  error-stub overloads.

---

## Content gaps

- `Assets/Scripts/Combat/Skelly Spear.asset` (5–10 dmg) is not in any enemy's `actionSlots` —
  orphaned.
- `Tephi`, `Witch Healer` (Witch), and `Skeleton Boss 2` (Killswitch) are authored but not placed
  in `Encounter.unity`. Note this is why Tephi's broken `40f` parry multiplier never showed up in
  play.
