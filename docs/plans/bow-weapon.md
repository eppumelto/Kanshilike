# Bow weapon

**Roadmap item:** not on the roadmap. `docs/PLAN.md` never mentions bows or arrows, and the demo scope table and task list don't call for them (`docs/architecture.md` also notes "Bows and deer aren't listed"). This plan finishes work that is already mostly built, at the user's request. It is a small detour from the demo tasks, not a demo task, so step 7 records it in the plan.
**Goal:** the player can find a bow and a bundle of arrows lying in the world, right-click to pick them up, equip the bow like any other weapon, and shoot wolves and deer with it.

## Design

### How far the bow already is

Confirmed by reading the code and assets. The game was not run, so step 1 checks this in play.

| Part | State |
|---|---|
| `BowItem`, `ArrowItem`, `ArrowProjectile` | Done |
| Shooting in `Character.CombatLoop`, `Attack`, `ShootArrow`, `HasGoodShot` | Done |
| Formulas in `CombatFormulaResolver` (`GetBow`, `GetBowSpread`, `GetBowHitChance`) and `CombatExchangeResolver.ResolveProjectileHit` | Done |
| Wounds: `CharacterStats.Wound`, read by `AnimalAI` to slow animals | Done, matches the arrow wounds design |
| `Assets/Items/Weapons/Bow.asset` and `Arrow.asset`, with icons | Done |
| Equip and unequip: `InventoryActions.TryEquip` handles any `EquippableItem`, and `twoHanded` displaces a shield | Done, no bow-specific code needed |
| Held model `bow_1_equipped.prefab`, projectile `arrow_1_projectile.prefab` | Done |
| `Assets/Skills/Bows.asset`, in both characters' `SkillSet` lists | Done |
| **World pick-up for the bow** | **Missing.** Axe, spear and sword have `axe_1`, `spear_1` and `sword_1`. The bow has no equivalent and exists only in Jonttuli's starting inventory (`test1.unity`, lines 4589-4596) |
| **World pick-up for arrows** | **Missing**, and `Collect` can only give one item (see below) |

### Decisions

- **Pick-ups follow the existing weapon pattern:** a prefab with `GenericInteractable` (`Collect`) and a non-trigger `BoxCollider` on the root, the same shape as `spear_1.prefab`. `SelectionManager` already sends right-clicks to it. No new interaction type.
- **A bow alone isn't usable, so arrows need a pick-up too.** Arrows are spent per shot, and `CombatLoop` stops with "Out of arrows" when none are carried. Without a source, a picked-up bow never fires.
- **`Collect` gains a count.** `GenericInteractable` calls `collectItem`, which adds one item and then destroys the object. Twenty single-arrow objects would be a chore (pillar 1), so `InteractionEntry` gets a `collectCount` field, set per prefab rather than in code (pillar 5). If only part of a bundle fits, the rest stays on the ground: no arrows are lost and no failure is silent.
- **No auto-equip.** Other weapons go into the inventory and are equipped with F, right-click or drag, so the bow does the same.
- **The world pick-up becomes the way to get the bow.** Jonttuli's starting bow and arrows look like a test shortcut, so they are removed unless the user prefers otherwise (see Open questions).
- **The combat loop is not touched.** Shooting already works, and the arrow wounds design says later skill outcomes belong in a separate change.

### Architecture

- **Finding 1 (humans can't fight humans), confirmed.** `Character.Attack` still takes an `IInteractable`, and only `Wolf.prefab` and `Deer.prefab` have `CombatEntityInteractable`. The bow can therefore only be ordered against animals until `AttackTarget` exists. A stray arrow can already hit a squad member, by design. This plan doesn't depend on the finding and doesn't address it.
- **Recommended order:** no conflict in the code. The plan adds nothing to the archery code in `Character`, which findings 1 and 2 will move. It does come ahead of the "Combat and orders" change that the architecture review puts first, and it doesn't advance that work.
- **Finding 5 (speed effects), confirmed.** `Character` overwrites `agent.speed` from encumbrance alone, so wounds slow animals but not people. That fits the current design and is left alone.
- **Hardcoded XP, confirmed.** `ArrowShotXP` and the 10 and 3 XP values in `HandleArrowHit` are in code. They are left with the matching melee values, to move together when combat moves out of `Character`.

## Steps

1. **Baseline check (Unity Editor, no changes).** In `Assets/Scenes/test1.unity`, equip Jonttuli's starting bow and attack a wolf.
   *Done when:* arrows fly, the stack count drops and a hit wolf slows. If shooting is broken, stop and report it, because the plan assumes it works.

2. **Add a count to `Collect`.**
   - `Assets/Scripts/Interaction/GenericInteractable.cs`, `InteractionEntry`: add `[Tooltip("Collect only: how many of the item this pick-up holds, e.g. a bundle of arrows.")] [Min(1)] public int collectCount = 1;`.
   - Same file, `ExecuteInteraction`, `Collect` case: treat the count as `Mathf.Max(1, entry.collectCount)`, call `interactor.CollectItems(entry.item, total)` and return if nothing was taken. If some remain, store them in `entry.collectCount` and return. Otherwise set `consumed` and destroy the object as now. `Harvest` is unchanged.
   - `Assets/Scripts/Character/Character.cs`: add `public int CollectItems(Item item, int count)`, which uses `inventory.Add(item, count)`, returns how many were taken, shows "Inventory full" when any don't fit, and grants no XP: picking up weapons or objects never gives Harvesting XP. `collectItem(Item, float)` calls it with a count of 1, then grants the `Harvesting` XP, and keeps its signature and result, so `Harvest` still works and still gives XP.
   - Use tabs in both files.

   *Done when:* the project compiles and existing pick-ups (axe, spear, shield, hats) still give one item. Their serialized data lacks the new field, so it keeps its default of 1.

3. **Write the bow's tuning values into its assets.** The fields were added after these assets were made, so they currently run on code defaults and don't appear in the files.
   - `Assets/Items/Weapons/Bow.asset`: add `minHitChance: 0.15` and `maxPursuitDistance: 45`.
   - `Assets/Items/CombatFormulaConfig.asset`: add `aimSkill: 5`, `aimSkillShare: 0.25`, `arrowWoundSpeedMultiplier: 0.4` and `minWoundSpeedMultiplier: 0.2`.

   Values match the code defaults, so behaviour doesn't change. *Done when:* the Inspector shows these same values after import.

4. **Create `Assets/Prefabs/Items/bow_1.prefab` (Unity Editor).**
   - Root: empty GameObject named `bow_1` at scale 1, with a non-trigger `BoxCollider` large enough to click from the RTS camera (about 0.4 x 0.2 x 1.2).
   - Root: `GenericInteractable` with `objectName: Bow` and one entry: `id: pickup`, `displayName: Pick up`, `actionType: Collect`, `item: Bow.asset`, `collectCount: 1`.
   - Child: a nested instance of `bow_1_equipped.prefab` at local scale 15, rotated to lie flat. Scale 15 matches the hand sockets, so it looks the same size on the ground as in the hand.

   *Done when:* the prefab and its `.meta` exist, generated by Unity, and the bow lies flat without sinking into the ground.

5. **Create `Assets/Prefabs/Items/arrows_1.prefab` (Unity Editor).**
   - Root as in step 4, with `objectName: Arrows` and `displayName: Pick up`, `actionType: Collect`, `item: Arrow.asset`, `collectCount: 20`.
   - Visual: a few thin cylinders using `Assets/Materials/SpearMaterial.mat`, the material the existing bow and arrow prefabs use.
   - **Do not nest `arrow_1_projectile.prefab`.** `ArrowProjectile.Update` destroys its object when `Time.time >= flightEndTime`, and that value is 0 until `Launch` runs, so the bundle would destroy itself on the first frame.

   *Done when:* the prefab contains no `ArrowProjectile` component.

6. **Place the pick-ups in `Assets/Scenes/test1.unity` (Unity Editor).**
   - Add one `bow_1` and one `arrows_1` beside `spear_1`, which is at (2.42, 0.406, -2.3). Start near (3.5, 0.2, -2.3) and (3.5, 0.2, -1.3), and adjust in the Scene view so both sit on the ground and on walkable NavMesh. Don't rebake the NavMesh.
   - Remove the bow and arrow entries from Jonttuli's `Inventory`. Keep the food entry.

   *Done when:* the scene diff shows only these two prefab instances and the two removed entries.

7. **Update `docs/PLAN.md`.** Add a ticked line under "Done", for example `- [x] Bows and arrows: pick up, equip, shoot; arrows are spent per shot and wound animals`, in the same commit as the code.
   *Done when:* the line is committed with the change.

8. **Play-test** using the checklist below (Unity Editor).

## Test plan

- **Automated:** new assembly `Kanshilike.Tests.EditMode`, in `Assets/Tests/EditMode/`, Editor-only and with no reference to `Assembly-CSharp`. `com.unity.test-framework` 1.6.0 is available only as a transitive dependency in `Packages/packages-lock.json`, so check it resolves when the assembly is added.

  These tests check asset wiring, which is where this feature can silently break. They read assets through `AssetDatabase`, `SerializedObject` and `GetComponent("GenericInteractable")`, so they don't need the game classes:
  - `Bow.asset`: `weaponSkill` is 10, `twoHanded` is true, and `icon` and `weaponPrefab` are set.
  - `Bow.asset` `attackRange` is below `chaseGiveUpDistance` in the `Brave`, `Deer` and `Shy` assets in `Assets/Scripts/NPC/Animals/Personalities/`. This is the rule stated in the `BowItem` comment.
  - `Arrow.asset`: `projectilePrefab` is set and `maxStack` is at least 20.
  - `bow_1.prefab`: root has `GenericInteractable` and a non-trigger collider, and its first entry is `Collect` with `Bow.asset` as the item.
  - `arrows_1.prefab`: the same with `Arrow.asset` and `collectCount` 20, and no `MonoBehaviour` whose type is named `ArrowProjectile` anywhere in the hierarchy.
  - `test1.unity` (read as text): references both new prefab GUIDs, and no `Inventory` entry starts with the bow.

  No standalone plain C# assembly is planned. The only new logic is one subtraction in `GenericInteractable`, and the rest depends on `Inventory` and `Character`, which are MonoBehaviours in `Assembly-CSharp`. Partial pick-ups are covered by the play-test instead.

- **Manual play-test** in `Assets/Scenes/test1.unity`:

  Pick up
  - [x] The bow and arrows are visible on the ground and can be right-clicked from normal camera distance.
  - [x] Right-click the bow: the character walks over, the bow goes into the inventory and the ground object disappears.
  - [x] Right-click the arrows: one stack of 20 appears, and no Harvesting XP is granted. The same goes for the bow and other weapons or objects. Harvesting blueberries still gives XP.
  - [x] With both characters selected, only one gets the bow and neither gets a duplicate.
  - [x] Bundle with a full inventory: "Inventory full" shows and the bundle stays.
  - [x] Bundle with room for only part of it: what fits is taken, the rest stays, and picking up again later gets the remainder.
  - [x] Axe, spear, shield, blueberries and well still work as before.

  Equip
  - [x] F, right-click and drag to the Hand slot all equip the bow, and it appears in the hand at a sensible size and angle.
  - [x] With a shield equipped, equipping the bow moves the shield to the bag. Equipping a shield while holding the bow moves the bow to the bag.
  - [x] Unequip returns the bow to the inventory. The icon looks right in both grid and slot, and weight changes correctly.

  Shoot, judged by what the player watches
  - [x] Attack a wolf from far away: the archer stops at range, turns, fires, and arrows arc visibly.
  - [x] The arrow count drops by one per shot.
  - [x] A hit wolf slows and charges the archer. It can still bite in melee.
  - [x] A hit deer flees, slows, and can be caught and finished. Does this feel like a hunt rather than a chase that misses a lot?
  - [x] Missed arrows stick in the ground and disappear after about 10 seconds.
  - [x] Running out of arrows shows "Out of arrows" and the attack stops. Picking up more arrows lets the archer attack again.
  - [x] A target that flees beyond 45 m shows "Target got away".
  - [x] An arrow can hit a squad member standing in the line of fire.
  - [x] After about 9 hits, the Console shows `[Skills] ... Bows reached level 1`. The skills panel has no Bows row (see Out of scope).

  Loot
  - [x] Let the archer die and loot the corpse with the other character: the arrows and the bow can be taken and used.

## Out of scope

- Skill-driven miss, wound and one-hit-kill outcomes (later, per the arrow wounds design)
- Shooting human NPCs by order (needs architecture finding 1), NPC archers
- Wound slowdown on humans (architecture finding 5)
- Recovering spent arrows, crafting bows or arrows, arrows as animal or NPC loot
- A quiver slot or an ammo count in the UI
- A Bows row in `SkillsPanel`. The scene lists only skills 0 to 4, so Spears, Swords, Block and Dexterity are missing too. This would be a UI pass of its own
- Melee behaviour while holding a bow. Today a counter uses the bow's damage with no arrow spent, and an archer keeps shooting at point-blank range with no penalty. Note these if the play-test shows they matter
- Moving arrow XP values into `CombatFormulaConfig` (architecture "Hardcoded XP")
- Spear and sword `SkillDefinition` assets, which don't exist, so those skills earn no XP
- A dedicated bow mesh. The current primitives are enough for the demo
- Crossbows: ruled out, ranged weapons are bows only

