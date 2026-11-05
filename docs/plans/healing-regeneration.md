# Healing and regeneration

**Roadmap item:** "Healing and regeneration (survival item 1)", the first task under "Current milestone: vertical slice demo" in `docs/PLAN.md`, detailed in "### 1. Healing and regeneration". It is in demo scope ("Survival kept as it is now, plus healing and campfire resting").
**Goal:** wounds now heal between fights. Characters recover slowly on their own and faster while sleeping, and can bind a wound with a Moss Dressing for quick healing. Doing that trains a new First Aid skill.

**Blocking open questions:** none. The work can start with the defaults below.

**Downed state: OUT of this plan and the demo.** See "Downed state" under Design. The roadmap line stays unticked.

## Design

### What the code does today

Confirmed by reading the current code and scene data. The game was not run.

| Fact | Where | Why it matters |
|---|---|---|
| `Heal()` has no callers | `CharacterStats.cs:151` | Changing its return type breaks nothing |
| One in-game hour is 2 real minutes | `test1.unity:907`, `timeMultiplier: 0.5` | All rates below are judged against this |
| Needs tick from `UpdateStats`, which returns early when `hasNeeds` is off | `CharacterStats.cs:77` | Wolves and deer have `hasNeeds: 0`, so they will not regenerate |
| **A sleep can't be cancelled by an order.** The sleep coroutine runs on `CharacterStats`, but `Character.CancelCurrentAction` only stops coroutines on `Character` | `CharacterStats.cs:123`, `Character.cs:201` | A character could walk off and fight while "asleep". With faster healing, that becomes an exploit, so it must be fixed here |
| Being hit doesn't wake a sleeper. Only death stops the routine | `CharacterStats.cs:142` | Same exploit |
| Every swing and arrow calls `NotifyAttacked`, hit or not | `CombatExchangeResolver.cs:48`, `:101` | One place to wake a sleeper or interrupt a treatment. Starvation doesn't call it, so hunger won't wake anyone |
| Hourly hunger and thirst from `UpdateStats` keep running during sleep, and `SleepRoutine` adds its own half-rate loss on top | `CharacterStats.cs:88`, `:181` | Sleeping costs 1.5x the waking rate, not the "slight" loss its comment describes. Left as is (see Out of scope) |
| The health bar is on `SquadCard`. `CharacterHud` has no health slider | `SquadCard.cs:100` | Healing is already visible, so no new UI is needed |
| `SkillsPanel` in the scene lists only skills 0 to 4 | `test1.unity:3314` | First Aid won't have a row, the same as Bows, Block and Dexterity |
| An item with no icon is invisible in the grid | `UIFactory.cs:78` | The new item must have a sprite |
| Skill assets are in `Assets/Skills/`. `Assets/Skills/Data/` doesn't exist, though `CLAUDE.md` and `SkillType.cs` name it | Project folders | `FirstAid.asset` goes beside the others in `Assets/Skills/` |
| Next free `SkillType` number is 11 | `SkillType.cs` | `FirstAid = 11` |

### Decisions

- **One healing tick, in `UpdateStats`.** The roadmap says to hook into `SleepRoutine`. The routine only sets an `IsSleeping` flag, and `UpdateStats` picks the rate from it. Healing inside the routine as well would apply twice per hour.
- **The sleep rate replaces the natural rate**, so the numbers read directly and don't add up.
- **Any order, or any attack, wakes a sleeper.** The hours already slept are kept. This closes the exploit and means a short nap is a valid choice, which matters because a full night takes 16 real minutes.
- **The item is a "Moss Dressing".** The code type stays general as `HealingItem`. Dried bog moss bound with linen ties the item to the setting's bogs, and the historical use of sphagnum as a wound dressing makes it plausible for the era.
- **Treatment is a timed action on a right-click in the inventory**, the existing "use" path (`InventoryWindow.UseItem`). The character stands still with a label above their head. The healing and the item cost both come at the end, so an interruption costs only time (pillar 2).
- **Enemy swings interrupt treatment.** The player has to break away from a fight first. That makes it a decision made under pressure, not a mid-combat health potion.
- **No automatic use.** Auto eat and drink exists because those are routine (pillar 1). A dressing is a scarce item, so spending one is the kind of decision the pillars want. With up to 10 recruits on the force path this could turn into a chore, so that is on the play-test list.
- **Self-treatment only.** Treating someone else needs a right-click target on squad members and a choice of action. Neither exists (architecture findings 1 and 4).
- **First Aid improves dressings only**, not natural or sleep healing: stronger heal, shorter action. XP is per health point actually restored, so topping up a scratch earns almost nothing.
- **A source of dressings:** two in each starting inventory, plus a harvestable "Bog Moss" patch using the existing `Harvest` interaction with no new code. This gives a reason to stop and gather (pillar 3) without crafting, which the roadmap leaves for later.
- **Opt-in through a config reference.** `CharacterStats` gets a `healingConfig` field and does nothing without it. Only the squad is wired up now. The Human NPC task decides whether guards regenerate.

### Tuning defaults

All in assets (pillar 5). Nothing below is hardcoded.

| Value | Default | In real time | Where |
|---|---|---|---|
| `naturalRegenPerHour` | 3 HP | A 30 HP wound (6 wolf bites at 5 damage) heals in 20 real minutes | `HealingConfig.asset` |
| `sleepRegenPerHour` | 8 HP | A full 8-hour night gives 64 HP in 16 real minutes; a 4-minute nap gives 16 HP | `HealingConfig.asset` |
| `firstAidHealScale` | 1 | Heal x bonus | `HealingConfig.asset` |
| `firstAidSpeedScale` | 1 | Time / bonus | `HealingConfig.asset` |
| `minTreatmentSeconds` | 1.5 | Floor on the action time | `HealingConfig.asset` |
| `firstAidXPPerHealthPoint` | 2 | 50 XP for a full dressing, so the first one gives level 1 | `HealingConfig.asset` |
| `healAmount` | 25 HP | | `MossDressing.asset` |
| `useSeconds` | 4 s | | `MossDressing.asset` |
| `xpPerLevel` curve | (0, 50) to (100, 1000) | About 10 dressings reach level 6 to 7 | `FirstAid.asset` |
| `bonusCurve` | (0, 1), (20, 1.5), (100, 2) | Level 7 is about 1.17: 29 HP in 3.4 s. Level 100 is 50 HP in 2 s | `FirstAid.asset` |

The First Aid curves are deliberately faster than other skills. The demo is one session with few dressings, and the default curves would give about +2% after 10 dressings, too small to notice.

### Design pillars

- **Decisions, not chores:** after a fight the player chooses whether to spend a dressing, sleep at a bed while time and food pass, or press on hurt. Natural healing needs no input.
- **Warn before punishing:** regeneration stops only at an empty bar, and both bars already show on the squad card.
- **Few mechanics:** no new meter, and the campfire resting task can reuse the same `Sleep` path.

### Downed state: out

The optional Kenshi-style knockout is left out of this feature and the demo:

1. **Scope.** The demo table lists healing and campfire resting as the only survival additions, and the demo tests the feud idea, not survival depth.
2. **Size.** `IsAlive` is read in more than 30 places (AI targeting, perception, aggro, selection, combat, inventory windows, game over). `Corpse` and `OnDied` both assume 0 HP means dead. A downed state has to be handled at each of them, and that would be larger than the rest of this plan.
3. **Dependencies.** Reviving a downed friend needs "treat another character", which depends on findings 1 and 4. Keeping the player from losing the game while only downed also has no answer yet.
4. **Feud rules.** Architecture finding 3 plans to hook the deed log on `OnDied`, so what counts as a killing should be settled there first. Knocking out instead of killing may become a real choice for the feud design, which is a reason to design it with relations after the demo verdict, not now.

The honest cost: pillar 2 favours slow, recoverable failure, and death stays final in the demo. If play-testing shows that deaths feel unfair, this is the first thing to reconsider.

### Architecture

- **Order:** no conflict. `docs/architecture.md` states that healing "doesn't depend on any of this" and can go ahead at any time, and this plan needs none of its fixes.
- **Finding 2 (actions are coroutines cancelled by `StopAllCoroutines`), confirmed.** Treatment adds one more such coroutine to `Character`, and `CancelCurrentAction` now also wakes a sleeper. Both are small and sit in one place, so the later current-order object can absorb them. This plan doesn't address the finding.
- **Finding 5 (speed effects), confirmed.** `CharacterStats.Wound` lasts until death and `Character` ignores it, so healing doesn't touch the slowdown. If wound slowdown ever reaches humans, healing will need to reduce it.
- **"`Character` is still large", confirmed.** It is now 721 lines. Treatment adds about 50 lines beside `TryConsume`, because it must share the cancel path. This is a small step the wrong way, and acceptable until findings 1 and 2 move code out.
- **"No tests":** the formulas go in one small plain C# assembly (step 1), as the review suggests for testable logic. This is not a project-wide split.

## Steps

Don't stage the unrelated uncommitted changes to `Assets/Prefabs/Items/arrow_1_projectile.prefab` and `bow_1.prefab`. Use tabs in the game scripts edited here, and keep the existing 4-space style in `SkillType.cs`. Don't edit `TimeManager.cs`: it isn't needed, and it isn't valid UTF-8.

1. **Add the plain C# rules assembly.**
   - Create `Assets/Scripts/Healing/Kanshilike.Healing.asmdef` with `"name": "Kanshilike.Healing"`, `"noEngineReferences": true`, `"autoReferenced": true` and no references, so `Assembly-CSharp` sees it automatically and it depends on nothing.
   - Create `Assets/Scripts/Healing/HealingRules.cs`, a `public static class` using `System.Math` only:
     - `float RegenPerHour(bool asleep, bool starving, bool dehydrated, float naturalPerHour, float sleepPerHour)`: 0 if starving or dehydrated, otherwise the sleep or natural rate.
     - `float HealthGained(float current, float max, float amount)`: what is actually restored, never negative, and 0 when `current <= 0`.
     - `bool CanTreat(float current, float max)`: alive and below max.
     - `float TreatmentHeal(float baseHeal, float skillBonus, float scale)`: `baseHeal * (1 + (skillBonus - 1) * scale)`, the same "(bonus - 1) is progress" convention as `CombatFormulaConfig`.
     - `float TreatmentSeconds(float baseSeconds, float skillBonus, float scale, float minSeconds)`: `baseSeconds / (1 + (skillBonus - 1) * scale)`, never below `minSeconds`.
     - `float TreatmentXP(float healthGained, float xpPerHealthPoint)`.

   *Done when:* the project compiles and the assembly has no reference to `UnityEngine` or `Assembly-CSharp`.

2. **Add `FirstAid` to `Assets/Scripts/Skills/SkillType.cs`:** `FirstAid   = 11,`. Change no other number.
   *Done when:* the diff is that one line.

3. **Add the config and item classes.**
   - `Assets/Scripts/Character/HealingConfig.cs`: a `ScriptableObject` with `[CreateAssetMenu(fileName = "HealingConfig", menuName = "Survival/Healing Config")]`, and the six `HealingConfig.asset` fields from the tuning table, each with a tooltip and `[Min]`.
   - `Assets/Scripts/Inventory/HealingItem.cs`: `HealingItem : Item` with `[CreateAssetMenu(fileName = "NewHealingItem", menuName = "Inventory/Healing")]`, `healAmount`, `useSeconds` and `progressLabel`, and `Reset()` setting `maxStack = 10`. Check that `git status` shows the file under the same `Assets/Scripts/Inventory` casing as its neighbours, because the architecture review notes a case mismatch on disk.

   *Done when:* both appear in the Create menu.

4. **Regeneration and sleep state in `Assets/Scripts/Character/CharacterStats.cs`.**
   - Add `[Header("Healing")] [SerializeField] private HealingConfig healingConfig;` and `public HealingConfig HealingConfig => healingConfig;`.
   - Add `public bool IsSleeping { get; private set; }` and `public event System.Action OnWokeUp;`.
   - `Heal` now returns `float` (the health actually gained), computed with `HealingRules.HealthGained`.
   - Add `ApplyRegeneration()`, called in `UpdateStats` right after `ApplyStarvation()`: return if `healingConfig` is null, get the rate from `HealingRules.RegenPerHour(IsSleeping, IsStarving, IsDehydrated, ...)`, call `Heal`, and log one short line only if health was gained.
   - `Sleep` sets `IsSleeping = true` after starting the routine. `SleepRoutine` calls `WakeUp()` as its last line, after the existing wake log.
   - Add `public void WakeUp()`: do nothing unless `IsSleeping`; otherwise `StopAllCoroutines()`, clear the flag and raise `OnWokeUp`. `SleepRoutine` calling it on itself is safe because it is the routine's last line.
   - `NotifyAttacked` also calls `WakeUp()`. `ReceiveDamage` clears `IsSleeping` on death next to its existing `StopAllCoroutines()`.
   - Starvation calls `ReceiveDamage` but not `NotifyAttacked`, so it doesn't wake sleepers. Leave that.
   - Leave the garbled characters in the two existing sleep log lines (184 and 187) alone unless the edit requires otherwise.

   *Done when:* it compiles, and with the config unassigned, regeneration doesn't happen and sleep otherwise behaves as before, apart from waking on orders and attacks.

5. **Treatment and waking in `Assets/Scripts/Character/Character.cs`.**
   - Add `private Coroutine treatmentRoutine;`.
   - `CancelCurrentAction`: set `treatmentRoutine = null` and call `stats?.WakeUp()`.
   - `Start` / `OnDestroy`: subscribe and unsubscribe `stats.OnWokeUp` to clear the action label.
   - Extract the label code from `MoveAndPerform` into `private void ShowActionLabel(string text)`, and use it from `Sleep()` to show "Sleeping..." after `stats.Sleep(8f)`.
   - Add `public bool TryTreatWounds(Inventory.Entry entry, out string reason)` beside `TryConsume`:
     - fails with a reason if already treating ("Already binding wounds"), if `HealingRules.CanTreat` is false ("Not wounded"), or if the entry isn't a `HealingItem` in this inventory;
     - logs a warning and fails if `stats.HealingConfig` is null;
     - otherwise calls `CancelCurrentAction()`, resets the agent's path if on the NavMesh, and starts the routine.
   - `private IEnumerator TreatWounds(Inventory.Entry entry, HealingItem item)`:
     - show `item.progressLabel`, wait `HealingRules.TreatmentSeconds(...)` using `skills.GetBonus(SkillType.FirstAid)` (1 if `skills` is null);
     - clear the label and stop if `!inventory.Contains(entry)`, in which case nothing is healed or spent;
     - otherwise `stats.Heal(HealingRules.TreatmentHeal(...))`, `inventory.RemoveOne(entry)`, grant First Aid XP with `HealingRules.TreatmentXP`, show floating text `+{gained:F0} HP` and log one line;
     - set `treatmentRoutine = null`.
   - `HandleAttacked`: if `treatmentRoutine != null`, call `CancelCurrentAction()` and show "Interrupted" for 2 seconds.
   - `AutoConsume` and `TryConsume` are unchanged, so dressings are never used automatically.

   *Done when:* it compiles, and a right-click order, a new treatment and an enemy swing all clear the label.

6. **Hook up the right-click in `Assets/Scripts/Inventory/UI/InventoryWindow.cs`, `UseItem`.** Before the `TryConsume` branch add `else if (entry.item is HealingItem) character.TryTreatWounds(entry, out reason);`.
   *Done when:* a right-click on a dressing starts treatment, and at full health shows "Not wounded" with no item spent.

7. **Create the assets (Unity Editor).** Let Unity generate every GUID and `.meta` file.
   - `Assets/Items/HealingConfig.asset`, beside `CombatFormulaConfig.asset`, with the tuning table values.
   - `Assets/Skills/FirstAid.asset` (menu `Skills/Skill Definition`): `skillType` First Aid (11), `displayName` "First Aid", `maxLevel` 100, and the two curves from the tuning table with linear tangents, matching how `Bows.asset` is set up.
   - `Assets/Sprites/moss_dressing.png`: a simple placeholder icon, imported as a sprite like `berry.png`. It must be visible, because a null icon draws nothing.
   - `Assets/Items/Medicine/MossDressing.asset` (menu `Inventory/Healing`): `itemName` "Moss Dressing", `description` "Dried bog moss bound with a strip of linen. Takes a moment to apply.", the icon, `maxStack` 10, `size` 1x1, `weight` 0.05, `healAmount` 25, `useSeconds` 4, `progressLabel` "Binding wounds...".
   - `Assets/Prefabs/Items/moss_patch_1.prefab`: root with a non-trigger `BoxCollider` large enough to click from the RTS camera, and a low, flat, moss-green child made from a primitive. `GenericInteractable`: `objectName` "Bog Moss", one entry with `id` "harvest", `displayName` "Gather", `actionType` Harvest, `item` MossDressing, `harvestDuration` 4, `progressLabel` "Gathering moss...", `maxUses` 3.

   *Done when:* the assets and their `.meta` files exist and show these values in the Inspector.

8. **Wire up `Assets/Scenes/test1.unity` (Unity Editor).** On both Jonttuli and Keppana:
   - `CharacterStats.healingConfig`: `HealingConfig.asset`.
   - `SkillSet.definitions`: add `FirstAid.asset` as a ninth entry.
   - `Inventory`: add 2 Moss Dressings in a free cell. A waterskin stack is 1x2 at (0,0), so (1,0) is free.

   Place one `moss_patch_1` on walkable ground near the existing pick-ups. Don't rebake the NavMesh, and don't wire up the scene wolf, `Wolf.prefab` or `Deer.prefab`.
   *Done when:* the scene diff shows only these changes.

9. **Update `docs/PLAN.md`** in the same commit as the code:
   - Tick the demo task "Healing and regeneration" and the first four lines of survival item 1. Add a short design note under the item (rates in `HealingConfig`, Moss Dressing, self-treatment only).
   - Leave the downed line unticked and append "(deferred: not in the demo, decide after the verdict)".
   - Add to "Later / ideas": "Treat another squad member (needs the interaction menu)" and "Show First Aid and other missing skills in `SkillsPanel`".
   - Add to "Known issues": "Hunger and thirst tick twice during sleep (`UpdateStats` and `SleepRoutine`)" and "`CLAUDE.md` and `SkillType.cs` say skill assets go in `Assets/Skills/Data/`, but they are in `Assets/Skills/`".

   *Done when:* the lines are in the commit.

10. **Run the tests and play-test** using the Test plan below (Unity Editor).

## Test plan

- **Automated:** two groups, run in the existing `Kanshilike.Tests.EditMode` assembly in `Assets/Tests/EditMode/`. Add `"Kanshilike.Healing"` to its `references`. It keeps no reference to `Assembly-CSharp`.

  `HealingRulesTests.cs`, against `HealingRules`:
  - `RegenPerHour`: natural when awake; sleep rate when asleep, replacing natural rather than adding to it; 0 when starving, dehydrated or both, including while asleep.
  - `HealthGained`: clamps at max; is 0 at full health, at or below 0 health, and for negative amounts.
  - `CanTreat`: false at full health and when dead; true when wounded.
  - `TreatmentHeal`: base value at bonus 1; 1.25 bonus gives 31.25 from 25; scale 0 disables the skill effect.
  - `TreatmentSeconds`: base value at bonus 1; 1.25 bonus gives 3.2 from 4; never below the minimum.
  - `TreatmentXP`: scales with health gained, and is 0 for 0 gained.

  `HealingWiringTests.cs`, using `AssetDatabase` and `SerializedObject` like `BowWiringTests`:
  - `FirstAid.asset` has `skillType` 11, and its `bonusCurve` evaluates to 1 at level 0.
  - `SkillType.cs` (read as text) still has every number from `Unarmed = 0` to `Bows = 10`, and `FirstAid = 11`.
  - `HealingConfig.asset`: `sleepRegenPerHour` is above `naturalRegenPerHour`, and both are above 0.
  - `MossDressing.asset`: icon set, `healAmount` and `useSeconds` above 0, `maxStack` of at least 2.
  - `moss_patch_1.prefab`: non-trigger collider on the root, and a `Harvest` entry whose item is `MossDressing.asset`.
  - `test1.unity` (read as text): the `FirstAid.asset` and `HealingConfig.asset` GUIDs each appear exactly twice, and the moss patch GUID appears at least once.
  - `Wolf.prefab` and `Deer.prefab` don't contain the `HealingConfig.asset` GUID.

  `CharacterStats` and `Character` are `MonoBehaviour`s in `Assembly-CSharp`, so waking and interruption are covered by the play-test.

- **Manual play-test** in `Assets/Scenes/test1.unity`. To save time, set `TimeManager.timeMultiplier` to 30 in play mode (one hour every 2 seconds), and lower a character's `currentHealth` in the Inspector. Don't save those values. Run the "Feel" section at the default speed.

  Natural regeneration
  - [ ] A wounded character's health bar on the squad card rises by 3 HP per in-game hour, and the Console shows a line for it.
  - [ ] Health stops at maximum, and no line is logged at full health.
  - [ ] With hunger or thirst at 0: health falls and doesn't rise. After eating or drinking, regeneration resumes at the next hour.
  - [ ] A wounded wolf or deer doesn't heal.
  - [ ] A dead character doesn't heal.

  Sleep
  - [ ] Right-click the Bed: "Sleeping..." appears on arrival and health rises by 8 HP per hour, not 11.
  - [ ] After 8 hours the character wakes on their own and the label goes.
  - [ ] A move order wakes the character, the label goes, and the rate returns to 3 per hour. Health gained so far is kept.
  - [ ] Attack orders and other interactions also wake them.
  - [ ] A wolf attacking a sleeper wakes them on its first swing, even one that misses or is blocked.
  - [ ] Being at 0 hunger doesn't wake a sleeper or give healing, but health still falls.
  - [ ] A sleeper who dies becomes a corpse as usual, with no errors in the Console.

  Moss Dressing
  - [ ] Both characters start with 2, and the icon is visible in the grid. Hovering shows the name and description.
  - [ ] Right-click when wounded: the character stops, "Binding wounds..." shows, and after 4 seconds health rises by 25, "+25 HP" floats up and the stack drops by one.
  - [ ] Right-click at full health: "Not wounded", and nothing is spent.
  - [ ] Right-click again during treatment: "Already binding wounds", and no second item is spent.
  - [ ] A move order during treatment cancels it: no healing, and nothing spent.
  - [ ] A wolf's swing during treatment shows "Interrupted": no healing, and nothing spent.
  - [ ] Dragging the stack to a nearby corpse's inventory during treatment cancels it without healing or errors. Moving it within the same grid doesn't cancel it.
  - [ ] With a single dressing left, using it removes the stack.
  - [ ] A dressing is never used automatically, even at very low health.
  - [ ] Closing the inventory during treatment doesn't stop it.
  - [ ] Dying during treatment gives no errors, and the dressings can be looted from the corpse.
  - [ ] Bog Moss: right-click gathers one dressing after 4 seconds and gives Harvesting XP. The patch stops offering "Gather" after 3 uses. With a full inventory, "Inventory full" shows and no use is spent.

  First Aid
  - [ ] One full dressing gives the Console line `First Aid reached level 1`.
  - [ ] A dressing on a tiny wound gives far less XP than on a large one.
  - [ ] After several dressings, the amount healed is visibly higher and the action visibly shorter.

  Feel, at the default time speed
  - [ ] Fight a wolf, then recover. Is the choice between dressing, sleeping and walking on hurt a real one?
  - [ ] Is 16 real minutes for a full night too long to wait? Does the player use short naps instead?
  - [ ] Is 2 dressings plus a patch too few or too many for a fight or two?
  - [ ] Would treating a whole squad one by one feel like a chore? Consider this for 10 recruits on the force path.

## Out of scope

- Downed state (see Design)
- Treating another squad member (findings 1 and 4)
- Automatic use of dressings
- Bleeding, infection, per-limb injuries and warmth (kept out of the demo, survival items 5, 8 and 10)
- Rest quality by place, and sleeping by a campfire (the "Campfire and resting" task reuses this `Sleep` path)
- Skipping or speeding up time while the squad sleeps
- Crafting dressings; Bog Moss gives one directly
- Regeneration for animals, and any change to the arrow wound slowdown
- Regeneration for guards and villagers (decided in the Human NPC task)
- A First Aid row in `SkillsPanel`; the panel is already missing several skills and needs a UI pass of its own
- A health slider in `CharacterHud`; the squad card already shows health
- Fixing the double hunger and thirst tick during sleep; it is recorded in `docs/PLAN.md` instead, and changing it would alter the existing survival balance
- Moving the hardcoded 8-hour sleep and `restPerHour` into config
- Final art for the moss patch and dressing icon

## Open questions

None need an answer before work starts. One is for you after the play-test: a full night takes 16 real minutes because there is no time skip. If short naps don't solve that, time skipping should be added to the "Campfire and resting" task.
