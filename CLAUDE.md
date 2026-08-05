# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

**Pirate Kingdom** is a Unity turn-based tactical RPG combat prototype (pirate theme). The
codebase is a self-contained **battle system** plus the beginnings of a **meta layer** — a
roguelite voyage where a crew carries damage between fights and death is permanent.

Built and working: combat, and run state (crew carry health across encounters, permadeath, JSON
save/load). Not built yet: the voyage map, ports/recruiting, XP and levelling, cross-run unlocks,
and any narrative layer. There is still only one scene.

- **Engine:** Unity `6000.3.5f1` (Unity 6.3)
- **Render pipeline:** Universal Render Pipeline (URP 17.3.0)
- **Input:** New Input System (`com.unity.inputsystem` 1.17.0) via generated `PlayerControls`
- **Only scene:** `Assets/Scenes/Encounter.unity`
- **Main branch:** `main`

Two companion documents, both worth reading before starting work:

- **`META.md`** — the meta-layer design and phased implementation plan. **Section 0 is a status
  summary: read it first** to see what's built, what's next, and the Editor tooling that exists.
- **`TODO.md`** — known bugs, dead code, and deferred cleanup. Check it before starting, and add
  to it when you find something you're not fixing now.

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
  - `ClickableCharacter.cs` — target selection. Implements `IPointerClickHandler` (world sprite
    body + `Collider2D` + a `Physics2DRaycaster` on the camera). Its `CharacterClicked()` is still
    public so a UI Button's OnClick can drive it too.
  - `ActionTargetingLine.cs` — LineRenderer from the selected action's source to the legal target
    under the cursor, plus valid / invalid / default cursor swaps. The line is a **player setting**
    (`GameSettings.ShowTargetingLine`, default on); the cursor feedback is always on.
  - `ParryVisualFeedback.cs`.
- `Generics and Managers/`
  - `TurnManager.cs` — **the heart of combat.** Initiative-based turn order (re-rolled each
    round), round counter, per-turn buff/cooldown updates, and win/lose (`EndBattle`) handling.
  - `GameManager.cs` — singleton; tracks `PlayerCharacters` / `EnemyCharacters` by tag. Those lists
    are **static** and the object is `DontDestroyOnLoad`, so they're re-populated on
    `SceneManager.sceneLoaded` and by `EncounterBootstrapper` after it spawns — a single `Awake`
    snapshot goes stale the moment combatants are spawned at runtime.
  - `CharacterManager.cs` — the **runtime MonoBehaviour** wrapper around a `Character`
    ScriptableObject: current health, taking/dealing damage, healing, feedbacks, death, drive.
  - `ActionsManager.cs` — builds the player action-button UI (incl. the End Turn button) and the
    shared `IsActionAvailable` cooldown check used by both player and enemy AI. The panel shows
    whoever's turn it is, **enemies included**, but buttons are only interactable for a
    `Player`-allegiance character — see "Turn ownership" below.
  - `ParrySystem.cs` + `ParryInputManager.cs` — real-time parry windows on incoming enemy attacks.
  - `PlayerInputHandler.cs` — maps the Drive input action to `EnterDriveMode`.
  - `CursorManager.cs` — the single owner of the hardware cursor. `DontDestroyOnLoad`, lazy
    `Instance`, tolerates being absent. **Anything that swaps the cursor must go through it**
    (`SetCursor` / `ResetToDefault`) — a component calling `Cursor.SetCursor(null, …)` directly
    restores the *system* cursor and silently discards the game's default.
  - `HealthManager.cs` — **currently an empty stub.**
- `Settings/` — player-facing settings. See the "Settings & accessibility" section of `TODO.md`.
  - `GameSettings.cs` — **static**, persisted in `PlayerPrefs`, with a `Changed` event. Deliberately
    *not* part of `RunState`: a run is per-voyage and discarded on death, settings are per-player
    and outlive every run. **Add new settings as properties here** rather than reading `PlayerPrefs`
    at the call site, so keys, defaults and change notification stay in one place.
  - `SettingsBridge.cs` — MonoBehaviour adapter, because a static class can't be a UnityEvent
    target. Settings UI wires to this, not to `GameSettings` directly.
  - Uses `System.Action` **fully qualified** — a bare `Action` is the game's own ability
    ScriptableObject. Same rule as the `Meta/` files.
- `Environment/` — 2D presentation helpers, independent of combat.
  - `ParallaxController.cs` + `ParallaxLayer.cs` — camera-referenced parallax with optional
    per-layer auto-scroll. One controller drives every layer so they share a `Depth Scale`.
  - `SortingOrderFromPosition.cs` — derives Order in Layer from world Y so lower-on-screen draws
    in front. Optional; combatants spawn from one prefab and would otherwise share one order.
- `Enemy/`
  - `Enemy.cs` — ScriptableObject subclass of `Character` with AI config (action weights,
    targeting strategy, heal threshold, drive config).
  - `EnemyManager.cs` — enemy AI turn logic: weighted action choice, target selection,
    parryable attack flow.
  - `EnemyAttack.cs` — coroutine attack sequence that opens the target's parry window. It does
    **not** roll damage; `EnemyManager` rolls once and passes the value into
    `StartAttack(target, action, damage)`.
  - `EnemyDriveManager.cs` — automated enemy drive usage strategies.
- `Meta/` — the run/voyage layer that sits above combat. See `META.md` for the full plan.
  - `ContentDatabase.cs` — ScriptableObject registry mapping stable string `id`s to `Character` /
    `Action` assets. Save data can't hold Unity object references, so run state stores ids and
    resolves them here. Lives at `Assets/Resources/ContentDatabase.asset` and is reached via
    `ContentDatabase.Instance` (`Resources.Load`, no scene wiring). Rebuild it via
    **Tools > Pirate Kingdom > Rebuild Content Database** after adding or deleting content assets.
  - `RunState.cs` — plain `[Serializable]` `RunState` + `CrewMemberState`. Everything that
    persists across encounters in one voyage: crew, their carried health, level/XP, action
    loadout, and permadeath flag. JSON-serialized, so it holds **ids, never object references**.
  - `RunManager.cs` — `DontDestroyOnLoad` singleton owning the live `RunState` and its save/load
    (`Application.persistentDataPath/run.json`). Has debug hooks to start a run without the
    voyage map, and context menus for Save / Load / Delete Save / Log Save Path.
  - `EncounterDefinition.cs` — ScriptableObject describing one fight: which `Enemy` assets, how
    many, display name, plunder reward.
  - `EncounterResult.cs` — the report one encounter produces: survivors and their end health, this
    fight's casualties, plunder awarded. **The single persistence boundary.**
    `TurnManager.BeginBattle()` opens the encounter via `RunManager.BeginEncounter()`; deaths are
    reported to `RunManager.ReportCrewDeath()` **in memory only** as they happen (a dying
    `CharacterManager` destroys its GameObject immediately, so casualties can't be read back at the
    end); `TurnManager.EndBattle()` then calls `RunManager.CompleteEncounter()`, which awards
    plunder, builds the result and calls `SaveRun()` **once**. Read it via `TurnManager.LastResult`
    or the `TurnManager.OnEncounterComplete` event. Don't reintroduce `SaveRun()` calls elsewhere —
    a mid-fight save reloads with the crew damaged and the enemies restored.
  - `EncounterBootstrapper.cs` — spawns the crew (from `RunState`) and the enemies (from the
    `EncounterDefinition`) on scene load, then calls `TurnManager.SetEncounter()` (so the reward is
    known at battle end) and `TurnManager.BeginBattle()`. Crew are bound to
    their run record explicitly via `CharacterManager.AssignCrewState()`; dead crew never spawn.
    **Two rules to preserve when touching combatant spawning:**
    - Spawn in `Awake()`, start the battle in `Start()`. Unity finishes every `Awake()` before any
      `Start()`, so the turn-order snapshot is complete by construction. Don't "fix" spawn ordering
      with Script Execution Order.
    - Instantiate under an **inactive** parent, assign `characterData`, *then* reparent onto the
      active spawn point. `DriveManager`, `ParrySystem` and `EnemyManager` all dereference
      `characterData` in `Awake()`, and the enemy prefab carries no default — a plain `Instantiate`
      throws before the data can be set.
  - `EncounterFlow.cs` — buttons for the victory/defeat screens: `RestartEncounter()` reloads the
    scene for another fight with the crew's carried damage, `StartNewRun()` rebuilds from Debug
    Starting Crew. Gives a repeatable attrition loop, and `RunState.encountersSurvived` counts the
    wins. **A stand-in for Phase 3's real scene transitions**, not a design.
    - Reloading works only because persistence already happened: `CompleteEncounter()` wrote the
      run once at `EndBattle`, and `RunManager` is `DontDestroyOnLoad` — its duplicate destroys
      itself *before* `EnsureRunLoaded()`, so the live run survives the reload.
    - `StartNewRun()` calls `RunManager.StartDebugRunNow()` rather than destroying the singleton.
      Destroying it would leave the static `Instance` dangling until end of frame, racing the
      queued scene load.
  - **No Meta file uses `using System;`** — it would make `System.Action` collide with the
    game's own `Action` ScriptableObject. System types are fully qualified instead.
- `Player/PlayerCombatController.cs`, `Tooltip/`, `Debugs/`, `PlayerControls.cs` (generated input).

## Key architecture concepts

- **ScriptableObject data vs. MonoBehaviour runtime.** `Character`/`Enemy`/`Action` are
  ScriptableObjects holding *authored data only*. `CharacterManager` is the *per-instance runtime
  state* in the scene, and owns `activeBuffs` + `actionCooldowns`.
  - **Never put mutable runtime state on a ScriptableObject.** Two reasons: several scene objects
    can share one asset (both Skeletons reference `Skeleton.asset`), and in the Editor writes to a
    ScriptableObject persist into the project asset on disk, so play sessions would permanently
    drift your authored data.
  - Consequently anything that needs buff/cooldown state takes a `CharacterManager`, not a
    `Character` — `ActionsManager.IsActionAvailable()`, `ActionsManager.LoadCharacterActions()`,
    `TooltipUI.ShowActionTooltipWithCharacter()`, `ActionButtonHover.SetActionWithCharacter()`.
  - Buffed stats are computed in `CharacterManager.RefreshStats()` (authored base + this
    instance's buffs). `CharacterManager.GetModifiedAttackPower()` is a *different* thing — it
    applies the Drive multiplier on top of the already-buffed `AttackPower`.
  - `reputation` is still on `Character`; it's authored, zeroed, and never read.
- **Stable asset ids.** `Character.Id` and `Action.Id` are serialized strings stamped once by
  `ContentDatabase`'s *Rebuild From Project*, derived from the asset name (`Black Sam` ->
  `black_sam`). Save data references content by these ids, so **never change an id once runs have
  been saved against it**, and never key save data off `characterName` or the asset filename.
  Rebuild never overwrites an existing id, which is what makes renaming an asset safe.
- **Run state is optional.** A `CharacterManager` binds to a `CrewMemberState` on `Start()` only
  when there's an active run and it's a `Player`; otherwise it falls back to full health. Every
  consumer of `RunManager.Instance` must tolerate it being null — playing `Encounter.unity`
  directly with no run is a supported case, and enemies never bind at all.
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
- **Turn ownership — the player may only act on their own turn.** Enforced in three places, and
  all three are load-bearing:
  - `ActionsManager.LoadCharacterActions()` sets `button.interactable = isAvailable && playerControlled`,
    where `playerControlled` is the loaded character's allegiance. The panel deliberately still
    *shows* the enemy's actions so the player can read what's coming — it just can't be clicked.
    **The End Turn button is gated the same way**, or the player could skip the enemy's turn.
  - `CombatController` checks `IsPlayerControlledTurn()` in both `SelectAction()` and
    `TryExecuteActionOnCurrentTarget()` — the single funnel every action passes through. UI state
    alone isn't enough: a click landing on the same frame the turn flips would otherwise resolve.
  - `TurnManager.SetCharacterTurn()` calls `CombatController.ClearSelection()` on every turn
    change. **A selection belongs to the turn it was made in** — without this, an action picked
    but never targeted stays live, and the next character to act resolves an action that isn't
    theirs (`PerformAction` attributes it to `currentCharacterTurn`).
  - `CombatController.IsValidTarget()` is **public on purpose** so UI feedback asks the same
    authority execution does. Don't reimplement targeting rules anywhere else.
- **Presentation is 2.5D: combatants are world sprites, not UI.** Orthographic camera, sprite
  bodies, and a **World Space** `CharacterUI` Canvas per character carrying the health bar, drive
  meter, parry indicator and floating damage text.
  - Draw order is **Sorting Layer + Order in Layer, never Z.** Layers back to front:
    `Default`, `Background Middle`, `Front Middle`, `Player + Enemies`, `Front Near`.
  - The scene Canvas is **Screen Space – Camera** on `Player + Enemies`, which is what lets the
    HUD interleave with the sprite layers at all.
  - `EnemyCharacterVariant.prefab` is a **Prefab Variant of `PlayerCharacter.prefab`** — presentation
    changes are made once on the base and inherited. (`EnemyCharacter[DEPRECIATED].prefab` is
    *not* its base and is genuinely unreferenced.)
  - **UGUI Graphics and `ParticleImage` only render under a Canvas.** Anything visual on a
    combatant must live under `CharacterUI`, not directly under the character root.
  - `CharacterUI` is anchored at `(0.5, 0.5)` — its centre — so its position doesn't depend on the
    root's `sizeDelta`. Don't re-anchor it to a corner; that made the enemy variant's UI fly off
    screen when the root rect changed.
  - Clicking and hovering both need a `Collider2D` on the combatant plus a **`Physics2DRaycaster`
    on the camera**. `IPointerEnterHandler`/`IPointerClickHandler` are EventSystem interfaces, not
    UI ones, so they work on sprites once a raycaster exists.
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
    - **Whoever resolves the action reads the multiplier off the _caster_ and applies it.**
      `CharacterManager.Heal(amount, driveMultiplier)` takes the caster's multiplier as a
      parameter and must never look one up itself — its own `driveManager` belongs to the heal's
      *target*, so doing so spent the healer's segments while consuming the target's idle stacks.
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
- **Reparenting writes compensating scale and position.** Unity preserves world transform when you
  drag an object into a differently-scaled parent, so it back-solves local values — a child of the
  screen-space Canvas picks up a scale like `108` or `0.009`. When you're deliberately moving
  between coordinate systems that compensation is exactly what must be discarded: **Transform ⋮ →
  Reset, then re-place in world units.** This produced two separate "the sprite is invisible" hunts;
  the sprite was rendering at about two pixels both times.
- **A Prefab Variant freezes every property it has ever overridden.** Later edits to the base don't
  reach those properties, and overrides whose target component was deleted from the base go dangling
  and silently stop applying. After changing the base, check the variant's Overrides dropdown and
  revert anything that should be tracking again.
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
