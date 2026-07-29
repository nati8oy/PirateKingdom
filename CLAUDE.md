# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

**Pirate Kingdom** is a Unity turn-based tactical RPG combat prototype (pirate theme). The
codebase is currently a self-contained **battle system** — a party of player crew fights a
group of enemies in a single encounter. There is no overworld, meta-progression, save system,
or narrative layer yet; the scripts implement combat only.

- **Engine:** Unity `6000.3.5f1` (Unity 6.3)
- **Render pipeline:** Universal Render Pipeline (URP 17.3.0)
- **Input:** New Input System (`com.unity.inputsystem` 1.17.0) via generated `PlayerControls`
- **Only scene:** `Assets/Scenes/Encounter.unity`
- **Main branch:** `main`

Known bugs, dead code, and deferred cleanup are tracked in **`TODO.md`** — check it before
starting work, and add to it when you find something you're not fixing now.

## How to run / test

This is a Unity project — there is no CLI build/test loop. Open the project in the Unity Editor
(6000.3.5f1) and press Play on `Assets/Scenes/Encounter.unity`. There are currently no automated
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
  - `EnemyAttack.cs` — coroutine attack sequence that opens the target's parry window. It does
    **not** roll damage; `EnemyManager` rolls once and passes the value into
    `StartAttack(target, action, damage)`.
  - `EnemyDriveManager.cs` — automated enemy drive usage strategies.
- `Player/PlayerCombatController.cs`, `Tooltip/`, `Debugs/`, `PlayerControls.cs` (generated input).

## Key architecture concepts

- **ScriptableObject data vs. MonoBehaviour runtime.** `Character`/`Enemy`/`Action` are
  ScriptableObjects holding *shared/base data*. `CharacterManager` is the *per-instance runtime
  state* in the scene. Note buffs/cooldowns live on the ScriptableObject (`Character`), so they
  are shared state — `Initialize()` is called to reset them at battle start.
- **Turn flow:** `TurnManager.BeginBattle()` → `GetTurnOrder()` (initiative = `Speed + Random(1,9)`)
  → `SetCharacterTurn()` → player picks action (UI) or enemy AI acts → `CompleteTurn()` advances
  the index → new round re-rolls order. Battle end is polled in `TurnManager.Update()`.
  - `BeginBattle()` **snapshots the turn order**, so every combatant must exist before it runs.
    `Start()` calls it automatically while the serialized `autoStartBattle` is ticked (the current
    setup, where crew and enemies are placed directly in `Encounter.unity`). Anything that spawns
    combatants at runtime must untick it and call `BeginBattle()` itself when spawning is done —
    don't try to win that race with Script Execution Order.
  - `Update()` early-returns until the battle has begun. Without that guard a scene that spawns
    its combatants would count zero living players on frame one and instantly declare a defeat.
- **d20 combat resolution:** roll 1–20. `1` = critical miss, `20` = critical hit (double dmg),
  otherwise `roll + attackPower >= target defense` to hit. Same system for player and enemy.
- **Drive system:** a 4-segment meter (`DriveMeter`, max 400, +50/turn regen) that fills from
  dealing damage, taking damage, and **parries** (per-character multipliers on `Character`).
  - **Stacking buff (the main mechanic).** A character commits segments as **drive stacks**
    (`DriveManager.driveStacks`, one segment each via `TryAddDriveStack()`) before acting. Stacks
    apply a **diminishing-returns multiplier** to the next **attack _or_ heal**:
    `1 + baseBonus·(1 + d + d² + …)`, where `baseBonus = Character.buffNextActionMultiplier - 1`
    (per-character) and `d = decayRate` (serialized on `DriveManager`, default 0.5). Defaults give
    1.50× / 1.75× / 1.875× / 1.9375×, hard-capped at 2.0×. Stacks are consumed (reset to 0) after
    the action and cleared at turn end (spent segments are **not** refunded). The multiplier is
    read via `GetNextAttackDamageMultiplier()` / `GetNextHealingMultiplier()`.
  - **Player input:** each press of the Drive action adds one stack to the selected crew member
    (`PlayerInputHandler` → `TryAddDriveStack()`). Sound + particles fire on **every** stack.
  - **Enemies** use the *same* stacking system: `EnemyDriveManager.EvaluateDriveUsage()` (called at
    the start of the enemy turn) picks how many stacks to commit per its `EnemyDriveConfig`
    (`strategy`, `stacksPerBuff`, `minimumSegmentsToUse`, `healthThreshold`, `usageChance`) authored
    on the `Enemy` ScriptableObject.
  - **Utility `DriveAction`s** (`HealSelf` / `RemoveDebuff` / `ReduceCooldown`) still exist as
    separate one-shot spends via `TryUseDriveAction()` (used by `DriveUI` buttons and the enemy
    low-health heal branch). `BuffNextAttack` now just adds a stack. `IsDriveMode` /
    `NextAttackBuffed` / `EnterDriveMode()` are retained only as `driveStacks`-backed shims —
    prefer the stacking API.
- **Parry:** enemy attacks have a windup; `EnemyAttack` opens a `ParrySystem` window on the target.
  A well-timed parry input (routed through the singleton `ParryInputManager`) negates most damage
  (**25% still lands**) and grants bonus drive.
  - **One attempt per attack.** `EnemyAttack.BeginParrySequence()` arms a single attempt at the
    *start of the windup* and claims the input target for the whole attack — deliberately wider
    than the timing window, so an early press is caught and **spent** rather than ignored. The
    first press sets `parryAttemptSpent` whether it lands or not; everything after it is dropped.
    Mashing is therefore not a strategy. `EndParrySequence()` releases it on every exit path
    (parried / landed / `CancelAttack`), and outside a sequence input is ignored entirely.
  - `EnemyAttack` reads `ParrySystem.ParrySucceeded` to decide whether the attack was parried.
    **Don't go back to inferring it from `IsParryWindowActive`** — the window also shuts on
    timeout, so that inference silently held only while `parryWindowDuration` (0.3s) happened to
    exceed `parryWindowStartOffset` (0.2s); tuning the window shorter would have made every attack
    register as parried.
  - **Single damage roll.** `EnemyManager.PerformParryableAttack()` rolls damage once (applying
    crit and the enemy's drive multiplier), stores it as `_pendingDamage`, and passes it into
    `EnemyAttack.StartAttack()`. `EnemyAttack` forwards it to `ParrySystem.SetIncomingDamage()`.
    Damage is applied later in `EnemyManager.OnAttackComplete()` — full on a hit, 25% on a parry.
    Keep these on one roll: the parry drive reward is scaled off the damage actually prevented.
  - **Parry drive reward** = `damage prevented × Character.parryBonusDriveMultiplier`, tuned so a
    typical ~5-damage hit pays ~25 drive (a quarter segment). Note a parry pays out **twice** —
    the parry bonus, plus `damageTakenDriveMultiplier` on the 25% chip via `TakeDamage`.
  - `ParryInputManager` is a scene singleton on a single `Parry Input Manager` GameObject in
    `Encounter.unity` — **not** `DontDestroyOnLoad`, because parry input is only meaningful inside
    an encounter and an instance surviving into another scene would still hold the shared input
    action when the next encounter loads its own. Its `OnEnable`/`OnDisable` are guarded by
    `_instance != this` — **do not remove those guards**, or a duplicate's `OnDisable` will call
    `Disable()` on the *shared* project-wide Parry action and silently kill parry input for
    everyone. `Instance` resolves lazily via `FindObjectOfType` so script execution order can't
    strand a `ParrySystem`.
  - There is **no registration step**. The manager only ever dispatches to `activeParrySystem`,
    which `ParrySystem.OpenParryWindow()` sets via `SetActiveParrySystem()`.
    `UnregisterParrySystem()` exists purely so a system that's disabled or destroyed mid-window
    doesn't stay the active target.
- **Feedbacks:** visual/audio juice goes through MMF_Player fields on `CharacterManager`
  (`dealDamageFeedback`, `healFeedback`, `missFeedback`, `parryFeedback`, attack/death audio).
- **Tags matter:** player crew objects are tagged `Player`, enemies tagged `Enemy`. Much of the
  target-finding logic relies on these tags (`GameObject.FindGameObjectsWithTag`).

## Conventions & gotchas

- Lots of `FindObjectOfType` / `FindObjectsOfType` and tag-based lookups — wiring is runtime, not
  via serialized references. Be careful when renaming tags or moving components.
- **Two different ways input is consumed, and they don't share state.** `PlayerControls.inputactions`
  is the **project-wide actions asset** (`com.unity.input.settings.actions` in
  `ProjectSettings/EditorBuildSettings.asset`).
  - `PlayerInputHandler` does `new PlayerControls()` — its own private `InputActionAsset` instance.
    Enabling/disabling it affects nothing else.
  - `ParryInputManager` uses a serialized `InputActionReference`, which resolves to the **shared**
    project-wide asset. Calling `Enable()`/`Disable()` there is global and is not ref-counted, so
    any component that disables one of those actions disables it for every other consumer.
- The code is prototype-quality with heavy `Debug.Log` tracing and several **deprecated methods
  kept for backwards compatibility** (marked in comments — e.g. `UpdateBuffsForNewRound`,
  old `StartAttack` overloads, `OnRoundComplete`). Prefer the non-deprecated paths.
- `HealthManager.cs` is an empty placeholder; damage actually flows through
  `CharacterManager.TakeDamage`.
- Buff durations are authored as floats on `Action` but converted to integer **turns**.
- When adding gameplay scripts, keep them under `Assets/Scripts/` in the matching subfolder and
  match the existing style (public serialized fields, `[Header]`/`[Tooltip]` attributes,
  ScriptableObject data + MonoBehaviour runtime split).

## Ways of working

- **UI is the user's domain.** The user handles all UI work (canvases, prefabs, buttons, layout,
  bindings) in the Unity Editor. When a feature needs UI, do the *code* side only: expose the
  necessary public methods, `[SerializeField]` fields, `UnityEvent`s / C# events, and hooks — then
  clearly tell the user what to wire up in the Editor. Do **not** attempt to build or wire UI
  yourself, and don't consider a feature "done" from a UI standpoint; the user wires and tests it
  in-Editor.
- Verification is always done by the user playing the scene in the Editor (see "How to run /
  test"). Provide concrete in-Editor test steps rather than assuming automated verification.

## Git

- Work off `main`; branch before committing if asked to commit.
- Only commit/push when the user explicitly asks.
