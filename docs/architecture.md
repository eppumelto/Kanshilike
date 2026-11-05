# Architecture review

Reviewed 2026-11-05, on `main` at `816dbe6`, against the vertical slice demo in
[`PLAN.md`](PLAN.md). The review read the code and the scene and prefab data; the game was
not run. Check findings against the code before acting on them, as it may have moved on.

**Summary:** the structure is in good shape for its size (about 6,400 lines), but it isn't
ready for human NPCs yet. Five gaps block demo tasks, and the first two should be fixed
before the "Human NPC base" task starts.

## Current structure

```
SelectionManager ──> PlayerControllable ──> Character ──> IInteractable
                                               │          (Generic / Combat / Corpse)
                                               ▼
AnimalAI ───────────> CombatExchangeResolver ──> CharacterStats, SkillSet, EquipmentManager
 (FSM + static helpers)    └─ CombatFormulaResolver + CombatFormulaConfig

InventoryScreen (UI) ──> InventoryActions ──> Inventory
CharacterHud, SelectionHud ──> SelectionManager
```

| Class | Job |
|---|---|
| `CombatActor` | Base class for anything that fights: `Character` and `AnimalAI` |
| `Character` | A person: walking, fighting, carrying, eating, carrying out orders |
| `PlayerControllable` | Squad membership: selection, ring and the live squad list |
| `CharacterHud` | Status bars and skills panel; shows the primary selected character |
| `AnimalAI` | State machine for wolves and deer; personality assets decide predator or prey |
| `Faction` | `Player` or `Wildlife`; a placeholder, see finding 3 |

## What works well

Keep these when changing the code around them.

- **Combat rules:** formulas are static, tuned through `CombatFormulaConfig`, and shared by
  melee, arrows and animals through `ICombatActor`.
- **Data:** items, skills and animal personalities are assets, so new content rarely needs
  new code.
- **Inventory:** `InventoryActions` is kept apart from the UI, and each move either
  completes or changes nothing.
- **Squad list:** `PlayerControllable.All` and `Joined` replace scene searches, so members
  added during play reach the selection list, squad bar and game-over check. Nothing has
  used this yet, so adding a member during play is untested.
- **Unity settings:** scenes and prefabs are saved as text, so they can be diffed.

## Findings that block the demo

Listed in the order the plan's tasks reach them.

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
and alarm. Built separately, each adds more special cases to `Character`, and anything else
using a coroutine there gets cancelled too.

**Fix:** give `Character` one current-order object, with a small `NpcBrain` component that
issues orders the same way the player does. `NpcBrain` can share `AnimalPerceptionUtility`,
but that currently takes an `AnimalPersonality`, so move the vision settings to their own
small type.

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

## Smaller risks

- **Dev tooling in demo builds:** `Assets/Plugins/NuGet/McpPlugin.dll.meta` enables the DLL for
  all platforms, and the MCP runtime assembly definition excludes none. MCP and its networking
  DLLs (45 DLLs, 19 MB) may ship in player builds. This has not been confirmed with a real
  build. Do a test build before the first play-test.
- **No tests:** there are no tests, assembly definitions or namespaces. A test assembly with its
  own asmdef can't reference `Assembly-CSharp`, so asmdefs have to come first. Start with
  `Relations` and the deed log as plain C# in one small assembly, since that's the logic the demo
  depends on and it needs no scene.
- **`Character` is still large:** at 704 lines, most of it is combat and archery. Findings 1 and 2
  are the natural time to move that out.
- **Game logic tied to UI:** `CharacterStats` creates the corpse, and `Corpse` opens the inventory
  screen directly.
- **Combat events:** `OnAttackWindup` and the related events have no subscribers, only `Character`
  raises them, and no script uses an `Animator`. Damage is applied instantly, so timing it to an
  animation will mean separating "resolve" from "apply".
- **Hardcoded XP:** attacker XP is written into `Character`, while defender XP lives in the config.
- **Repo housekeeping:**
  - Git tracks `Assets/Scripts/Inventory`, but the folder on disk is `inventory`.
  - There is no `.gitattributes`.
  - `.claude/skills` files show as modified after each session.
  - Bows and deer aren't listed in `PLAN.md`.
  - `Old/` still compiles, and only `SampleScene` uses it.

## Deliberately left for later

None of these affect whether the feud idea is fun, so they can wait until the demo has been judged.

- Save system and stable item IDs (the plan says the demo needs no saving)
- World streaming
- Throttling animal perception, which runs an allocating physics search per animal per frame
- A project-wide asmdef split
- Replacing the `OnGUI` game-over screen, or unifying the three UI styles

## Recommended order

1. **Combat and orders:** `AttackTarget`, the current-order object and self-defence, as one change.
   Test it with one hostile human NPC in `test1` before building on it.
2. **`NpcBrain` and shared vision settings:** this is the plan's "Human NPC base" task.
3. **Identity, `Relations` and the deed log:** with tests, replacing the `Faction` rule.
4. **Interaction menu:** with hover info for relations.
5. **Speed modifiers and crouch:** when stealth work starts.
6. **Test build:** check for MCP before the first play-test.

Healing comes first in the plan and doesn't depend on any of this, so it can go ahead at any time.
