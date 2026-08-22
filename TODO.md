# TODO / Known Issues

Running backlog of unresolved issues found while working in this repo. Newest investigations at
the top of each section. See `CLAUDE.md` for how the systems actually work.

**Settings & accessibility** near the bottom is a *forward-looking* list rather than a bug list —
things we expect to need, collected as they surface. Add to it in passing; don't treat it as work
that's due.

---

## Outstanding right now

### Phase E — class assets don't exist yet
`Character` no longer holds any stats; they all come from a `ClassDefinition`, and **a class is
required** — an asset without one logs an error and fights at 1 HP. Nothing about Phase E can be
verified until the class assets are authored and every Character and Enemy asset points at one.
See `META.md` Phase E.

### Three action assets are mis-authored
| Asset | Currently | Should be |
|---|---|---|
| `Slow` | Debuff / Speed / **-3** | **3** — Debuff already negates, so -3 makes the target *faster* |
| `Expose` | Debuff / Defense / **-3** | **3** — same double negation, currently makes the target *harder* to hit |
| `Sturdy` | Buff / **Accuracy** / 10 | Probably `Damage Reduction / 0.25` or `Defense / 3`; +10 accuracy isn't sturdiness |

`buffValue` is `[Min(0f)]` now, but that only clamps as the Inspector draws the field — the saved
negatives survive until each asset is opened and re-entered.

---

## Tuning & game feel

Notes on values that are *correct in code* but read as broken in play. Worth checking before
debugging a system that "doesn't work".

### ~~An "Attack" buff sounded like damage but changed accuracy~~ — RESOLVED by renaming
`BuffType.Attack` is now **`BuffType.Accuracy`**, which is what it has always done: it feeds the
to-hit roll and nothing else. `UpdateBuffDisplay()` and the tooltip both use `Type.ToString()`, so
the in-game label now reads "Accuracy: +2" and stops implying damage.

**Damage buffs are drive's job, deliberately.** Making accuracy buffs also raise damage was tried
and reverted — it would have let a character stack an accuracy buff and a drive spend into one
compounded damage number, and duplicated a system that already works. The two stay disjoint:

| | Affects |
|---|---|
| Drive stacks | Damage — multiplicative, 1.5×–2.0× |
| Accuracy buffs | To-hit roll — flat, additive |

**Renaming enum members is safe; reordering is not.** Unity serializes enums by integer value, so
`Accuracy` staying at position 0 keeps every authored Action asset pointing at the right buff.
Append new types at the end — a remark on the enum says so.

~~Still true: a *Defense* buff changes nothing, because the to-hit roll clamps.~~ **No longer true —
the roster has been retuned into the Phase B band and Defense is live.** Crew sit at defense 13,
enemies at 11/14/17 against attack powers of 5-15, so hit chances span 60-95% and there is real
headroom for the roll to move in. A -3 Defense debuff is worth +10 to +15 percentage points in most
matchups, in both directions.

The one asset still outside the band is **`Witch Healer` (defense 3)**, which everything hits at the
95% ceiling — a Defense debuff on it genuinely does nothing.

Speed buffs work as expected — initiative is `Speed + Random(1,9)`, re-rolled each round.

### `buffNextActionMultiplier` has a narrow usable band — roughly 1.4 to 1.6

Now a **per-class** value on `ClassDefinition`, so this is a decision per class rather than per
character. The dial is squeezed from both ends and the middle is not wide.

The multiplier for *n* committed stacks is `1 + baseBonus·(1 + d + d² + …)`, where
`baseBonus = buffNextActionMultiplier - 1` and `d = decayRate` (0.5), **hard-capped at 2.0×**.

**The ceiling.** At `2.0`, one stack already computes `1 + 1.0 = 2.0` and hits the cap — so stacks
2, 3 and 4 do *literally nothing*, and committing them is a pure loss. Anything at or above ~1.9 is
effectively a one-stack system. At `1.5` you get the intended 1.50 / 1.75 / 1.875 / 1.9375 curve.

**The floor.** Drive multipliers are applied to small integer damage ranges and then rounded in
`TakeDamage`. Below about `1.5` the bonus frequently moves the damage number by **0 or 1**, so
committing a whole segment — an expensive, deliberate choice — produces no visible feedback. The
system works; the reward rounds away. This is what made an early `1.25` character read as a broken
drive system.

Consequences worth keeping:

- **Diminishing returns compress the floor further.** Stacks 3 and 4 add `baseBonus·d²` and
  `baseBonus·d³` — at a low base bonus those are fractions of a point, i.e. segments spent for
  nothing after rounding.
- **Check legibility at the SMALLEST damage roll a class can apply**, not the midpoint. A multiplier
  that reads well on a 7-damage swing can be invisible on a 2-damage one.
- **The same rounding applies to heals** (`Heal` rounds too), so a healer class with low heal ranges
  has the identical problem.
- The narrowness of the band is itself an argument for spending drive **sideways** rather than
  deeper — see "Different ways to use drive".

### Parry drive is paid twice, so the two dials have to be set together

Also per-class now. A successful parry pays out from *both*:

1. `parryBonusDriveMultiplier` × damage prevented, and
2. `damageTakenDriveMultiplier` × the 25% chip that still lands, via the normal `TakeDamage` path.

**Reference point:** a clean parry should pay roughly **25 drive** — a quarter segment at the current
100-per-segment meter — for a typical ~5-damage hit. That's `parryBonusDriveMultiplier ≈ 5`.

**So a tank class wants a *lower* parry bonus, not a higher one.** A class built around
`damageTakenDriveMultiplier` (getting hit builds drive) is already collecting on the chip, and giving
it a high parry bonus on top double-dips. Historically the character with `damageTakenDriveMultiplier`
of 10 needed his parry bonus down near 2.5 to sit level with the rest of the roster.

If you'd rather it paid once, gate the drive block in `TakeDamage` behind `if (!wasParried)` — then
every class can use the same ~5 baseline. That's a feel change to the "tank builds drive by getting
hit" identity, so it should be its own decision.

---

## Bugs

### A player action sometimes doesn't seem to end the turn — INTERMITTENT, unresolved
**Status: partly fixed, root cause not established. Reported against `DriveGrant`, but nothing about
the symptom is specific to that action type.**

Symptom: after resolving an action the player appears to still be able to act, instead of the turn
passing on. Now happens *unreliably* rather than every time, which is the main thing to explain.

**Already fixed, don't re-fix:** `CombatController.PerformAction()` charges the cooldown and calls
`CompleteTurn()` from a `finally` (see `CLAUDE.md`). Previously an exception while applying an effect
skipped both, which doesn't stall the game — it hands out a **free, off-cooldown extra action**,
because the selection is still live and the buttons are still interactable. That path is closed:
entering `PerformAction` now always costs the turn. **So whatever remains is not this.**

Three candidates, in the order they explain *intermittency*:

1. **It may not be a bug at all — the turn is passing to another crew member.** Initiative is
   `Speed + Random(1,9)`, re-rolled every round, so two crew frequently act back to back. When they
   do, `CompleteTurn()` → `SetCharacterTurn()` repopulates the bar with the *next* crew member's
   actions, still interactable, and from the player's seat that is indistinguishable from "my turn
   didn't end". Frequency would track how often crew are adjacent in the turn order — which fits
   "works, but unreliably" better than either option below.
   **Discriminator:** the turn marker and the "X's Turn" label move, and the console logs
   `<name> completed their turn, buffs updated`.
2. **`PerformAction` is never reached.** `TryExecuteActionOnCurrentTarget()` returns early on an
   invalid target, correctly leaving the turn open — a misclick must not cost a turn. Intermittent
   if clicks sometimes land on the wrong combatant, or get eaten by UI over the sprite.
   **Discriminator:** console shows `Invalid target for action <name>`.
3. **An exception mid-resolution.** Now harmless to turn flow, but it would leave the effect
   half-applied. Most likely source is an MMF_Player with an unassigned target — the
   `driveGrantedFeedback` / `driveDrainedFeedback` slots are new and start empty.
   **Discriminator:** a red error in the console.

**Next step when picking this up:** it's intermittent, so catch it rather than reason about it —
note the acting character's name and the turn order at the moment it happens, and grab the whole
console rather than just the last line. A temporary log at the top of `SetCharacterTurn()` printing
who just gained the turn would settle candidate 1 immediately, which is worth doing first because it
is the cheapest to rule in or out and would mean there's no bug here at all.

### ~~Enemies could never miss~~ — DONE
`EnemyManager` rolled a d20 for **crits only** (`RollForCritical`), with no to-hit check anywhere in
the enemy path and no miss branch — every enemy attack landed unless parried. Two consequences that
had gone unnoticed: `attackPower` on all four enemy assets was authored and never read, and
`defenseValue` on **crew** was equally dead, since nothing ever rolled against it.

`EnemyManager.RollToHit()` now mirrors `CombatController`'s rule exactly — natural 1 misses, natural
20 hits, otherwise `roll + attackPower >= target defense` — and is applied on both the parryable and
direct attack paths. `ResolveMiss()` plays the target's `Miss()` and consumes the enemy's drive
stacks, matching the player's crit-miss branch.

**A miss skips the parry sequence** rather than opening a window over an attack that can't connect:
`EnemyAttack` spends the parry attempt on the first press whether or not it lands, so a window over
a guaranteed miss would cost the player their attempt for nothing.

*Balance note:* enemy attacks now land less often than they used to, so any tuning done before this
was against enemies that hit 100% of the time. Crew `defenseValue` is now live and worth checking.

### Stealth doesn't cover multi-target actions
`AllAllies` / `AllEnemies` still resolve against a single clicked target, so a party-wide stealth
isn't authorable yet — deliberately, since it would let the crew forfeit every enemy turn. When
multi-target lands (see "Multi-target attacks" in `META.md` Phase B), the stealth filter needs
applying per-target inside the loop, and `FilterHiddenTargets`'s all-hidden fallback becomes much
more likely to matter.

### `Action.TargetType` means different things to the player and to enemies
Found while adding the drive actions. `CombatController.IsValidTarget()` reads it **absolutely** —
`SingleEnemy` means "an object tagged `Enemy`". `EnemyManager.SelectTarget()` reads it
**caster-relatively** — its `SingleEnemy` case is commented "Enemy targeting players" and returns
players.

So the same enum member resolves to opposite sides depending on who owns the asset. It happens to
work out today because an action asset is only ever slotted on one side, and the two readings agree
for the common cases (an enemy's `SingleEnemy` attack wants players; a player's wants enemies).

**The authoring rule that follows:** `SingleEnemy` = "someone on the other side", `SingleAlly` =
"my side", and an action asset is **not** portable between crew and enemies — a `DriveDrain`
authored for the crew can't simply be dropped into an enemy's `actionSlots` and mean the same thing.

Worth unifying eventually, but it's a rename-and-audit job across every authored asset, so not now.

**Multi-target attacks are what make this urgent.** While every action hits one target the two
readings are merely confusing; once `AllEnemies` resolves against a whole side, one inverted read
means an enemy's AoE hits its own allies or a crew AoE hits the party. See "Multi-target attacks —
what it would touch" in `META.md` Phase B, which lists this as a prerequisite.

### ~~Enemy buff and debuff actions silently do nothing~~ — DONE
`EnemyManager` applied buffs by reflection: `HasMethod(buffTarget, "ApplyBuff")` then
`buffTarget.SendMessage("ApplyBuff", new object[] { … })` with **four** arguments, falling back to
`"AddBuff"` with **three**. `SendMessage` only ever passes a **single** argument, so the `object[]`
went in as one parameter and matched neither `AddBuff(BuffType, float, float)` nor any `ApplyBuff`
overload — and `ApplyBuff` never existed on `CharacterManager`. The bare `catch` swallowed the
failure, so there wasn't even a log line.

Now calls `buffTarget.AddBuff(...)` directly, matching `CombatController`, negating `buffValue` for
the debuff case. `HasMethod()` is deleted; there is no reflection left in the file.

Fixed as part of adding the protection buff (`BuffType.DamageReduction`) — that's the first buff
anyone would realistically want on an enemy, so shipping it against a no-op path would have made the
feature look broken on half the roster.

**Behaviour note kept deliberately:** `SingleAlly` still resolves to the casting enemy rather than
`SelectTarget()`'s pick, so an enemy buff is a self-buff. That differs from the `DriveGrant` branch,
which honours the pick. There is no `Self` target type to distinguish "me" from "an ally", and a
protection or accuracy buff is almost always meant for the caster — adding one is the real fix if
enemy support characters are ever wanted.

### `BuffType.Health` grants max health but no actual health
`CharacterManager.cs:260` folds Health buffs into `MaxHealth`, and nothing adjusts `CurrentHealth`.
So a `+10` Health buff widens the bar without healing anyone, and on expiry `MaxHealth` drops back
with `CurrentHealth` possibly left above it — every later `TakeDamage` then reads as a character
sitting over 100%.

Decide which it should be: a shield that grants the health too (raise `CurrentHealth` by the same
amount on apply, and clamp on expiry), or drop `Health` from the enum. **Until then don't author
actions using it** — the other three types are safe.

### ~~`CombatController`'s buff branch dereferences `buffEffectText` unguarded~~ — DONE
Fixed by deleting the text display outright. `buffEffectText` listed each buff and its remaining
turns; `BuffIconDisplay` now shows the same information as icons, and carries the turn count next
to each one if its optional `turnsText` is wired.

The field is gone from `CharacterManager`, along with the string building in `UpdateBuffDisplay()`
and the unguarded write in `CombatController`'s Buff branch — which ran *before* `AddBuff`, so a
missing reference lost the buff itself, not just the display.

`UpdateBuffDisplay()` is now just the `BuffsChanged` event plus the sustained-particle refresh.

*Editor step:* the `txt_buff_amount` GameObject under `CharacterUI` on `PlayerCharacter.prefab` is
now unreferenced — delete it.

### `GetModifiedAttackPower()` has no callers
`CharacterManager.cs:466` applies the drive multiplier to attack power, and nothing invokes it.
Drive reaches damage through `GetNextAttackDamageMultiplier()` instead, so `attackPower` affects
**accuracy only** and drive has no effect on accuracy at all.

Decide which is intended: either wire it into the to-hit rolls so drive also improves accuracy, or
delete it. Leaving it implies a relationship between drive and accuracy that doesn't exist.

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

### `TooltipUI` carries the whole Canvas across scene loads — known, deferred
`TooltipUI.Awake()` calls `DontDestroyOnLoad(gameObject)`, but it sits on `TooltipPanel`, a child of
`Actions` under the **Canvas**. `DontDestroyOnLoad` on a non-root object acts on the root of that
hierarchy, so the entire Canvas persists.

After a scene reload (the restart button) you get two Canvases: the stale one with the victory panel
still active and an empty action grid, and the fresh one `ActionsManager` correctly populated. The
stale one renders over the top. That single cause produced both "the victory UI won't hide" and "the
action bar is missing after the next encounter", and it's why stop-and-replay was the only thing that
fixed it — a domain reload clears the static and there's only ever one Canvas on a fresh Play.

The static `instance` is also never cleared in `OnDestroy`.

**Fix when it's wanted:** drop `DontDestroyOnLoad` (a tooltip is only meaningful inside an encounter,
same reasoning as `ParryInputManager`) and clear the static in `OnDestroy`. Deferred by choice — it
matters again as soon as anything reloads a scene, so Phase 3 can't ship without it.

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

### `ParryVisualFeedback`'s message path is now unused
Removing the test-only "FAILED" message left `ShowFeedbackMessage()`, `DisplayFeedbackCoroutine()`
and four serialized fields (`parryStatusText`, `parrySuccessColor`, `parryFailedColor`,
`feedbackDisplayTime`) with no live caller — the success `PARRY!` line above it was already
commented out.

**Not deleted on purpose:** `parryStatusText` is wired to `txt_parry_status` under `CharacterUI` on
the prefab, and removing the field would silently orphan a UI object that's the user's to manage.
Either delete both together, or keep the path for Phase C, where an authored impact effect on a
parry is a natural place to reuse it.

Note the successful-parry `"Parry!"` text is **not** part of this path — it comes from
`CharacterManager.Parry()` writing `actionStatusText`, a separate field.

### The drive utility actions are unreachable, and one of them is a stub
Two independent problems, either of which alone makes `ReduceCooldown` impossible to use.

**Nothing ever builds the buttons.** `DriveUI.CreateActionButtons()` is the only player-facing route
to `TryUseDriveAction()`, and it has **no callers** — it waits to be handed a container and a prefab,
and nothing ever does. `DriveUI` is on `PlayerCharacter.prefab` but not in the scene, so the utility
menu has never existed at runtime. `PlayerInputHandler` maps the Drive input to `TryAddDriveStack()`
and nothing else. The only live caller of `TryUseDriveAction()` anywhere is `EnemyDriveManager`'s
low-health heal branch.

**`ExecuteReduceCooldown()` does nothing.** It logs, returns `true`, and never touches a cooldown —
its own comment says it "would need to be implemented based on your action/cooldown system". Since
`TryUseDriveAction()` charges the segments *before* dispatching and treats `true` as success, wiring
a button to it today would spend 3 segments for a debug line. It's the only one of the four that's a
stub; `HealSelf` and `RemoveDebuff` are real, which is why the enemy heal branch works.

Consequence for tuning: **`reduceCooldownCost` is dead weight** and shouldn't influence decisions
about the meter's size. `healSelfCost` is the one that matters, since enemies actually reach it.

If it's revived, the cooldown reduction needs to act on `CharacterManager.actionCooldowns` — that
comment predates cooldowns moving off the ScriptableObject. See "Different ways to use drive" below.

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

## Defining hero classes — area for exploration

**Not a task list. A direction worth defining before Phase 4 needs it.**

> **Phase E answers the numbers half of this.** `META.md` now plans a `ClassDefinition`
> ScriptableObject supplying base stats, per-level growth and status resistances per class, which is
> what makes a class mean something numerically and gives §7's recruitment generator its bands.
> It deliberately does **not** cover the active/passive question below — class covers numbers only,
> because letting it gate identity, stats *and* loadout at once means no single thing is the real
> constraint. Everything below still stands as the open decision.

**Partly answered since this was written.** `ClassDefinition` now supplies every stat, and
`Action.allowedClasses` (a `ClassDefinition[]`, empty = anyone) is the **active** half of gating —
wired, though not enforced until Phase 5's loadout screen exists. The `CharacterClass` enum is gone;
the asset reference is the identity.

`minimumLevel` from the original commented-out hook is still unbuilt, and would sit beside
`allowedClasses` as a second requirement.

The direction: **classes should differ in what they can *do*, not just in their numbers.**

### Active vs passive

- **Active** — abilities only that class can use. The `allowedClasses` hook above is one shape
  (gate an `Action` by class); a per-class action pool is another. Note `Character.availableActions`
  already exists, unused, and was probably meant for exactly this.
- **Passive** — always-on traits with no home in the codebase at all. Buffs are turn-limited by
  design (`ActiveBuff.TurnsRemaining`), so passives can't reuse them without special-casing "never
  expires". They'd need either a permanent modifier list on `CharacterManager` or to be folded into
  `RefreshStats()` alongside the authored base.

### The worked example: class-gated drive

The clearest place classes could express themselves, because the machinery is already built and
merely ungated — see "Different ways to use drive" below.

| Class | Might spend drive on |
|---|---|
| A healer class | `RemoveDebuff` — shake off what enemies apply |
| A bruiser class | `BuffNextAttack` — the damage spike |
| A gunner class | `ReduceCooldown` — reload the big shot |
| A duellist class | ? — parry-adjacent, given the parry system already rewards drive |

**Build this as a field on `ClassDefinition`** — a `List<DriveAction>` per class — rather than a
`switch` on class somewhere. Every class-gated behaviour wants to be data on the class asset; a
switch is what made adding a class a code change in the first place.

`DriveManager.GetAvailableActions()` is the natural gate: it already decides what the utility menu
would offer, so filtering it by `characterData.characterClass` is a small change once the menu exists.
That makes drive read differently in each character's hands without new systems.

### Things to settle

- **Where gating lives.** Class on the `Action` (`allowedClasses`), class-specific pools, or class
  filtering at the point of use. Picking one matters — gating by class *and* level *and* loadout
  simultaneously means no single thing is the real constraint.
- **Passives need a home** before any are authored, or each will be special-cased.
- **Enemies share `Character`**, so they carry `characterClass` too. Decide whether it means anything
  for them or is player-only — and if it's player-only, that's a reason to move it off the base.
- **This is also the recruitment axis.** META.md §7 proposes a `CrewArchetype` per class for
  generating rank-and-file crew, so whatever combat identity a class gets also defines what a
  recruited crew member plays like. Worth designing once, for both.

## Different ways to use drive — area for exploration

**Not a task list. A direction worth returning to.**

Drive was a stronger pillar in early development: a pool you *chose how to spend*, rather than one
button that makes the next hit harder. The machinery for that still exists — `DriveAction` has four
members and `TryUseDriveAction()` charges and dispatches them — but the choice was never surfaced,
and today drive collapses to "add a stack, hit harder".

Worth reopening because the interesting decision isn't *when* to spend, it's *what to spend on*.

### What's already half-built

| | State |
|---|---|
| `BuffNextAttack` | Live, and the only one players reach — it's what the Drive input does |
| `HealSelf` | Implemented, reachable only by enemies (`EnemyDriveManager` low-health branch) |
| `RemoveDebuff` | Implemented, unreachable |
| `ReduceCooldown` | **Stub** — charges segments, changes nothing. See the section above. |

So two of the four work and are simply not exposed. The gap is a menu, not a system.

**Partly answered by `DriveGrant` / `DriveDrain`.** "Spend on someone else" below is now built, from
the other direction: rather than donating your own segments, an `Action` moves raw meter — onto an
ally, or off an enemy. That makes drive a thing the party plays *with* rather than a private
resource, and it needed no drive-menu UI, which is what had blocked every other option here.

Follow-ups it opened:

- ~~**No targeting strategy picks by drive.**~~ **DONE.** `Enemy.TargetingStrategy.HighestDrive`
  added, resolved by `EnemyManager.GetHighestDriveTarget()`. Falls back to a random pick when every
  candidate is at zero drive — without that the opening turn of every fight would be deterministic,
  since an ordered pick over all-equal values always returns the same crew member.
- **Drain can't touch committed stacks**, by design (see `CLAUDE.md`). A "disrupt" effect that
  knocks the stacks off a charged-up enemy would be a genuinely different action, and it needs a
  way to reach `DriveManager.ClearStacks()`, which is currently private.
- **`HealthDrain` landed alongside it** — an attack that heals the attacker for a share of the
  health the target actually lost. See the drive/health drain notes in `CLAUDE.md`.

### Death blow drive bonus — idea, not built

Killing an enemy currently pays exactly the same drive as any other hit of that damage. A bonus for
the finishing blow would reward committing drive to close a fight rather than chipping, and would
give a reason to aim at the nearly-dead target that isn't purely "remove a threat".

What's already there and what isn't:

- **The hook exists and is unused.** `CharacterManager.OnDeath` has **zero subscribers**. It's the
  obvious place.
- **It doesn't carry the killer.** `OnDeath` is a parameterless delegate, so it can't say who to pay.
  Either it grows a `CharacterManager killer` parameter, or the award happens at the damage site —
  `CombatController` and `EnemyManager` both already know both parties there, which is probably
  simpler and avoids a per-death event that needs unsubscribing.
- **`OnDeath` fires TWICE per death** — once from `TakeDamage` when health hits 0, once from
  `Update` on the frame the object is destroyed. Harmless today only because nothing listens. Fix
  that before hanging anything off it, or the bonus pays double.
- Decide whether it's a flat amount, a fraction of a segment, or scaled by the victim's max health
  (so a boss kill pays more than a Skeleton). And whether **enemies** get it too — the drive economy
  has been kept symmetric so far.

### Overkill pays full drive, on both sides

`OnDamageDealt` receives the *rolled* damage, not health actually removed, so a 20-damage hit on a
3 HP target earns drive for 20. Both sides do this, so it's at least consistent — but it sits oddly
next to `HealthDrain`, which deliberately siphons from health **actually lost** so overkill isn't
rewarded.

`TakeDamage` already returns the health lost, so aligning them is a one-line change at each damage
site. Left alone because it's a balance decision — rewarding overkill makes big hits better than they
look, which may well be wanted — rather than an oversight.

### Directions worth exploring

- **Shake off debuffs.** `RemoveDebuff` exists and works, and **now has a reason to exist**: the
  `SendMessage` bug is fixed, so enemies can actually apply debuffs, and there are more worth
  shaking off than there were — a Speed or Defense debuff now genuinely bites, and a negated
  `DamageReduction` (vulnerability) or `CritChance` is a real threat. Still unreachable for the
  player only because nothing builds the drive utility menu.
- **Reset action cooldowns.** Needs real implementation against `CharacterManager.actionCooldowns`.
  The design question is whether it clears one chosen action (a decision, needs target UI) or all of
  them (simpler, blunter).
- **Buff heals as well as attacks.** Already supported — `GetNextHealingMultiplier()` exists and
  `Heal()` takes a caster multiplier — but nothing distinguishes "spend on damage" from "spend on
  healing" at the point of choosing, so it isn't a decision the player makes.
- **Spend on someone else.** Nothing in the model requires drive to be spent on its owner. A support
  character donating a segment is the kind of thing that makes a party feel like a crew.

### Constraints to design within

- **Most of the roster caps on the first stack.** With `buffNextActionMultiplier` at 2.0, one stack
  computes 2.0 and hits the hard cap, so stacks 2-4 do nothing for Kolo Aka, Biku Bale, Skeleton and
  Skeleton Elite. Any "spend more for more damage" axis is already saturated — which is an argument
  for spending *sideways* rather than deeper.
- **A shorter meter makes each choice weightier.** See the 4-vs-3 segment notes; overflow is
  discarded, so a big meter that only feeds one option mostly wastes drive.
- **Enemies use the same system.** `EnemyDriveConfig` already has a `strategy` enum, so any new
  option should be expressible as enemy behaviour too, or the two paths drift.
- **The UI is the actual missing piece.** `DriveUI.CreateActionButtons()` is written and waiting for
  a caller. Reviving this is mostly a UI and design job, not an engine one.

## Settings & accessibility

A holding pen for things a player may reasonably want to change, so they're grouped rather than
scattered across a dozen Inspectors. Add to it whenever a hard-coded value turns out to be a
preference — that's cheaper than rediscovering the list later.

**The settings *panel* is deliberately deferred.** The plumbing exists (`GameSettings`,
`SettingsBridge`), but building UI for a single toggle isn't worth it — the panel gets built once
several of these are ready, so it can be laid out for the real set rather than retrofitted. Until
then each built setting gets a `[ContextMenu]` on its owning component for in-Editor testing;
`ActionTargetingLine` has **Toggle Targeting Line Setting** on its ⋮ button.

**Where settings live — decided.** `Assets/Scripts/Settings/GameSettings.cs`, a static class over
`PlayerPrefs` with a `Changed` event. Explicitly *not* `RunState`, which is per-voyage and discarded
on death. **Add each new setting as a property on `GameSettings`**, never by reading `PlayerPrefs`
at the call site — that's what keeps keys, defaults and change notification in one place. UI wires
to `SettingsBridge`, since a static class can't be a UnityEvent target.

If settings ever outgrow a flat handful of scalars, `GameSettings` is the only thing that has to
change to move them to a JSON file beside `run.json`.

Also worth stating up front: **no setting should be the only channel for information.** Target
validity currently reads through *both* colour (line tint) and shape (cursor image), which is the
pattern to keep — a colour-blind player loses nothing, and so does a player who turns the line off.

### Motion & visual feedback

| Item | Existing hook |
|---|---|
| Show/hide the action targeting line | ✅ **Built.** `GameSettings.ShowTargetingLine`, default on, persisted. Needs only the UI toggle |
| Parallax auto-scroll speed, incl. off | ✅ `ParallaxController.SetScrollMultiplier(0)` |
| Screen shake / feedback intensity | None. MMF_Players are per-character on `CharacterManager`; needs a global scale or a Feel-level toggle |
| Damage number size | None. `actionStatusText` (`UI_action_text`) is a plain TMP_Text |
| Reduce or disable particle effects | None. `driveParticles` (ParticleImage) + `PF_PFX_hit` |

### Timing & input

The most important group — the parry is a real-time reflex check inside an otherwise turn-based
game, so it's the single biggest accessibility barrier here.

| Item | Existing hook |
|---|---|
| Parry window duration | `ParrySystem.parryWindowDuration` (0.3s) is serialized and has a public `ParryWindowDuration` getter, but nothing sets it at runtime |
| Parry assist / auto-parry / disable | None. Would need a branch in `EnemyAttack.BeginParrySequence()` |
| Drive stack input — hold instead of repeated presses | None. `PlayerInputHandler` maps one press to one stack |
| Confirm-before-acting (guard against misclicks) | None |
| Full remapping | Partial. `PlayerControls.inputactions` exists; no rebinding UI |

### Cursor

| Item | Existing hook |
|---|---|
| Custom cursor set | ✅ `CursorManager` owns all swaps |
| Cursor size / high-contrast variant | Needs alternate textures; `CursorManager` already the single swap point |

### Audio

| Item | Existing hook |
|---|---|
| Master / SFX volume | None. AudioSources and MMF_Sound feedbacks are per-character, so this needs mixer groups rather than per-object volume |

### Text & legibility

| Item | Existing hook |
|---|---|
| Text size / dyslexia-friendly font | None. TMP throughout, so a shared font asset swap is feasible |
| Tooltip dwell time / pin tooltips | None. `TooltipUI` shows on hover, hides on exit |
| Combat log | None. Currently only `Debug.Log` tracing — a player-facing log would also help anyone who misses a fast feedback |

## Content gaps

- `Assets/Scripts/Combat/Skelly Spear.asset` (5–10 dmg) is not in any enemy's `actionSlots` —
  orphaned.
- `Tephi`, `Witch Healer` (Witch), and `Skeleton Boss 2` (Killswitch) are authored but not placed
  in `Encounter.unity`. Note this is why Tephi's broken `40f` parry multiplier never showed up in
  play.
