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
  - `Character.cs` — **ScriptableObject** base for all characters: authored stats, class,
    allegiance, art, drive multipliers, status resistances, and the `BuffType` enum. Enemies
    subclass this. **Authored data only** — buffs and per-action cooldowns live on the runtime
    `CharacterManager`, not here (moved in Phase 1a).
  - `ClassDefinition.cs` — per-class baseline stats, per-level growth, and baseline status
    resistances. **Optional**: a `Character` with none assigned resolves exactly as it always did.
    See "Class-driven stats" below.
  - `Action.cs` — ScriptableObject defining an ability. Seven `ActionType`s: Attack, Heal, Buff,
    Debuff, DriveGrant, DriveDrain, HealthDrain. **`ActionType` is serialized by integer value —
    append new members, never reorder them**, same rule as `Character.BuffType` and
    `Enemy.TargetingStrategy`.
    - The Inspector is grouped so **each value section is headed with the action types that read
      it** — Damage (Attack, Health Drain) / Healing (Heal) / Buff-Debuff / Drive (Drive Grant,
      Drive Drain) / Life Steal (Health Drain) / Enemy Windup. Only one section applies to any given
      action; the Inspector showing a field does not mean that field is live.
    - **Field order is Inspector layout only.** Unity serializes by field *name*, so regrouping is
      safe on authored assets; renaming needs `[FormerlySerializedAs]`.
    - `autoTargetSelf` skips target selection and resolves on the caster — see "Turn ownership".
  - `CombatController.cs` — resolves **player** actions on a target (d20 hit rolls, crits, drive
    multipliers), then charges the cooldown and calls `TurnManager.CompleteTurn()` **from a
    `finally`** so a fault mid-resolution can't hand back a free extra action. Also owns the
    `autoTargetSelf` shortcut in `SelectAction()`.
  - `DriveManager.cs` / `DriveMeter.cs` / `DriveUI.cs` — the "Drive" super-meter resource system.
  - `ClickableCharacter.cs` — target selection. Implements `IPointerClickHandler` (world sprite
    body + `Collider2D` + a `Physics2DRaycaster` on the camera). Its `CharacterClicked()` is still
    public so a UI Button's OnClick can drive it too.
  - `ActionTargetingLine.cs` — LineRenderer from the selected action's source to the legal target
    under the cursor, plus valid / invalid / default cursor swaps, plus lighting that target's
    `img_target_indicator`. The line is a **player setting**
    (`GameSettings.ShowTargetingLine`, default on); the cursor and indicator feedback are always on.
  - `StatusEffect.cs` — the status system's data types: `StatusEffectKind` (Bleed / Poison / Burn /
    Stun, **append-only**), `DamageSource` (Direct / OverTime), the runtime `StatusEffect`, the
    authored `StatusResistance` and `StatusApplication`. **Bleed, Poison and Burn are mechanically
    identical** and differ only in icon and colour — adding a kind is an enum member plus Editor
    wiring, never a system. `Stun` exists in the enum but is **inert until Phase D2**;
    `TryApplyStatus` warns if you author one.
  - `StatusIconDisplay.cs` — one icon slot per status kind on a combatant, driven by
    `CharacterManager.StatusesChanged`. Sibling of `BuffIconDisplay` rather than an extension of it:
    buffs key off `BuffType` and show a signed magnitude, statuses key off `StatusEffectKind` and
    show damage per turn plus turns remaining.
    - Slots default to **`StatusEffectStyle.Tint(kind)`**, the same single definition the floating
      damage number uses, so a red `-3 Bleed` and a red bleed icon can't drift apart. `HexColour()`
      derives from that one `Color` rather than being a second literal.
    - **The sprite can come from the action.** `StatusApplication.icon` is optional and carried onto
      the live `StatusEffect`, so two actions of the same kind can look different — a "Vanish" and a
      "Smoke Bomb" both apply Stealth but needn't share an icon. The slot's own sprite is the
      per-kind fallback when an action supplies none. On a refresh the **most recent** application
      wins the icon, unlike the values, which take the max — there's no "higher" of two sprites.
    - The damage label is **blanked for kinds that deal none**, so a stun or stealth slot doesn't
      show a meaningless "0".
    - **Wire it on `PlayerCharacter.prefab` only.** `EnemyCharacterVariant` is a variant of it and
      inherits components added to the base, which is exactly how `BuffIconDisplay` already reaches
      enemies — it exists on the base and nowhere else.
  - `ParryVisualFeedback.cs`.
- `Generics and Managers/`
  - `TurnManager.cs` — **the heart of combat.** Initiative-based turn order (re-rolled each
    round), round counter, per-turn buff/cooldown updates, and win/lose (`EndBattle`) handling.
  - `GameManager.cs` — singleton; tracks `PlayerCharacters` / `EnemyCharacters` by tag. Those lists
    are **static** and the object is `DontDestroyOnLoad`, so they're re-populated on
    `SceneManager.sceneLoaded` and by `EncounterBootstrapper` after it spawns — a single `Awake`
    snapshot goes stale the moment combatants are spawned at runtime.
  - `CharacterManager.cs` — the **runtime MonoBehaviour** wrapper around a `Character`
    ScriptableObject: current health, taking/dealing damage, healing, feedbacks, death, drive. Owns
    all per-instance state — `activeBuffs`, `actionCooldowns`, and the run record — and derives the
    buff-dependent values combat reads: `DamageReductionFraction`, `CritThreshold` /
    `CritChancePercent`, plus the four stats in `RefreshStats()`.
    - `TakeDamage()` returns the health **actually lost** (rounded, capped by remaining health), for
      `HealthDrain` to siphon from. Every other caller ignores it.
    - `SetTargeted()` / `SetHoverTargeted()` are two independent owners of the one target indicator.
  - `ActionsManager.cs` — builds the player action-button UI (incl. the End Turn button) and the
    shared `IsActionAvailable` cooldown check used by both player and enemy AI. The panel shows
    whoever's turn it is, **enemies included**, but buttons are only interactable for a
    `Player`-allegiance character — see "Turn ownership" below.
  - `ParrySystem.cs` + `ParryInputManager.cs` — real-time parry windows on incoming enemy attacks.
  - `PlayerInputHandler.cs` — maps the Drive input action to `DriveManager.TryAddDriveStack()`, one
    stack per press, on whichever crew member currently holds the turn. (It used to be described as
    calling `EnterDriveMode`; that's now only a `driveStacks`-backed shim — prefer the stacking API.)
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
    targeting strategy, heal threshold, drive config). `TargetingStrategy` includes `HighestDrive`,
    which is what makes a `DriveDrain` or `HealthDrain` feel aimed — the health-based strategies
    would otherwise drain whoever happens to have the emptiest meter. It falls back to a random pick
    when every candidate is at zero drive, so the opening turn isn't deterministic. **Append-only
    enum**, like every other serialized enum here.
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
  - `EncounterSequence.cs` — an ordered gauntlet of `EncounterDefinition`s, with `repeat` per row and
    a `RepeatLast` / `Loop` / `Stop` behaviour when it runs out. Assigned on `RunManager`; position
    lives in `RunState.sequenceIndex`, which is why **Tools > Pirate Kingdom > Restart Gauntlet**
    exists — editing the asset otherwise looks like it does nothing. A linear stand-in for Phase 3's
    map, reached through the same `RunManager.GetCurrentEncounter()` seam a map graph will use.
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
- `Tooltip/`
  - `TooltipUI.cs` — scene singleton driving the one shared tooltip label. Four entry points:
    `ShowActionTooltip` (bare), `ShowActionTooltipWithCharacter` (hover on an action button, adds
    cooldown state), `ShowEnemyActionTooltip` (what the enemy is about to do), and
    `ShowTargetTooltip` (hover on a target, the only one that previews an *outcome*).
    - **All four switch on `ActionType`, so a new type renders blank in four places** unless each is
      updated. Same for the buff formatters, which exist because `buffValue` means different units
      per `BuffType`.
    - **Use hex in `<color=…>`, never a colour name.** TMP recognises only black, blue, green,
      orange, purple, red, white and yellow; anything else — `cyan`, `magenta`, `grey` — fails to
      parse *silently* and the raw tag is drawn as literal text. `<color=cyan>` on the Buff label did
      exactly that. The colours are now `const` hex strings at the top of `TooltipUI`.
    - Every tag lives inside an **interpolated** (`$"…"`) string, since they reference those consts.
      A plain `"…"` renders `{ColourBuff}` literally — the same class of bug, one layer down.
    - `ShowTargetTooltip` must model everything that will actually modify the number — attacker's
      drive multiplier, attacker's `CritChancePercent`, and the **target's `DamageReductionFraction`**
      — or it quotes damage that never lands and the shortfall reads as a bug.
    - Still carries the `DontDestroyOnLoad`-on-a-Canvas-child bug logged in `TODO.md`.
  - `ActionButtonHover.cs` / `TargetHoverHandler.cs` — EventSystem hover handlers that call into it.
- `Debugs/`
  - `StatusEffectDebugMenu.cs` — the **Tools > Pirate Kingdom > Debug** items for applying bleed,
    poison, burn, stun and stealth without authoring an action and landing an attack first. All
    bypass the resistance roll and all require Play mode.
  - `Debug_DriveButtons.cs`, `Debug_Player.cs` — scene-wired drive testing.
- `Player/PlayerCombatController.cs` (inert — its input handling is commented out),
  `Combat/ActionSet.cs` (**unused**; a 6-slot action holder superseded by `Character.actionSlots`),
  `PlayerControls.cs` (generated input).

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
    instance's buffs). `CharacterManager.GetModifiedAttackPower()` applies the Drive multiplier on
    top of the already-buffed `AttackPower`, but **nothing calls it** — see `TODO.md`.
  - `reputation` is still on `Character`; it's authored, zeroed, and never read.
- **Class-driven stats (Phase E).** A `ClassDefinition` asset supplies **every** stat a character
  has, scaled by level. `Character` itself holds no numbers.
  - **A class is REQUIRED, and `Character` carries no stats of its own at all.** Health, accuracy,
    defense, speed, the three drive rates, `buffNextActionMultiplier` and status resistances were all
    removed from `Character` — they live only on `ClassDefinition`. `Character` is now identity and
    content: name, id, art, audio, action slots, level, allegiance, class.
  - **There is deliberately no per-character override.** Two characters of the same class at the same
    level have identical numbers and differ only in name, art, audio and action slots. If a named
    character ever needs different numbers, **give it its own one-off `ClassDefinition`** rather than
    reintroducing per-character deltas.
  - **There is no `CharacterClass` enum — the `ClassDefinition` asset reference IS the class
    identity.** Adding a class is creating an asset: no code change, no append-only hazard, nothing
    to keep in sync. The enum and `Character.characterClass` were deleted because they stored the
    same fact a second time, which is the only reason the two could ever disagree.
  - `Character.ValidateClassSetup()` errors on a **missing class** — the character would resolve to
    zero everything and be clamped to 1 HP. Logged from `CharacterManager.Start()`.
  - **Class-gated actions: `Action.allowedClasses`**, a `ClassDefinition[]` where **empty means
    anyone**. That default is what keeps it cheap — most actions are unrestricted, so you fill it in
    only for the class-defining ones, and adding a class never means revisiting them.
    - **Nothing enforces it at combat time, deliberately.** `ActionsManager` builds the bar straight
      from `actionSlots`, and refusing to draw an authored slot would present as a missing action
      rather than the authoring mistake it is. `Character.ValidateActionSlots()` warns instead.
      Gating becomes a real constraint at Phase 5's loadout screen, which is the first point a
      *player* chooses what goes in a slot.
  - **Read resolved values, never a class field directly.** `Character.ResolveMaxHealth(level)` and
    friends, or the `CharacterManager` properties (`BuffNextActionMultiplier`,
    `DamageTakenDriveMultiplier`, `ParryBonusDriveMultiplier`, …). Going straight to the
    `ClassDefinition` skips level scaling, and going to `characterData` no longer finds anything —
    which is why `DriveManager` and `ParrySystem` were changed to use the resolved properties.
  - **Only health, attack, defense and speed scale with level.** The drive multipliers and
    resistances are flat class traits: they're identity dials, not power curves, and compounding
    them with level makes a high-level character disproportionately better at everything at once.
  - **Resistances come from the class alone**, clamped to 0-1 at the `GetResistance()` accessor —
    which stays the single read point, so equipment and armour can layer in there later.
  - **`CharacterManager.Start()` order is load-bearing: `ResolveCrewState()` → `RefreshStats()` →
    `ApplyStartingHealth()`.** The crew record carries the *level* stats resolve at, and
    `RefreshStats` sets the `MaxHealth` that starting health is clamped against. Get it wrong and
    crew load at the wrong health because they were sized at level 1 — which presents as a save bug.
  - Level comes from `CrewMemberState.level` for crew in a run, and the authored `Character.level`
    otherwise (enemies, and crew played outside a run). Floored at 1.
  - **Enemies get the whole system for free**, since `Enemy` subclasses `Character` — the class
    reference, `level`, and every resolver. Because enemies never bind to run state they always use
    the authored `level`, so one class asset plus level 1 / 3 / 5 Enemy assets is a scaling family
    with the numbers authored once.
    - **`Enemy`'s AI config is NOT covered** — action weights, targeting strategy, drive config and
      plunder live on `Enemy` itself, so behaviour stays per-asset even when stats are shared.
    - **Enemy classes need nothing special.** There's no archetype label to fill in any more, so a
      "Skeleton Grunt" class is just an asset with numbers — it never has to claim to be a crew
      archetype. (An earlier `CharacterClass.None` existed for exactly that problem and went with
      the enum.)
  - **Save format is unchanged** — `RunState` stores a `characterId` and the Character asset carries
    the class reference, so `ClassDefinition` needs no id in `ContentDatabase` and no `saveVersion`
    bump.
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
  - **Anything that skips a turn must go through `TurnManager.AdvancePastSkippedTurn()`, never call
    `CompleteTurn()` directly from `SetCharacterTurn()`.** The two are mutually recursive —
    `SetCharacterTurn → CompleteTurn → SetCharacterTurn` — so a skip condition true of every
    combatant recurses until the process dies. It happens inside one frame, so nothing can clear the
    condition: no `Update`, no destruction of the dead, no battle-end poll. **This has killed the
    Editor once** (93 MB log). `AdvancePastSkippedTurn` caps consecutive skips at one lap of the
    turn order and stops with a single error.
  - **`CurrentHealth <= 0` does not mean "dead".** It also means **"`Start()` hasn't run yet"** —
    `BindToRunState()` is what fills it in, and `BeginBattle()` runs during the Start phase where
    Unity's ordering between GameObjects is arbitrary. Testing health alone read every
    not-yet-initialised character as a corpse, which is precisely what caused the crash above. Gate
    any death test on something that proves damage was actually dealt.
  - **`CombatController.PerformAction()` charges the cooldown and calls `CompleteTurn()` from a
    `finally`.** Resolution and handover used to be a plain sequence, so anything that threw while
    applying an effect skipped both. That does not stall the game — it hands out a **free extra
    action**: the selection is still live and the buttons are still interactable, so the player just
    picks something else, off cooldown. A broken feedback reference on one `Action` therefore became
    a balance exploit that also hid its own stack trace. Acting costs the turn whether or not the
    effect resolved cleanly. The null-target guard sits *above* the try on purpose — a click that
    never became an action must not cost anything.
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
- **d20 combat resolution:** roll 1–20. `1` = critical miss, `20` always hits, otherwise
  `roll + attackPower >= target defense` to hit. A hit crits (double dmg) when
  `roll >= CharacterManager.CritThreshold`. Player side in `CombatController.PerformAction()`,
  enemy side in `EnemyManager.RollToHit()` — keep the two rules identical if you change either.
  - **"Natural 20 always hits" and "this roll crits" are two different tests**, and only the second
    moves. The auto-hit check stays pinned at 20; `CritThreshold` starts at 20 and drops one per
    point of `BuffType.CritChance`. So a buffed 18 can crit — but it still has to beat the target's
    defense to land, which is what makes it impossible to crit on a miss.
  - `CritThreshold` is clamped to `[11, 21]`: 11 caps buffed crit chance at 50%, and 21 is
    deliberately reachable — no d20 face satisfies it, so a negative `CritChance` buff switches
    crits off rather than wrapping around. `CritChancePercent` is the same thing for tooltips.
  - **Crit applies to healing too**, on the same threshold, or the buff would silently mean nothing
    in a support character's hands.
  - **The enemy roll is separate from the crit roll.** `EnemyManager` rolls twice: `RollToHit()`
    decides whether the attack lands, `RollForCritical()` decides whether it doubles — the latter
    now returns a bool and applies the threshold itself, so no call site re-tests `== 20`. The
    player's single roll decides both. That asymmetry is still logged in `TODO.md` and a
    `CritChance` buff makes it slightly more visible: an enemy's crit is gated behind passing a
    separate to-hit roll first.
  - **A missed enemy attack skips the parry sequence entirely** rather than opening a window over
    nothing. A parry attempt is spent whether or not it lands, so making the player burn theirs on
    an attack that could never connect would be a loss with no decision in it.
  - **`BuffType.DamageReduction` is the protection buff and is the one buff type that isn't a stat.**
    Every other type is a flat additive modifier folded into `CharacterManager.RefreshStats()`; this
    one is a **fraction** applied in `TakeDamage()` at the moment damage lands, because there is no
    "damage taken" stat for it to modify. **Don't route it through `RefreshStats()`.**
    - Authored on `Action.buffValue` as a fraction (0.25 = 25% less), unlike the flat points every
      other type uses. Stacks additively, clamped to `[-1, 0.9]` — protection can never reach
      immunity, and a vulnerability (the negated Debuff case, which comes for free) can at worst
      double incoming damage.
    - Applied **first thing** in `TakeDamage`, so the floating number, the health loss, the drive
      earned and the return value all agree on what actually got through. It therefore reduces the
      drive gained from being hit as well — protection that cut damage but paid full drive would be
      a pure win, and the parry path already pays drive on damage taken rather than damage swung.
    - **Multiplicative with a parry, not special-cased.** A parried hit arrives already cut to its
      25% chip and protection applies on top; they're independent mitigations.
    - It also silently reduces what a `HealthDrain` attacker heals, since that's derived from health
      actually lost. That's correct, and free.
  - **`BuffType.CritChance` is authored in flat d20 points, not percent** — each point is one more
    face that crits, so `+2` means 15% instead of 5%. Points rather than a percentage because a
    percentage can only ever land on a multiple of 5, so "12%" would silently become 10% or 15%.
    Tooltips render it as the percentage it buys, since `+2` means nothing to a player.
  - **`Action.autoTargetSelf`** resolves an action on its caster with no target click and ends the
    turn — for self-buffs and guard actions where clicking your own portrait is busywork. Handled in
    `CombatController.SelectAction()` so it covers every route in, and checked *before* the
    automatic-targeting branch, since "cast on me" is a stronger statement than "pick at random".
    It needs a `TargetType` the caster is legal for; on a `SingleEnemy` action it warns and falls
    back to manual targeting rather than erroring, because an action that eats the turn without
    resolving is worse than one that just asks for a target. Player-side only — enemies already
    resolve ally-targeted buffs onto themselves.
- **Status effects (Phase D1): bleed / poison / burn, as riders on an attack.** A separate
  `activeStatuses` list on `CharacterManager` — **not** more `BuffType` members, because `BuffType`
  means "additive stat modifier folded into `RefreshStats()`" and two exceptions have already been
  carved out of that rule. Authored as a `List<StatusApplication>` on `Action`.
  - **Riders roll only on a landed hit, and a successful parry prevents them.** The parry still eats
    the 25% chip but stops the bleed outright, which is what makes parrying a status attack better
    than absorbing it. Four application sites — `CombatController`'s Attack and HealthDrain branches,
    `EnemyManager.OnAttackComplete` (guarded on `!wasParried`) and `PerformDirectAttack`.
  - **Effects refresh, never stack.** Re-applying a kind takes the higher of each value, including
    duration, so a weak late application can't cut short a strong one still running.
  - **A DoT tick owns its own presentation.** `TickDamageOverTime()` writes the floating text
    *before* calling `TakeDamage`, which deliberately skips its own text write for `OverTime` — only
    the tick knows which effects are running, and a bare `-3` is indistinguishable from a normal hit.
    The text names them and colours them per kind via `StatusEffectStyle`, using **TMP rich-text
    tags rather than `actionStatusText.color`**, because that component is shared with the damage
    and heal numbers and a tint would leak into the next one.
    - It also logs a `[DoT]` line on every tick. Without it a tick was completely silent: it skips
      the drive block, which held the only `Debug.Log` on that path.
    - **Tools > Pirate Kingdom > Debug** has Bleed/Poison/Burn/Stun Selected Character, Bleed and
      Stun All Enemies / All Crew, and Clear All Status Effects — for exercising the system without
      authoring an action and landing an attack first. All bypass the resistance roll, and all
      require **Play mode**: combatants are spawned at runtime, so nothing exists to afflict in edit
      mode.
    - The "Selected Character" items **fall back to whoever holds the turn** when nothing suitable is
      selected. That fallback exists because the selection requirement is a real trap — spawned
      combatants sit nested under the Combatants spawn points, and clicking one in the *Game* view
      doesn't set Selection at all, so it's easy to invoke these with nothing selected and conclude
      the whole system is broken when the tool never reached a character. It happened.
    - `CharacterManager` carries the same thing as a `[ContextMenu]`, but that hides on the
      *component's* ⋮ button and is easy to miss — the Tools menu is the reliable route, same
      reasoning as `RunManager`'s tools.
  - **`DamageSource.OverTime` skips both protection and drive.** Armour stops blades, not poison — so
    DoT is the designated counter to a heavy-protection build. And drive is paid on the initial hit
    alone, so an attack dealing 0 that only applies a bleed generates no drive at all; that's the
    authored trade-off. `TakeDamage` takes the source as an enum, not a third bool.
  - **Total DoT per turn is capped at `MaxDoTFractionPerTurn` (15%) of max health.** Effects refresh
    rather than stack, but bleed, poison and burn can all tick at once without "stacking" and bury a
    crew member. A fraction, not a flat number — the roster spans 20 to 90 health.
  - **A rider always applies unless the target resists it.** Actions carry no chance of their own —
    `StatusApplication` has `kind`, `damagePerTurn` and `turns`, and nothing else. One roll, against
    `1 − resistance`.
    - **`chanceToApply` was removed because subtractive resistance didn't scale.** Under the old
      `chance − resistance`, a given resistance was worth wildly different amounts depending on the
      action it met: 25% resistance ate a quarter of a 100% rider but *all* of a 25% one, producing
      silent immunity from two innocuous numbers meeting. With one dial that can't happen, and 25%
      resistance always means exactly 25% fewer applications.
    - **Consequence for authoring:** damage and duration now carry *all* the differentiation between
      two riders of the same kind, so those ranges have to spread more than they otherwise would.
    - **Band resistances boldly — 0.4–0.6 where a class is meant to resist something.** Below ~0.3
      it's barely felt: at 0.2, across a fight with three applications, resistance stops nothing at
      all about half the time.
  - **Resistance reduces the CHANCE only**, never damage or duration, so the outcome is binary — full
    effect or nothing. Read through `CharacterManager.GetResistance()` and **never off
    `characterData` directly** — that single accessor is where the class baseline and the later
    equipment layer slot in. Resistance is deliberately *not* buffable.
  - **Everyone ticks together at the top of the round**, in `TurnManager.AdvanceToNextRound()` —
    not per-character at the start of their own turn. **The order inside that method is the point:**
    tick → `GetTurnOrder()` → `SetCharacterTurn()`. Statuses resolve *before* initiative is rolled,
    so anyone who bleeds out is simply absent from the new order and never has to be skipped
    mid-round. It also reads as one clear beat rather than dribbling out across the round.
    - `GetTurnOrder()` filters on **`CharacterManager.IsAlive`**, which is what makes that work: the
      corpse is still in the scene at that moment, because Unity doesn't destroy it until the dying
      object's next `Update()` and that can't run inside the synchronous chain.
    - **Use `IsAlive`, never `CurrentHealth <= 0`.** An uninitialised character also reads 0 health,
      and filtering those would empty the turn order at battle start — that comparison is what
      crashed the Editor. `IsAlive` treats not-yet-initialised as alive on purpose.
    - `RefreshStats()` is called before each tick, since the cap is a fraction of `MaxHealth` and the
      tick now runs *before* `GetTurnOrder()`, which used to be what refreshed stats each round.
    - **The round transition is a coroutine**, with `statusTickDelay` before the tick and
      `statusTickSettleDelay` after it (both serialized on `TurnManager`). The first gives the tick
      its own beat instead of landing on the tail of the previous turn; the second stops the next
      character's turn taking the screen back before the damage numbers can be read. This is a
      precursor of Phase C's `TurnSequence` and should fold into it.
    - **`Update()`'s "current character died" branch is suppressed while
      `roundTransitionInProgress`.** During the pause `currentCharacterTurn` still points at the
      *previous* round's last character, who may have just died — without the guard that branch fires
      every frame, each one calling `CompleteTurn()` and starting another round transition. Same
      log-spam death spiral as before, through a different door.
    - **Stun is the exception: turn-scoped, not round-scoped.** `AdvanceStatuses()` deliberately
      skips anything where `StatusEffect.IsTurnScoped`, and `ConsumeStunForSkippedTurn()` spends it
      at the moment the turn it steals would have happened. Expiring it on the round tick would burn
      a 1-turn stun before its owner ever reached a turn to lose.
    - **The stun resistance ramp is self-correcting and needs no cap.** Each consecutive skipped turn
      adds +50% (`StunResistanceStacks`, folded into `GetResistance(Stun)`), and
      `ClearStunResistanceRamp()` resets it the moment the character actually acts. Once the ramp
      exceeds the incoming chance the stun fails, they act, and acting clears it — so it can neither
      reach permanent immunity nor be chained indefinitely.
    - **`TurnManager.AcceptingPlayerInput` is false during a round transition or a stun skip**, and
      is checked both in `CombatController.IsPlayerControlledTurn()` and at the top of
      `CompleteTurn()`. Through both pauses `currentCharacterTurn` lags reality and the action bar is
      still showing whatever was last loaded — so a crew member's buttons stay live, and an action
      (or the End Turn button, which calls `CompleteTurn` directly) would collide with the hand-over
      already in flight. **This is a fourth turn-ownership layer; don't remove it.**
    - Buffs still tick at the end of each character's **own turn**. The two schedules differ, but
      the durations are numerically equivalent — every character gets exactly one turn per round.
  - **Stealth (`StatusEffectKind.Stealth`) — the first BENEFICIAL status, and the first that changes
    what's *legal* rather than dealing damage.** A hidden character can't be targeted by a hostile
    action, but can still be healed and buffed — which is the whole reason to hide a support
    character.
    - **Hostility derives from `Action.IsHostile`, which reads `ActionType` — never `TargetType`.**
      That's load-bearing: `TargetType` is read caster-relatively by enemies, so an enemy's
      "SingleEnemy" points at the crew, whereas an Attack is hostile whoever swings it. `ApplyStatus`
      is judged by its payload instead: a stealth cast is friendly, a poison dart isn't.
    - **Beneficial kinds skip the resistance roll** (`StatusEffectRules.IsBeneficial`) — being good
      at shrugging off poison must not make an ally's stealth harder to receive.
    - **Stealth breaks when its bearer acts hostilely**, on both sides. Without that a hidden
      character attacks with impunity for the duration, which isn't a defensive tool — it's a win.
      Friendly actions leave it intact.
    - **Turn-scoped, like stun** — `turns` counts the bearer's OWN turns, and
      `ConsumeStealthForCompletedTurn()` spends one from `OnTurnComplete()`. So `turns: 1` means
      "hidden until the end of your next turn", which always covers a full round of enemy turns.
      **Round-scoping it was a real bug:** stealth is applied part-way through a round, so expiring
      at the round tick would have covered every enemy turn if the caster acted first and *none at
      all* if they acted last. A skipped turn still spends it — being stunned shouldn't extend how
      long you stay hidden.
    - **A turn already underway can't consume it** (`StatusEffect.SkipNextTurnConsumption`, granted
      by `GrantGraceIfAppliedDuringOwnTurn` when the bearer is the acting character). Without that a
      **self-cast** is applied and consumed inside one frame — `OnTurnComplete` fires for the very
      turn it was cast in — so it never visibly happens at all. Ally-casts don't get the grace,
      since their turn hasn't started, and both routes end up protecting for about one round.
    - **Two presentation channels, mirroring buffs exactly** — `stealthFeedback` (`MMF_Player`,
      one-shot burst on entering stealth, like `buffFeedback`) and `stealthParticles`
      (`ParticleImage`, sustained for the duration, like `buffParticles`). **The types are not
      interchangeable:** `ParticleImage` genuinely holds between `Play()` and `Stop()`, whereas
      `MMF_Player.PlayFeedbacks()` runs its feedbacks once through. Using an MMF_Player alone for a
      sustained effect was a bug and read as "stealth has no feedback at all".
    - **A "Visible" line is written to `actionStatusText`** — the same floating text the parry and
      stun messages use — whenever stealth ends, by any route. Driven from the same transition as the
      feedback, so it can't be missed at one of the three exits.
    - Both are driven from `NotifyStatusesChanged()` — the single funnel every status mutation goes
      through. Stealth ends three different ways (turn spent, broken by acting, debug clear), so
      deriving the effect from current state in one place is what stops one of them leaving it
      running forever.
    - **Enforced in exactly two places**: `CombatController.IsValidTarget` (which
      `ActionTargetingLine` and `TooltipUI` both now defer to) and `EnemyManager.SelectTarget`.
    - **`EnemyManager.FilterHiddenTargets` falls back to attacking a hidden target when *every*
      candidate is hidden.** Otherwise a fully hidden party makes every enemy forfeit its turn, which
      stalls the fight rather than adding tension. Single-ally-only stealth makes that hard to reach
      but not impossible — three crew hiding each other in sequence gets there.
    - `TooltipUI` no longer keeps its own copy of the targeting rules. It did, and stealth is exactly
      what made the duplicate bite: hover said "valid" while the click refused.
  - **`ActionType.ApplyStatus` applies riders and nothing else** — no damage, no to-hit roll. Generic
    rather than an `ActionType.Stealth` so future beneficial statuses (regeneration, haste) cost
    nothing but an enum member. For a status that *can* miss, use an Attack with 0 damage and a rider
    — that path rolls to hit and this one doesn't.
  - Statuses are **per-battle** — nothing reaches `RunState`, exactly like buffs.
  - **Buffs and debuffs have separate feedback channels**, two each: a one-shot `MMF_Player` cast
    burst (`buffFeedback` / `debuffFeedback`) and a sustained `ParticleImage`
    (`buffParticles` / `debuffParticles`). Separate rather than shared because the "you got stronger"
    flourish is the wrong signal for being weakened. **Debuffs originally fired neither** — both gates
    were `amount > 0f` — so a Slow landed completely silently.
    - The two sustained particles are tracked independently, so a character carrying a +2 Accuracy
      and a −3 Speed at once shows both. Each is null-guarded separately: an early return on a
      missing `buffParticles` used to skip the debuff one entirely.
  - **Every buff type has a working debuff form for free.** `ActionType.Debuff` negates `buffValue`
    before calling `AddBuff`, on both the player and enemy paths, so Speed, Defense, Accuracy,
    Crit Chance and Damage Reduction all debuff with no extra code — author them as `Debuff` with a
    positive `buffValue`. There is no separate debuff enum and there should not be one.
    - **`buffValue` is `[Min(0f)]` for that reason.** A negative value on a `Debuff` double-negates
      into a *buff* — a "Slow" authored at `-3` speed made its target **faster** — and nothing about
      the number looks wrong until you check the sign. The clamp only applies as the Inspector draws
      the field, so an already-saved negative survives until that asset is opened and re-entered.
  - **`attackPower` affects accuracy only, never damage** — and so do `BuffType.Accuracy` buffs,
    which stack onto it in `RefreshStats()`. **Damage bonuses are drive's job alone.** Keeping the
    two disjoint is a deliberate decision: letting accuracy buffs also raise damage was tried and
    reverted, because it let one character compound a buff and a drive spend into a single damage
    number and duplicated a system that already worked.
  - `CharacterManager.GetModifiedAttackPower()` applies the drive multiplier to attack power but
    **has no callers**; drive reaches damage through `GetNextAttackDamageMultiplier()`. See `TODO.md`.
  - **`HealthDrain` uses the same d20 rule** — it's an attack that also heals, so the rule is
    written out in full in `CombatController.PerformAction()` and reached via `RollToHit()` on the
    enemy side, exactly like `Attack`. **If you change the to-hit rule, there are now four places to
    keep identical, not two.**
    - The heal is a share of the health the target **actually lost**, not of the damage rolled.
      That's what `CharacterManager.TakeDamage()`'s return value is for — it reports rounded damage
      capped by remaining health, so a killing blow on a 3hp target drains 3, not 20. Health moves
      between the two combatants; it is never created.
    - **Drive multiplies it exactly once.** Drive scales the damage, and the heal is derived from
      the damage, so `Heal()` is deliberately called with the default `1f` multiplier. Passing
      `GetNextHealingMultiplier()` as well would pay one drive spend out twice.
    - Authored as `Action.healthDrainRatio` (0–1). **Rounding is the trap here** — `Heal()` rounds,
      so a low ratio against a small damage range heals 0 and reads as broken. Sanity-check against
      `minDamage`, not the midpoint. Same failure mode as the drive multipliers.
- **Drive system:** a 4-segment meter that fills from dealing damage, taking damage, and **parries**
  (per-character multipliers on `Character`).
  - **`DriveMeter` is a serialized field inside `DriveManager`, so its values live on the prefab, not
    in code.** `PlayerCharacter.prefab` currently has `maxDriveValue 400`, `totalSegments 4`,
    `regenerationPerTurn 32` — note the regen differs from the class default of 50, which is the
    proof that editing `DriveMeter.cs` does nothing to existing prefabs. Change the meter on the
    prefab. Segment size is `maxDriveValue / totalSegments`, so changing the segment count without
    changing the max silently rescales every drive-gain value in the game.
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
  - **Both sides earn drive from damage dealt**, via `CharacterManager.OnDamageDealt()` →
    `damageInflictedDriveMultiplier`. The player pays it from `CombatController`; enemies pay it from
    `EnemyManager.GrantDriveForDamageDealt()`, called on all three damage paths — direct, parryable,
    and the 25% chip that survives a parry. **The enemy side was missing entirely until it was
    fixed**, so `damageInflictedDriveMultiplier` was authored on every Enemy asset and never read,
    and enemies could only build drive from being hit and from turn regeneration.
  - **There is no death-blow bonus** — a kill pays the same as any other hit of that damage. Idea and
    its complications are logged in `TODO.md`; note `OnDeath` currently fires twice per death.
  - **Player input:** each press of the Drive action adds one stack to the selected crew member
    (`PlayerInputHandler` → `TryAddDriveStack()`). Sound + particles fire on **every** stack.
  - **Drive manipulation actions (`DriveGrant` / `DriveDrain`).** An `Action` can move raw drive on
    its target: grant it to yourself or a crewmate, or strip it from an enemy. Amount is authored
    on `Action.driveAmount`, positive for both, negated by the drain case — the same idiom as
    `Buff`/`Debuff`. Resolution is `CharacterManager.GainDrive()` / `LoseDrive()` → `DriveManager`
    → `DriveMeter.AddDrive()` / `RemoveDrive()`.
    - **Raw meter, never stacks.** Committing a stack stays the owner's own decision on their own
      turn; a support character fills the meter, they don't spend it for you.
    - **The caster's drive stacks deliberately do not multiply it.** A drive spend that makes more
      drive is a feedback loop — same reasoning that keeps accuracy buffs out of damage.
    - **Draining never claws back committed stacks.** `TryAddDriveStack()` spends the drive at
      commit time, so a stack is already paid for. It can't come up anyway: enemies commit stacks
      at the start of their own turn and spend them in the same turn, so a player's drain only ever
      reaches banked meter.
    - Both methods return the amount that **actually** moved, clamped by a full or empty meter, and
      that's the number the floating text reports — a drain on an empty enemy must read as the
      wasted turn it was. `TooltipUI.DescribeDriveOutcome()` previews the same clamped result plus
      the segment change, because **segments are the unit that buys anything**: a grant that doesn't
      cross a segment boundary gives the target nothing they can spend.
    - Enemy AI weights them via `Enemy.driveGrantWeight` / `driveDrainWeight`, read by
      `EnemyManager.GetBaseWeight()`. Without a weight entry an action still gets the `0.1f`
      fallback, so a new `ActionType` is never truly unreachable for enemies.
  - **Enemies** use the *same* stacking system: `EnemyDriveManager.EvaluateDriveUsage()` (called at
    the start of the enemy turn) picks how many stacks to commit per its `EnemyDriveConfig`
    (`strategy`, `stacksPerBuff`, `minimumSegmentsToUse`, `healthThreshold`, `usageChance`) authored
    on the `Enemy` ScriptableObject.
  - **Utility `DriveAction`s are effectively unreachable.** `HealSelf` / `RemoveDebuff` /
    `ReduceCooldown` exist as one-shot spends via `TryUseDriveAction()`, but the only player-facing
    route — `DriveUI.CreateActionButtons()` — **has no callers**, and `DriveUI` isn't in the scene.
    The only live caller is `EnemyDriveManager`'s low-health heal branch. `ExecuteReduceCooldown()`
    is additionally a **stub** that charges segments and changes nothing. See `TODO.md`, and
    "Different ways to use drive" there for the direction this could go.
  - `BuffNextAttack` now just adds a stack. `IsDriveMode` / `NextAttackBuffed` / `EnterDriveMode()`
    are retained only as `driveStacks`-backed shims — prefer the stacking API.
- **Target telegraph:** before an enemy attacks, it shows `img_target_indicator` on its victim for
  `EnemyManager.targetHighlightDuration`, then starts the attack.
  - The pause sits **before** the attack, not inside it. The windup is already the parry cue; this
    is the separate "who" cue, and folding them together blurs two signals into one.
  - `EnemyManager.TelegraphThenAct()` is **the first place an enemy turn yields**, so it's the first
    that has to cope with the target dying or the battle ending mid-flight.
  - `ClearHighlight()` runs on every exit — attack resolved, attack missed, target died during the
    telegraph, no pending target, and `OnDisable`. That last one matters: an enemy killed
    mid-telegraph would otherwise leave its victim highlighted permanently.
  - **The same indicator doubles as the player's hover target marker**, since "this character is
    about to be acted on" is one idea and deserves one symbol. It therefore has **two independent
    owners**, and `CharacterManager` tracks them as separate flags (`SetTargeted()` for the enemy
    telegraph, `SetHoverTargeted()` for hover) with visibility derived from either being set.
    **Don't collapse them back into one bool** — whichever finished last would hide the marker
    while the other still wanted it.
  - **Hover targeting is driven from `ActionTargetingLine`'s per-frame evaluation, not from
    `TargetHoverHandler`'s pointer-enter/exit events.** Exit events only fire when the pointer
    *moves*, and the normal case is clicking the target you're already hovering — so an
    event-driven version leaves the marker lit on a character nobody is targeting any more, right
    through the enemy's turn. Re-deriving it each frame from the live selection means it clears
    itself the moment `TurnManager` clears the selection.
  - **A self-cast shows no indicator.** The marker means "someone else is about to act on you";
    aiming it at yourself while self-healing reads as an incoming threat. Suppressed by comparing
    the hovered character against `CombatController.GetCurrentCharacter()`.
- **Parry:** enemy `Attack` **and `HealthDrain`** actions have a windup; `EnemyAttack` opens a
  `ParrySystem` window on the target. The routing test in `EnemyManager.PerformAction()` is
  "is this a damaging swing", not "is this an Attack" — a damaging enemy action the player can't
  parry reads as a bug. Parrying a drain cuts the enemy's *healing* as well as the damage, for free:
  the siphon is applied in `OnAttackComplete()` from health actually lost, which the 25% chip
  already accounts for.
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
  - **Tag and `Character.allegiance` are two independent notions of "side", and both are live.** The
    tag decides who can *target* this character (`CombatController.IsValidTarget`,
    `EnemyManager.RefreshTargetList`, `GameManager.FindCharacters`). Allegiance decides *turn
    ownership* (`ActionsManager` button interactability, `CombatController.IsPlayerControlledTurn`,
    whether `EnemyTurnAction()` fires), *run persistence* (only Player-allegiance binds to
    `CrewMemberState`) and *win/lose* (`TurnManager.CountAliveCharacters`).
  - One wrong dropdown therefore produces three unrelated-looking symptoms at once — targetable by
    its own side, wrong action bar, counted for the wrong side at battle end. **`CharacterManager`
    validates the two agree on `Start()`** and logs an error naming both; nothing else cross-checks
    them, and `EncounterBootstrapper` only verifies the tag against what it meant to spawn.
  - **Count aliveness with `CharacterManager.IsAlive`**, never by existence and never by
    `CurrentHealth <= 0` directly. `TurnManager.CountAliveCharacters` and `GameManager`'s two
    `GetAlive*` helpers all used to answer "exists", so a 0-health character counted until Unity
    destroyed it on its next `Update()` — which delayed every battle end by a frame.

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
