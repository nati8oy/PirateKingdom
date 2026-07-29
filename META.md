# Meta Layer — Design & Implementation Plan

The plan for the layer *around* combat. Combat itself (`Encounter.unity`) is built and stays
broadly as-is; this document covers the voyage, the crew that survives across it, and progression.

See `CLAUDE.md` for how the combat systems work today, and `TODO.md` for outstanding bugs.

---

## 0. Status — read this first

**Phase 1 is complete and verified in the Editor. Phase 2 is the next work, not yet started.**

What exists and works today:

| | |
|---|---|
| `Assets/Scripts/Meta/ContentDatabase.cs` | `id -> asset` registry. Asset at `Assets/Resources/ContentDatabase.asset`, 8 characters + 9 actions registered and stamped. |
| `Assets/Scripts/Meta/RunState.cs` | `RunState` + `CrewMemberState`, plain serializable C#. |
| `Assets/Scripts/Meta/RunManager.cs` | Owns the live run, JSON save/load at `Application.persistentDataPath/run.json`. Scene object in `Encounter.unity`. |
| `CharacterManager.BindToRunState()` | Crew take starting health from the run; health and permadeath write back. |

The loop that works right now: press Play in `Encounter.unity` → crew spawn at their carried
health → fight → win → health and any deaths persist → press Play again and the crew are as you
left them.

**Editor tooling** (both under `Tools > Pirate Kingdom`, because `[ContextMenu]` entries are easy
to miss — on a MonoBehaviour they're on the *component's* ⋮ button, on a ScriptableObject they're
in the Inspector's ⋮ with the asset selected, and **never** on a Project-window right-click):

- **Rebuild Content Database** — re-scan and stamp ids. Run after adding/deleting content assets.
- **Delete Run Save** — wipe the save so the next Play rebuilds from `Debug Starting Crew`.
- **Log Run Save Path** — prints the save location and whether it exists.

**Gotcha that has already cost time twice:** an existing save always beats the RunManager's
`Debug Starting Crew` list, so edits to that list appear to do nothing until you delete the save.
The console states which path was taken on every play.

**Enemies now spawn** from an `EncounterDefinition` via `EncounterBootstrapper` (Phase 2a).
Crew are still hand-placed in the scene — Phase 2b spawns them from `RunState`.

**Open proposal:** a **2.5D presentation refactor** (combatants become sprites in 3D space rather
than UI objects under a Canvas) is written up in §6 as an optional interlude between 2a and 2b.
Not committed to — costed and waiting on a decision.

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
| `GameManager` | Destroys duplicates. Fine. But `PlayerCharacters` / `EnemyCharacters` are **static** lists that must be re-populated on every scene load, not just at `Awake`. |
| `TooltipUI` | Destroys duplicate GameObjects. Fine. |
| `ParryInputManager` | **Done.** Now a single GameObject in `Encounter.unity`, off the character prefab. `DontDestroyOnLoad` removed (an instance surviving into the map scene would still hold the shared input action when the next encounter loads its own), `Instance` resolves lazily so it can't lose a race with spawning, dead registration list deleted. |

That leaves `GameManager` as the one still needing attention before Phase 3, when scene
transitions become real: its `PlayerCharacters` / `EnemyCharacters` are **static** lists populated
once at `Awake`, and they'll need re-populating on every scene load.

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

### Phase 2 — Parameterized encounters ⬅ NEXT, not started

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

### ⟡ Optional interlude — 2.5D presentation refactor

**Not part of the meta plan, and not committed to.** Written up for a decision after 2a is wired.
Combatants are currently UI objects (`RectTransform` under a Canvas); this converts them to sprites
in 3D space with a camera, for a 2.5D look.

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

So: **finish 2a's Editor wiring → do this as its own isolated slice → then 2b.** Spawn points get
positioned once, in 3D, and visual changes are never being debugged at the same time as spawning
changes.

Doing it before the later phases matters more than doing it before 2b: ports, the voyage map and
the loadout screen all add UI that would otherwise need reconciling with whatever convention this
lands on.

#### Still to decide
- **Camera:** perspective, or orthographic with 3D depth. Both are legitimate 2.5D looks.
- **Billboarding:** a billboard script, or — if the camera is fixed — simply pre-rotating sprites
  to face it.

---

**2b — Crew spawned from `RunState`. Code done; Editor steps outstanding.**
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

**Decision needed — `CrewManager`.** Its `Awake()` finds `Player`-tagged objects, but crew are now
spawned during the bootstrapper's `Awake()`, and Awake order between components is arbitrary, so it
will usually find nothing. It now warns instead of silently building an empty roster. Nothing in
gameplay reads what it builds — its only consumer is `Debugs/Debug_Crew.cs`. Options: delete both,
or drive it from the bootstrapper after spawning. Deleting also removes the last caller of
`Character.Initialize()`, which is the last thing resetting `reputation` on the ScriptableObject —
see the `reputation` open question in §7.

**2c — `EncounterResult`.**
One object returned at battle end carrying survivors' health, deaths, XP and plunder. Consolidates
persistence into a single boundary and removes the scattered `SaveRun()` calls currently in
`TurnManager.EndBattle()` and `CharacterManager`'s death path. This is the seam Phase 3 hands
control back to the map through.

*Verify:* the console shows exactly one save per encounter, and the numbers match what happened.

**2d — `GameManager` static-list refresh.** (small, but blocks Phase 3)
`GameManager.PlayerCharacters` / `EnemyCharacters` are **static** lists populated once in `Awake`.
With combatants spawned at runtime and scenes loading repeatedly they go stale immediately.
Re-populate on scene load and after spawning.

*Verify:* `CombatController`'s automatic-targeting path (`useManualTargeting = false`) still finds
valid targets after a respawn.

---

**Known question for 2b — `CrewManager` breaks when crew are spawned.** It's present in
`Encounter.unity` and assembles the crew from `Player`-tagged objects in `Awake()`, which will now
run *before* any crew exist, so it'll silently build an empty roster. Its only consumer is
`Debugs/Debug_Crew.cs` (a logger) — nothing in gameplay reads `GetCrewMembers()` or the
`crewSlot1..4` fields. So the options are: move it after spawning, fold it into the bootstrapper,
or delete it along with `Debug_Crew`. Deleting is the honest call unless it's wanted for a crew UI
later — flag it rather than assume.

It also calls `crewMembers[i].Initialize()`, which is the last thing still resetting
`Character.reputation` on the ScriptableObject. If `CrewManager` goes, that goes with it, which
ties into the `reputation` open question in §7.

### Phase 3 — The voyage map
`MapGraph` + seeded generator, `Voyage.unity`, node selection, scene transitions, save/resume.
Node types: Fight, Elite, Port, Event, Boss.

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

**Run length.** Node count drives all tuning. 12-15 is the genre norm for a ~30-45 min run.

**Crew roster size vs. berths.** The party is 4 (`CrewManager.MAX_CREW_SIZE`), but only 3 crew are
placed in `Encounter.unity` and `Tephi` is authored-but-unplaced. Once 2b spawns from `RunState`,
"who's in the scene" stops being an Editor decision and becomes a run-state one — so the intended
starting party size needs deciding. 3 or 4?

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
