# Meta Layer — Design & Implementation Plan

The plan for the layer *around* combat. Combat itself (`Encounter.unity`) is built and stays
broadly as-is; this document covers the voyage, the crew that survives across it, and progression.

See `CLAUDE.md` for how the combat systems work today, and `TODO.md` for outstanding bugs.

---

## 0. Status — read this first

**Phases 1 and 2 are complete and verified in the Editor, as is the 2.5D presentation refactor.**

**Phase D — Status effects is COMPLETE and verified in the Editor.**

| | |
|---|---|
| **D1 — Status effect substrate + damage over time** | ✅ **DONE, verified.** Bleed, and the same mechanic reskinned as poison or fire. Per-character resistance gates whether it lands. DoT ignores protection and grants no drive, and everyone ticks together at the top of the round. |
| **D2 — Stun** | ✅ **DONE, verified.** Skips a turn, with Darkest Dungeon's stacking stun-resistance ramp so a unit can't be chained indefinitely. |
| **E — Class definitions & level scaling** | ✅ **Code complete**, awaiting Editor verification. A class asset supplies base stats and resistances; level scales them; per-character values become deltas. Stops every new character needing a dozen hand-set numbers. |

**E was deliberately held until D was proven**, because it touches every character asset and
`RefreshStats()`, which everything reads. Nothing in D depended on it: resistances are authored
per-character behind the single `CharacterManager.GetResistance()` accessor, so moving that data onto
a class is invisible to every caller — which is exactly the seam E now uses.

**Phase F — Counter / riposte** is queued behind E and **not designed yet** — a placeholder capturing
the idea (parry + an extra button, spending drive, for a set amount of damage) along with the
complications already visible in the code. Read it before designing, not after.

**Phase C — Turn sequencing & impact effects** is queued behind E. Tunable time in combat: an
authored per-turn rhythm every character follows, and an authored impact when an attack lands or is
parried. Deliberately scoped to add time *around* resolution rather than refactor it. **Phase D
already built three of the beats C is meant to own** — `statusTickDelay`, `statusTickSettleDelay` and
`stunSkipDuration`, all serialized on `TurnManager` — so C should absorb them rather than add a second,
competing pacing system.

**Phase 3 — the voyage map** is the next *meta-layer* work and nothing blocks it; it's simply queued
behind the combat work above.

**Phase B — Balancing & action additions** runs in parallel and is the user's track: combat content
and tuning, with engineering help on request for anything needing new mechanics (damage over time,
status effects) rather than new numbers. B and C are lettered, not numbered, because neither has an
ordering against 3–6.

**Phase B has since taken most of the mechanical work**, so the action vocabulary is now much wider
than "attack, heal, buff". Landed since this plan was written, all code-complete and awaiting
Editor verification:

| | |
|---|---|
| `ActionType.DriveGrant` / `DriveDrain` | Move raw drive onto an ally or off an enemy. Drive became something the party plays *with* rather than a private resource. |
| `ActionType.HealthDrain` | Attack that heals the attacker from health **actually lost**. Parryable, so a parry cuts the healing too. |
| `BuffType.DamageReduction` | Protection, as a fraction. The one buff type that isn't a stat — applied in `TakeDamage()`, not `RefreshStats()`. Negated, it's a vulnerability. |
| `BuffType.CritChance` | Widens the crit window, in d20 points (+5% each). Applies to attacks, drains and heals. |
| `Action.autoTargetSelf` | Self-cast actions resolve with no target click and end the turn. |
| `Enemy.TargetingStrategy.HighestDrive` | Makes an enemy drain aim at the fullest meter rather than by health. |
| Enemy buffs fixed | The `SendMessage` reflection path never bound — every enemy buff and debuff silently did nothing. Now calls `AddBuff` directly. |
| Roster retuned into the band | Defense values moved into the §Phase B baseline, so **defense and its debuffs are live** and hit chances span 60–95% instead of clamping at 95%. |

**Every debuff comes free** from the buff it mirrors — `ActionType.Debuff` negates `buffValue`, so
Speed, Defense, Accuracy, Crit Chance and Damage Reduction all debuff with no extra code.

Two combat-wide invariants worth knowing before touching resolution, both in `CLAUDE.md`:
`CombatController.PerformAction()` completes the turn from a `finally` (a fault mid-resolution used
to hand back a free extra action), and `CharacterManager.TakeDamage()` now returns health actually
lost, which is what makes life steal honest about overkill.

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

### Phase D — Status effects ✅ COMPLETE (D1 and D2, both verified in the Editor)

**Goal:** attacks can leave something behind. Damage over time (bleed, and the same mechanic reskinned
as poison or fire) and stun (skip a turn), with per-character **resistance** deciding whether they
land at all.

Lettered like B and C because it's combat mechanics, not meta-layer sequence. Sits ahead of Phase C
by choice — see §0.

#### Decisions already made — do not relitigate these while building

| Decision | Why |
|---|---|
| **A separate `StatusEffect` list, NOT new `BuffType` members** | `BuffType` means "additive stat modifier folded into `RefreshStats()`". DoT and stun are neither, and two exceptions have already been carved out (`DamageReduction` reads in `TakeDamage`, `CritChance` at roll time). A third and fourth would make one enum mean four different things. |
| **Kinds share mechanics and differ only in presentation** | Poison and fire *are* bleed with a different icon and colour. Adding one is an enum member plus Editor wiring, not a system. Per-kind mechanical differences can branch later if ever wanted. |
| **One instance per kind — refresh, never stack** | Re-applying takes the higher `damagePerTurn` and resets duration. Keeps display and tuning legible. |
| **DoT ignores `DamageReduction` entirely** | Protection covers the initial hit only. Armour stops blades, not poison. Consequence, and it's intended: DoT is the designated counter to a heavy-protection build. |
| **DoT grants NO drive to the victim** | Drive is calculated on the initial hit alone. An attack that deals 0 and only applies a DoT therefore generates no drive at all — that's the authored trade-off for it. |
| **Resistance reduces the CHANCE only** | Never the damage or the duration. Additive, resolved in one roll, matching Darkest Dungeon. |
| **Resistance is not buffable** | Authored base only for now; armour and carried items become the modifier layer later. Every read still goes through one accessor so that layer can be added without touching call sites. |
| **A successful parry prevents the rider** | The 25% chip still lands but the bleed does not. Makes parrying a status attack meaningfully better than eating it, and the parry is already the game's skill expression. |
| **No source tracking** | DoT damage is unattributed. No dangling reference to a caster who has since died, and nothing needs to award them anything (see the no-drive rule above). |
| **Statuses are per-battle** | Nothing reaches `RunState`, exactly like buffs. No `saveVersion` bump. |

---

**D1 — Substrate + damage over time. ✅ DONE, verified in Editor.**

New file `Assets/Scripts/Combat/StatusEffect.cs`:

```csharp
// APPEND-ONLY, like every other serialized enum in this project.
public enum StatusEffectKind { Bleed, Poison, Burn, Stun }

[System.Serializable] public class StatusEffect      { kind; damagePerTurn; turnsRemaining; }
[System.Serializable] public class StatusResistance  { kind; [Range(0,1)] value; }   // authored on Character
[System.Serializable] public class StatusApplication { kind; damagePerTurn; turns; [Range(0,1)] chanceToApply; }
```

`StatusApplication` is the authored *rider* on an `Action`; `StatusEffect` is the live instance on a
`CharacterManager`. Keeping them separate is what stops authored data being mutated at runtime — the
same rule as `Character` vs `CharacterManager`.

`CharacterManager` gains:

- `activeStatuses` list + `ActiveStatuses` read-only view + a `StatusesChanged` event (mirrors
  `BuffsChanged`, so a display never polls).
- **`GetResistance(StatusEffectKind)`** — the single accessor. Reads `Character.statusResistances`
  today; gains the equipment layer later and no caller changes.
- **`TryApplyStatus(StatusApplication)`** — rolls `chance − GetResistance(kind)` clamped to `[0,1]`,
  then refreshes-or-adds. Returns whether it landed.
- **`TickDamageOverTime()`** — sums `damagePerTurn` across DoT kinds, clamps the total to
  `MaxHealth * MaxDoTFractionPerTurn`, applies it, returns the amount.
- **`AdvanceStatuses()`** — decrement, drop expired, fire `StatusesChanged`.
- `const float MaxDoTFractionPerTurn = 0.15f` — **a fraction of max health, not a flat number**,
  because the roster spans 20–90 health and a flat cap would be trivial for the boss and lethal for a
  Skeleton. This is the ceiling that stops several kinds ticking at once from overwhelming a crew.
  Note the earlier "does the cap apply before or after protection" question dissolved: DoT never sees
  protection.

`TakeDamage` signature change — an enum, not a third bool, because `TakeDamage(5f, false, true)` is
unreadable and this distinction will grow more rules:

```csharp
public enum DamageSource { Direct, OverTime }
public float TakeDamage(float damage, bool wasParried = false, DamageSource source = DamageSource.Direct)
```

`OverTime` **skips both `ApplyDamageReduction()` and the drive block**. Existing callers are untouched
by the default.

New MMF slot `damageOverTimeFeedback` on `CharacterManager`, used when the source is `OverTime`,
falling back to `dealDamageFeedback` when unassigned. An impact flash and shake on a bleed tick reads
as being hit again, which is the wrong signal.

`Action` gains a `List<StatusApplication> statusEffects` under a new
**"Status Effects On Hit (Attack, Health Drain)"** header. Applied **only on a landed hit** — a miss
applies nothing, matching how a miss already skips the parry sequence.

**Four application sites**, because resolution is still duplicated: `CombatController`'s Attack and
HealthDrain branches, and `EnemyManager`'s `OnAttackComplete` (guarded on `!wasParried`) and
`PerformDirectAttack`. One shared `ApplyStatusEffects(action, target)` helper keeps each to one line.
Another vote for the shared resolver in the multi-target sketch.

**Round-start hook** in `TurnManager.AdvanceToNextRound()` — **everyone ticks together**, not each
at the start of their own turn:

1. `RoundComplete()` (round counter)
2. `TickRoundStatusEffects()` — every living combatant: `RefreshStats()` → `TickDamageOverTime()` →
   `AdvanceStatuses()`
3. `GetTurnOrder()` — re-roll initiative, **filtered on `CharacterManager.IsAlive`**
4. `SetCharacterTurn()`

**Steps 2 and 3 being in that order is the whole design.** Anyone who bleeds out is resolved before
initiative is rolled, so they're simply absent from the new order and never need skipping mid-round.
That deletes the entire "a corpse has the turn" case rather than handling it.

`IsAlive` is what makes the filter safe — the dead are still in the scene at that point (Unity
destroys them on their next `Update()`, which can't run inside this synchronous chain), and an
*uninitialised* character also reads 0 health, so a raw `CurrentHealth <= 0` test would empty the
turn order at battle start. That exact comparison crashed the Editor once.

Buffs still tick at the end of each character's own turn. The schedules differ but the durations are
numerically equivalent, since every character gets exactly one turn per round.

*Also needed:* a `StatusIconDisplay` component mirroring `BuffIconDisplay` (I write it, user wires the
slots), and status riders shown in all four `TooltipUI` methods.

*Verify:* a bleed attack should tick for its authored damage at the start of the victim's next turn,
ignore any protection they have, grant them no drive, and expire after the right number of turns. A
target with resistance authored should visibly shrug some off.

---

**D2 — Stun. ✅ DONE, verified in Editor.**

- `StatusEffectKind.Stun`, `damagePerTurn` 0.
- `CharacterManager.IsStunned` — any live Stun status.
- `stunResistanceStacks` (int) + `StunResistanceBonus => stacks * 0.5f`, folded into
  `GetResistance(Stun)`.

**The Darkest Dungeon ramp**, which is the whole point of the design: each consecutive skipped turn
adds +50% stun resistance, and the stacks **reset the moment the character actually takes a turn**.
It's self-correcting and needs no cap — once the ramp exceeds the incoming chance the stun fails, the
character acts, and acting clears it. It can neither run away into permanent immunity nor be chained
indefinitely.

Held as a **plain counter, not a real buff** — `BuffType` is stat modifiers, and a display-only member
would muddy that further. `StunResistanceStacks` is exposed so the UI can show "Stun Resist +100%",
which is the part the player needs.

**Built differently from the plan above, and the reason matters.** The original ordering — read
`IsStunned` before `AdvanceStatuses()`, both inside the turn — stopped working once D1 moved damage
over time to the round boundary, because `AdvanceStatuses()` then runs *before anyone's turn*. A
1-turn stun applied in round N would have expired at the top of round N+1, before its owner ever
reached a turn to lose.

So **stun became turn-scoped while the damage kinds stayed round-scoped**:

- `StatusEffect.IsTurnScoped` marks it, and `AdvanceStatuses()` skips those entirely.
- `ConsumeStunForSkippedTurn()` spends it at the moment the stolen turn would have happened, ramping
  the resistance in the same call.
- `ClearStunResistanceRamp()` runs on the path where a character actually acts.

The check sits in `SetCharacterTurn()` **before the turn marker, the action bar and the enemy AI
kick-off**, so a stunned character never gets live buttons or starts an attack.

That split is now the general rule: anything measured in the victim's own turns is turn-scoped,
anything measured in elapsed time is round-scoped.

> **The one real hazard: recursion — and it already bit during D1.** `CompleteTurn()` ends by calling
> `SetCharacterTurn()`, so a skip that calls `CompleteTurn()` directly gives
> `SetCharacterTurn → CompleteTurn → SetCharacterTurn`, and a skip condition true of every combatant
> recurses until the process dies. D1's DoT-death check did exactly this and **crashed the Editor with
> a 93 MB log**; the trigger was that `CurrentHealth <= 0` also means "`Start()` hasn't run yet",
> so every uninitialised character read as a corpse.
>
> Two things are already in place as a result, so D2 inherits them: the skip goes through
> **`TurnManager.AdvancePastSkippedTurn()`**, which caps consecutive skips at one lap of the turn
> order and stops with a single error, and `turnSkipDepth` resets whenever someone actually acts.
> **Route the stun skip through that method — never call `CompleteTurn()` directly.**
>
> The guard makes a runaway survivable, not correct. A stunned character stays in the turn order, so
> an all-stunned field is far easier to reach than an all-dead one. **As built**, the skip runs
> through `SkipStunnedTurnRoutine`, a coroutine that unwinds the stack *and* buys the visible
> "STUNNED" beat — and it yields at least one frame **even at `stunSkipDuration: 0`**, because a
> coroutine runs synchronously up to its first yield and a zero duration would otherwise put the whole
> chain back on one stack. `EnemyManager.TelegraphThenAct()` is the precedent.

**A hole this closed on the way past.** Making the round boundary asynchronous in D1 left the action
bar live during the pause: `currentCharacterTurn` still pointed at the previous round's last
character, so a crew member's buttons stayed interactable and an action resolved there collided with
the hand-over already in flight. The stun skip has the same shape.
**`TurnManager.AcceptingPlayerInput`** is now false during both, checked in
`CombatController.IsPlayerControlledTurn()` *and* at the top of `CompleteTurn()` — the second because
the End Turn button calls straight in, bypassing `CombatController`. That's a fourth turn-ownership
layer; see `CLAUDE.md`.

*Verified:* a stunned character's turn is visibly skipped rather than silently dropped; stunning the
same character on consecutive turns gets progressively harder and then stops landing; once they take
a turn it's as landable as it was at the start.

### Phase E — Class definitions & level scaling ✅ CODE COMPLETE, awaiting Editor verification

**Goal:** stop authoring every stat on every character. A class defines the baseline — health, attack,
defense, speed, drive multipliers and **status resistances** — and level scales it. Creating a new
crew member becomes "Duelist, level 3, +2 attack" instead of a dozen hand-set numbers.

This collapses three things already half-planned: `Character.CharacterClass` exists and **nothing
reads it** (`TODO.md`), §7 proposes a `CrewArchetype` per class for generating recruits, and Phase 5
needs level to actually feed stat resolution. One asset serves all three — §7 already says it's worth
designing once for both.

```csharp
[CreateAssetMenu(menuName = "Scriptable Objects/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    CharacterClass classId;
    // Level 1 baseline
    float maxHealth, attackPower, defenseValue, speed;
    float buffNextActionMultiplier;
    float damageInflictedDriveMultiplier, damageTakenDriveMultiplier, parryBonusDriveMultiplier;
    List<StatusResistance> statusResistances;
    // Growth per level beyond 1
    float healthPerLevel, attackPerLevel, defensePerLevel, speedPerLevel;
}
```

`Character` gets a `ClassDefinition` reference, and its existing flat stat fields become **deltas on
top of the class** rather than absolutes:

```
effectiveMaxHealth = class.maxHealth + (level-1)*class.healthPerLevel + character.maxHealthDelta
```

Black Sam becomes "Duelist, +3 attack" — so the authored data answers the question that actually
matters: *how is this character special?*

**As built, a class is REQUIRED and `Character` holds no stats of its own.** The plan above had the
class as an optional baseline with per-character deltas on top; that was simplified once the roster
was going to be rebuilt from scratch. Health, accuracy, defense, speed, the drive rates and the
resistance list were removed from `Character` entirely — it is now identity and content only (name,
id, art, audio, action slots, level, allegiance, class).

**The trade, stated plainly:** two characters of the same class at the same level are numerically
identical, so a named anchor like Black Sam can't differ from a generic member of their class by
stats alone — only by action slots, art and name. If that becomes a problem, the answer is a one-off
`ClassDefinition` for that character, not reintroducing deltas. The "Adopt Class" migration tooling
was dropped for the same reason.

Resolution happens in **`CharacterManager.RefreshStats()` only**, which is already the single funnel
every stat read passes through. Buffs, protection, crit and resistances stay exactly where they are.

**Two things that will bite:**

- **Level must be known before `RefreshStats()` runs.** Crew level lives in `CrewMemberState`, enemies
  use the authored `Character.level`. `Start()` currently calls `RefreshStats()` *before*
  `BindToRunState()`. Fine for spawned crew (the bootstrapper assigns the record before the object
  wakes), but hand-placed crew taking the id-matching fallback would resolve at level 1 and then bind
  health against the wrong `MaxHealth`. Needs reordering, and it would present as a save bug.
- **Deltas read worse in the Inspector than absolutes.** "+3" doesn't tell you Black Sam hits at 15.
  Mitigate with a `[ContextMenu]` "Log Resolved Stats" on `Character`; a custom inspector drawing them
  live is the nicer version if it grates.

**Stays per-character:** name, art, tint, audio, action slots, and the deltas. Class covers numbers
only — class-*gated abilities* (the `allowedClasses` idea in `TODO.md`) stay a separate decision, or
class ends up controlling identity, stats and loadout at once and no single thing is the real
constraint.

Save format is unchanged — `RunState` stores `characterId`, the Character asset references its class,
so no new ids and no `saveVersion` bump.

**What this unlocks:** the equipment layer for resistances (armour and carried items), and §7's
generated rank-and-file crew, which needs exactly these bands to roll within.

*Verify:* convert one crew member to a class and confirm their resolved stats match what they had
before. Level them up and watch the stats move. Leave the rest unconverted and confirm nothing about
them changes.

### Phase F — Counter / riposte — queued behind E, not designed yet

**The idea, as stated:** a crew member with drive available who parries an attack can *counter* it —
instead of pressing the parry button alone, they press an additional button at the same time, and are
granted the opportunity to counter for a set amount of damage.

**Deliberately not designed here.** This is a placeholder so the idea and its known complications
survive; the shape is still to be worked out. What follows is what already exists to build on and
what will need deciding, not a plan.

#### What it can build on

The parry system already does most of the hard part. `ParrySystem` opens a timing window during
`EnemyAttack`'s windup, knows the incoming damage (`SetIncomingDamage`), records the outcome in
`ParrySucceeded`, and already pays out on success. A counter is another branch off
`PerformSuccessfulParry()` rather than a new system.

#### Four complications worth knowing before designing it

- **The two input paths don't share an asset instance.** `ParryInputManager` uses a serialized
  `InputActionReference`, which resolves to the **shared project-wide** `PlayerControls.inputactions`;
  `PlayerInputHandler` does `new PlayerControls()` and gets its own **private** instance. So "parry
  and the counter button pressed together" currently spans two different asset instances. Either the
  counter action joins the shared asset, or the check has to reach across both. This is already
  logged as a gotcha in `CLAUDE.md` and it lands squarely on this feature.
- **"One attempt per attack" is deliberate, and wider than the timing window.**
  `EnemyAttack.BeginParrySequence()` arms a single attempt at the *start of the windup* precisely so
  an early press is caught and **spent**, which is what stops mashing being a strategy. A two-button
  counter has to answer: does pressing the counter button alone spend the attempt? How far apart can
  the two presses be and still count as "at the same time"? Simultaneity in input is a tolerance
  window, not an instant — and whatever that window is, it must not become a way to get two bites.
- **A counter deals damage during the enemy's turn**, which nothing currently does.
  `EnemyAttack.ExecuteAttackSequence()` is mid-coroutine and `EnemyManager.OnAttackComplete()` will
  still fire and call `CompleteTurn()`. **If the counter kills the attacker**, that's death
  mid-sequence — the same sharp edge already flagged for Phase C and for damage over time, and it
  needs the same care: `CharacterManager` destroys the GameObject on its next `Update()`.
- **A parry already pays out drive twice** — the parry bonus, plus `damageTakenDriveMultiplier` on
  the 25% chip. If a counter *spends* drive, you'd be spending and earning in the same instant, so
  the economics need looking at rather than assuming.

#### Open questions

- **Input shape:** simultaneous press, hold-a-modifier through the parry, or a follow-up prompt after
  a successful parry (a riposte). The third sidesteps the simultaneity problem entirely.
- **Cost:** segments, drive stacks, or just "any drive available"? And how much.
- **Damage:** flat authored number, scaled by the damage prevented, or off a stat.
- **Where it's authored.** If counters are class-flavoured — a Duelist counters harder — then
  **Phase E's `ClassDefinition` is the natural home, and E lands first**, so the field can go in from
  the start rather than being retrofitted onto every asset later.
- **Which attacks can be countered?** Only the parryable ones, presumably — currently `Attack` and
  `HealthDrain`.
- **Do enemies get it?** Everything else in combat has been built symmetrically, and enemies can't
  parry at all today (`parryBonusDriveMultiplier` on enemy assets is authored and never read — see
  `TODO.md`). Countering may be the thing that finally makes enemy parry worth wiring, or the place
  to decide it's deliberately player-only.

#### One caution

`TODO.md` already calls the parry **"the single biggest accessibility barrier"** in the game — a
real-time reflex check inside an otherwise turn-based system. Requiring two buttons at once raises
that floor again. The settings list already carries "Parry assist / auto-parry / disable" as an
unbuilt item; whatever shape the counter takes should be reachable by a player using that assist, or
it becomes content they can never see.

**Sequencing note:** Phase C is also queued behind E, and a counter is exactly the kind of thing C's
impact effects are for — the counter's hit is an authored beat. Doing C first would likely make this
cheaper, but that's a call for when it comes up.

### Phase C — Turn sequencing & impact effects

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
| `Crit` | same, when the roll met `CharacterManager.CritThreshold` (20 unbuffed, lower with a `CritChance` buff — no longer a fixed natural 20) |
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

**This has since been applied — the baseline below is the authored roster, not a proposal.** Crew sit
at defense 13 / attack 11-15; Skeleton 8/11, Elite 10/17, Boss 12/14. Hit chances now span 60-95%
rather than clamping at 95%, so **defense is live and misses are no longer only natural 1s.**

The one asset still outside the band is `Witch Healer` (attack 5, defense 3), which everything hits
at the ceiling.

*Superseded, kept for the reasoning:* it used to be that every `defenseValue` (3-5) sat below every
`attackPower` (5-11), so the subtraction clamped, defense was inert, and the only misses were
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
  stay drive's job so the two don't compound. **Defense buffs and debuffs now work** — the roster has
  been retuned into the band above, so the to-hit roll no longer clamps. A -3 Defense debuff is worth
  +10 to +15 percentage points to whoever is attacking the target. Speed buffs work as expected.
  - **Every debuff comes free from the buff it mirrors** — `ActionType.Debuff` negates `buffValue`,
    so Speed, Defense, Accuracy, Crit Chance and Damage Reduction all have working debuff forms with
    no extra code. Author them as `Debuff` with a positive `buffValue`.
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
  - **It's the mitigation dial that can't clamp.** Defense also works now that the roster is in
    band, but Defense stops helping once a matchup is already at the 5% floor or 95% ceiling,
    whereas protection scales the damage itself and always does something.
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
  Healer) plus a pirate name pool; generated crew roll stats within an archetype band.
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
