# Kanshilike

A squad-based survival RPG inspired by Kenshi, built in **Unity 6 (6000.3.8f1)**.
You control a small group of characters who must eat, drink, rest, fight wildlife
(wolves for now), loot and survive in an open world.

**Setting: Iron Age Finland** (roughly 500 BC – 1100 AD): boreal forest, lakes,
bogs and harsh winters. Every character has a tribe (the game's races), a village
and a family; together they decide relations with the player and NPCs (see
"Tribes" and "Villages, families and relations" in the plan). **Relations and
feuds are the game's driving force next to survival: choices have lasting
consequences and the world remembers them.** Keep items,
buildings and names fitting the era. The
setting and the design pillars (survival should stay fun, not become a chore)
are in `docs/PLAN.md`; check new mechanics against them.

## Planning

**The roadmap and current work live in [`docs/PLAN.md`](docs/PLAN.md).**

**Current focus: the vertical slice demo** (see "Current milestone" in the plan).
Build only what the demo needs; full-game features wait until the demo has been
play-tested.

- At the start of a session, read `docs/PLAN.md` to see what is done and what is next.
- When you finish an item, tick it off (`- [x]`) in the same commit as the code change.
- When a new idea or bug comes up, add it to the plan instead of keeping it only in chat.
- Larger features get a short design note under their item in the plan before coding.
- Before changing how characters, combat, orders or relations fit together, read
  [`docs/architecture.md`](docs/architecture.md): the known gaps and their recommended order.

## Project layout

- `Assets/Scripts/` holds all game code. The main areas:
  - `Character/`: `CharacterStats` (health, hunger, thirst, tiredness), `Character` (a person: fights, carries, follows orders), corpses and corpse loot
  - `Combat/`: `CombatActor` (base for `Character` and `AnimalAI`), `Faction`, exchange and formula resolution, attacker slots, facing; tuning lives in `Assets/Items/CombatFormulaConfig.asset`
  - `Inventory/`: grid inventory, item types (`Item` ScriptableObjects), equipment, `Inventory/UI/`
  - `Interaction/`: `GenericInteractable` (Drink, Eat, Sleep, Collect, Harvest, Attack)
  - `NPC/Animals/`: `AnimalAI` for wolves and deer (perception, aggro memory, chase and combat), groups, spawning
  - `Selection/`: RTS selection, `PlayerControllable` (squad membership), `CharacterHud`, rings, squad bar
  - `Skills/`: `SkillType`, `SkillSet`, skill definitions and the panel
  - `TimeManager.cs`: the day cycle; it broadcasts `OnTimeChanged`, and needs tick hourly from it
  - `Old/`: legacy random terrain and village generation; not in active use
- `Assets/Items/`: item assets (Food, Drinks, Weapons, Shields, Headwears, Misc)
- `Assets/Skills/Data/`: `SkillDefinition` assets
- The scene in the build is `Assets/Scenes/test1.unity`.

## Conventions

- **Script file names must match their class names** (Unity requirement).
- **`SkillType` numbers are permanent.** Unity serializes enums by number: never change
  or reuse a value, and give new skills the next free number. Each new skill needs a
  matching `SkillDefinition` asset in `Assets/Skills/Data/`.
- Items are ScriptableObjects deriving from `Item` (or `EquippableItem`); create them via
  the `Inventory/...` asset menu.
- Input uses the old Input Manager (`Input.GetKey...`). Current bindings: WASD pan,
  Q/E rotate camera, I inventory, F use/equip in inventory, Ctrl+A select all,
  R and Esc in UI, LMB select, RMB order.
- Keep the indentation style of the file you are editing (most scripts use tabs).
- Commit `.meta` files together with their assets. Never hand-edit GUIDs.
- Gameplay logs go to the Unity Console (`Debug.Log`); keep them short and informative.

## Tooling

- The Unity MCP package (`com.ivanmurzak.unity.mcp`) lets an agent drive the Unity Editor.
  The skills in `.claude/skills/` only work when a Unity Editor is running and connected.
  Cloud sessions have no Editor, so there you can only edit code and assets as text;
  play-testing has to happen locally.

## Agent workflow

`/feature <description>` runs a feature through the subagents in `.claude/agents/`, then stops for you to review and commit:

1. `planner` writes `docs/plans/<feature>.md` and changes nothing else.
2. `implementer` carries out that plan.
3. `test-writer` adds and runs tests under `Assets/Tests/` only.
4. `reviewer` checks the changes against `docs/architecture.md` without editing.

Compiling and running tests need a connected Unity Editor; without one, agents report that work as not verified. Passing tests don't replace a play-test.

## Workflow

- Work on a branch, open a PR, and merge into `main`.
- One roadmap item (or a few small ones) per branch or PR keeps changes reviewable.
- Run the EditMode tests before merging, and don't merge while any fail.
