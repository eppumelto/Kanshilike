# Kanshilike roadmap

Living plan for the project. Tick items off in the same commit that finishes them.
Mark the item being worked on with **(in progress)**.

## Setting

**Iron Age Finland**, roughly 500 BC – 1100 AD. Boreal pine and spruce forest,
lakes, rivers, bogs and long, cold winters. Small farmsteads and hill forts, fur
trade, iron smelted from bog ore. Wildlife: wolves, bears, elk, lynx.

Use the setting for flavour and for item and building choices, for example:
- shelters such as a lean-to (*laavu*) and a hut or tent (*kota*); a sauna as a late-game base building
- furs and wool for warmth; iron axes, spears and round shields; dried fish and meat
- Finnish place and character names

## Design pillars

The goal is survival that creates **decisions and stories, not chores**. Realism is
a source of flavour, not a rule to follow everywhere. Check every new mechanic against these:

1. **Every need has a clear, fun answer.** If a mechanic only asks the player to
   click a button regularly, automate it (like auto eat and drink) or cut it.
2. **Warn before punishing.** Show a warning state long before damage starts, and
   make failures slow and recoverable rather than instant deaths.
3. **Survival drives travel and planning.** Needs should push the player to choose
   where to go, when to camp and what to carry, not to micro-manage the squad.
4. **Few mechanics, each with weight.** Prefer one system that ties into others
   (the campfire covers warmth, cooking, rest and safety) over many small meters.
5. **Tunable.** Keep rates in config values so a difficulty setting can scale
   them, and so survival can be made gentler if play-testing shows it's tedious.

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
- [ ] Rest quality: how well a character sleeps depends on where (see item 3)

### 3. Safe resting places
The player needs somewhere to sleep without being eaten.

*Design note:* rest quality comes from the place: open ground (poor rest, wolves
may attack), by a campfire (normal rest, wolves keep their distance), in a shelter
such as a *laavu* or *kota* (good rest, warm). A settlement or home base is the
safest. Danger works through things the player can see coming: wolves are drawn
to sleepers in the dark and scared off by fire, and a squad member can stand
watch so sleepers are woken when something approaches.
- [ ] Rest-quality value on Sleep interactions (ground, fire, shelter, bed)
- [ ] Wolves avoid lit campfires; sleeping characters in the open can be targeted
- [ ] Night watch: an awake squad member wakes sleepers when a threat comes near
- [ ] Placeable shelter (*laavu*) built from wood and branches

### 4. Campfire building
The core of life on the road. One fire covers warmth, cooking, rest and safety
(design pillar 4).

*Design note:* three steps, nothing more: **craft** a campfire from wood, **place**
it on the ground, **click to light** it. No fire-starting tools, kindling or
tinder; lighting always works. The fire burns for a set number of hours and can
be fed more wood.
- [ ] Wood item from harvesting trees or deadwood
- [ ] Craft a campfire item from wood (first crafting recipe)
- [ ] Place mode: put the campfire down on valid ground
- [ ] "Light" interaction: one click lights it
- [ ] Campfire burns down over in-game hours; "Add wood" interaction; light radius at night
- [ ] Campfire is a heat source (item 5), a cooking station (item 7) and a safe-rest spot (item 3)

### 5. Warmth
*Design note:* a single **warmth** meter, not a full temperature simulation. It
falls at night and in winter and when wet, and rises by a fire, in a shelter and
with warm clothing. Being cold slows the character and stops healing. Only long,
neglected cold does damage, with a clear warning first (design pillar 2).
- [ ] Add `warmth` to `ClothingItem` (fur and wool are warm, iron armour is not)
- [ ] Warmth meter in `CharacterStats`, ticked hourly; colder at night via `TimeManager`
- [ ] Heat sources: campfire and shelter radius
- [ ] Cold states: chilly (warning), cold (slower, no healing), freezing (slow damage)
- [ ] Later: seasons and snow, which make winter travel a real expedition

### 6. Hungry and thirsty warning states
- [ ] Intermediate states (e.g. below 25) with small penalties before damage starts
- [ ] Show the state on the character UI / squad card

### 7. Cooking and spoilage
- [ ] Food freshness: raw meat spoils over time and spoiled food can be harmful
- [ ] Cook Raw Meat into Cooked Meat at a campfire (fills more, keeps longer)
- [ ] Smoked or dried meat and fish that keep for long journeys
- [ ] Cooking skill

### 8. Bleeding
- [ ] Wolf bites can cause bleeding (damage over time) until bandaged
- [ ] Ties into the bandage item from item 1

### 9. Encumbrance
- [ ] Moving over the weight limit slows you down
- [ ] Carrying heavy loads trains Strength and Athletics

### 10. Body-part injuries (bigger milestone)
- [ ] Per-limb health: a hurt leg slows you, a hurt arm weakens attacks
- [ ] Works with directional combat and the shield arc

## Later / ideas

- [ ] Basic crafting (bandages, waterskins, spears) from harvested materials
- [ ] More wildlife fitting the setting: bears, elk, lynx
- [ ] Settlements and other NPCs: farmsteads, hill forts, traders
- [ ] Sauna as a base building (best rest, warmth and healing)
- [ ] Difficulty settings that scale survival rates (design pillar 5)
- [ ] Saving and loading the game
- [ ] Clean up or remove `Assets/Scripts/Old/`

## Known issues

- Mixed indentation (tabs and spaces) across scripts
