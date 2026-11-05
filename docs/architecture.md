# Architecture review

Reviewed 2026-11-05, on `main` at `5a5ab79`, against the vertical slice demo in
[`PLAN.md`](PLAN.md). This pass read the code and the scene and prefab data, ran the EditMode
tests and checked the open scene in Unity; the game was not played. Check findings against the
code before acting on them, as it may have moved on.

**Summary:** healing, the first tests and the agent workflow landed well, and the structure is
in good shape for its size (about 6,600 lines). It still isn't ready for human NPCs. Six gaps
block demo tasks, and findings 1, 2 and 6 should be fixed as part of reaching the "Human NPC
base" task.

## Current structure

```
SelectionManager ──> PlayerControllable ──> Character ──> IInteractable
                                               │          (Generic / Combat / Corpse)
                                               ▼
AnimalAI ───────────> CombatExchangeResolver ──> CharacterStats, SkillSet, EquipmentManager
 (FSM + static helpers)    └─ CombatFormulaResolver + CombatFormulaConfig

InventoryScreen (UI) ──> InventoryActions ──> Inventory
CharacterHud, SelectionHud ──> SelectionManager
CharacterStats, Character ──> HealingRules (plain C#, Kanshilike.Healing assembly)
```

| Class | Job |
|---|---|
| `CombatActor` | Base class for anything that fights: `Character` and `AnimalAI` |
| `Character` | A person: walking, fighting, carrying, eating, treating wounds, carrying out orders |
| `PlayerControllable` | Squad membership: selection, ring and the live squad list |
| `CharacterHud` | Status bars and skills panel; shows the primary selected character |
| `AnimalAI` | State machine for wolves and deer; personality assets decide predator or prey |
| `HealingRules` | Healing formulas with no Unity dependency, so they can be tested alone |
| `Faction` | `Player` or `Wildlife`; a placeholder, see finding 3 |

## What works well

Keep these when changing the code around them.

- **Combat rules:** formulas are static, tuned through `CombatFormulaConfig`, and shared by
  melee, arrows and animals through `ICombatActor`.
- **Data:** items, skills, animal personalities and healing rates are assets, so new content
  rarely needs new code.
- **Inventory:** `InventoryActions` is kept apart from the UI, and each move either
  completes or changes nothing.
- **Squad list:** `PlayerControllable.All` and `Joined` replace scene searches, so members
  added during play reach the selection list, squad bar and game-over check. Nothing has
  used this yet, so adding a member during play is untested.
- **Pure rules:** `HealingRules` is plain C# in its own assembly with fast tests. `Relations`
  and the deed log should follow this pattern.
- **Workflow:** `/feature` and `docs/plans/` give each feature a design and review trail.
- **Unity settings:** scenes and prefabs are saved as text, so they can be diffed.

## Findings that block the demo

Numbers stay stable so plans can refer to them.

### 1. Humans can't fight humans

**Blocks:** Human NPC base.

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

### 2. Nothing can give a human NPC ongoing orders

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

- **Dev tooling in demo builds:** `Assets/Plugins/NuGet/McpPlugin.dll.meta` enables the DLL for
  all platforms, and the MCP runtime assembly definition excludes none. MCP and its networking
  DLLs (45 DLLs, 19 MB) may ship in player builds. This has not been confirmed with a real
  build. Do a test build before the first play-test.
- **Combat isn't testable yet:** the test assembly can't reference `Assembly-CSharp`, where
  `Character` and `CombatFormulaResolver` live, so the wiring tests search scene and source files
  as text. Move the combat formulas into a rules assembly before the shared attack work, and make
  it a single `Kanshilike.Rules` assembly rather than adding one per feature.
- **`Character` is still growing:** it is 805 lines, up from 704 before healing. Most of it is
  combat, archery and action handling, and findings 1 and 2 are the natural time to move that out.
- **Item use is spread out:** `TryConsume`, `TryTreatWounds` and `InventoryWindow` each check item
  types separately, so every usable item adds another branch.
- **Game logic tied to UI:** `CharacterStats` creates the corpse, and `Corpse` opens the inventory
  screen directly.
- **Combat events:** `OnAttackWindup` and the related events have no subscribers, only `Character`
  raises them, and no script uses an `Animator`. Damage is applied instantly, so timing it to an
  animation will mean separating "resolve" from "apply".
- **Hardcoded XP:** attacker XP is written into `Character`, while defender XP lives in the config.
- **Repo housekeeping:**
  - There is no `.gitattributes`.
  - `Old/` still compiles, and only `SampleScene` uses it.

## Deliberately left for later

None of these affect whether the feud idea is fun, so they can wait until the demo has been judged.

- Save system and stable item IDs (the plan says the demo needs no saving)
- World streaming
- Throttling animal perception, which runs an allocating physics search per animal per frame
- A project-wide asmdef split
- Replacing the `OnGUI` game-over screen, or unifying the three UI styles

## Recommended order

1. **Rules assembly:** move the combat formulas in and add tests, as a safety net for step 2.
2. **Shared attack and orders:** `AttackTarget`, the current-order object and self-defence, as one
   change, with treatment and sleep moved onto orders. Test it with one hostile human NPC in `test1`
   before building on it.
3. **Human NPC base:** the `Character` prefab, `NpcBrain` and shared vision settings.
4. **Identity, `Relations` and the deed log:** with tests, replacing the `Faction` rule.
5. **Interaction menu:** with hover info for relations.
6. **Speed modifiers and crouch:** when stealth work starts.
7. **Test build:** check for MCP before the first play-test.
