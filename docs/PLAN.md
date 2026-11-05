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
6. **Relations and feuds are the driving force.** Surviving is half the game;
   the other half is navigating relations between families and villages. Problems
   can be solved in several ways (force, stealth, talk, or something in between),
   each with lasting consequences, and **the world remembers what you did**.

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
- [x] Bows and arrows

## Current milestone: vertical slice demo

**Goal:** a small, playable demo that answers one question: **is the feud idea
fun?** Everything below this section is the full game and comes later. Only
build what the demo needs, and keep the rest out until the demo has been played
and judged.

*Design note:* the demo is the smith's daughter quest (see "Feuds as the driving
force") from start to finish in a tiny world. Two approaches are enough to test
the idea: **force** and **stealth**. The demo runs as one play session, so the
world only has to remember deeds while the game is running: **no save system
needed yet**.

### Scope

| In the demo | Kept out (later) |
|---|---|
| 2 villages: the smith's home village and the raiders' village | More villages, a world map, settlements growing |
| About 6 named families split between the villages, some with relatives in both | Large family trees, marriages |
| One playable tribe (e.g. Hämäläinen) plus the raiders' tribe | Tribe choice, all five tribes, traders |
| Human NPCs: villagers and guards who walk, fight and sleep | Daily routines, jobs, trade |
| The quest: the smith asks for help, the daughter is held, return her | A quest system with many jobs |
| **Force:** recruit a band of villagers and attack | Pillaging, burning buildings |
| **Stealth:** at night, crouched, outside guards' view cones | Full stealth system |
| Deed log in memory: killed, helped, freed | Many deed types, saving |
| Relation scores with reasons shown in a simple UI | Full relation UI, score bands for every behaviour |
| **One revenge party** from a family the player wronged | Ongoing feuds, blood money, settling feuds |
| Survival kept as it is now, plus healing and campfire resting | Warmth, cooking, bleeding, encumbrance, injuries |

### Tasks (in order)
- [x] Healing and regeneration (survival item 1); the demo needs wounds to heal between fights ([plan](plans/healing-regeneration.md))
- [x] Shared attack and orders: one attack path and a current-order object for players and AI, plus self-defence, so human NPCs don't add a third way to fight (findings 1 and 2 in [architecture](architecture.md))

      *Design note:* combat formulas move into the plain C# `Kanshilike.Rules` assembly
      (`CombatRules`, next to `HealingRules`) with tests, before anything else changes.
      `CombatActor` owns the attack slot and the strike itself (`TryTakeAttackSlot`,
      `ResolveStrike`), which both `Character` and `AnimalAI` use. A `Character` runs one
      `CharacterOrder` at a time: move, attack, interact, sleep and treat wounds. Giving a new
      order ends the old one cleanly, replacing the hand-wired coroutines. `AttackTarget(CombatActor)`
      is the one way to start a fight, for the player and later for NPC brains. Self-defence: a
      character attacked by a hostile actor fights back unless it is walking somewhere or already
      fighting, and gives up a chase it started itself after a set distance. Archery moves out of
      `Character` into `CharacterArchery`. Behaviour for the player stays the same otherwise.
- [ ] Human NPC base: a `Character` prefab, with villager and guard prefabs on top of it, using the shared attack and orders and reusing the wolf AI's navigation and perception
- [ ] Demo map: two villages with houses, a smithy, a hut where the daughter is held
- [ ] Families and villages as data, with relation scores and a deed log (in memory)
- [ ] Simple relation UI: hover or select an NPC to see village, family, score and reasons
- [ ] Quest: talk to the smith, accept, free the daughter, bring her back, reward
- [ ] Prisoner: the daughter follows the player once freed; guards react if they see it
- [ ] Force path: recruit up to ~10 villagers into the squad for the attack
- [ ] Stealth path: night-time sneaking, guard view cones, being spotted triggers alarm
- [ ] Consequences: killings set family feuds; after the quest, a revenge party from a
      wronged family comes to the home village (force path), or tension stays low (stealth path)
- [ ] Campfire and resting on the road between the villages (simplified survival items 3–4)
- [ ] Play-test and write down the verdict below

### Fun test: questions the demo must answer
- Does choosing between force and stealth feel like a real decision?
- Does the revenge party feel earned (the player understands why it came)?
- Do players care about the named families, or are they just enemies?
- Does survival add tension on the road, or does it get in the way?
- Would a player want to play again and try the other approach?

**Verdict:** *(to be filled in after play-testing)*

## Next: survival mechanics

Full-game survival features, ordered by priority. Only item 1 (and a simple
version of items 3–4) is needed for the demo. Each item notes where the work goes.

### 1. Healing and regeneration
`CharacterStats.Heal()` is never called, so every wound is permanent.
Design: [plans/healing-regeneration.md](plans/healing-regeneration.md). Rates live in `HealingConfig` (`Assets/Items/HealingConfig.asset`); the item is a Moss Dressing, gathered from Bog Moss patches; treatment is self-only. Implemented and compiled; the play-test is still to do.
- [x] Slow natural regeneration per in-game hour, only while not starving or dehydrated
- [x] Faster regeneration while sleeping (hook into `SleepRoutine`)
- [x] Bandage / first-aid item that heals over a short timed action
- [x] First Aid skill (next free `SkillType` number, plus a `SkillDefinition` asset) that speeds healing and makes it stronger
- [ ] Optional: downed instead of dead at 0 HP (Kenshi-style knockout); only killed if damage continues (deferred: not in the demo, decide after the verdict)

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

## Tribes

Five tribes act as the game's character races. Each has its own attributes, with
strengths and weaknesses. Three are playable; the Swedes and Novgorodians come
from outside and are met as **traders and raiders**.

*Design note:* keep each tribe to **two strengths and one weakness**, and build
them from things the game already has: skill starting levels and XP rates
(`SkillType`), needs rates (`CharacterStats`), warmth, carry weight and trade
prices. No tribe should be the obvious best pick; each one suits a different
playstyle. The values below are a **first draft** to tune in play-testing.

### Playable

| Tribe | Home | Playstyle | Strengths | Weakness |
|---|---|---|---|---|
| **Suomalainen** (Finns proper) | South-west coast, Varsinais-Suomi | Farmer and trader | Better trade prices; food fills more (farming and fishing know-how) | Weaker in the wilderness: loses warmth faster |
| **Hämäläinen** (Tavastians) | Inland lakes and forests, Häme | Hunter and woodsman | Faster Harvesting and better loot from animals; resists cold (warmth lasts longer) | Worse trade prices (outsiders to the trade routes) |
| **Karjalainen** (Karelians) | East, around Lake Ladoga | Warrior | Higher starting combat skills; earn combat XP faster | Get hungry faster |

### Not playable: traders and raiders

| Tribe | Comes from | As traders | As raiders |
|---|---|---|---|
| **Ruotsalainen** (Swedes) | Across the sea, from the west | Arrive by boat on coasts and rivers; sell iron weapons, salt and silver | Raid coastal settlements in groups; well armed, heavy armour |
| **Novgorodilainen** (Novgorodians) | The east, from Novgorod | Travelling merchants with caravans; buy furs, sell cloth, jewellery and grain | Organised war bands that collect tribute; shields and spears |

### Tasks
- [ ] `TribeDefinition` ScriptableObject: name, description, playable flag,
      skill modifiers, needs-rate multipliers, warmth and trade modifiers
- [ ] A tribe field on characters; modifiers applied in `CharacterStats` and `SkillSet`
- [ ] Show the tribe on the character UI / squad card
- [ ] Tribe choice when starting a game or recruiting characters (playable tribes only)
- [ ] Needs human NPCs first (only wolves exist now): human NPC AI, plus the
      relations system in the next section
- [ ] Traders: a trading window that uses the inventory, with tribe-based prices
- [ ] Raiders: hostile groups that travel to and attack the player's camp or settlement
- [ ] Tribe-fitting names, clothing and equipment for each tribe

## Villages, families and relations

Every character has three layers of identity. Together they decide how the
character feels about the player and about other NPCs.

| Layer | Example | What it is | What it mainly affects |
|---|---|---|---|
| **Tribe** | Hämäläinen | Culture and "race" (see Tribes) | Attributes; a broad starting attitude between tribes |
| **Village / faction** | A Hämäläinen hill-fort village | The political group the character lives in and is loyal to | Alliances, wars, trade access, who is hostile on sight |
| **Family** (*suku*) | The Kurki family | Blood ties across villages | Personal loyalty, feuds and revenge, recruitment |

Families can span several villages, so the layers can pull in different
directions: a man from a hostile village may still help the player because his
sister's family is the player's ally.

### Feuds as the driving force

Relations and feuds are what the game is about, alongside survival (design
pillar 6). The detailed feud rules will be designed later; this is the vision
they must serve.

**Example: the smith's daughter.** A village is raided and the smith's daughter
is taken. The smith hires the player to bring her back. The player chooses how:

| Approach | What the player does | Consequence |
|---|---|---|
| **Force** | Raises a band of 10 men, pillages and burns the raiders' village | Daughter freed, but **every survivor and their families** now feud with the player |
| **Stealth** | Sneaks in at night, takes her and leaves | Few or no deaths; the raiders are angry but there may be no blood feud |
| **In between** | Kills only the guards, pays a ransom, talks or threatens | Consequences in proportion to the harm done |

The smith's family is grateful in every case. What differs is who else now has
a reason to hate the player.

**Principles**
- **The world remembers.** Deeds are stored permanently, per family and per
  person: who killed whom, who burned what, who helped. Survivors come back
  for revenge, years later in game time if needed. Nothing resets on its own.
- **Choices, not a right answer.** Force is fast and loud, stealth is slow and
  risky, talking costs money or favours. Each opens and closes different doors.
- **Feuds spread through families.** Because families span villages, one burned
  village can make enemies in places the player has never been.
- **Consequences are visible.** The player can always see which families hate
  them and why (relation UI), so feuds feel earned, not random.

**What this needs** (most of it after human NPCs exist)
- Jobs and requests given by NPCs (e.g. "rescue my daughter")
- Kidnapping and prisoners: NPCs can be taken, held and freed
- Stealth: sneaking, night-time cover, guards with sight lines
- Raising a band: hiring or calling in allies for one fight
- Burning and pillaging settlements
- A permanent deed log per family and per character, saved with the game
- Revenge: families with a feud send people after the player

*Design note:* each character's attitude is one **relation score** (-100 to 100)
toward the player and toward other groups, built from:
`tribe relation + village relation + family relation + personal history`.
To keep this readable (design pillar 1), the UI always shows **why**: e.g.
"Same tribe +10, village at war with yours -40, you saved his brother +30".
Score bands set behaviour: hostile (attacks), unfriendly (won't trade or talk),
neutral, friendly (better prices, help in fights), loyal (can be recruited).

**What changes relations**
- Trading, gifts, helping in fights and completing requests raise them.
- Stealing, attacking and killing lower them. Hurting someone angers their
  village; **killing** someone angers their whole family, wherever its members live.
- **Blood feud:** killing a family member can start a feud. That family's
  members become hostile and may come looking for revenge, even from friendly
  villages. Feuds can be settled by paying blood money or with time.
- Recruiting a character makes their family friendlier. Recruiting someone from
  a feuding family brings the feud into your squad.

**The player's side**
- The player's squad is its own small group with its own reputation, starting
  out as a household of one family (chosen at the start, with the tribe).
- Squad members keep their village and family. Their relations carry over to the
  squad, partly: a recruit's family likes you more; their family's enemies like you less.

### Tasks
- [ ] Data: `VillageDefinition` (tribe, home location, relations to other villages)
      and `FamilyDefinition` (name, tribe, members across villages)
- [ ] Village and family fields on characters, next to the tribe
- [ ] `RelationManager`: stores group-to-group relations and per-character personal history,
      and computes a character's relation score with its reasons
- [ ] Relation events: trade, gift, help, steal, attack, kill; who hears about each
- [ ] Behaviour from score bands: hostile NPCs attack, friendly ones trade and help
- [ ] Relation UI: show village, family and the reasons for the score on the character panel
- [ ] Design the detailed feud rules (see "Feuds as the driving force"), then build: start, revenge parties, settling with blood money
- [ ] Recruitment that uses relations (loyal characters can join the squad)
- [ ] Player household: choose tribe and family at the start of a game
- [ ] Later: marriages between families as alliances; village leaders; family trees

## Later / ideas

- [ ] Basic crafting (bandages, waterskins, spears) from harvested materials
- [ ] More wildlife fitting the setting: bears, elk, lynx
- [ ] Settlements and other NPCs: farmsteads, hill forts, traders
- [ ] Sauna as a base building (best rest, warmth and healing)
- [ ] Difficulty settings that scale survival rates (design pillar 5)
- [ ] Saving and loading the game (required for the world to remember deeds; see Feuds)
- [ ] Clean up or remove `Assets/Scripts/Old/`
- [ ] Treat another squad member (needs the interaction menu)
- [ ] Show First Aid and other missing skills in `SkillsPanel`

## Known issues

- Mixed indentation (tabs and spaces) across scripts
- Hunger and thirst tick twice during sleep (`UpdateStats` and `SleepRoutine`)
- `CLAUDE.md` and `SkillType.cs` say skill assets go in `Assets/Skills/Data/`, but they are in `Assets/Skills/`
- [x] Picking up weapons or objects (`Collect`) must never give Harvesting XP. Harvesting, such as picking berries (`Harvest`), keeps its XP.
