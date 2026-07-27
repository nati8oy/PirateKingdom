# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

**Pirate Kingdom** is a Unity turn-based tactical RPG combat prototype (pirate theme). The
codebase is currently a self-contained **battle system** — a party of player crew fights a
group of enemies in a single encounter. There is no overworld, meta-progression, save system,
or narrative layer yet; the scripts implement combat only.

- **Engine:** Unity `6000.1.10f1` (Unity 6)
- **Render pipeline:** Universal Render Pipeline (URP 17.1.0)
- **Input:** New Input System (`com.unity.inputsystem`) via generated `PlayerControls`
- **Only scene:** `Assets/Scenes/Encounter.unity`
- **Main branch:** `main`

## How to run / test

This is a Unity project — there is no CLI build/test loop. Open the project in the Unity Editor
(6000.1.10f1) and press Play on `Assets/Scenes/Encounter.unity`. There are currently no automated
tests (the Test Framework package is installed but unused). Do not attempt to `dotnet build` or
run a headless build unless the user asks — verification is done by playing in the Editor.

## Where the game code lives

All first-party gameplay code is under `Assets/Scripts/`. **Everything else under `Assets/` is
third-party and should not be modified** unless explicitly requested:

- `Assets/Feel/` — MoreMountains **Feel / MMFeedbacks / MMTools** (game-feel, juice, feedbacks)
- `Assets/AssetKits/ParticleImage/` — UI particle system
- `Assets/TextMesh Pro/` — TMP resources

### First-party script map (`Assets/Scripts/`)

- `Combat/`
  - `Character.cs` — **ScriptableObject** base for all characters (stats, class, allegiance,
    buffs, per-action cooldowns). Enemies subclass this.
  - `Action.cs` — ScriptableObject defining an ability (Attack/Heal/Buff/Debuff, target type,
    damage/heal ranges, cooldown, windup, icon).
  - `CombatController.cs` — resolves **player** actions on a target (d20 hit rolls, crits, drive
    multipliers) and calls `TurnManager.CompleteTurn()`.
  - `DriveManager.cs` / `DriveMeter.cs` / `DriveUI.cs` — the "Drive" super-meter resource system.
  - `CrewManager.cs` — assembles the player crew (max 4) from `Player`-tagged objects.
  - `ClickableCharacter.cs`, `ParryVisualFeedback.cs`.
- `Generics and Managers/`
  - `TurnManager.cs` — **the heart of combat.** Initiative-based turn order (re-rolled each
    round), round counter, per-turn buff/cooldown updates, and win/lose (`EndBattle`) handling.
  - `GameManager.cs` — singleton; tracks `PlayerCharacters` / `EnemyCharacters` by tag.
  - `CharacterManager.cs` — the **runtime MonoBehaviour** wrapper around a `Character`
    ScriptableObject: current health, taking/dealing damage, healing, feedbacks, death, drive.
  - `ActionsManager.cs` — builds the player action-button UI (incl. the End Turn button) and the
    shared `IsActionAvailable` cooldown check used by both player and enemy AI.
  - `ParrySystem.cs` + `ParryInputManager.cs` — real-time parry windows on incoming enemy attacks.
  - `PlayerInputHandler.cs` — maps the Drive input action to `EnterDriveMode`.
  - `HealthManager.cs` — **currently an empty stub.**
- `Enemy/`
  - `Enemy.cs` — ScriptableObject subclass of `Character` with AI config (action weights,
    targeting strategy, heal threshold, drive config).
  - `EnemyManager.cs` — enemy AI turn logic: weighted action choice, target selection,
    parryable attack flow.
  - `EnemyAttack.cs` — coroutine attack sequence that opens the target's parry window.
  - `EnemyDriveManager.cs` — automated enemy drive usage strategies.
- `Player/PlayerCombatController.cs`, `Tooltip/`, `Debugs/`, `PlayerControls.cs` (generated input).

## Key architecture concepts

- **ScriptableObject data vs. MonoBehaviour runtime.** `Character`/`Enemy`/`Action` are
  ScriptableObjects holding *shared/base data*. `CharacterManager` is the *per-instance runtime
  state* in the scene. Note buffs/cooldowns live on the ScriptableObject (`Character`), so they
  are shared state — `Initialize()` is called to reset them at battle start.
- **Turn flow:** `TurnManager.GetTurnOrder()` (initiative = `Speed + Random(1,9)`) →
  `SetCharacterTurn()` → player picks action (UI) or enemy AI acts → `CompleteTurn()` advances
  the index → new round re-rolls order. Battle end is polled in `TurnManager.Update()`.
- **d20 combat resolution:** roll 1–20. `1` = critical miss, `20` = critical hit (double dmg),
  otherwise `roll + attackPower >= target defense` to hit. Same system for player and enemy.
- **Drive system:** a 4-segment meter (`DriveMeter`, max 400, +50/turn regen) that fills from
  dealing damage, taking damage, and **parries** (per-character multipliers on `Character`).
  Segments are spent on `DriveAction`s (BuffNextAttack / HealSelf / RemoveDebuff / ReduceCooldown)
  or to enter **Drive Mode** (multiplies next attack/heal). Enemies auto-spend via
  `EnemyDriveManager` strategies.
- **Parry:** enemy attacks have a windup; `EnemyAttack` opens a `ParrySystem` window on the target.
  A well-timed parry input (routed through the singleton `ParryInputManager`) negates most damage
  (**25% still lands**) and grants bonus drive.
- **Feedbacks:** visual/audio juice goes through MMF_Player fields on `CharacterManager`
  (`dealDamageFeedback`, `healFeedback`, `missFeedback`, `parryFeedback`, attack/death audio).
- **Tags matter:** player crew objects are tagged `Player`, enemies tagged `Enemy`. Much of the
  target-finding logic relies on these tags (`GameObject.FindGameObjectsWithTag`).

## Conventions & gotchas

- Lots of `FindObjectOfType` / `FindObjectsOfType` and tag-based lookups — wiring is runtime, not
  via serialized references. Be careful when renaming tags or moving components.
- The code is prototype-quality with heavy `Debug.Log` tracing and several **deprecated methods
  kept for backwards compatibility** (marked in comments — e.g. `UpdateBuffsForNewRound`,
  old `StartAttack` overloads, `OnRoundComplete`). Prefer the non-deprecated paths.
- `HealthManager.cs` is an empty placeholder; damage actually flows through
  `CharacterManager.TakeDamage`.
- Buff durations are authored as floats on `Action` but converted to integer **turns**.
- When adding gameplay scripts, keep them under `Assets/Scripts/` in the matching subfolder and
  match the existing style (public serialized fields, `[Header]`/`[Tooltip]` attributes,
  ScriptableObject data + MonoBehaviour runtime split).

## Git

- Work off `main`; branch before committing if asked to commit.
- Only commit/push when the user explicitly asks.
