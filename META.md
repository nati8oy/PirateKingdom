# Meta Layer — Design & Implementation Plan

The plan for the layer *around* combat. Combat itself (`Encounter.unity`) is built and stays
broadly as-is; this document covers the voyage, the crew that survives across it, and progression.

See `CLAUDE.md` for how the combat systems work today, and `TODO.md` for outstanding bugs.

---

## 0. Status — read this first

**Phases 1 and 2 are complete and verified in the Editor, as is the 2.5D presentation refactor.**

**Phase C — Turn sequencing & impact effects is the next work.** Tunable time in combat: an authored
per-turn rhythm every character follows, and an authored impact (sprite animation, feedback, or both)
when an attack lands or is parried. Deliberately scoped to add time *around* resolution rather than
refactor it, so it doesn't touch damage, death or the parry system.

**Phase 3 — the voyage map** is the next *meta-layer* work and nothing blocks it; it's simply queued
behind C.

**Phase B — Balancing & action additions** runs in parallel and is the user's track: combat content
and tuning, with engineering help on request for anything needing new mechanics (damage over time,
status effects) rather than new numbers. B and C are lettered, not numbered, because neither has an
ordering against 3–6.

What exists and works today:

| | |
|---|---|
| `Assets/Scripts/Meta/ContentDatabase.cs` | `id -> asset` registry. Asset at `Assets/Resources/ContentDatabase.asset`, 8 characters + 9 actions registered and stamped. |
| `Assets/Scripts/Meta/RunState.cs` | `RunState` + `CrewMemberState`, plain serializable C#. |
| `Assets/Scripts/Meta/RunManager.cs` | Owns the live run, JSON save/load at `Application.persistentDataPath/run.json`. Scene object in `Encounter.unity`. |
| `Assets/Scripts/Meta/EncounterDefinition.cs` | What one fight *is*: which enemies, how many, display name, plunder reward. |
| `Assets/Scripts/Meta/EncounterBootstrapper.cs` | Spawns crew (from `RunState`) and enemies (from the definition), then starts the battle. |
| `Assets/Scripts/Meta/EncounterResult.cs` | The report one encounter produces — survivors, casualties, plunder. |
| `CharacterManager.AssignCrewState()` | The spawner binds each crew member to its run record explicitly. |

The loop that works right now: press Play in `Encounter.unity` → crew and enemies both spawn, crew at
their carried health → fight → win → health, deaths and plunder persist in one write → press Play
again and the crew are as you left them, minus the dead.

**Editor tooling** (both under `Tools > Pirate Kingdom`, because `[ContextMenu]` entries are easy
to miss — on a MonoBehaviour they're on the *component's* ⋮ button, on a ScriptableObject they're
in the Inspector's ⋮ with the asset selected, and **never** on a Project-window right-click):

- **Rebuild Content Database** — re-scan and stamp ids. Run after adding/deleting content assets.
- **Delete Run Save** — wipe the save so the next Play rebuilds from `Debug Starting Crew`.
- **Log Run Save Path** — prints the save location and whether it exists.

**Gotcha that has already cost time twice:** an existing save always beats the RunManager's
`Debug Starting Crew` list, so edits to that list appear to do nothing until you delete the save.
The console states which path was taken on every play.

**The 2.5D presentation refactor is DONE** (§6). Combatants are world sprites with a World Space
`CharacterUI` canvas each, the camera is orthographic, parallax layers are in, and spawn points have
moved out of the Canvas into a world-space `Combatants` root. Phase 3's map UI can now be authored
against a settled convention.

---

## 1. Design pillars

Three decisions that everything else follows from:

**Roguelite voyage.** A branching sea chart. Every node loads `Encounter.unity`, so the existing
combat carries the whole game. A crew wipe ends the run; you restart with permanent unlocks.

**HP carries, permadeath.** Damage persists between fights. A crew member who drops is gone for
the remainder of the run. Attrition is the core pressure.

**Progression on three axes.** Action loadout (slot earned `Action` assets into the 6 slots),
crew levels (XP grows stats), and recruitment (hire replacements mid-run). No ship upgrades.

The through-line: **the run is your crew.** You arrive with four, you lose some, you replace them
with worse or stranger ones, and the crew you reach the boss with is the story of the run.

---

## 2. The blocking problem: ScriptableObject state — SOLVED in Phase 1a

Kept here because it's the constraint the whole architecture is shaped around, and it must not be
undone.

`Character` used to hold mutable state — `activeBuffs`, `actionCooldowns` — as instance fields
*on the ScriptableObject*. That was already a logged bug (two Skeletons share `Skeleton.asset`),
and the meta layer made it fatal, because the meta stores current HP, level, XP and loadout per
crew member.

Had that state stayed on the SO:

- In the Editor, writes to a ScriptableObject **persist into the project asset**. `Black Sam.asset`
  would accumulate damage, levels, and loadout changes across play sessions — your authored data
  drifting every time you press Play.
- Permadeath would be unimplementable — "dead" is a property of *this run's* Black Sam, not of the
  Black Sam asset.
- Two crew from the same asset (or two Skeletons) would share one health pool.

**The rule now, permanently:** `Character` / `Enemy` / `Action` are **authored data, never written
to at runtime.** Per-battle state lives on `CharacterManager`; per-run state lives in `RunState`.

> The one exception still outstanding is `Character.reputation` — authored, zeroed by
> `Initialize()`, never read. See the open question in §7.

---

## 3. Data model

Plain `[Serializable]` C# classes — not ScriptableObjects, not MonoBehaviours. This is what gets
JSON-serialized to disk. **As built** (`Assets/Scripts/Meta/RunState.cs`):

```csharp
[System.Serializable]
public class RunState
{
    public int saveVersion = CurrentSaveVersion;  // discard incompatible saves rather than half-load
    public int plunder;                            // run currency
    public List<CrewMemberState> crew;             // living + dead; dead kept for the run summary
    // Phase 3 adds: seed, MapGraph map, currentNodeId, lootPool
}

[System.Serializable]
public class CrewMemberState
{
    public string characterId;                     // -> Character asset, via ContentDatabase
    public string displayName;                     // generated crew get their own rolled name
    public float currentHealth;
    public int level = 1;
    public int xp;
    public List<string> actionLoadout;             // <= 6 Action ids
    public bool isDead;                            // permadeath — set once, never cleared
}
```

Adding fields later is cheap (absent fields take their defaults on load). Renaming or removing
them is not — that's what `saveVersion` is for.

### Asset lookup: `ContentDatabase`

`RunState` stores **string ids, not object references** — object references don't survive JSON.
`ContentDatabase` holds the authoritative lists and resolves `id -> asset` both ways.

Ids are stamped once from a sanitised asset name (`Black Sam` -> `black_sam`) and **never
overwritten**, which is what makes renaming an asset safe. Never key save data off
`characterName` or the asset filename.

### Stat resolution

Currently: `CharacterManager.RefreshStats()` = authored base from the SO + this instance's buffs.
Level is stored but **not yet applied** — Phase 5 folds it in, roughly:

```
effectiveMaxHealth = characterData.maxHealth + (level - 1) * growth.healthPerLevel
```

`CharacterManager` reads its `CrewMemberState` on spawn and never writes back to `characterData`.

---

## 4. Flow architecture

```
  Boot ──▶ Voyage.unity ◀────────────┐
             │  pick a node          │  EncounterResult
             ▼                       │  (survivors' HP, deaths, XP, plunder)
        Encounter.unity ─────────────┘
             │
             └─ crew wipe ──▶ Run Summary ──▶ meta unlocks ──▶ Boot
```

**`RunManager`** — new `DontDestroyOnLoad` singleton owning the live `RunState`. Handles save/load
and hands an `EncounterRequest` to the combat scene / receives an `EncounterResult` back.

**`EncounterDefinition`** (ScriptableObject) — what a fight *is*: which enemies, how many, spawn
positions, reward tier. Node types (Fight / Elite / Boss) pick from pools of these.

**`EncounterBootstrapper`** — lives in `Encounter.unity`. On load: reads `RunState`, spawns crew
prefabs for living crew, spawns enemies from the `EncounterDefinition`, then explicitly calls
`TurnManager.BeginBattle()`.

> **Ordering gotcha — done.** `TurnManager` now exposes `BeginBattle()`, and `Update()`
> early-returns until it has run (otherwise a spawning scene counts zero living players on frame
> one and instantly declares a defeat). `Start()` self-starts only while the serialized
> `autoStartBattle` is ticked, which keeps today's hand-placed `Encounter.unity` working. The
> bootstrapper unticks it and calls `BeginBattle()` once spawning is done.

### Save

`RunState` → JSON via `JsonUtility` → `Application.persistentDataPath`. Write on every node
transition and on battle end. Feel ships `MMSaveLoad`, but plain `JsonUtility` is fewer moving
parts and no third-party coupling; recommend that unless you want MMSaveLoad's encryption.

---

## 5. Cross-scene singletons — now a real problem

Three components call `DontDestroyOnLoad` and have only ever run in a single scene that never
reloads. Once the player bounces between `Voyage` and `Encounter` a dozen times per run, their
duplicate-handling gets exercised hard:

| Component | Status |
|---|---|
| `GameManager` | **Done (2d).** Destroys duplicates, and the static `PlayerCharacters` / `EnemyCharacters` lists now re-populate on `SceneManager.sceneLoaded` and after the bootstrapper spawns. `OnEnable`/`OnDisable` are guarded on `instance != this` so a duplicate can't unsubscribe the real one. |
| `TooltipUI` | Destroys duplicate GameObjects. Fine. |
| `ParryInputManager` | **Done.** Now a single GameObject in `Encounter.unity`, off the character prefab. `DontDestroyOnLoad` removed (an instance surviving into the map scene would still hold the shared input action when the next encounter loads its own), `Instance` resolves lazily so it can't lose a race with spawning, dead registration list deleted. |

All three are handled. `RunManager` is a fourth `DontDestroyOnLoad` singleton, added in Phase 1c —
it destroys duplicates and resolves `Instance` lazily, and is the one that's *meant* to survive
between the map and an encounter, since it owns the run.

---

## 6. Phases

Each phase should be playable/verifiable before the next. UI is yours throughout — I do the code
side and tell you what to wire.

**How this has been working, and should keep working.** Phases are cut into slices (1a/1b/1c) that
each leave the project in a playable, testable state. Code lands, the user play-tests in the
Editor, and only then does the next slice start. Nothing here is verified by compilation or
automated tests — there are none — so *"code complete"* and *"verified"* are tracked separately in
this document, and no slice is marked ✅ until the user confirms it in the Editor.

### Phase 1 — Run state foundation ✅ COMPLETE

**1a — Runtime combat state off the ScriptableObjects. ✅ DONE.**
`activeBuffs` and `actionCooldowns` moved from `Character` to `CharacterManager`; `Character` is
now authored data only. Everything that queries buff/cooldown state takes a `CharacterManager`.
*Closed three `TODO.md` bugs along the way:* the shared-ScriptableObject-state bug, the
round-end double-tick of cooldowns, and enemy cooldowns never starting.

*Verify:* damage/debuff one Skeleton — the other should be unaffected. Buff durations should tick
once per turn, not twice. `Skeleton.asset` on disk should be unchanged after a play session.

**1b — Ids + `ContentDatabase`. ✅ DONE, verified in Editor.**
`Character` and `Action` each gained a serialized `id` with a public `Id` getter, and
`Assets/Scripts/Meta/ContentDatabase.cs` resolves `id -> asset` in both directions. Purely
additive — nothing reads it yet, so no behaviour change.

Ids are stamped from a sanitised asset *name* (`Black Sam` -> `black_sam`) rather than the asset
GUID, so save files stay readable. Stamping happens in an explicit `Rebuild From Project` context
menu rather than `OnValidate`, because `AssetDatabase` calls during validation are an import-time
hazard. **Existing ids are never overwritten** — that would silently invalidate saved runs — so
renaming an asset after its first stamp is safe.

*Editor step — user's domain:* create `Assets/Resources/`, add a Content Database asset there via
**Assets > Create > Scriptable Objects > Content Database** (it must be named `ContentDatabase`,
since it's found by `Resources.Load` with no scene wiring), then run
**Tools > Pirate Kingdom > Rebuild Content Database**. Re-run it after adding or deleting any
Character/Enemy/Action asset.

> The same rebuild is also a `[ContextMenu]` on the asset, but note those only appear in the
> **Inspector's ⋮ menu** with the asset selected — never on a right-click in the Project window.

*Verify:* the console reports 8 characters and 9 actions stamped, and the assets show their new
ids in the Inspector.

**1c — `RunState` + `RunManager`. ✅ DONE, verified in Editor.**
`Assets/Scripts/Meta/RunState.cs` defines `RunState`/`CrewMemberState`; `RunManager.cs` owns the
live run and its JSON persistence at `Application.persistentDataPath/run.json`.
`CharacterManager.BindToRunState()` takes starting health from the crew record, and health/death
are written back.

Design notes:
- **Everything tolerates there being no run.** No `RunManager`, no save, or no matching crew
  record all fall back to full health, so pressing Play straight into the encounter scene still
  works. That fallback is what keeps the project playable through Phases 1–2.
- **Enemies never bind.** They're per-encounter; nothing about them persists.
- **Binding by `characterData.Id` is a temporary bridge** for scene-placed crew. It can't tell two
  crew sharing one Character asset apart. Phase 2's spawner assigns `CrewMemberState` explicitly
  and this goes away.
- **Saves happen at encounter boundaries, not continuously.** Health syncs in memory on every
  damage/heal, but the disk write only happens on death and at `EndBattle`. This is deliberate:
  battle state (enemy health, buffs, turn order) isn't serialised, so a mid-fight save would
  reload with the crew damaged and the enemies back to full. Per-encounter saving means quitting
  mid-fight rewinds cleanly to the start of that encounter, and closes the save-scum hole where
  you quit to undo a bad turn. Resuming *inside* a battle would mean serialising the whole
  encounter — a much larger job, and not planned.
  - Consequence for testing: taking damage and pressing Stop **will not** persist. Finish the
    battle first, or watch `currentHealth` change live in the RunManager Inspector.
- `RunState.saveVersion` exists so incompatible old saves are detected and discarded rather than
  half-loaded. Bump `CurrentSaveVersion` on any breaking format change.
- Neither Meta file uses `using System;` — it would make `System.Action` collide with the game's
  own `Action` ScriptableObject. Keep it that way.

*Editor step — user's domain:* add a `Run Manager` GameObject to `Encounter.unity`, and populate
its **Debug Starting Crew** list with the crew assets placed in the scene (Black Sam, Kolo Aka,
Biku Bale). `Start Debug Run If No Save` should stay ticked until the voyage map exists.

> **An existing save always beats Debug Starting Crew.** `Awake()` loads the save if there is one,
> so editing that list looks like it does nothing. Reset it with
> **Tools > Pirate Kingdom > Delete Run Save** (works while not playing, and needs no RunManager
> instance), then press Play. The console states which path was taken on every play.
>
> The same actions also exist as `[ContextMenu]` entries on the component — but note those only
> appear on the **component's ⋮ button** in the Inspector, not on the GameObject and not in the
> Project window. The Tools menu is the reliable route.

*Verify:* play once and take damage, then stop and play again — crew should return at the health
they finished on, visible live in the RunManager Inspector. Kill a crew member and confirm
`isDead` flips and persists. Confirm `Black Sam.asset` on disk is untouched throughout. Use
**Delete Save** on the RunManager to reset.

### Phase 2 — Parameterized encounters ✅ COMPLETE

Goal: `Encounter.unity` stops being a hand-authored fight and starts **building itself** from
(a) an `EncounterDefinition` for the enemies and (b) `RunState` for the crew. That's what lets one
scene serve every node on the voyage map.

**Groundwork already done** (during Phase 1, don't redo it):
- `TurnManager.BeginBattle()` exists, is idempotent, and snapshots turn order. `Update()`
  early-returns until it runs — without that, a spawning scene counts zero living players on
  frame one and instantly declares defeat.
- `TurnManager.autoStartBattle` (serialized, currently **ticked**) makes `Start()` self-start.
  The bootstrapper unticks it and calls `BeginBattle()` once spawning is finished.
- `ParryInputManager` is a single scene object, no longer per-crew-member on the prefab.

**What the scene looks like today** — established by inspection, so the plan is grounded:
- Crew are 3 instances of `Assets/Prefabs/PlayerCharacter.prefab`.
- Enemies are instances of `Assets/Prefabs/EnemyCharacterVariant.prefab`.
- `Assets/Prefabs/EnemyCharacter[DEPRECIATED].prefab` is referenced nowhere — ignore/delete it.
- Both prefabs carry their own health bar, feedbacks and audio as children, so a spawned instance
  is complete. The meaningful per-instance override is **position**, which spawn points solve.

---

**2a — `EncounterDefinition` + enemy spawning. ✅ DONE, verified in Editor.**
`Assets/Scripts/Meta/EncounterDefinition.cs` (which enemies, how many, display name, plunder
reward) and `Assets/Scripts/Meta/EncounterBootstrapper.cs` (spawns them, then calls
`BeginBattle()`). Crew stay hand-placed until 2b.

Two ordering constraints, both load-bearing — **don't undo them**:

- **Spawning happens in `Awake()`, battle start in `Start()`.** Unity guarantees every `Awake()`
  finishes before any `Start()`, so the turn-order snapshot in `BeginBattle()` is complete by
  construction rather than by luck. This is the correct fix for the race, *not* Script Execution
  Order.
- **Instances are created under an inactive staging parent, then reparented to the spawn point.**
  `DriveManager.Awake()`, `ParrySystem.Awake()` and `EnemyManager.Awake()` all dereference
  `CharacterManager.characterData`, and `EnemyCharacterVariant.prefab` has **no default assigned** —
  so a plain `Instantiate` throws before the data can be set. An inactive parent defers `Awake`;
  reparenting onto the active spawn point is what wakes the object, with its data already in place.
  Any future combatant spawning must follow the same pattern.

Also moved `CharacterManager.driveManager` resolution from `Start()` to `Awake()`. `BeginBattle()`
reaches it via `UpdateBuffsForTurn()`, so leaving it in `Start()` meant whichever `Start()` ran
second silently skipped the first turn's drive regen. (Same edit fixed a latent NRE: `Start()` read
`characterData.characterName` *above* its own null check.)

*Editor steps — user's domain:*
1. Create one or two encounters via **Assets > Create > Scriptable Objects > Encounter Definition**
   (e.g. "Two Skeletons" and "Skeleton + Elite").
2. Add an `Encounter Bootstrapper` GameObject to `Encounter.unity`. Assign the encounter, set
   **Enemy Prefab** to `EnemyCharacterVariant.prefab`, and fill **Enemy Spawn Points** with empty
   child transforms positioned where enemies belong — under the same Canvas as the existing
   combatants, since these are UI objects (RectTransform).
3. **Delete the hand-placed enemies from the scene**, or you'll get both those and the spawned ones.
4. Untick **Auto Start Battle** on `TurnManager` so the bootstrapper owns battle start.
   (Leaving it ticked still works — `BeginBattle()` is idempotent and spawning is already done by
   then — but one owner is clearer.)

*Verify:* two different `EncounterDefinition` assets produce two visibly different fights in the
same scene, with correct turn order, working parry, and no instant victory/defeat on frame one.
Leaving the definition empty falls back to hand-placed enemies, so the scene stays playable.

> **If the victory screen appears the instant you press Play**, the bootstrapper spawned nothing
> and `CheckBattleEndConditions()` found zero enemies on frame one. Check the console — the
> bootstrapper logs which precondition failed. In practice it's an unassigned **Enemy Prefab**.

---

### ⟡ Interlude — 2.5D presentation refactor ✅ DONE

**Landed as Option A** (sprite bodies + a world-space Canvas per character). Kept below because the
findings explain *why* the current setup looks the way it does.

**What shipped:**

- Combatants are `SpriteRenderer` bodies on Sorting Layer `Player + Enemies`. The prefab root stays
  a `RectTransform` — a point-anchored RectTransform under a plain Transform behaves exactly like a
  Transform, so rebuilding the root was unnecessary and the variant link survived.
- The prefab already had a `CharacterUI` child Canvas; it became **World Space** at scale 0.01 and
  now holds the health bar, drive meter, parry indicator and floating damage text.
- Sorting layers authored back to front: `Default`, `Background Middle`, `Front Middle`,
  `Player + Enemies`, `Front Near`. The scene Canvas is **Screen Space – Camera** on
  `Player + Enemies`.
- Parallax landed as its own slice: `Assets/Scripts/Environment/ParallaxController.cs` +
  `ParallaxLayer.cs`, plus optional `SortingOrderFromPosition.cs`.
- Spawn points moved out of the Canvas into a world-space `Combatants` root at scale 1.
- Code cost was as predicted: `ClickableCharacter` gained `IPointerClickHandler`. Everything else
  was Editor work.

**Correction to the finding below:** the two prefabs do *not* each carry their own health bar and
feedbacks — `EnemyCharacterVariant.prefab` is a **Prefab Variant of `PlayerCharacter.prefab`**, so
the conversion was done once and inherited. (`EnemyCharacter[DEPRECIATED].prefab` is not its base.)

**Three traps this cost time on, all recorded in `CLAUDE.md` and `TODO.md`:** reparenting writing
compensating scale/position, prefab variants freezing overridden properties, and UGUI Graphics not
rendering outside a Canvas.

---

**Original write-up, kept for the reasoning.** The **camera question is settled — orthographic**, and
background parallax turned out to be separable from this refactor entirely.

**Why it's cheaper than it sounds** — three things checked in the codebase, not assumed:

| Finding | Consequence |
|---|---|
| Of 25 MMF feedbacks on `PlayerCharacter.prefab`, only **one** is UI-bound (`MMF_Image`). The rest are `MMF_Sound` ×8, `MMF_Scale` ×5 (targets `Transform`, and `RectTransform` is one), `MMF_TMPColor` ×5, `MMF_TMPAlpha` ×2, `MMF_Events` ×3, `MMF_Pause` ×1. | The game-feel layer survives the move essentially intact — the part that looked most expensive isn't. |
| `TargetHoverHandler` uses `IPointerEnterHandler` / `IPointerExitHandler`, which are **EventSystem** interfaces, not UI ones. | Hover works on 3D objects with a `PhysicsRaycaster` on the camera plus colliders — **no code change**. |
| `TextMeshProUGUI` and 3D `TextMeshPro` both derive from `TMP_Text`, which is the type `CharacterManager` declares. | Swapping the components doesn't touch C#. |

Clicking currently goes through a UI `Button` on `PlayerCharacter.prefab`, so that becomes
`IPointerClickHandler` on `ClickableCharacter` — a few lines.

#### Option A — hybrid: sprite bodies + a small world-space Canvas per character
**Easy, and almost entirely Editor work. Recommended.**

The body becomes a `SpriteRenderer` in 3D; health bar, name, status text and turn marker stay on a
world-space Canvas parented to it. Every UI type survives — `Slider`, `Image`, `ParticleImage`,
`TMP_Text` — so `CharacterManager`, `ParryVisualFeedback` and `DriveUI` need **no changes**. This
is the conventional 2.5D approach.

Code cost: one small change (`ClickableCharacter` click handling). `EncounterBootstrapper`'s
`ResetLocalTransform()` already handles both `RectTransform` and plain `Transform`, so **2a needs
nothing**.

#### Option B — fully UI-free characters
**Moderate: four files, all mechanical, none architectural.**

| Thing | Where | Replacement |
|---|---|---|
| `Slider healthBar` | `CharacterManager` | scaled sprite or shader fill |
| `Image turnMarker` | `CharacterManager` | `SpriteRenderer` |
| `ParticleImage driveParticles` | `CharacterManager` + `DriveManager` (`.Play()` / `.Stop()`) | `ParticleSystem` |
| `Image parryTimerBar` (uses `fillAmount`) | `ParryVisualFeedback` | material fill or scaled child |
| `MMF_Image` ×1 | prefab | `MMF_SpriteRenderer` |

The `ParticleImage` → `ParticleSystem` swap is the only one with any reach, and it's two call sites.

#### Sequencing — the one real interaction

Nothing in the meta layer is blocked by this: `RunState` and spawning are data-only and don't care
about presentation. The single point of contact is **spawn points** — 2a is where they get
authored, and converting to 2.5D re-authors their positions.

Done ahead of Phase 3, which was the right order: ports, the voyage map and the loadout screen all
add UI that would otherwise have needed reconciling with whatever convention this landed on.

#### Camera & parallax — decided (orthographic)

External input from an engineer, recorded because it settles two open questions and shrinks the
scope:

**You don't need a 3D scene for any of this.** Unity has one renderer and every scene has a Z axis.
The "2D" project template only changes *defaults* — camera projection, sprite import mode, scene
view orientation, lighting. So "go 2.5D" is not a project-level switch.

Parallax has two implementations:

| | How it works | Cost |
|---|---|---|
| **Perspective camera**, layers spaced along Z | The projection matrix does the parallax for free | Distant layers shrink, so every layer's art has to be scaled for its depth |
| **Orthographic camera**, scripted movement | Z offsets do nothing visually; you move layers yourself | You write the script, but art is authored at face value |

**Most 2D games use the second, and that's the call here.** Orthographic camera, each layer under its
own empty GameObject, ordered with **Sorting Layer + Order in Layer rather than Z**.

Two implementation details worth not rediscovering:

- **Drive layer positions from the camera's _absolute_ position, not accumulated per-frame deltas.**
  Absolute can't drift; accumulating error does.
- **Endless scrolling:** either keep three copies and wrap one as it leaves view, or set the texture
  wrap mode to `Repeat` and scroll the texture offset instead of moving transforms. The second is
  cheaper but needs a tileable texture and a **separate material instance per layer** (otherwise
  layers share one material and scroll together).

**What this changes about the plan:**

- **Billboarding is no longer a question.** It only existed because the camera might have been
  perspective. A fixed orthographic camera has nothing to billboard toward.
- **Parallax is decoupled from the combatant refactor.** Background parallax needs an orthographic
  camera and sprite layers; it does *not* require moving combatants off the Canvas. It can land as
  its own small slice, before or after this, or instead of it.
- **Option A still holds.** A world-space Canvas has its own Sorting Layer / Order in Layer fields,
  so the per-character UI participates in exactly the same ordering scheme as `SpriteRenderer`s —
  one convention across both.
- **Caveat, now resolved:** combatants used to live under a **Screen Space – Overlay** Canvas, which
  doesn't move with a world camera at all and composites above everything. Switching it to
  **Screen Space – Camera** on the `Player + Enemies` sorting layer is what let the HUD interleave
  with the sprite layers; the combatants then moved out of the Canvas entirely.
- With orthographic, depth reads entirely from art, scale and parallax speed — the projection
  contributes no depth cue. That's the normal 2D trade, just worth stating.

---

**2b — Crew spawned from `RunState`. ✅ DONE, verified in Editor.**
`EncounterBootstrapper` now spawns living crew as well as enemies, sharing one staging area.
`CharacterManager.AssignCrewState()` lets the spawner bind the record **explicitly**; the
id-matching path in `BindToRunState()` is kept only as a fallback for hand-placed crew, clearly
marked as such. Dead crew are filtered out by `RunState.LivingCrew` and never spawn.

Notes:
- **`characterData` must be assigned before the object wakes** — this is why crew spawn through the
  inactive staging area too, even though the crew prefab *does* carry a default. `DriveManager.Awake()`
  caches `buffNextActionMultiplier` off `characterData`, so waking as the prefab's default (Black Sam)
  and swapping afterwards would leave the wrong character's drive multiplier cached for the whole fight.
- Crew are named from `CrewMemberState.displayName` where present, falling back to the authored
  `characterName` — so generated crew will show their rolled name rather than the archetype's.
- **Action loadout is not applied yet.** `CrewMemberState.actionLoadout` is populated and saved, but
  `ActionsManager` still reads `characterData.actionSlots`. Applying per-crew loadouts needs an
  instance-level action list on `CharacterManager` (the SO is shared and must not be written to) —
  that's Phase 5.

*Editor steps — user's domain:*
1. On the `Encounter Bootstrapper`, set **Crew Prefab** to `PlayerCharacter.prefab` and fill
   **Crew Spawn Points** with one empty child transform per berth (3–4), under the same Canvas.
2. **Delete the hand-placed crew from the scene.** Leaving them gives you both them and the spawned
   crew — and the hand-placed ones aren't filtered by the run, so dead crew would reappear.
3. Leave **Crew Prefab** empty at any point to fall back to hand-placed crew.

*Verify:* kill a crew member, finish the fight, play again — they're absent and the fight starts
with the survivors at their carried health. **Delete Run Save** then Play restores the full crew.

**`CrewManager` — resolved: deleted.** Its `Awake()` found `Player`-tagged objects, but crew are now
spawned during the bootstrapper's `Awake()` and Awake order between components is arbitrary, so it
usually found nothing. Nothing in gameplay read the roster it built; its only consumer,
`Debugs/Debug_Crew.cs`, had an unassigned reference and was already inert. Both are gone.

That removed the last caller of `Character.Initialize()`, which was the last thing writing to a
Character ScriptableObject at runtime, so **that method is deleted too** rather than left as a
ready-made way to reintroduce the problem. `reputation` itself is still authored and still never
read — see the open question in §7.

*Editor step — user's domain:* the `Crew` and `Debugs` GameObjects in `Encounter.unity` now carry
missing scripts. Both are leaf objects with nothing but a Transform and the deleted component, so
delete them outright.

**2c — `EncounterResult`. ✅ DONE, verified in Editor.**
`Assets/Scripts/Meta/EncounterResult.cs` is the report one encounter produces: survivors and their
end health, this fight's casualties, and the plunder paid. Persistence now happens at exactly one
boundary.

How it fits together:

- `TurnManager.BeginBattle()` → `RunManager.BeginEncounter()`. Opened there rather than in the
  bootstrapper so it runs once per fight whichever of the two owns battle start.
- `CharacterManager`'s death path → `RunManager.ReportCrewDeath()`, which flips `isDead` **in memory
  only**. It can't wait for the end of the battle: a dying `CharacterManager` destroys its
  GameObject on the spot, so casualties are unreadable from the scene by then.
- `TurnManager.EndBattle()` → syncs survivors' health, then `RunManager.CompleteEncounter()` awards
  plunder, builds the result, and calls `SaveRun()` **once**. `TurnManager.LastResult` holds it and
  `TurnManager.OnEncounterComplete` fires it.

Notes:

- **Rewards are victory-only**, and a null `EncounterDefinition` just means no payout — so a
  hand-placed fight with no definition stays playable.
- **No XP.** `CrewMemberState.xp` is still only ever read. Awarding it belongs with Phase 5, which
  also has to fold level into stat resolution; splitting those across phases would mean XP
  accumulating with no visible effect.
- **Behaviour change worth knowing:** a death no longer writes through the instant it happens, so
  quitting mid-fight now rewinds the deaths *as well as* the damage. Previously the corpse stuck
  while everyone else's wounds were refunded. Same policy as documented in 1c, applied consistently.
- `TurnManager.OnEncounterComplete` is declared as `System.Action<EncounterResult>` — fully
  qualified, because a bare `Action` is the game's own ability ScriptableObject.

*Editor steps — user's domain:* nothing is required for it to work. To show a results screen, hook
`TurnManager.OnEncounterComplete` (or read `TurnManager.LastResult` when the victory UI opens) — the
result carries `encounterName`, `playerVictory`, `plunderAwarded`, `plunderTotal`, and a per-crew
list of `displayName` / `endHealth` / `died`.

*Verify:* finish a fight and confirm the console shows **exactly one** `[RunManager] Run saved` plus
one `[TurnManager] Encounter complete — …` line whose numbers match what happened. Kill a crew
member and check they're listed as `died` and absent next encounter. Win with a `plunderReward` set
and watch `plunder` climb in the RunManager Inspector; lose and confirm it doesn't.

**2d — `GameManager` static-list refresh. ✅ DONE, verified in Editor.**
`GameManager` now re-populates on `SceneManager.sceneLoaded`, and `EncounterBootstrapper` calls
`FindCharacters()` after spawning. `GetAlivePlayerCount()` / `GetAliveEnemyCount()` refresh before
counting, since as static entry points they gave callers no way to know the cache was stale.

Smaller than this plan assumed: the only real consumer, `CombatController.FindRandomValidTarget()`,
already called `FindCharacters()` itself, and the two count helpers had **zero callers** — so the
staleness was latent rather than a live bug.

`OnEnable`/`OnDisable` are guarded on `instance != this`, matching `ParryInputManager` — without it
a duplicate being destroyed in `Awake` would unsubscribe the real instance.

*Verify:* `CombatController`'s automatic-targeting path (`useManualTargeting = false`) still finds
valid targets after a respawn.

---

### Phase C — Turn sequencing & impact effects ⬅ NEXT

**Goal:** put tunable time into combat. Every character's turn follows the same authored rhythm —
action, attack animation, a beat to show lasting effects, a beat before the next character — and the
moment an attack lands or is parried plays an authored impact that can be a sprite animation, a
feedback, or both.

Lettered like Phase B because it's combat presentation, not part of the meta-layer sequence. It
doesn't block Phase 3 and Phase 3 doesn't block it, but it's the next thing being worked on.

**Already shipped as a precursor:** the enemy **target telegraph** — `img_target_indicator` shows on
the victim for `EnemyManager.targetHighlightDuration` before the attack starts. It's the pattern this
phase generalises: a tunable pause carved out around resolution, with the presentation beat separate
from the gameplay one. Worth reading `TelegraphThenAct()` before designing `TurnSequence`.

#### What already exists (this is a unification, not a new system)

Three sequencers are already in the project and none of them talk to each other:

| Where | What it does |
|---|---|
| `MMF_Player` | A real sequencer already — every feedback has `Timing.InitialDelay`, plus `MMF_Pause` and `MMF_Events`. ~25 on the character prefab. |
| `EnemyAttack.ExecuteAttackSequence()` | A hand-written coroutine timeline: windup → parry window offset → resolve. Hardcoded. |
| `TurnManager.actionDelay` | Serialized and **never read**. Dead field, and exactly the hook this phase needs. |

The player's attack has no timeline at all — `CombatController.PerformAction()` rolls, damages, plays
feedbacks and completes the turn inside one frame.

#### The design boundary — read before building

**Presentation order is tunable. Resolution order is not.** Gameplay resolution stays exactly where
it is today; this phase only adds *time around it*. If the asset ever lets you drag "apply damage"
above "roll to hit", you get bugs that present as timing problems and are miserable to diagnose.

That constraint is what keeps this a tuning tool rather than a scripting language, and it's why this
phase is roughly a day rather than a week: **no async refactor of damage, death or the parry
system.**

#### C1 — `TurnSequence`

One ScriptableObject on `TurnManager`, reused by every character so the whole fight keeps one
rhythm. An ordered list of phases, each with a `duration`:

| Kind | What it does |
|---|---|
| `PlayFeedback` | Plays a named MMF slot on the acting character (attack / heal / buff / …), optionally waiting for completion |
| `Hold` | Waits. The space between characters, and where lasting effects on the target are shown. |
| `Upkeep` | Runs the existing `CharacterManager.OnTurnComplete()` — buffs tick and expire, cooldowns, drive — and fires expiry feedbacks |

Authoring the sequence from the brief looks like:

```
1. PlayFeedback  attack   0.8s
2. Hold                   0.6s   <- lasting effects (stun, DoT) show here
3. Upkeep                 0.4s   <- "buff expired" plays here
```

Rows can be reordered, retimed, added or removed. **Handover is implicit at the end of the list**
rather than a phase, so it can't be accidentally reordered into the middle.

`CompleteTurn()` becomes a coroutine that walks the sequence and then does the existing advance.
Three guards, all small:

- **Re-entrancy.** A second `CompleteTurn()` while a sequence runs would double-advance
  `currentTurnIndex`. One bool.
- **Battle end mid-sequence.** If the last enemy dies during the gap, the sequence must stop rather
  than start the next character's turn.
- **`OnTurnComplete()` moves** out of the top of `CompleteTurn()` into the `Upkeep` phase, so buff
  expiry happens at the beat its feedback plays on. Nothing else runs in between, so it's safe.

Plus one new MMF slot on `CharacterManager` for the buff-expiry feedback, wired like `buffFeedback`.

#### C2 — Impact effects

A separate beat from turn handover: impacts fire **when an attack resolves**, which can happen more
than once per turn and, for enemies, mid-windup. So it can't live on `TurnSequence`.

`CombatFeel` ScriptableObject at `Assets/Resources/CombatFeel.asset`, reached via
`CombatFeel.Instance` — same `Resources.Load` pattern as `ContentDatabase`, so `CharacterManager`
finds it without per-prefab wiring. Maps outcome to effect:

| Outcome | Fires from |
|---|---|
| `Hit` | `CharacterManager.TakeDamage()` |
| `Crit` | same, when the attack rolled a natural 20 |
| `Parry` | `TakeDamage(wasParried: true)` / `CharacterManager.Parry()` |
| `Miss` | `CharacterManager.Miss()` |

Each entry is an **`ImpactEffect`**, and every field is optional so it can be a sprite animation, a
feedback, or both:

- `impactPrefab` — spawned at the target, self-destructing after `lifetime`. Carries whatever you
  want: an `Animator`, a sprite sequence, a `ParticleSystem`, its own `MMF_Player`.
  `PF_PFX_hit.prefab` is the existing example of this idiom.
- `feedbackSlot` — an MMF_Player already on the target's `CharacterManager`
- `lifetime`, `offset`

**Deliberately not built now:** per-character or per-weapon impact overrides (a pistol shot vs a
cutlass). The `Action` asset is the natural place for an override field when it's wanted — the
lookup is written so adding one later doesn't change call sites.

#### What this phase deliberately does not do

No async refactor of combat resolution. Damage, death, drive and the parry system keep their current
synchronous flow. `EnemyAttack`'s coroutine already calls `CompleteTurn()` when it finishes, so the
turn sequence runs *after* it and the two never collide — **nothing about parry needs touching.**

#### Known sharp edge

**Death destroys the GameObject immediately.** A sequence or impact holding a target reference must
tolerate it vanishing mid-run. Same edge already flagged for damage over time in Phase B.

*Verify:* every character's turn should have the same visible rhythm, with a readable gap between
characters rather than an instant cut. Reordering rows in the `TurnSequence` asset should visibly
change that rhythm with no code edit. Landing a hit and parrying one should play different impacts,
and killing the last enemy mid-sequence should end the battle cleanly rather than starting another
character's turn.

---

### Phase B — Balancing & action additions (ongoing, parallel to 3–6)

**Yours, not a blocking phase.** Combat content and tuning: authoring new `Action` assets, setting
attack/defense/health across the roster, and finding out what a crew can actually survive. It runs
alongside the numbered phases rather than in front of them — nothing here blocks Phase 3, and
Phase 3 doesn't block any of it.

Lettered rather than numbered on purpose, so it doesn't imply an ordering against 3–6.

**My side is on request.** Anything needing new mechanics rather than new numbers — damage over
time, status effects, new `ActionType`s — comes to me. Everything authored in the Inspector is
yours.

#### Tooling that already exists for this

| | |
|---|---|
| `EncounterSequence` | Ordered gauntlet of encounters. `repeat` per row, `RepeatLast` to hold at the hardest fight. Assign on `RunManager`. |
| Attrition log | `EncounterFlow` logs each encounter's end health per crew member as a fraction of max, flagging anyone under `lowHealthWarning`. The curve is the thing to read, not the run length. |
| **Tools > Pirate Kingdom > Restart Gauntlet** | Back to encounter one, crew and their damage untouched. `sequenceIndex` persists, so editing a sequence otherwise looks like it does nothing. |
| `Enemy.plunderReward` | Per-enemy; `EncounterDefinition.TotalPlunderReward` sums them plus `bonusPlunder`. Payouts follow from what's in a fight. |

#### The starting point for attack/defense

The only number that matters is the **gap** between attacker and target:

```
minimum roll to hit = defenseValue - attackPower + 1     (floored at 2, since 1 always misses)
hit chance          = (21 - minimum roll) / 20
```

**As authored today every `defenseValue` (3–5) sits below every `attackPower` (5–11), so the
subtraction clamps and everything hits 95%.** Defense is inert, and the only misses in the game are
natural 1s. Enemies gaining the ability to miss (see `TODO.md`) changed nothing on its own for the
same reason.

A worked baseline, anchored on **crew defense ≈ 13, crew attack ≈ 10–11**:

| | Skeleton | Skeleton Elite | Skeleton Boss 2 |
|---|---|---|---|
| Role | Chaff / tempo | Evasive threat | Attrition wall |
| `maxHealth` | 20 | 40 | 90 |
| `attackPower` | 8 | 10 | 12 |
| `defenseValue` | 11 | 17 | 14 |
| `speed` | 6 | 12 | 4 |
| `plunderReward` | 5 | 15 | 50 |
| Hits crew | 75% | 85% | 95% |
| Crew hit it | 95% | 65–70% | 80–85% |

Two cautions from deriving those: **accuracy is a far sharper dial than health** at this damage
scale, so reach for `attackPower` before `maxHealth` when something feels oppressive; and don't
push crew hit rates far below ~60%, because with 3–4 combatants a miss costs a whole turn and
fights get long and swingy fast.

#### Damage over time — what it would touch

Sketched so the cost is known, not designed. The per-turn tick it needs already exists.

- `CharacterManager.activeBuffs` + `UpdateBuffsForTurn()` is the existing per-turn hook, called from
  `TurnManager.SetCharacterTurn()`. DoT ticks belong there.
- `Character.BuffType` currently means *stat modifier*. Recurring damage is a different kind of
  thing and probably wants its own list rather than overloading buffs — the stacking, refresh and
  display rules differ.
- Ticks must go through `CharacterManager.TakeDamage()` so feedbacks, drive gain and the death path
  all behave. **Death mid-tick is the sharp edge:** a dying `CharacterManager` destroys its
  GameObject immediately, so anything iterating combatants while ticking has to tolerate one
  vanishing mid-loop.
- `Action` needs the authoring fields (damage per turn, duration, whether it stacks or refreshes).
- Enemy AI weighting: `EnemyManager.ChooseBestAction()` scores by `ActionType`, so a new type needs
  a weight or enemies will never pick it.

#### Multi-target attacks — what it would touch

Sketched so the cost is known, not designed. Wanted for both crew and enemies.

**It's a half-built feature, and the slate is clean.** `Action.TargetType` already has `AllEnemies`
and `AllAllies`, and both sides already accept them — they just resolve against one target
(`CombatController.IsValidTarget()` accepts any enemy-tagged click; `EnemyManager.SelectTarget()`
returns `_playerCharacters[0]`, with a comment saying as much). **All 13 authored Action assets are
`SingleEnemy` or `SingleAlly`**, so implementing real multi-target semantics changes no existing
asset's behaviour.

**Two things to pay down first, because AoE is what makes them dangerous:**

- **`TargetType` means two different things** — absolute in `CombatController`, caster-relative in
  `EnemyManager` (see `TODO.md`). Benign while each asset lives on one side only. With AoE, one
  inverted read means an enemy's "all enemies" resolves against its own side, or a crew AoE hits
  the party.
- **The d20 rule is already duplicated** across `CombatController.PerformAction()` and
  `EnemyManager`, and `HealthDrain` made it four hand-synced copies. Adding per-target loops to both
  writes the loop twice and then merges it. **Extract a shared resolver taking
  `(caster, action, targets[])` first**, with single-target as the one-element case. This is the
  call that shapes everything else.

**The decision without a clean answer: parry.** Single-target at every level — `ParryInputManager`
dispatches to exactly one `activeParrySystem`, `EnemyAttack.StartAttack()` takes one GameObject,
`_pendingDamage`/`_pendingTarget` are single-valued, and one shared Parry input action means three
crew physically cannot each parry their own hit. Two viable shapes:

- *AoE enemy attacks are unparryable.* Simplest, and defensible design — AoE becomes the threat you
  can't mitigate, only prevent or survive. Needs strong telegraphing or it reads as broken parry.
- *One press mitigates party-wide.* Elegant, but the parry drive reward is
  `damage prevented × parryBonusDriveMultiplier` **per character**, so one press paying out across
  three crew is an enormous drive swing needing retune.

Recommend the first, chosen explicitly rather than by default.

**Drive economy scales with the target count, both ways.** `damageInflictedDriveMultiplier` is 4 and
a segment is 100, so a 3-target AoE for 5 each generates **60 drive from one action** — over half a
segment. Every victim separately gains `damageTakenDriveMultiplier`, so an enemy AoE tops up the
whole party at once. And a drive stack spent on an AoE is worth N× one spent on a single target.
Decide deliberately whether drive gain stays per-damage-point (scales with N) or becomes per-action.

**Traps, all concrete:**

- **`OnAttackPerformed()` must fire once per action, not per target** — inside the loop, target 1
  consumes the stacks and targets 2+ silently lose the multiplier. Same for `UseAction()`.
- **Turn completion exactly once.** Firing `EnemyAttack` per target makes `OnAttackComplete` fire N
  times, and each calls `CompleteTurn()` — the enemy would burn N turns.
- **Per-target rolls.** Roll to-hit *and* damage per target so each target's `defenseValue` and crits
  matter; note it multiplies variance. The "single damage roll" rule in `CLAUDE.md` exists only to
  keep the parry reward honest, so it doesn't bind if AoE is unparryable.
- **Death mid-iteration is safe today and won't stay safe.** `TakeDamage` only fires `OnDeath`; the
  `Destroy` happens in `CharacterManager.Update()` next frame, so a synchronous loop is fine. Phase C
  staggers resolution across frames, at which point it isn't. Snapshot the list and null-check per
  iteration regardless. (Aside: `OnDeath` fires twice per death — from `TakeDamage` and again from
  `Update` — harmless only because nothing subscribes to it.)
- **Enemy AI can't see how many an action would hit.** `ChooseBestAction()` weights purely by
  `ActionType`, so an AoE is strictly better against 3 crew and worthless against 1 with no way to
  tell. Needs a situational modifier scaling by living target count. `SelectTarget()` also returns a
  single `CharacterManager`.

**UI is single-target throughout.** `ActionTargetingLine.ApplyHoverTarget()` tracks exactly one
`hoverTarget` (the per-character flag underneath handles multiples fine, so this is a set change),
the line draws to one point, and `TooltipUI.ShowTargetTooltip()` previews one hit chance where an
AoE has a different one per target.

**Sequencing: do this after Phase C.** Three simultaneous damage feedbacks and floating numbers read
as mush, and Phase C is precisely about putting authored time around resolution — building AoE first
means redoing its presentation.

#### Open items belonging to this phase

Detail in `TODO.md`:

- **Parry drive retune** — Inspector values still unapplied (Tephi 40→5, Black Sam 10→5, Kolo Aka 5→2.5).
- **Tephi is undertuned** — attack 5 and the roster's lowest `buffNextActionMultiplier` (1.5).
- **`Skelly Spear` (5–10) is in no enemy's `actionSlots`** — the Elite is its natural home.
- **`GetModifiedAttackPower()` has no callers** — decide whether drive should affect accuracy, or delete it.
- **Crit asymmetry** — the player's single roll both hits and crits; enemies roll to-hit and crit
  separately. Slightly more visible now: with `BuffType.CritChance` widening the window, an enemy's
  crit is gated behind passing a *separate* to-hit roll first, so the same buff value buys an enemy
  marginally fewer crits than a crew member. Both sides now read `CharacterManager.CritThreshold`,
  so unifying is a matter of deleting `RollForCritical()` and testing the to-hit roll instead.
- **Drive multipliers below ~1.5 vanish into `Mathf.Round`** on small damage ranges. Sanity-check new
  tuning against `minDamage`, not the midpoint.

**Buff actions — read these three before authoring any.** The player path works and new buff/debuff
`Action` assets need no code, but:

- ~~**Enemy buff/debuff actions silently no-op**~~ — **fixed.** `EnemyManager` now calls `AddBuff`
  directly instead of through an unbindable `SendMessage`. Enemy buffs work.
- **`BuffType.Health` grants max health but no actual health**, and can leave a character above
  100% when it expires. Avoid the type until it's decided; the rest are safe.
- **`BuffType.Attack` is now `BuffType.Accuracy`** — it feeds the to-hit roll only. Damage bonuses
  stay drive's job so the two don't compound. **Defense buffs still do nothing at all** until
  defense values move into the band above — the to-hit roll clamps. Speed buffs work as expected.
- **`BuffType.CritChance` is authored in flat d20 points** — each point is one more critting face,
  so `+1` is +5% and `+2` is +15% total. Capped at 50% (threshold 11); a negative value switches
  crits off entirely rather than wrapping. Applies to attacks, health drains **and heals**.
  - Cheapest damage dial that isn't drive: it multiplies existing damage rather than adding to it,
    so it scales with whatever the weapon already does and doesn't need retuning per action.
- **`BuffType.DamageReduction` is the protection buff, and is authored in different units** — a
  FRACTION (0.25 = 25% less damage), where every other type is flat points. Stacks additively,
  capped at 90% so protection can never reach immunity; as a Debuff it becomes a vulnerability,
  floored at -100%. It's the one buff type that isn't a stat, so it's applied in
  `CharacterManager.TakeDamage()` rather than `RefreshStats()`.
  - **It's the reliable mitigation dial.** Unlike Defense, it works at the current stat spread
    rather than waiting for defense values to move into the band above.
  - Watch the rounding, as ever: damage is rounded after reduction, so 25% off a 2-damage hit is
    invisible. Sanity-check against `minDamage`.

Buff icons are built — `BuffIconDisplay` on the combatant root, driven by
`CharacterManager.BuffsChanged`. One slot per buff type, sprite plus optional turns text.

*Verify:* run a gauntlet and read the attrition log — a tuned fight should cost health without
routinely killing, and the curve across a sequence should trend downward rather than falling off a
cliff at one encounter.

---

### Phase 3 — The voyage map — queued behind Phase C, not started
`MapGraph` + seeded generator, `Voyage.unity`, node selection, scene transitions, save/resume.
Node types: Fight, Elite, Port, Event, Boss.

This is the first phase with **two scenes**, so it's where the `DontDestroyOnLoad` singletons
(`RunManager`, `CursorManager`) finally get exercised, and where `RunState` gains `seed`,
`MapGraph map`, `currentNodeId` and `lootPool` (bump `CurrentSaveVersion` when it does).

Answer **run length** (§7) before generating a map — node count drives all tuning.

*Verify:* a full run start→boss, quit mid-run and resume from disk.

### Phase 4 — Port nodes
Heal, recruit, buy. The recruitment path that makes permadeath survivable.

### Phase 5 — Progression
XP award and level-up, the loadout screen (slot earned Actions), Action drops from combat.

### Phase 6 — Meta unlocks
What persists across runs: starting roster options, Actions entering the loot pool, starting
plunder. Run summary screen.

---

## 7. Open decisions

**Recruitment pool size — needs an answer before Phase 4.** There are currently **4** player
`Character` assets, and 3 are the starting crew. With a 4-berth party, permadeath, and a
~12-15 node run, expect to lose 2-4 crew per run. A pool of 4 means you run dry immediately and
every run converges on the same survivors.

Realistically you need **8-12 recruitable crew**. Two ways there:

- *Author them all.* Most control, most work — each needs stats, drive multipliers, art, audio.
- *Archetype + generation.* A `CrewArchetype` SO per class (Duelist / Muscle / Musketeer /
  WitchDoctor) plus a pirate name pool; generated crew roll stats within an archetype band.
  Named characters stay as hand-authored uniques.

I'd recommend the hybrid: your 4 named crew as anchors, generated rank-and-file for the rest.
It fits the fantasy (nobodies you pick up in port) and makes losing a generic crew member sting
differently than losing Black Sam.

> **This depends on classes meaning something first.** `Character.CharacterClass` exists on every
> character and **nothing reads it** — two crew of different classes differ only in stat values
> today. Generation bands are not worth building until a class implies actual capability: which
> actions it can take, what it can spend drive on, what passives it has. See "Defining hero classes"
> in `TODO.md`. Designing it once covers both recruitment and combat identity.

**Run length.** Node count drives all tuning. 12-15 is the genre norm for a ~30-45 min run.

**Crew roster size vs. berths.** The party is 4 (now just the bootstrapper's crew spawn point
count, since `CrewManager` and its `MAX_CREW_SIZE` are deleted), but only 3 crew are
in `Debug Starting Crew` and `Tephi` is authored but not in it. Now that 2b spawns from `RunState`,
"who's in the fight" is a run-state decision rather than an Editor one — there are 4 crew spawn
points and 3 crew, so the intended starting party size needs deciding. 3 or 4?

**`Witch Healer` is authored with `allegiance: Enemy`** but appeared in a debug crew list. Only
Player-allegiance characters bind to run state, so as crew it would silently never carry health.
`CrewMemberState.CreateFrom()` now warns about this. Decide whether Witch is a recruitable crew
member (flip to `Player`) or purely an enemy.

**Does XP survive death?** Level is per-`CrewMemberState`, so it dies with them — intended. But
it means a late recruit is permanently behind. Consider recruits scaling to the current map depth
rather than starting at level 1.

**What is `reputation` for?** It's authored on `Character`, zeroed in `Initialize()`, and never
read. It's an obvious hook for a meta currency or a crew-loyalty stat, but it needs a purpose or
it should go.

---

## 8. What this plan does *not* change

Combat resolution (d20, crits, defense), the Drive stacking system, the parry system, feedbacks,
and the action data model all stay as they are. The meta layer sits above them and feeds them
different inputs.
