# Kanshilike roadmap

Living plan for the project. Tick items off in the same commit that finishes them.
Mark the item being worked on with **(in progress)**.

## Done

- [x] RTS camera, box selection, selection rings, select-all, squad bar
- [x] Character stats: health, hunger, thirst, tiredness (ticked hourly by `TimeManager`)
- [x] Empty hunger and thirst bars deal damage
- [x] Death: corpses, dead characters can't be selected, game over with restart
- [x] Combat: skill-scaled ratings, defenders earn XP, attacker slots, turning to face opponents
- [x] Shields: directional block (140° front arc), two-handed weapons (spear)
- [x] Wolf AI: perception, aggro memory, chase and combat, bites
- [x] Kenshi-style grid inventory: drag and drop, weight, stacking, looting
- [x] Food and drink, automatic eating and drinking, refillable waterskins
- [x] Interactions: Drink, Eat, Sleep, Collect, Harvest, Attack
- [x] Every wolf attack logged to the Unity Console

## Next: survival mechanics

Ordered by priority. Each item notes where the work goes.

### 1. Healing and regeneration
`CharacterStats.Heal()` is never called, so every wound is permanent.
- [ ] Slow natural regeneration per in-game hour, only while not starving or dehydrated
- [ ] Faster regeneration while sleeping (hook into `SleepRoutine`)
- [ ] Bandage / first-aid item that heals over a short timed action
- [ ] First Aid skill (next free `SkillType` number, plus a `SkillDefinition` asset) that speeds healing and makes it stronger
- [ ] Optional: downed instead of dead at 0 HP (Kenshi-style knockout); only killed if damage continues

### 2. Make tiredness matter
Tiredness rises and sleeping lowers it, but nothing reads the value.
- [ ] Penalties when tiredness is high: slower movement, attack speed and harvest speed
- [ ] Pass out when tiredness reaches 100
- [ ] Better rest in a bed than on the ground (a rest-quality value on the Sleep interaction)

### 3. Hungry and thirsty warning states
- [ ] Intermediate states (e.g. below 25) with small penalties before damage starts
- [ ] Show the state on the character UI / squad card

### 4. Cooking and spoilage
- [ ] Food freshness: raw meat spoils over time and spoiled food can be harmful
- [ ] Campfire interactable: cook Raw Meat into Cooked Meat (fills more, keeps longer)
- [ ] Cooking skill

### 5. Bleeding
- [ ] Wolf bites can cause bleeding (damage over time) until bandaged
- [ ] Ties into the bandage item from item 1

### 6. Temperature and weather
- [ ] Add `warmth` to `ClothingItem` (trade-off between armor and warmth)
- [ ] Cold nights from `TimeManager`; campfires are heat sources
- [ ] Penalties or damage from being cold

### 7. Encumbrance
- [ ] Moving over the weight limit slows you down
- [ ] Carrying heavy loads trains Strength and Athletics

### 8. Body-part injuries (bigger milestone)
- [ ] Per-limb health: a hurt leg slows you, a hurt arm weakens attacks
- [ ] Works with directional combat and the shield arc

## Later / ideas

- [ ] Basic crafting (bandages, waterskins, spears) from harvested materials
- [ ] More wildlife and other NPC types
- [ ] Saving and loading the game
- [ ] Clean up or remove `Assets/Scripts/Old/`

## Known issues

- Mixed indentation (tabs and spaces) across scripts
