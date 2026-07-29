# Meta Layer — Design & Implementation Plan

The plan for the layer *around* combat. Combat itself (`Encounter.unity`) is built and stays
broadly as-is; this document covers the voyage, the crew that survives across it, and progression.

See `CLAUDE.md` for how the combat systems work today, and `TODO.md` for outstanding bugs.

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

## 2. The blocking problem: ScriptableObject state

**This must be fixed first. Nothing else in this plan is safe until it is.**

`Character` currently holds mutable state — `activeBuffs`, `actionCooldowns`, `reputation` — as
instance fields *on the ScriptableObject*. `TODO.md` already logs this as a bug because two
Skeletons share one asset. The meta layer makes it far worse, because the meta wants to store
**current HP, level, XP, and action loadout** per crew member.

If that state lives on the SO:

- In the Editor, writes to a ScriptableObject **persist into the project asset**. `Black Sam.asset`
  would accumulate damage, levels, and loadout changes across play sessions. Your authored data
  drifts every time you press Play.
- Permadeath is unimplementable — "dead" is a property of *this run's* Black Sam, not of the
  Black Sam asset.
- Two crew from the same asset (or two Skeletons) share one health pool.

**The fix:** `Character` / `Enemy` / `Action` become **pure authored data, never written to at
runtime.** All mutable state moves into a plain-C# run model (§3). This is the single largest
piece of work in the plan and it touches `TurnManager`, `CharacterManager`,
`ActionsManager.IsActionAvailable`, `EnemyManager`, `CrewManager`, and every `GetModified*()`
call site.

It is also, conveniently, the fix `TODO.md` already prescribes for the duplicate-Skeleton bug.
Doing it here closes that bug too.

---

## 3. Data model

Plain `[Serializable]` C# classes — not ScriptableObjects, not MonoBehaviours. This is what gets
JSON-serialized to disk.

```csharp
[Serializable]
public class RunState
{
    public int seed;
    public MapGraph map;
    public int currentNodeId;
    public int plunder;                       // run currency
    public List<CrewMemberState> crew;         // living + dead, dead kept for the run summary
    public List<string> lootPool;              // Action ids still available to drop this run
}

[Serializable]
public class CrewMemberState
{
    public string characterId;                 // -> Character asset, via ContentDatabase
    public string displayName;                 // generated crew get their own name
    public float currentHealth;
    public int level;
    public int xp;
    public List<string> actionLoadout;          // <= 6 Action ids
    public bool isDead;
}
```

### Asset lookup: `ContentDatabase`

`RunState` stores **string ids, not object references** — object references don't survive JSON.
A `ContentDatabase` ScriptableObject holds the authoritative lists (all `Character`s, `Enemy`s,
`Action`s, crew prefabs) and resolves `id -> asset` both ways.

Add a `public string id` field to `Character` and `Action`, authored once per asset. Do **not**
key off `characterName` or the asset filename — renaming either would silently invalidate saves.

### Stat resolution

Final stats become a function of authored base + level + run buffs, computed at battle start:

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

### Phase 1 — Run state foundation
Move mutable state off the ScriptableObjects. Add `id` to `Character`/`Action`, build
`ContentDatabase`, define `RunState`/`CrewMemberState`, add `RunManager` with save/load.
`CharacterManager` initializes from `CrewMemberState` instead of `characterData.maxHealth`.

*Verify:* start a fight with a debug `RunState` where Black Sam is at 20/60 — he spawns at 20.
Kill him, confirm `isDead` flips in the run state and `Black Sam.asset` on disk is untouched.

*Closes:* `TODO.md` "Duplicate scene instances share ScriptableObject state".

### Phase 2 — Parameterized encounters
`EncounterDefinition` SO, `EncounterBootstrapper`, explicit `TurnManager.BeginBattle()`,
`EncounterResult` written back to `RunState` on `EndBattle`. Move `ParryInputManager` into the
scene first.

*Verify:* two different `EncounterDefinition`s produce different fights in the same scene, and
survivor HP persists into the second one.

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
