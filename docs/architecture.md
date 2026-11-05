# Architecture review

Reviewed 2026-11-05, on `main` at `5a5ab79`, against the vertical slice demo in
[`PLAN.md`](PLAN.md), and updated the same day after the shared attack and orders work, which
resolved findings 1 and 2. Check findings against the code before acting on them, as it may
have moved on.

**Summary:** people and animals now share one attack path, characters run one order at a time
and defend themselves, and the combat maths is tested. Four gaps still block demo tasks, plus the
NPC-side half of finding 2. Finding 6 (no character prefab) comes first, as part of the "Human
NPC base" task.

## Current structure

```
SelectionManager ──> PlayerControllable ──> Character ──> CharacterOrder (one at a time:
                                               │          Move, Attack, Interact, Sleep, TreatWounds)
                                               ▼
Character, AnimalAI ──> CombatActor.ResolveStrike ──> CombatExchangeResolver
                                                        └─ CombatFormulaResolver ──> CombatRules

InventoryScreen (UI) ──> InventoryActions ──> Inventory
CharacterHud, SelectionHud ──> SelectionManager
CombatRules, HealingRules: plain C# in the Kanshilike.Rules assembly, covered by EditMode tests
```

| Class | Job |
|---|---|
| `CombatActor` | Base class for anything that fights: `Character` and `AnimalAI`; owns the attack slot and the strike |
| `Character` | A person: carrying, eating, defending itself, and running one `CharacterOrder` at a time |
| `CharacterOrder` | What a character is doing: `MoveOrder`, `AttackOrder`, `InteractOrder`, `SleepOrder`, `TreatWoundsOrder` |
| `CharacterArchery` | Aiming and shooting for characters with a bow |
| `PlayerControllable` | Squad membership: selection, ring and the live squad list |
| `CharacterHud` | Status bars and skills panel; shows the primary selected character |
| `AnimalAI` | State machine for wolves and deer; personality assets decide predator or prey |
| `CombatRules`, `HealingRules` | Combat and healing formulas with no Unity dependency, so they can be tested alone |
| `Faction` | `Player` or `Wildlife`; a placeholder, see finding 3 |

## What works well

Keep these when changing the code around them.

- **Combat rules:** formulas live in `CombatRules`, are tuned through `CombatFormulaConfig`, and are
  shared by melee, arrows and animals. Every melee strike goes through `CombatActor.ResolveStrike`.
- **Orders:** `Character.GiveOrder` ends the current order before starting the next, and each order
  tidies up in `End`. New behaviour (follow, hold post, alarm) should be a new `CharacterOrder`, and
  NPC brains should give orders the same way the player does.
- **Data:** items, skills, animal personalities and healing rates are assets, so new content
  rarely needs new code.
- **Inventory:** `InventoryActions` is kept apart from the UI, and each move either
  completes or changes nothing.
- **Squad list:** `PlayerControllable.All` and `Joined` replace scene searches, so members
  added during play reach the selection list, squad bar and game-over check. Nothing has
  used this yet, so adding a member during play is untested.
- **Pure rules:** `CombatRules` and `HealingRules` are plain C# in `Kanshilike.Rules` with fast
  tests. `Relations` and the deed log should go in the same assembly.
- **Workflow:** `/feature` and `docs/plans/` give each feature a design and review trail.
- **Unity settings:** scenes and prefabs are saved as text, so they can be diffed.

## Findings that block the demo

Numbers stay stable so plans can refer to them.

### 1. Humans can't fight humans (resolved)

Resolved by the shared attack and orders work: `Character.AttackTarget(CombatActor)` starts every
fight, people and animals strike through `CombatActor.ResolveStrike` and its attack slot, and a
character attacked by a hostile actor fights back unless it is walking away or already fighting.
It was play-tested with a hostile human copy of a squad member. Kept below for the record.

**Blocked:** Human NPC base.

`Character.Attack` takes an `IInteractable`, and only the wolf in `test1` has
`CombatEntityInteractable`. The squad has no `IInteractable`, so a guard can't attack them
and they can't attack a villager. Animals avoid this by calling the combat resolver directly
through `AnimalChaseCombatExecutor`, which leaves two separate attack paths with their own
slot, range and timing logic.

Characters also don't defend themselves. They only turn to face an attacker, so an idle squad
member never hits back apart from a chance to counter when defending. Guards would behave the
same way.

**Fix:** add an `AttackTarget(CombatActor)` method used by players and AI alike. Interactions
become just one way to call it. Add self-defence in the same change.

### 2. Nothing can give a human NPC ongoing orders (partly resolved)

The order mechanism is done: `Character` runs one `CharacterOrder` at a time, and treatment and
sleep are orders now. Still to do, in the Human NPC task: `NpcBrain`, the follow and hold-post
orders, and vision settings that don't need an `AnimalPersonality`. Kept below for the record.

**Blocks:** Human NPC base, prisoner, force path.

Actions are coroutines cancelled by `StopAllCoroutines`, and the only orders are move,
interact and attack. The demo needs follow (the freed daughter, recruits), hold a post, sleep
and alarm. Healing has already added to this: `Character` now tracks `combatLoopRoutine` and
`treatmentRoutine`, while sleep lives in `CharacterStats` with its own label callback. Each is
wired into `CancelCurrentAction` and `HandleAttacked` by hand. The code looks correct, but every
new action built this way adds more special cases.

**Fix:** give `Character` one current-order object, with a small `NpcBrain` component that
issues orders the same way the player does. Move treatment and sleep onto it in the same change.
`NpcBrain` can share `AnimalPerceptionUtility`, but that currently takes an `AnimalPersonality`,
so move the vision settings to their own small type.

### 3. Hostility can't express relations

**Blocks:** families and villages, consequences.

`FactionRelations.AreHostile(Faction, Faction)` treats any other faction as an enemy, so
adding a village faction would make the home village attack the player. Relations in the plan
depend on the individual, their family and past deeds, and the player can be at feud with one
family but not its village.

**Fix:**

- Give each character an identity asset with tribe, village and family.
- Add a `Relations` service that decides hostility for a pair of actors.
- Keep `Wildlife` as a simple rule inside that service.

The hook for the deed log already exists: `CharacterStats.OnDied` passes the killer, including
arrow shooters. It only fires per character, so the log needs one place that hears every death.

### 4. Only the first interaction can be chosen

**Blocks:** quest, relation UI, force path.

`SelectionManager` always takes `options[0]` and sends it to the whole selection. A villager
who can be talked to, recruited or attacked needs a small menu, and talking should involve just
one character. The hover UI for relations also has no home, since nothing tracks what's under
the mouse except selection.

**Fix:** add an interaction menu and a hover target to the selection code.

### 5. Nothing combines movement speed effects

**Blocks:** stealth path.

`Character` overwrites `agent.speed` every frame from encumbrance alone, and `AnimalAI` does its
own version. Crouching needs a stance and a speed change, and the wound slowdown currently has
no effect on humans.

**Fix:** add one small function that combines speed multipliers, along with a crouch state that
guard perception can read.

### 6. There is no character prefab

**Blocks:** Human NPC base, force path.

`Character` exists only on scene objects in `test1` and `Iides_map`, with no prefab anywhere.
Villagers, guards and recruits all need one, and their settings (skills, healing config, combat
config) are currently set by hand on each object.

**Fix:** turn a squad member into a `Character` prefab early in the Human NPC task. The HUD has
already moved out of `Character`, so nothing scene-only should be needed on it.

## Smaller risks

- **`Character` size:** 536 lines, down from 805, with orders, archery and item use in their own
  places. Keep new behaviour in orders and item types rather than adding it back.
- **Damage is instant:** there are no animations yet. When they arrive, give people and animals one
  event seam for wind-up and impact, and split "resolve" from "apply" so damage lands on the impact.
- **Play-mode tests are scene-free:** they build characters in code with no NavMesh, so walking,
  chasing and archery are still only checked by hand.

### Resolved on 2026-11-05

- **Dev tooling in demo builds:** the MCP runtime doesn't start itself, but its code and NuGet DLLs
  used to ship. **Kanshilike > Build Windows Demo** (`Assets/Editor/DemoBuild.cs`) builds with the
  package's `UNITY_MCP_EXCLUDE_FROM_COMPILATION` switch and removes the plugin's unused libraries,
  after checking that nothing in the build refers to them. A test build ran with no load errors.
- **Behaviour testing:** game code is in the `Kanshilike.Game` assembly, so tests can reach it.
  `Assets/Tests/PlayMode` covers orders, self-defence, sleep, treatment, item use and corpses.
- **Item use:** each item type decides what using it means through `Item.TryUse`, and
  `Character.TryUseItem` is the one way in, for the inventory window and automatic eating alike.
- **Game logic tied to UI:** `CombatActor` leaves the corpse, and `Corpse` raises `LootRequested`,
  which the inventory screen listens to.
- **Unused combat events:** removed, since nothing listened and nothing animates yet.
- **Hardcoded XP:** combat XP is in `CombatFormulaConfig`; walking, hauling and harvesting XP are
  per-character settings under **Training**.
- **Repo housekeeping:** `.gitattributes` added; `Old/` and `SampleScene`, which only ran the
  old generators, removed (they are in git history).

## Deliberately left for later

None of these affect whether the feud idea is fun, so they can wait until the demo has been judged.

- Save system and stable item IDs (the plan says the demo needs no saving)
- World streaming
- Throttling animal perception, which runs an allocating physics search per animal per frame
- Splitting `Kanshilike.Game` into smaller assemblies
- Replacing the `OnGUI` game-over screen, or unifying the three UI styles

## Recommended order

1. **Human NPC base:** the `Character` prefab, `NpcBrain`, follow and hold-post orders, and shared
   vision settings.
2. **Identity, `Relations` and the deed log:** in `Kanshilike.Rules` with tests, replacing the
   `Faction` rule.
3. **Interaction menu:** with hover info for relations.
4. **Speed modifiers and crouch:** when stealth work starts.
5. **Test build:** check for MCP before the first play-test.
