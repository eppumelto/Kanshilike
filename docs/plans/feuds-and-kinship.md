# Feuds and kinship (former phase 4): relationships, communities, grudges, revenge and blood price

> **Revisit this plan after phases 1-3 are playable; expect changes.**

**Roadmap item:** not checked. `docs/PLAN.md` and `docs/architecture.md` do not exist in the repo (checked again 2026-11-12). This was phase 4 of `docs/plans/human-npcs.md` and follows the design agreed with the user. Before starting, confirm whether feuds and kinship are in the vertical slice demo or come after it (open question 1).
**Goal:** People remember. Killing someone's kin or comrade starts a grudge that ranges from a cold refusal to a hunt, which the player can settle by sending an envoy to pay a blood price in valuable goods (swords, furs, silver) or with more blood. Kinship (parent, son, daughter, sibling, spouse) makes the closest feuds. People belong to a tribe, a village or camp led by an elder, a kin group and a household: the village holds the faction, kin stay kin across factions, and killing an elder sets the whole village feuding.

## What this plan needs from `human-npcs.md`
All of phases 1-3 working, in particular these seams (listed in that plan under "Seams for feuds"). This plan builds on them and does not change their code, except where the hooks table below says so.

| From human-npcs.md | Used here for |
|---|---|
| `FactionId`, `FactionRelationTable`, `FactionRelations` (`Table`, `PlayerId`, `Register`, `Find`), `FactionDefinition`, `DefinitionAsset` | person, community and death factions; community faction assets; tribe, kin group and household assets |
| `FactionRules.Resolve` and `CharacterFaction` (`CommunitySource`, `InvalidateAll`, `OwnFactionChanged`, `Explain`) | community members take their community's faction with no write-back |
| `Attitude` with `IAttitudeProvider`, `AttitudeResult.RefusesDealings`, `AttitudeReason` (append only, in `Assets/Scripts/Rules/AttitudeResult.cs`) | opinion, grudge, kin and Cold refusals |
| `RecruitBlock.Unwilling` (6), `Recruitable` calling `Attitude.RefusesDealings` and `RefusesSquad` | grudge holders refuse to join |
| `INpcRole` with `StillWants`, `NpcBrainRules.RoleMayReplace`, issued-role tracking | `RevengeRole` |
| `NpcBrain.HoldFor`, `InCombat`, `Character`, `Archetype`, `HomePosition`, `IsRoleOrder` | revenge, rally, hunting, talk pause |
| `NpcArchetype` | gains `vengefulness`, `sight`, `revengeChaseDistance` |
| `INpcInteractionSource`, `INpcTalkSource` | "Offer blood price", tribe barks, grudge barks |
| `InteractOrder.Target` and `ActionId` | parley |
| `PlayerControllable.Joined` | recruits leave their community |
| `NpcDebugReport.Providers`, the NPC Debugger window, `ISetupCheck`, the editor asmdef `Kanshilike.Game.Editor` | social debug sections, setup checks, the `SocialConfig` preview |
| `TestWorld`, `TestFactions` (PlayMode helpers) | PlayMode tests |
| `Townsperson.prefab`, `Townsperson_NoRecruit.prefab`, the `test1` anchors and townsfolk | content |

## Design

Four sub-phases. Each one ends in a working, tested state.

| Sub-phase | Delivers | Needs |
|---|---|---|
| 4a | People with stable ids, stored bonds and earned opinion, derived default bonds, the death ledger with killer attribution, grudges with four tiers, decay and feud limits, the personal attitude provider. The social structure as pure rules: tribe, community (with elder and faction), kin group and household ids on each person, `CommunityRegistry`, the community faction source, the close-kin attitude rule, news delays per layer, household blood price settlement. **Tools:** the social debug sections, the social setup checks, and the sectioned `SocialConfig` with its preview table. Mostly Rules code, plus the minimal game wiring to record deaths. | human-npcs phase 1 |
| 4b | Who learns about a death and when, authored bonds, opinion earned in play. `TribeDefinition` with skill leanings at spawn, `KinGroup` and `Household` assets, the `Community` scene component, `PersonTies` with all four memberships, village faction changes reaching live characters, recruits leaving their community. Grudges now appear in game. Setup checks for these components. | human-npcs phase 2 (`NpcArchetype`), 4a |
| 4c | `RevengeRole`: NPCs act on grudges (attack on sight, call friends, hunt), and the parley rule. | human-npcs phases 2 and 3, 4b, and a minimal perception step |
| 4d | Grudge barks on Talk and paying blood price in valuable goods, household settlement. The valuable-items setup check. | human-npcs phase 3, 4c |
| 4e | **Feud UI for the player:** a message feed, markers over grudge holders, the Feuds panel (feuds against the squad, blood spilled, the squad's own grudges, a Kin tab) and a confirm dialog before a blood price is paid. | 4d |

### Codebase facts (checked against current code, 2026-11-12, plus what human-npcs.md adds)
- `CharacterStats` raises `OnDamaged(float, GameObject source)`, `OnDied(GameObject source)` and `OnAttacked(GameObject attacker, bool blocked)` (CharacterStats.cs:49-52, 168-184). Melee hits pass the attacker root, counters pass the defender (CombatExchangeResolver.cs:55), arrows pass the shooter (:115), so a stray arrow that kills a bystander credits the archer. An arrow whose shooter was destroyed passes a destroyed object, which compares equal to null.
- Starvation and dehydration call `ReceiveDamage(damage, null)` (CharacterStats.cs:140). Null source means "unattributed".
- `TimeManager` has no day counter. `currentTime` wraps at 24, and `HourClock` treats a backwards jump as passing midnight. This plan needs days for decay, so it adds a pure `DayClock` that follows the same rule. Nothing else changes in `TimeManager`.
- `Character` has no `Awake`, `OnEnable` or `OnValidate`. `CombatActor.Awake` is `protected virtual`. Character already subscribes to `OnDied` and `OnAttacked` in `Start` and has `HandleDied`/`HandleAttacked`.
- After human-npcs phase 1, `Character.Faction` is resolved by `FactionRules.Resolve` (squad, then community source, then own) and cached. **Nothing writes a faction into a serialized field**, so a community's faction reaches combat by installing a community source and invalidating caches, not by writing.
- `AttackOrder` exposes `CombatActor Target` (AttackOrder.cs:20), so a faction change can find fights against members.
- Animals are `AnimalAI : CombatActor`, not `Character`, so they never get a person id. Their `Faction` is a `FactionId` (Wildlife).
- `Corpse.ExecuteInteraction` handles `loot`. Only the squad can loot today.
- `TreatWoundsOrder.Run` heals at the end (`patientStats.Heal(...)`, TreatWoundsOrder.cs:79).
- `PlayerControllable.Joined` (static event) fires on OnEnable, so it also fires for recruits.
- `Item` (`Assets/Scripts/Inventory/Item.cs`, abstract ScriptableObject) has `itemName`, `icon`, `description`, `maxStack`, `size`, `weight`. It has no worth or value field. `Inventory` exposes `Entries` and `RemoveOneOf(Item)`; `InventoryActions.InReach(Component, Component)` is the reach check.
- Content that can be valuable already exists: `Assets/Items/Weapons/Sword.asset` ("Sword", a WeaponItem) and `Assets/Items/Misc/WolfPelt.asset` ("Wolf Pelt", a MiscItem, `maxStack 3`, weight 1.5). There is no silver item.
- `SkillSet` (`Assets/Scripts/Skills/SkillSet.cs`) builds its skill entries in `Awake` from its `definitions` list and has `GainXP(SkillType, float)` (handles multi-level-ups), `GetLevel`, `GetBonus`. There is no `SetLevel`. Tribe skill leanings therefore go in as XP through `GainXP`, after `Awake`.
- There is no save system. All data in this plan is plain classes in Rules with only string, int, float, bool and enum fields (factions as `FactionId` strings), keyed by person id strings and with no object references, so a later save can write it as it is. No saving is built now.
- There is no "weapon drawn" state on characters; a character is either running an `AttackOrder` or not. The parley rule uses that.

### People: `PersonId` and `PersonRegistry` (4a)
- Every `Character` gets a serialized `personId` string (a GUID, `Guid.NewGuid().ToString("N")`):
  - Scene objects get one in `OnValidate` when it is empty, so Jonttuli and Keppana keep theirs across sessions.
  - On a prefab asset `OnValidate` clears it (`PrefabUtility.IsPartOfPrefabAsset`, inside `#if UNITY_EDITOR`), so each scene instance gets its own as an override.
  - At runtime `Awake` fills it if still empty (characters spawned in play).
  - Duplicating a scene object with Ctrl+D copies the id. At runtime, registering an id that another live character already holds gives the newcomer a fresh id and logs a warning. The setup validator and an EditMode wiring test catch duplicates in `test1` before that.
- `PersonRegistry` (Rules) maps id to a `PersonRecord`: name, sex (`Sex { Male = 0, Female = 1 }`, from a new serialized `Character.sex` field), own faction (`FactionId`), tribe id, community id, kin group id, household id, squad id (`"player"` or null), vengefulness, and state (`Alive 0, Dead 1, Gone 2`). Gone means destroyed without dying (despawned). Any membership id may be null. Memberships stay on the record after death, so bonds to the dead can still be derived (see derived bonds).
- The live `Character` for an id is in the Game-side `Social` directory (`Social.Find(id)`, null when dead or gone). Rules code never sees a `Character`.

### Social structure: tribe, community, kin group, household (rules in 4a, content in 4b)
Each person has four independent memberships. They are not nested: a kin group spans villages and factions, and a village can hold people of more than one tribe.

| Layer | Finnish | What it is | Authored as | Fixed? | Derived bond between members | News of a death |
|---|---|---|---|---|---|---|
| Tribe | heimo | Culture and descent: skill leanings, names, a small cultural bias toward other tribes. No leader, no politics. | `TribeDefinition` asset | Fixed at birth | none | none from the tribe itself |
| Community | kylä (village) or camp | Where you live. Holds the faction and has an elder. | `Community` scene component | Can move (recruiting leaves it) | Village: `Neighbour` +20. Camp: `Comrade` +40. Member to elder: also `Elder`, at least +40. | after `newsDelayHours` (3) |
| Kin group | suku | Extended lineage | `KinGroup` asset | Fixed | `Family` +30 | after `kinNewsDelayHours` (24) when not in the victim's household or community |
| Household | talo | Close family under one roof | `Household` asset | Fixed for the demo | `Family` +60, plus exact kin edges (authored) | after `newsDelayHours` (3) |

- **No free-text ids.** Tribe, kin group and household are `DefinitionAsset` assets picked with the object picker; a community is a scene component referenced directly. A typo cannot silently create a new family: there is nothing to type. Kin groups and households are **assets, not scene components**, because a kin group spans villages (and later scenes), and a household should be referable from any member without a scene object to point at; using the same kind for both keeps the inspector consistent. Their `Id` (from `DefinitionAsset`) is what the Rules records store.
- **Tribe (`TribeDefinition`, 4b):** `id`, `displayName`, `skillLeanings` (a list of `SkillType` and XP), `maleNames` and `femaleNames`, optional `barkLines`, and `opinionOf` (a list of other tribe and a small opinion, -30..30). Example assets: Hämäläiset and Karjalaiset.
  - **Skill leanings are XP**, applied once at spawn with `SkillSet.GainXP` from `PersonTies.Start` (after `SkillSet.Awake` has built its entries). XP rather than levels because `SkillSet` has no level setter, and XP goes through the existing level-up path and each skill's own XP curve. The sizes are open question 6.
  - **Names:** when a registering character has an empty `characterName`, `Social.Register` picks one from the tribe's list for its sex. Named scene characters keep their names.
  - **Barks:** `PersonTies` is an `INpcTalkSource` that adds the tribe's bark lines at priority 0 with hold, mixed with the archetype's.
  - **Cultural bias is an Attitude term, not stored opinion.** `SocialSimulation.AttitudeOf` adds `TribeOpinionTable.Get(selfTribe, otherTribe)` to the effective opinion, **only between strangers** (no effective bond flags either way), clamped to -100..100. Why a term and not relationship entries: entries for every stranger pair would fill the `RelationshipBook` with N² entries that would also have to be saved, and they would feed closeness and so create grudges between strangers. A term is sparse, never creates a grudge, and stops mattering once two people know each other. The default bias is small (Hämäläiset and Karjalaiset -10 toward each other), so on its own it never crosses the ±60 thresholds; it only tips an already soured stranger over (two blows give -50, plus -10 is -60, Hostile). Whether tribes should affect attitude at all is open question 7. The table is directional, keyed by tribe id strings, 0 for a missing entry, the same tribe, or a person with no tribe.
- **Community (`CommunityRecord`, Rules):** `{ string Id; string Name; CommunityKind Kind (Village = 0, Camp = 1); FactionId Faction; string ElderId }`, kept in a `CommunityRegistry`. Membership is stored once, as `PersonRecord.CommunityId`.
  - **Communities hold the faction.** Every village can have its own `FactionDefinition` asset (the demo villages both use Townsfolk). A person's faction is decided by the one resolver from human-npcs.md, `FactionRules.Resolve(inSquad, communityFaction, ownFaction, player)`:
    - On the Game side, `Social` installs a `CharacterFaction.CommunitySource` while `SocialWorld` is enabled. It returns the faction of the person's registered community. `Character.Faction` uses it with no write-back and no re-sync.
    - On the Rules side, `SocialSimulation.EffectiveFaction(id)` calls the same `FactionRules.Resolve` with the record's squad id, community faction, own faction and the player id it was constructed with. The Sim uses it everywhere it compares factions (`KillerWitnessed`, attitude).
    - With no `SocialWorld` (or `Social.Active` false), there is no community source, so every character uses its own faction (or Player in the squad).
    - A member's own faction is kept, unused while it has a community: if it leaves, it falls back to it. Recruits set their own faction to Player (human-npcs phase 3).
  - **Cache invalidation:** `Social` calls `CharacterFaction.InvalidateAll()` after `RegisterCommunity`, `SetCommunityFaction`, `MoveTo` and when installing or removing the source, and `Character.InvalidateFaction()` after registering a character. `Social` listens to `CharacterFaction.OwnFactionChanged` to update `PersonRecord.OwnFaction`.
  - `SetCommunityFaction(id, faction)` changes the community and so every member's resolved faction in one call.
  - **Faction change mid-fight:** fights in progress are not cancelled, with one exception. Right after a community's faction changes, any live non-squad character whose `CurrentOrder` is an `AttackOrder` between a member of that community whose resolved faction changed and its target (either direction), and whose `Attitude.Of(attacker, target)` is now Allied, gets `GiveOrder(null)`, so its brain returns to the schedule. A fight that is now Neutral or Hostile continues: once blows are traded it is personal, and self-defence plus the -25 attack opinion keep it consistent. Squad orders are never cancelled; the player decides.
  - **Moving:** `MoveTo(personId, communityId or null)` only changes `CommunityId` (and clears an elder role in the old community). Because default bonds are derived, nothing else needs fixing up: the new community's Neighbour or Comrade bonds and its elder apply at once, the old ones stop. Kin, authored bonds and earned opinion stay. **Recruiting:** `Social` handles `PlayerControllable.Joined` by setting the squad id and calling `MoveTo(id, null)`, so the recruit leaves its community.
  - **Village vs camp default bond:** villagers get a `Neighbour` flag at `neighbourBondOpinion` (+20) rather than `Comrade`, so village ties are milder than a war band's. Camp members get `Comrade` at `campBondOpinion` (+40). A separate flag, instead of reusing `Comrade` at a lower opinion, keeps closeness tunable per layer and lets barks and UI tell neighbours from comrades later.
- **Kin group (suku):** members who are not in the same household get `Family` at `kinGroupBondOpinion` (+30), closeness 0.7. Kin groups cross communities and factions.
- **Household (talo):** members get `Family` at `householdBondOpinion` (+60), plus exact kin edges (Parent, Child, Sibling, Spouse) authored through `PersonTies`. Beds stay on `NpcBrain.assignedAnchors`: a household-owned bed lookup would add a second anchor lookup path for nothing the demo needs (see Out of scope).
- **Close kin across factions** (`AttitudeRules`, 4a): Parent, Child, Sibling and Spouse are never Hostile to each other because of faction relations alone. A grudge at `hostileAt` or more, or an opinion at or below `hostileOpinion`, still makes them Hostile. Plain `Family` (the household or kin group layer) does not count: a distant cousin in a bandit camp is still a bandit.
- **Elder:** no tribe chief. Each community has one elder (`ElderId`), set from the `Community` component.
  - The elder decides the community's faction. In the demo that is only an API (`Community.SetFaction`, which calls `Social.SetCommunityFaction`); persuasion and politics are out of scope.
  - Members' derived bond to their elder adds the `Elder` flag (read from the member's side: "other is my elder"; not kin, not mirrored) and an opinion base of at least `elderBondOpinion` (+40). With the Neighbour (0.2) and Elder (0.3) weights, a villager's closeness to the elder is 0.4 + 0.2 + 0.3 = 0.9.
  - **Killing the elder:** every member learns as news after `newsDelayHours`, and each gets a grudge evaluated with the bond they had to the elder at the moment of death (the death record keeps which community the victim led). At vengefulness 0.6 that is 54, tier Rally: they call out and rally. The death uses `elderDeathGrudgeCap` (20) instead of `maxGrudgesPerDeath` (4), so the whole village feuds. Feud generations still bound the chain. Whether the whole village or only the elder's household and kin should feud is open question 8.
  - **Elder dies, leaves or is despawned:** `ElderId` becomes null. Successor selection is out of scope; a community with no elder works the same, only no one has the `Elder` bond to a living person. `SetElder(communityId, personId)` exists for a later successor system. Because the bond is derived, it only sets `ElderId`: every member's `Elder` flag and opinion base move to the new elder at once, and earned opinion toward the old elder stays as it was.
- **Future direction (out of scope, documented only):** a kin-group head; factions as alliances of communities; a chieftain who emerges as the strongest elder of an alliance.

### Relationships: stored part, derived part, effective relationship (4a)
- **`RelationshipBook` stores only what cannot be derived**, sparse and directional, `(fromId, toId)` to `StoredRelationship { BondFlags Bonds; bool HasBase; int Base; int Delta }`:
  - `Bonds`: authored non-kin flags (Friend, Lover, Rival, ...) and kin flags;
  - `HasBase`/`Base`: an authored opinion that replaces the derived base;
  - `Delta`: opinion earned in play (attacks, fighting together, treatment, blood price floor).
  - An entry with no flags, no base and a delta of 0 is removed. Storage grows with authored bonds and with pairs that actually interacted, **not with population squared**.
- **Derived part** (pure, `DefaultBondRules.Between(from, to, fromCommunity, tuning, out Relationship derived)`): the union of the flags of every layer the two share and the highest of those layers' opinions: household (Family, `householdBondOpinion`), kin group (Family, `kinGroupBondOpinion`; the household value is higher when both apply), community (Village: Neighbour, `neighbourBondOpinion`; Camp: Comrade, `campBondOpinion`), and the elder (when `fromCommunity.ElderId == to.Id` and `from` is not the elder: Elder, `elderBondOpinion`). Ids match only when both are non-empty and equal (ordinal). Returns false when nothing is shared or `from == to`. `fromCommunity` may be null.
- **Effective relationship** (pure, `RelationshipRules.Effective(bool hasDerived, Relationship derived, StoredRelationship stored)`), the only thing anyone reads:
  - flags = derived flags | stored flags;
  - opinion = clamp(-100..100, (stored base if `HasBase`, else derived opinion, else 0) + `Delta`).
  - So an authored bond replaces the derived opinion base but only adds flags: authoring cannot remove a layer's flag (change the membership instead).
  - `SocialSimulation.Relationship(from, to)` looks up both records and the from-community, computes the derived part and combines. It is O(1) per pair.
- **Opinion changes** go through the Sim, which knows the base: `AddOpinion(from, to, change)` stores `Delta = RelationshipRules.DeltaAfterChange(base, delta, change)`, which keeps the *effective* opinion inside -100..100 (a delta never piles up past the clamp). `RaiseOpinionTo(from, to, floor)` raises the effective opinion to at least `floor` by adjusting `Delta`, and never lowers it.
- **Why derived:** retuning a bond opinion in `SocialConfig`, moving a person to another community and changing an elder need no data fix-up, and nothing is lost when they happen: the effective value is recomputed from current memberships every time.
- **Remembered neighbours:** after leaving a community, Neighbour or Comrade flags and their opinion base toward the old community are **no longer derived**, so they are gone. Earned deltas stay (a former neighbour you fought still dislikes you by that amount), and so do authored bonds, kin and the kin group and household layers, which do not depend on the community. A recruit therefore still hears of kin deaths (24 h) and household deaths (3 h).
- `[Flags] BondFlags { None = 0, Family = 1, Friend = 2, Lover = 4, Comrade = 8, Rival = 16, Parent = 32, Child = 64, Sibling = 128, Spouse = 256, Neighbour = 512, Elder = 1024 }`, with explicit values because they will be serialized. `Neighbour` is the village layer; `Elder` is read from the holder's side ("other is my elder") and is not kin.
- **Kinship.** `Family` is a generic relative (the household and kin group layer). `Parent`, `Child`, `Sibling` and `Spouse` are exact kin, always stored, and read from the holder's side: `Child` on `(Aino, Veikko)` means "Veikko is Aino's child".
  - Kin flags always mirror: `RelationshipBook.SetKin(a, b, kin)` sets `Child` on a→b and `Parent` on b→a (and the reverse); `Sibling` and `Spouse` are set both ways. Opinion is not mirrored. `SetBonds`/`AddBonds` refuse kin flags so they cannot be set one-sided; authored bonds and tests go through `SetKin`.
  - Son and daughter are not separate flags: they are `Child` plus the child's sex. `KinTerms.Term(BondFlags kin, Sex other)` (Rules) gives son/daughter, father/mother, brother/sister, husband/wife, or null for non-kin. Barks use it: "You killed my son, {victim}!".
  - Kin is permanent: entries with kin flags are never removed, and the bond survives the death of either person (the ledger needs it), and leaving a community.
- **Closeness** (0..1) is computed from the effective relationship, never stored: `clamp01(opinion / 100 + sum of bond weights)`. Weights come from config: Family 0.4, Lover 0.5, Friend 0.2, Comrade 0.1, Rival -0.3, Neighbour 0.2, Elder 0.3, Child 0.7, Parent 0.6, Spouse 0.6, Sibling 0.5. So a village neighbour at +20 is 0.4, a camp comrade at +40 is 0.5, a kin-group relative at +30 is 0.7, a villager to their elder is 0.9, a household member at +60 is 1.0, and a parent is close to their child even at opinion 0 (0.7): you can quarrel with your son and still avenge him.
- **Bonds to the dead** are derived from the dead person's last memberships (records keep them). For an elder's death, the grudge evaluation adds the Elder layer for members of the community the victim led (`DeathRecord.ElderOfCommunityId`), because `ElderId` was cleared at death.

### Attitude: personal feelings over faction relations (4a)
- `AttitudeRules.Resolve(FactionRelation faction, bool closeKin, int opinion, float grudgeIntensity, SocialTuning t)` (Rules) returns the human-npcs `AttitudeResult { Relation; Reason; RefusesDealings }`. `AttitudeReason` gets the appended values `Opinion = 1, Grudge = 2, Kin = 3`. `closeKin` is true when the holder has Parent, Child, Sibling or Spouse toward the other. `opinion` is the effective opinion plus the tribe term for strangers. The relation is decided in this order:
  1. Faction Hostile and not close kin: Hostile (reason Faction). Liking a bandit does not make him a friend; that is out of scope.
  2. Grudge intensity at or above `hostileAt` (36): Hostile (Grudge). This also holds between close kin: a mother whose husband was killed by their son holds a grudge against the son, and the grudge wins.
  3. Opinion at or below `hostileOpinion` (-60): Hostile (Opinion). This holds even between two townsfolk, whose factions are Allied, and between close kin.
  4. Opinion at or above `alliedOpinion` (+60): Allied (Opinion). Such a friend treats the first blow as an accident and does not fight back. Every attack also lowers opinion by 25 (see earned opinion), and that drop is applied *before* the self-defence check, so a friend at +75 fights back from the second blow on. Open question 5. A well-liked son in a hostile camp is Allied by this rule too.
  5. Faction Hostile but close kin: Neutral (Kin). Kin never start a fight with each other over faction, but fight back when attacked.
  6. Otherwise the faction relation (Faction).
  - **`RefusesDealings`** is true when the grudge intensity is at least `minGrudgeIntensity` (15, the Cold tier) or the relation is Hostile. **Cold is not Hostile:** a Cold grudge leaves the relation as the steps above decide it, and only refuses Talk small talk, Recruit and (later) trade.
- `SocialAttitudeProvider : IAttitudeProvider` (Game) is installed into `Attitude.Provider` by `SocialWorld` while enabled. It defers to plain faction relations (returns false) when:
  - either side is not a `Character` (animals),
  - `Social.Active` is false,
  - or `self` is in the player squad. **Squad members never act on personal feelings**: their opinions and grudges are recorded and can drive UI or mood later, but never change who they fight or deal with. This includes the close-kin rule: a recruit's mother in an enemy village will not attack the recruit over faction, but the recruit follows the player's orders.
  - Otherwise it returns `Sim.AttitudeOf(selfId, otherId, FactionRelations.Get(self.Faction, other.Faction))`.
  - `RefusesSquad(self)`: self holds a grudge of at least `minGrudgeIntensity` against any live squad member (direct or through squad scope).
- Attitude is directional. `Of(a, b)` is how `a` regards `b`.
- **Callers** (all already wired in human-npcs; no change here):
  1. `Character.DefendAgainst` (`Attitude.FightsBack`).
  2. `NpcInteractable.GetAvailableInteractions` and talk hold (`Attitude.Explain`): reason Faction Hostile shows Attack only; reason Grudge or Opinion Hostile shows Talk, Recruit (refused), the blood price option and Attack; a Cold grudge shows the normal options, but Talk gives a grudge bark with no hold and Recruit is refused.
  3. `Recruitable.TryRecruit` (`Attitude.Of`, `RefusesDealings`, `RefusesSquad` → `RecruitBlock.Unwilling`).
  4. `RevengeRole` targeting (4c). When the bandit phase writes `ActorPerception.FindNearestHostile`, it should take the hostility test as a predicate so BanditRole can pass `Attitude.Of`. The same predicate is what lets a bandit spare his mother.
  5. `Social.SetCommunityFaction`, to decide which fights end on a faction change (4b).
- **Callers that stay on faction relations:** `AnimalPerceptionUtility` (animals have no persons), `Character.TryTreatWounds` and `SelectionManager.TryOrderTreatment`, so treating wounds keeps the simple faction rule.

### Deaths: `DamageLog` and `DeathLedger` (4a)
- `DeathRecord { int Index; string VictimId; string KillerId (null when no person killed them); bool KillerFactionKnown; FactionId KillerFaction; bool KillerWitnessed; bool VictimWasElder; string ElderOfCommunityId; int Day; float Hour; WorldPoint Position; int FeudGeneration; int GrudgesCreated; List<InheritedGrudge> InheritableGrudges }`. `InheritedGrudge { string TargetId; string VictimId; int DeathIndex; float Intensity; int Generation }` is the snapshot children take over (see Grudges). `WorldPoint` is a plain `{ float X, Y, Z }` struct in Rules, because Rules cannot use `Vector3`.
- **Killer attribution:**
  1. If the `OnDied` source resolves to a `CombatActor`, that actor is the killer. A `Character` gives a killer id and faction; an animal gives only its faction.
  2. If the source is null, the killer is the most recent entry in the victim's `DamageLog` within `attributionWindowHours`, or unknown.
  - `DamageLog` keeps up to 6 distinct recent damagers per victim, each with id (or null), faction and time. A repeat damager updates its time. It is fed by `OnDamaged`, not `OnAttacked`, so counters are logged and blocked swings are not. It is cleared when the victim dies.
- **Window: 0.25 in-game hours, measured in game time, not `Time.time`.** Future bleed-out and poison damage will tick on in-game hours like starvation does, so a wall-clock window would break as soon as time is sped up. At the default `timeMultiplier` (0.5 h per real minute) 0.25 h is 30 real seconds, which is about the length of one fight plus a short bleed. Starvation counts as unattributed too: a squad member who starves 10 game minutes after a townsperson hit them blames that townsperson. That is acceptable for the demo, because townsfolk do not starve (`hasNeeds = false`).

### Grudges (4a)
- `Grudge { string HolderId; string TargetId; string VictimId; int DeathIndex; GrudgeCause Cause; float Intensity (0..100); int CreatedDay; int LastDecayDay; int Generation; bool Inherited; bool HasLastKnown; WorldPoint LastKnown }`. There is at most one grudge per (holder, target).
- `GrudgeCause { Killing = 0 }`, an explicit value because it is serialized. 4a only creates `Killing` grudges. It is there so later causes (theft, insult, broken deal) can be added without touching inheritance: only `Killing` grudges are ever inherited, so a stolen bucket never passes to the next generation.
- **Created when the holder learns who killed someone they were close to.** A killing nobody saw makes no grudge at all (see Knowledge). All of these must hold:
  - the killer is a person (`KillerId` not null) and is not Dead (Gone is allowed);
  - the holder is alive and is neither the killer nor the victim;
  - closeness(holder to victim), from the effective relationship (plus the elder layer for an elder's death), is at least `grudgeClosenessThreshold` (0.35);
  - the death's `FeudGeneration` is at most `maxFeudGeneration` (2);
  - `intensity = (closeness × vengefulness × 100 + kinBonus) × generationFalloff ^ generation`, and the result is at least `minGrudgeIntensity` (15). `kinBonus` is `kinGrudgeBonus` (20) when the victim was the holder's Child, Parent, Spouse or Sibling, else 0. This is a blood feud: a parent at vengefulness 0.6 whose son is killed gets 1.0 × 60 + 20 = 80, Hunt;
  - the death has made fewer than `maxGrudgesPerDeath` (4) grudges so far, or fewer than `elderDeathGrudgeCap` (20) when `VictimWasElder`. Witnesses are processed closest first.
- A holder can be close kin of the killer (a son kills his father; the mother learns). The grudge is created as for anyone, and by the attitude order it overrides kinship. Damping such grudges is not planned.
- Vengefulness (0..1) comes from `NpcArchetype.vengefulness` (default 0.6), or `SocialConfig.defaultVengefulness` (0.5) for characters with no archetype, such as squad members.
- **Merge:** a second grudge against the same target becomes one grudge with `min(100, max(old, new) + stackBonus)`, the newest victim (for barks), and the higher generation.
- **Cap:** at most `maxGrudgesPerHolder` (3) targets. A new grudge stronger than the weakest replaces it; otherwise the new one is dropped.
- **Tiers** (`GrudgeTier { None = 0, Cold = 1, Hostile = 2, Rally = 3, Hunt = 4 }`; each tier includes the behaviour of the ones below it):

  | Tier | From intensity | Behaviour |
  |---|---|---|
  | Cold | `minGrudgeIntensity` 15 | refuses Talk small talk, Recruit and trade; grudge barks; **no attack**; relation unchanged |
  | Hostile | `hostileAt` 36 | attitude Hostile toward the target; attack on sight |
  | Rally | `rallyAt` 48 | call-out bark and rally: close friends nearby join |
  | Hunt | `huntAt` 68 | hunts the target when it is out of sight |

  A grudge below 15 but above `forgetBelow` (10) is remembered (shown in the debug view) but has no effect.
  - **Thresholds chosen** so that at the default NPC vengefulness (0.6) every typical bond lands in its intended tier with no row exactly on a threshold, and the same holds at the squad default (0.5). `grudgeClosenessThreshold` is 0.35 (was 0.4) so a plain neighbour (0.4) does not sit on it either:

    | Bond to the victim | Closeness | Intensity at 0.6 | Tier | Intensity at 0.5 | Tier |
    |---|---|---|---|---|---|
    | Village neighbour (Neighbour +20) | 0.4 | 24 | Cold | 20 | Cold |
    | Camp comrade (Comrade +40) | 0.5 | 30 | Cold | 25 | Cold |
    | Kin-group relative (Family +30) | 0.7 | 42 | Hostile | 35 | Cold |
    | Villager whose elder is killed (Neighbour + Elder, +40) | 0.9 | 54 | Rally | 45 | Hostile |
    | Household member, not close kin (Family +60) | 1.0 | 60 | Rally | 50 | Rally |
    | Parent, child, sibling or spouse in the household or kin group | 1.0 | 80 (60 + 20 kin) | Hunt | 70 | Hunt |

    Close kin with no shared household or kin group (authored kin only, opinion 0) are lower: child 62, parent or spouse 56, sibling 50 at 0.6 (Rally). Otherwise Hunt needs a very vengeful person, a lover, or two killings.
- **Decay:** `decayPerDay` (5) per whole in-game day. Each grudge tracks `LastDecayDay`, so a jump of several days applies all of them at once, and a grudge created during a jump decays only from its own day. A grudge below `forgetBelow` is removed. `GrudgeRules.DaysUntilForgotten(intensity, t)` gives the days left for the debug view (a Cold 24 is forgotten after 3 days).
- **Resolved** (removed) when the target dies, whoever kills them; when blood price is paid (4d); or by decay. Grudges held by someone who dies are removed, except that the hardest ones pass to their children (below).
- **Inheritance: children take over the hardest grudges** (decided: open question 3).
  - When a holder dies, every grudge they hold with `Cause == Killing` and intensity at least `inheritAt` (60) is copied into `DeathRecord.InheritableGrudges` (target id, victim id, death index, intensity, generation) before the holder's grudges are removed. Lesser grudges die with the holder.
  - A **child** of the dead holder (`Child` flag, holder's side) takes them over when they learn of the parent's death, as a witness or through news. Knowing the parent's killer is not needed: a father's grudge passes on even if nobody saw who killed him. Friends, spouses and siblings do not inherit; they already get their own grudges from the killing itself.
  - The inherited grudge has intensity `× inheritFactor` (0.75), the **same** generation (inheriting is not a new killing, so it does not use up the feud chain), `Inherited = true`, and decays from the day it is taken over. It merges with any grudge the child already holds against that target, and counts toward the child's per-holder cap. It is skipped if the target is dead, or is the child.
  - Worked example: Veikko's brother is murdered by Jonttuli. Veikko holds 80 (Hunt, close kin). Veikko dies before getting revenge; his son Aatu learns of it and takes over 60 (Rally). Only a sibling, child, parent, spouse or household member's killing reaches 60, so the rule matches "murder of an uncle, not a stolen bucket".
  - It falls off on its own: a grandchild would need the son's 60 to still be at least 60, and it becomes 45 after one more hand-over, so a feud passes down at most two generations at default tuning, with decay usually ending it sooner.
- **Feud chaining.** `DeathRecord.FeudGeneration` is 0 for an ordinary killing. If, at the moment of death, the killer held a grudge against the victim (revenge), or the victim held one against the killer (an avenger killed by their target), it is that grudge's generation + 1. Grudges born from a death inherit its generation. Four limits stop chain explosions:
  1. the generation cap (2: murder, revenge, counter-revenge, then nothing more);
  2. the falloff (0.6 per generation);
  3. the per-death cap (the larger elder cap only for an elder's death);
  4. the per-holder cap.
  Friends pulled into a fight (4c) take a grudge with the caller's generation, so they cannot restart the count.
- **Squad scope.** A grudge at or above `squadScopeAt` (85) against a member of the player squad extends to every live member of that squad: `Attitude` is Hostile toward all of them, and `RevengeRole` may target any of them. Below that, only the killer is the target. This is pure: the registry stores the squad id, and `SocialSimulation.EffectiveGrudge(holder, other)` does the lookup.
- **Squad members as holders.** They get grudges like anyone (for example Keppana learns that a townsperson killed her authored friend). These are recorded and raise `Social.GrudgeCreated`, which shows "Keppana swears vengeance on Aino" above them. That is the hook for a mood system later. They never act on a grudge on their own (see Attitude).

### Knowledge (4b)
- `KnowledgeBook` stores, per (person, death), whether the person knows of the death and whether they know the killer. A grudge is evaluated the first time `KnowsKiller` becomes true for that pair, never again. A grudge that was paid off or forgotten therefore does not come back.
- **Witnesses** learn instantly when the death is recorded: every live, awake (`!IsSleeping`) registered character within `witnessRadius` (20 m) of the victim, other than the victim. There is no line-of-sight test (see Out of scope). Witnesses always learn the killer.
- `KillerWitnessed` is true if at least one witness is neither the killer nor Allied with the killer by effective faction. A killer's own comrades keep quiet.
- **News**, per layer (pure, `NewsRules.Delay(victim, recipient, tuning, out hours)`), for registered people who did not witness:
  - the victim's household and community members learn after `newsDelayHours` (3 in-game hours);
  - kin-group members who are in neither learn after `kinNewsDelayHours` (24), wherever they live, including a recruit in the squad;
  - nobody else hears.
  - Pending news is delivered when `SocialWorld` advances the clock. If the holder or the killer has died before delivery, no grudge results.
- **Corpse:** anyone who loots the body learns of the death.
- News and corpse inspection reveal the killer only if `KillerWitnessed`. **A killing nobody saw causes no grudge** (decided: open question 4); there is no setting for it. With no witness, the village knows Aino is dead but not who did it. This gives the player a real choice about when and where to fight. Children still inherit the dead person's own hardest grudges (see Grudges), because that needs only news of the death, not the killer.

### Bonds and earned opinion (4b)
- **Memberships:** a `PersonTies` component holds `tribe` (`TribeDefinition`), `community` (`Community`), `kinGroup` (`KinGroup`) and `household` (`Household`). Registration copies their ids into the `PersonRecord`; derived bonds follow automatically.
- **Hand-placed bonds:** `PersonTies.bonds` is a list of `{ Character other; BondFlags flags; int opinion; bool mutual = true }`. They are applied in `Start` (after every OnEnable has registered): non-kin flags are added and `opinion` becomes the stored base, both ways when `mutual`; a kin flag goes through `SetKin`.
- **Earned opinion** (stored as deltas), all values in `SocialConfig`:
  - Attacked by someone: victim to attacker, `attackedOpinion` (-25) per attack event (hit or not).
  - Fought together: when someone dies, every pair of distinct person damagers in the victim's log, if neither regards the other as Hostile, gets `foughtTogetherOpinion` (+10) both ways.
  - Wounds treated by someone else: patient to healer, `treatedWoundsOpinion` (+15), on a successful `TreatWoundsOrder` that healed more than 0.
- Bonds are never earned in play; only opinion is (see Out of scope).

### `RevengeRole` (4c)
- **Perception choice: a minimal step inside 4c, not waiting for the bandit phase.** RevengeRole never searches for "the nearest hostile". It only asks whether it can see one known person. So it needs `ActorPerception.CanSee(Transform self, Transform target, PerceptionSettings s)`: range, half-angle FOV, eye height, and an optional line-of-sight raycast against `s.mask`. That is about 20 lines of new code and leaves `AnimalPerceptionUtility` and AnimalAI untouched, which was the reason the extraction was deferred. Waiting for the bandit phase would block 4c on an unrelated feature. The bandit phase adds `FindNearestHostile` to the same class and moves AnimalAI over. `PerceptionSettings.requireLineOfSight` lets PlayMode tests run without physics layers.
- `RevengeRole : MonoBehaviour, INpcRole` on the townsperson prefab. It reacts at Role priority, so Flee (50) and self-defence (External 40) still win. It does nothing while `Social.Active` is false or the NPC is asleep.
- **Targets:** the holder's grudges at tier Hostile or above whose target has a live, alive `Character`. If any grudge is at squad scope, every live squad member counts too. **Envoys under parley are removed** (below). The nearest visible candidate wins. Each sighting updates that grudge's `LastKnown`. Cold grudges never produce targets.
- **Hostile:** attack on sight: `AttackOrder(target, archetype.revengeChaseDistance)`.
- **Rally:** as Hostile, plus, at most once per `calloutCooldownSeconds`, a call-out bark ("{target}! You killed {victim}!") and a rally: every live NPC with a `RevengeRole` within `rallyRadius`, whose closeness to the holder is at least `rallyCloseness` (0.35) and who is not already fighting, joins the feud (`SocialSimulation.JoinFeud`). That gives them a grudge against the target at `max(hostileAt, holder intensity × rallyIntensityFactor)` with the holder's generation, so their own RevengeRole attacks on sight. No special assist state is needed.
- **Hunt, target not visible:** hunt.
  1. Start the hunt at the grudge's `LastKnown` (the death position if nothing better). It is given up `huntGiveUpHours` (6) in-game hours after it started, or when the grudge drops below Hunt.
  2. Before setting out, compare strength with `HuntRules.Decide`. Strength is `currentHealth × max(0.1, attack damage)` from `CombatFormulaResolver.BuildAttackProfile`. The target side is the target alone, or, at squad scope, all live squad members within `rallyRadius` of the target. The decision:
     - **Hunt** if the hunter's strength is at least the target's × `gatherBelowRatio` (1.0);
     - **Gather** otherwise, if close friends (closeness ≥ `rallyCloseness`, within `gatherRadius`, not fighting) are available. Up to `maxHuntParty` (3) of them, strongest first, until the ratio is met, join the feud and follow the hunter;
     - **Hunt** anyway if the party is at least the target's × `huntMinStrengthRatio` (0.6);
     - otherwise **Wait**: no hunt, but still attack on sight.
  3. The hunt itself is a `WanderOrder(lastKnown, huntSearchRadius, wanderPauseSeconds)`. Party members use `FollowOrder(hunter)`. When anyone sees the target, they switch to `AttackOrder` at the same Role priority (allowed by `RoleMayReplace`).
- Roles return false from `TryGetReaction` when their current order already does the job, for example already attacking the visible target.

### Parley (4c rule, used by 4d)
- **Definition.** A squad member E is an **envoy to** holder H while all of these hold:
  1. E is alive and in the player squad;
  2. E's `CurrentOrder` is an `InteractOrder` whose `ActionId` is `"bloodprice"` (`Parley.OfferActionId`) and whose `Target` is H's `NpcInteractable`. Because that is E's current order, E is not running an `AttackOrder`: walking up with the offer is the only thing E is doing. (There is no weapon-drawn state in the game; if one is added, also require E's weapon to be sheathed.)
- **Effect, only between H and E:**
  - H's `RevengeRole` removes E from its candidates, so H neither attacks nor calls out against E; `StillWants` returns false for a Role `AttackOrder` on E, so an attack already started by the role stops at the next think.
  - Self-defence is unchanged: a fight that is already running as an External order (E was fighting H a moment ago) continues until it ends, and if E attacks, E's order is no longer the offer, so the parley is over.
  - Every other holder, including H's housemates, still treats E as before. Only the holder being approached holds fire.
- **Ends** the moment E's order ends: payment made, offer refused (not enough valuables, NPC in combat, too far), the target gone, or the player giving E another order. There is no grace period (open question 2).
- This gives a peaceful way out even at squad scope and even for the killer: any squad member, including the grudge target, can walk up with the offer. `Parley` is a small Game static (`IsEnvoyTo(Character envoy, Character holder)`), tested in PlayMode.

### Talk barks and blood price (4d)
- **Talk** to an NPC who holds a grudge (`GrudgeTalk : INpcTalkSource`, priority 10, on the prefab):
  - against the speaker, or against the speaker's squad at squad scope, at any tier including Cold: a line from `SocialConfig.grudgeBarksToTarget` ("You killed {victim}!"; when the victim was kin, a line from `kinGrudgeBarksToTarget` such as "You killed my {kin}, {victim}!", with `{kin}` from `KinTerms.Term`). `Hold = false`, and the attitude refuses dealings anyway, so there is no talk pause.
  - against another squad member below squad scope: a line from `grudgeBarksAboutSquad` ("{target} spilled {victim}'s blood. Pay, or bleed."), with `Hold = true`, so the normal talk pause follows.
  - `{victim}`, `{target}` and `{kin}` are filled by `BarkFormat.Fill` (Rules) from registry names and `KinTerms`.
- **"Offer blood price"** (`BloodPriceOffer : INpcInteractionSource`, id `bloodprice`, primaryOnly) appears when the NPC holds a grudge of **any tier** (Cold included) against any member of the player squad, and the NPC is not Hostile by faction (those NPCs show Attack only). Iron Age, no money: the price is paid in **valuable goods**.
  - **Valuables:** `Item` gets `[Min(0)] int worth` (abstract barter worth) and `bool valuable` (counts for blood price). Swords, furs and silver are valuable. Every existing item defaults to worth 0 and not valuable. Worth is never displayed as a price or coin; the only player-facing use is the refusal "Wants valuables worth {n}".
  - **What is settled: one payment per household per death.** `SocialSimulation.QuoteBloodPrice(payeeId)`:
    1. the primary grudge is the payee's strongest grudge against a squad member (its target T and victim V);
    2. it settles that grudge plus every grudge held by a living member of the payee's household against T for the same victim V;
    3. the **required worth uses the strongest of those grudges**, so paying the calmest housemate costs the same as paying the angriest.
    - Grudges for another victim (a merged grudge whose newest victim differs) and grudges held outside the household are not settled. A payee with no household settles only their own.
  - **Required worth** is `BloodPriceRules.RequiredWorth(intensity, worthPerIntensity, minWorth)` = `max(minWorth, ceil(intensity × worthPerIntensity))`, with `bloodPriceWorthPerIntensity` 1.0 and `bloodPriceMinWorth` 10. So a Cold neighbour's 24 wants worth 24, a Hostile 42 wants 42, a Rally 60 wants 60, a Hunt 80 wants 80. With the content values (wolf pelt 10, sword 30, silver arm-ring 40) that is three pelts or a sword; a sword and two pelts; a sword and three pelts, or silver and two pelts; two silver arm-rings.
  - **Payment picks automatically** from the payer's (primary interactor's) carried inventory. The caller groups the inventory into stacks, one per distinct valuable item with `worth > 0`, in first-seen inventory order. `BloodPriceRules.TrySelect(stacks, requiredWorth, into)` is a pure greedy function over `(key, worth, count)` stacks:
    1. If the sum of `worth × count` over all stacks is below `requiredWorth`, return false and leave `into` empty.
    2. Sort the stacks by worth, highest first; equal worths keep their input order (stable).
    3. Walk the sorted stacks with `total = 0`. For each stack, while `total < requiredWorth`, take `min(count, ceil((requiredWorth - total) / worth))` units, add them to `into` as `(key, taken)`, and add `taken × worth` to `total`.
    4. Stop when `total >= requiredWorth` and return true.
    - It does not minimise overpay. With silver (40) and three pelts (10) against a price of 30 it takes the silver, not three pelts. That is deliberate for the demo; choosing what to hand over belongs to the later barter UI.
  - `BloodPriceRules.Check(alive, inCombat, inReach, carriedWorth, requiredWorth)` returns `BloodPriceBlock { None = 0, Dead = 1, InCombat = 2, TooFar = 3, NotEnoughValuables = 4 }`, checked in enum order. `carriedWorth` is the sum of `worth × count` over the payer's valuable items.
  - Payment must be full. The selected items are taken, every settled grudge is removed, and each settled holder's opinion of the target is raised to at least `bloodPriceOpinionFloor` (-20). The NPC shows an accept line.
  - An elder's death still makes the whole village feud, so it takes one payment per household, which is meant to make killing an elder expensive.
- **Approaching:** the parley rule protects the envoy from the holder being paid. Other holders still attack their targets on sight, so sending a squad member who is nobody's target is still the safest way.
- Recruit cost stays an item and a count (human-npcs phase 3). It could later use worth the same way.

### Feud UI (4e)
The NPC Debugger is a development tool. This is what the **player** sees. It follows how the existing HUD is made: built in code with `UIFactory` on its own canvas (like `SelectionHud` and the inventory windows), no prefabs to wire.

**What the player is allowed to know.** A grudge is hidden until its holder shows it. `Grudge` gets a `Revealed` flag (append). Nothing in the UI shows an unrevealed grudge against the squad, so an avenger the player has never met can still arrive as a surprise (open question 9).
- A grudge becomes revealed when the holder: speaks a grudge bark to a squad member (`GrudgeTalk`), calls out (`RevengeRole` Rally), starts a revenge attack on a squad member, or is offered a blood price. Friends who join through a call-out are revealed with it.
- `SocialSimulation.Reveal(holderId, targetId)` sets the flag, returns true the first time and raises `GrudgeRevealed`. A merge keeps `Revealed` if either grudge had it.
- The squad's own killings are always known to the player, and so are grudges held by squad members.

**Sim events the UI listens to** (all in Rules, added to `SocialSimulation`): `GrudgeCreated` (exists), `GrudgeRevealed(Grudge)`, `GrudgeTierChanged(Grudge, GrudgeTier oldTier)` (merge, `JoinFeud`, decay crossing a tier), `GrudgeRemoved(Grudge, GrudgeEnd reason)` with `GrudgeEnd { TargetDied = 0, Paid = 1, Forgotten = 2, HolderDied = 3 }`.

**Pure view model (Rules, EditMode-tested).** The UI draws rows; it never walks the books itself.
- `FeudReport.Build(SocialSimulation sim, string squadId, double now, FeudReport into)` fills three lists:
  - `Against`: one `FeudRow` per **revealed** grudge held by a living non-squad person against a squad member: `HolderId`, `HolderName`, `HouseholdId`, `CommunityName`, `TargetId`, `TargetName`, `VictimName`, `KinTerm` (the holder's kin word for the victim from `KinTerms.Term`, or null), `Tier`, `SquadWide` (intensity at or above `squadScopeAt`), `DaysLeft` (the same value the debugger shows), `PriceBand`, and `GroupKey` = (household, victim, target). Sorted by tier, then intensity, highest first. Rows with the same `GroupKey` are one household's feud: one blood price settles them all, so the panel draws them as one block and `PriceBand` comes from the household quote (`QuoteBloodPrice`).
  - `Held`: grudges held by squad members (holder, target, victim, kin word, tier). Record only; the row says so.
  - `Killings`: deaths from the last `killingsShownDays` (10) whose killer is in the squad: victim, community, killer, day, and `Witnessed` (`KillerWitnessed`).
- `KinReport.Build(sim, personId, into)`: the person's tribe, community, kin group and household ids, and one row per person they have an exact kin flag, `Friend` or `Lover` toward (name, kin word or bond, alive or dead, community name). Derived neighbours and kin-group relatives are not listed; they would flood the list.
- **Blood price is shown in words, never as a number.** `BloodPriceBand { Small = 0, Fair = 1, Heavy = 2, Great = 3 }` from `BloodPriceRules.Band(requiredWorth, tuning)` with `bloodPriceFairFrom` 30, `bloodPriceHeavyFrom` 50, `bloodPriceGreatFrom` 75. At the default numbers Cold (24) is small, Hostile (42) fair, Rally (54-60) heavy, Hunt (80) great. This keeps the no-prices rule (open question 10 asks whether the "Wants valuables worth {n}" refusal should use words too).
- Display names for tribe, kin group and household are resolved Game-side from their assets (`displayName`, falling back to the id). Rules only carries ids.

**Message feed (generic, reusable).** `GameMessages.Post(string text, MessageKind kind = Info)` (Game static, `MessageKind { Info = 0, Warning = 1, Good = 2 }`) with a `Posted` event, and a `MessageFeed` HUD: up to 5 lines at the top left, newest at the bottom, each fading after `messageSeconds` (8). It knows nothing about feuds, so hunger warnings and later systems can use it. Feud messages (lines live in `SocialConfig`, filled by `BarkFormat.Fill`):

| Event | Message | Kind |
|---|---|---|
| Revealed | "{holder} holds a grudge against {target} for the death of {victim}." (with "their {kin}" when kin) | Warning |
| Tier rises (revealed grudges only) | to Rally: "{holder} is calling others against {target}."; to Hunt: "{holder} is hunting {target}." | Warning |
| Becomes squad-wide | "{holder}'s grudge is now against all of you." | Warning |
| Removed: Paid | "The blood price is paid. {holder}'s household lets the matter rest." (once per payment, not per grudge) | Good |
| Removed: Forgotten | "{holder} has let the grudge against {target} fade." | Good |
| Removed: TargetDied or HolderDied | none (the death is visible) | |
| Squad member gets a grudge | "{holder} swears vengeance on {target}." This replaces the floating text of step 12 as the main feedback; the floating text stays. | Info |
| The squad kills a person | "{killer} has killed {victim} of {community}." plus " There were witnesses." when `KillerWitnessed` | Info |

**Markers over grudge holders.** A small diamond above the head of every living NPC with a revealed grudge against the squad, drawn on the HUD canvas at `Camera.WorldToScreenPoint(head)` and hidden when behind the camera. Colour by tier: Cold `UIFactory.MutedTextColor`, Hostile `WarningColor`, Rally and Hunt red (`InvalidColor` at full alpha); Hunt pulses. While a parley toward that holder is active the marker is green (`ValidColor`), so the player sees that the holder is holding fire.

**Feuds panel.** Toggled with `F` (a `KeyCode` field on `SocialWorld`; F is unused today), closed with Escape or its own button. A 520 x 600 window on the right, drawn under the inventory windows (canvas order 90). It refreshes on the sim events and every 0.5 s while open. Two tabs:
- **Feuds**
  - *Against us*: one block per `GroupKey`. Header: "Aino's household, Kuusikylä: Hunt" in the tier colour. Lines: "Aino, for her son Veikko, against Jonttuli"; "against all of you" when squad-wide; "Fades in about 9 days"; "Asks a great blood price." and, for the primary selected squad member, "Keppana carries enough" or "Keppana does not carry enough" (from `BloodPriceRules.TotalWorth` against the quote). At most 8 blocks, then "and N more".
  - *Blood spilled*: "Day 3: Jonttuli killed Veikko of Kuusikylä. Witnessed." or "No one saw." This is the warning before any grudge is revealed.
  - *Our grudges*: "Keppana, for her friend Aino, against Heikki (she will not act on it)."
  - Empty state: "No one holds a grudge against you."
- **Kin** (for the primary selected squad member, from `KinReport`): tribe, village, kin group, household, then "Aino, mother (Kuusikylä)", "Veikko, son (dead)". Empty state: "No known kin."

**Blood price confirm dialog.** Paying is no longer silent. When the "Offer blood price" interaction passes its checks and the feud UI is on, a dialog opens instead of paying at once: "Aino's household asks a great blood price for Veikko. Keppana will hand over: 2 x Silver arm-ring." with **Pay** and **Walk away**. The list is what `BloodPriceRules.TrySelect` picks; choosing other goods stays with the later barter UI.
- **The parley holds while the dialog is open.** The offer order has ended by then, so `Parley` gets `BeginOffer(envoy, holder)` and `EndOffer()`, and `IsEnvoyTo` is also true for a pending offer. The holder stands still facing the envoy (`NpcBrain.HoldFor`).
- The dialog closes as **Walk away** when the envoy gets any other order, either side dies, they are out of reach, the holder enters combat, or after `offerTimeoutSeconds` (20 real seconds), so a parley cannot be held open for ever.
- **Pay** runs the checks and `TrySelect` again (the inventory may have changed), then pays exactly as step 28 does.
- With no feud UI in the scene (`BloodPriceOffer.Confirm` is null: PlayMode tests, or `showFeudUi` off) payment is immediate as in 4d, so the 4d tests do not change.

**Wiring.** `FeudHud` (Game) owns the canvas, the panel, the markers and the dialog, turns sim events into `GameMessages`, and is created by `SocialWorld` when `showFeudUi` is on (default on). `MessageFeed` creates itself on the first `Post`. World clicks must not pass through the panel or the dialog: use the same guard `SelectionManager` uses for the inventory windows.

### Orchestration and wiring
- **Rules:** `SocialSimulation` owns `PersonRegistry`, `CommunityRegistry`, `TribeOpinionTable`, `RelationshipBook`, `DamageLog`, `DeathLedger`, `KnowledgeBook` and `GrudgeBook`, and takes a `SocialTuning`, a `FactionRelationTable` and the player `FactionId`. Its methods take ids, factions, positions and the current time in total in-game hours, so all logic above is EditMode-testable:
  - `Register`, `RegisterCommunity`
  - `EffectiveFaction`, `SetCommunityFaction`, `SetElder`, `MoveTo`, `SetOwnFaction`, `SetSquad`, `MarkGone`
  - `Relationship` (effective), `Closeness`, `AddOpinion`, `RaiseOpinionTo`, `SetAuthoredBond`
  - `RecordAttack`, `RecordDamage`
  - `RecordDeath(victimId, killer, position, witnesses, now)`, which returns the record and does everything listed under deaths and knowledge
  - `Learn`, `Advance(now)` (deliver news, decay)
  - `AttitudeOf`, `EffectiveGrudge`
  - `JoinFeud`, `QuoteBloodPrice`, `PayBloodPrice`
  - It raises `GrudgeCreated`.
- **Game:**
  - `Social` (static) is the thin adapter: the live directory, `Active`, `Now`, a `Sim` instance, and `Report*` methods called from the hooks. It is reset in `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` and by tests.
  - `SocialWorld` (scene component) holds the `SocialConfig`, turns `Social.Active` on and off, installs and removes `Attitude.Provider` and `CharacterFaction.CommunitySource`, drives a `DayClock` from `TimeManager.OnTimeChanged`, calls `Sim.Advance` when a whole in-game hour passes, and turns `GrudgeCreated` into floating text for squad holders. On `Awake` it registers communities before characters.
  - `SocialConfig` (ScriptableObject) holds all tuning in sections, with tooltips and a preview table, and builds the Rules `SocialTuning` through `ToTuning()`.
  - `Community` (scene component, 4b) registers its community and reports faction changes.

### Ease of use and visibility (4a, with checks added per sub-phase)
- **Debug view.** The human-npcs NPC Debugger (an editor window; the reasons are in that plan) gets a `SocialDebugProvider` registered in `NpcDebugReport.Providers`, so the window itself does not change. For the selected character it adds:
  - **Faction:** the phase-1 section already shows the effective faction and why; with the community source installed it reads "Townsfolk (community Kuusikylä)".
  - **Memberships:** tribe, community (kind, elder, faction), kin group, household, squad, state, vengefulness.
  - **Relationships:** the top 8 by closeness (then by absolute opinion) over registered people, each with effective flags, opinion as base + earned delta, closeness, and "close kin" when it applies.
  - **Grudges held:** target, victim, intensity, tier, generation, days left until forgotten, last known position; **grudges against this person**, by holder.
  - **Deaths known:** victim, killer (or "unknown"), day and hour, witnessed or news.
  - **Attitude toward the squad:** relation, reason, refuses dealings, and the numbers behind it ("Cold grudge 24 (Aino)", "opinion -65").
- **Setup validator.** New `ISetupCheck` classes (found automatically): duplicate or empty person ids (4a); more than one `SocialWorld`, or one without a config (4a); `PersonTies` with missing references (an object field that points at a deleted asset or object), an authored bond with no or self `other` or two kin flags, a squad member with a community (4b); communities with an empty or duplicate id or no faction (error) or without an elder, or whose elder is not a member (warning) (4b); duplicate ids among tribe, kin group and household assets, or a tribe with an empty name list (4b); items marked `valuable` with `worth` 0 (4d). Each check arrives in the same step as the component it checks.
- **Tuning.** `SocialConfig` is grouped into sections with `[Header]`: Opinion, Bonds: default opinions, Bonds: closeness weights, Grudges: creation, Grudges: tiers, Grudges: decay and limits, Knowledge, Revenge: rally and hunt, Blood price, Barks. Its custom inspector shows a **read-only preview table**: for each typical bond (neighbour, comrade, kin-group relative, household member, elder, parent, child, sibling, spouse) the closeness, intensity and tier at a vengefulness slider (default 0.6, the Townsperson value) and at `defaultVengefulness`, from the pure `GrudgePreview.Rows`. A row whose closeness or intensity sits exactly on a threshold is flagged, and a row below the closeness threshold shows "no grudge". Changing any number updates the table at once.

### Hooks into human-npcs phases 1-3
Most integration goes through the seams; this is everything this plan changes in phases 1-3 files.

| Where | Change | Step |
|---|---|---|
| `Character` | `personId` (+ `OnValidate`, `Awake`), `sex`, `OnEnable` → `Social.Register`, `OnDestroy` → `Social.Unregister`, `HandleDamaged` → `Social.ReportDamaged` | 5 |
| `Character.HandleAttacked` | first line `Social.ReportAttack(this, attacker)`, so the opinion drop happens before `DefendAgainst` | 5 |
| `Character.HandleDied` | first line `Social.ReportDeath(this, killer)` | 5 |
| `AttitudeResult.cs` | append `AttitudeReason` values | 1 |
| `NpcArchetype` | add `vengefulness` (4b), and `sight` plus `revengeChaseDistance` (4c) | 9, 19 |
| `Townsperson.prefab` | add `PersonTies` (4b), `RevengeRole` (4c), `GrudgeTalk` and `BloodPriceOffer` (4d) | 15, 25, 30 |
| `Attitude.Provider`, `CharacterFaction.CommunitySource` | installed by `SocialWorld`; no code change in phases 1-3 | 5 |
| `PlayerControllable.Joined` | subscribed by `SocialWorld` (squad id, leave community); no code change | 5, 9 |
| `NpcBrain`, `INpcRole`, `NpcInteractable`, `Recruitable`, `RecruitRules`, `SelectionManager`, `Attitude`, `InteractOrder` | **no change** | |

### Edge cases
- **Killer unknown** (null source and nothing in the log window, or a destroyed shooter): the death is recorded with `KillerId` null. People still learn of it, but nobody is blamed and no grudges are made.
- **Killer is an animal:** `KillerId` is null and `KillerFaction` is Wildlife. **No grudges against animals**, not even species-wide ones: wolves already attack on sight, so a grudge would add nothing. Hunting the pack that killed someone is out of scope. The ledger keeps the faction, so it can be added later.
- **Victim with no bonds:** every closeness is 0, so no grudges. Witnesses still learn of the death.
- **Mutual kill in the same frame** (two archers): deaths are processed in call order. A dies: A's friends get grudges against B, who is still alive. Then B dies: those grudges resolve, and B's friends get none, because A is dead. The result is the same in either order: no lasting grudges. Pending news against a killer who has since died is dropped on delivery.
- **Holder recruited while holding a grudge against a squad member:** blocked by `RecruitBlock.Unwilling` (Cold) or `Hostile` (Hostile and above). If the grudge is against someone outside the squad, the recruit joins and keeps it, record only, because squad members never act on grudges.
- **A squad member learns that a squadmate killed their friend:** the grudge is recorded and the floating text shows. Nothing else happens.
- **Target recruited (by the player or, later, any faction):** grudges are keyed by person, so a faction change does not clear them. Recruiting a murderer brings the feud into the squad: holders become Hostile (or Cold) to that member, and at squad scope Hostile to everyone.
- **Target despawned** (destroyed alive): marked Gone. The grudge stays, RevengeRole ignores it (no live character), and decay ends it.
- **Holder despawned:** marked Gone. Its grudges stay and decay.
- **Time jumps** (inspector edits now, sleep skips later): `DayClock` counts a backward jump as one new day, the same as `HourClock`. Decay is computed from day differences, and news due during the jump is delivered at once and decays only from its due day. `TimeManager` cannot jump more than one day today. A future multi-day skip only has to call `Social.Sim.Advance` with the new total, and that path is EditMode-tested.
- **Chain explosion:** bounded by the four limits under feud chaining, plus the rule that no grudge is made against a dead killer. An EditMode test runs a 5-step chain and checks that it stops.
- **Allied-by-opinion abuse** (attacking a friend who will not fight back): -25 per attack applied before the check, so it lasts at most one or two blows.
- **Community faction change while members are mid-fight:** members' resolved factions change at once (cache invalidation). Fights that are now Allied end (the brain resumes the schedule); fights that are Neutral or Hostile continue. Squad orders are untouched. New fights follow the new relation.
- **Elder dies:** `ElderId` becomes null in the same `RecordDeath` call (`VictimWasElder` and `ElderOfCommunityId` are recorded first). Members still learn as news and get grudges, evaluated with the elder layer, up to the elder cap. No successor is chosen. The same happens when the elder is despawned (`MarkGone`) or recruited (`MoveTo` away), without grudges.
- **Recruited member leaves their community:** `MoveTo(id, null)`; the faction is Player through the squad rule and the own faction. Neighbour bonds to the old village stop being derived (earned deltas remain); kin, Family through the kin group and household, and authored bonds remain, so they still learn of kin deaths (24 h) and of household deaths (3 h, by household id), and can still get grudges, record only.
- **Person with no tribe, community or kin group** (the squad, loners): own faction, no derived bonds from the missing layers, tribe term 0, no skill leanings, name kept as authored. Everything else works.
- **Kin in an enemy village:** changing a village's faction to Hostile does not make close kin attack each other; they are Neutral (Kin) and fight back if attacked. Plain `Family` kin-group relatives are not protected.
- **Same person is kin and grudge target:** a grudge of `hostileAt` or more wins (Attitude check 2); a Cold grudge leaves them Neutral (Kin) but refusing dealings. The kin bond stays; when the grudge ends (paid, decayed, target dies) the kin rule applies alone again.
- **Unknown community on a person** (the `Community` object disabled): `EffectiveFaction` falls back to the person's own faction, and no community layer is derived. When the community registers, its faction and derived bonds apply at once with no fix-up.
- **Retuning between sessions** (editing `SocialConfig` and re-entering Play mode): derived bonds and closeness use the new numbers immediately; grudges created earlier in a session keep the intensity they were created with.
- **Blood price offered by the grudge target:** allowed. The parley stops the payee from attacking the target while walking up; other holders still may.
- **Offer refused:** the parley ends with the order. An envoy who is also a target is attacked again at once if the holder is Hostile or worse; the refusal text says how much is needed.

- **Feud UI: unrevealed grudges** are in no list, marker or message. A hidden avenger is revealed by its first bark, call-out or attack, which posts the Revealed message at that moment.
- **Feud UI: confirm dialog open when things change** (the envoy is given another order, someone dies, the holder is attacked, the timeout passes): the dialog closes as Walk away, nothing is taken, and the parley ends. Pay re-runs the checks, so a dropped or stolen valuable cannot be paid with.
- **Feud UI: holder despawned or dead:** its rows and marker disappear on the next refresh; `Against` lists living holders only.
- **Feud UI off** (`showFeudUi` false, or no `SocialWorld`): no HUD objects are created, blood price is paid without a dialog, and `GameMessages.Post` still works for other systems.

## Steps

All paths are relative to `C:\Users\Eppu\Kanshilike`. "(Editor)" marks steps that need the Unity Editor. New Rules files go in `Assets/Scripts/Rules/Social/` (covered by `Kanshilike.Rules.asmdef`, no `UnityEngine`). Game files go in `Assets/Scripts/NPC/Social/`. Editor files go in `Assets/Scripts/Editor/` (the `Kanshilike.Game.Editor` assembly from human-npcs). Rules types are global (no namespace), like `HourClock`.

### Phase 4a: people, relationships, deaths and grudges (needs human-npcs phase 1)

1. **Rules data types.**
   - `Assets/Scripts/Rules/AttitudeResult.cs` (from human-npcs): append `Opinion = 1, Grudge = 2, Kin = 3` to `AttitudeReason`.
   - `WorldPoint.cs`: `[Serializable] public struct WorldPoint { public float X, Y, Z; }` and `static float DistanceSq(WorldPoint a, WorldPoint b)`.
   - `DayClock.cs`:
     - `Advance(float hour)`: the first call only seeds; a backwards hour adds a day.
     - `int Day`, `double TotalHours => Day * 24 + hour`.
     - `static int DayOf(double totalHours)`.
   - `PersonRegistry.cs`:
     - `PersonState { Alive = 0, Dead = 1, Gone = 2 }`.
     - `Sex { Male = 0, Female = 1 }`.
     - `[Serializable] class PersonRecord { string Id, Name, TribeId, CommunityId, KinGroupId, HouseholdId, SquadId; Sex Sex; FactionId OwnFaction; float Vengefulness; PersonState State; }`.
     - `PersonRegistry`: `Add` (returns false on a duplicate id), `TryGet`, `Get`, `All`, `SetState`, `SetSquad`, `SetCommunity(id, communityId)`, `SetOwnFaction`, `MembersOf(communityId)`, `KinGroupOf(kinGroupId)`, `HouseholdOf(householdId)`. The last three return alive members only, and match only non-empty ids.
     - `static bool SameId(string a, string b)`: both non-empty and ordinal-equal. Every layer comparison uses it.
   - `CommunityRegistry.cs`:
     - `enum CommunityKind { Village = 0, Camp = 1 }` (serialized: never renumber).
     - `[Serializable] class CommunityRecord { string Id; string Name; CommunityKind Kind; FactionId Faction; string ElderId; }`.
     - `CommunityRegistry`: `Add` (false on a duplicate id or empty id), `TryGet`, `All`, `SetFaction(id, faction)` (false for an unknown id), `SetElder(id, personId)`, `ClearElder(personId)` (clears every community whose elder it is; returns the cleared ids).
   - `TribeOpinionTable.cs`: directional `Set(fromTribeId, toTribeId, int opinion)` and `Get(fromTribeId, toTribeId)`, which is 0 when either id is empty, the ids are equal, or nothing is set.
   - `RelationshipBook.cs`:
     - `[Flags] BondFlags` with explicit values as in the design (including `Neighbour = 512`, `Elder = 1024`), and `const BondFlags KinMask = Parent | Child | Sibling | Spouse`.
     - `[Serializable] struct Relationship { int Opinion; BondFlags Bonds; }` (effective) and `[Serializable] struct StoredRelationship { BondFlags Bonds; bool HasBase; int Base; int Delta; bool IsDefault; }`.
     - `RelationshipBook`:
       - `GetStored(from, to)`
       - `SetDelta(from, to, delta)`, `SetBase(from, to, opinion)`, `ClearBase`
       - `AddBonds`, `SetBonds` (non-kin flags only; kin flags in the argument throw `ArgumentException`)
       - `SetKin(a, b, BondFlags kin)`: one of Parent, Child, Sibling, Spouse; mirrors as in the design
       - `BondFlags KinOf(holder, other)`: just the kin flags
       - `bool IsCloseKin(holder, other)`: `KinOf != None`
       - `Entries` (for a later save)
       - an entry equal to the default is removed; kin entries never are.
   - `SocialTuning.cs`: a plain `[Serializable] class` with public fields and the defaults from the `SocialConfig` table in step 5. Add `static SocialTuning Default => new SocialTuning()`.
   - `DamageLog.cs`:
     - `Record(victimId, attackerId, FactionId attackerFaction, double now)`, keeping 6 distinct attackers.
     - `TryLastAttacker(victimId, now, window, out entry)`.
     - `RecentPersonAttackers(victimId, now, window)`.
     - `Clear(victimId)`.
   - `DeathLedger.cs`: `DeathRecord` as in the design (including `VictimWasElder` and `ElderOfCommunityId`), plus `Add`, `Get(index)`, `All`.
   - `KnowledgeBook.cs`:
     - `bool Learn(personId, deathIndex, bool knowsKiller, out bool killerNewlyKnown)`, `Knows`, `KnowsKiller`, `KnownBy(personId)` (for the debug view).
     - `PendingNews { string PersonId; int DeathIndex; double DueHours; }` with `Schedule` and `TakeDue(now, List<PendingNews> into)`, in due order.
   - `Grudge.cs` (the class as in the design) and `GrudgeBook.cs`:
     - `Get(holder, target)`, `HeldBy(holder)`, `Against(target)`
     - `AddOrMerge(Grudge, SocialTuning)`, which applies merge and cap and returns the stored grudge or null if it was dropped
     - `Remove`, `RemoveHeldBy(holder)`, `RemoveAgainst(target)`
     - `All`
   - Done when: Rules compiles.

2. **Rules logic.**
   - `GrudgeRules.cs` (static, pure):
     - `float Closeness(Relationship r, SocialTuning t)` (including the Neighbour and Elder weights)
     - `float Intensity(float closeness, float vengefulness, BondFlags kinToVictim, int generation, SocialTuning t)`
     - `GrudgeTier { None = 0, Cold = 1, Hostile = 2, Rally = 3, Hunt = 4 }` and `GrudgeTier Tier(float intensity, SocialTuning t)`
     - `float Merge(float a, float b, SocialTuning t)`
     - `float Decayed(float intensity, int days, SocialTuning t)` (0 or more days; negative counts as 0)
     - `bool Forgotten(float intensity, SocialTuning t)`
     - `int DaysUntilForgotten(float intensity, SocialTuning t)`: the smallest whole number of days after which `Decayed < forgetBelow`; 0 when already below; `int.MaxValue` when `decayPerDay <= 0`.
     - `int GrudgeCapFor(bool victimWasElder, SocialTuning t)`: `elderDeathGrudgeCap` or `maxGrudgesPerDeath`.
   - `KinTerms.cs` (static, pure): `string Term(BondFlags kin, Sex other)` as in the design.
   - `AttitudeRules.cs`: `Resolve(faction, closeKin, opinion, grudgeIntensity, t)` returning `AttitudeResult` as in the design, including `RefusesDealings`.
   - `DefaultBondRules.cs` (static, pure): `bool Between(PersonRecord from, PersonRecord to, CommunityRecord fromCommunity, SocialTuning t, out Relationship derived)` as in the design.
   - `RelationshipRules.cs` (static, pure): `Relationship Effective(bool hasDerived, Relationship derived, StoredRelationship stored)`, `int DeltaAfterChange(int baseOpinion, int delta, int change)`, `int DeltaAfterRaise(int baseOpinion, int delta, int floor)`, all keeping the effective opinion in -100..100.
   - `NewsRules.cs` (static, pure): `bool Delay(PersonRecord victim, PersonRecord recipient, SocialTuning t, out double hours)`: same household or same community gives `newsDelayHours`; otherwise same kin group gives `kinNewsDelayHours`; otherwise false. The victim themself gives false.
   - `GrudgePreview.cs` (static, pure): `struct GrudgePreviewRow { string Bond; float Closeness; float Intensity; GrudgeTier Tier; bool BelowCloseness; bool OnThreshold; }` and `IReadOnlyList<GrudgePreviewRow> Rows(SocialTuning t, float vengefulness)` for the nine bonds in the design. Each row builds its `Relationship` with the same tuning fields `DefaultBondRules` uses (kin rows: household Family plus the kin flag, kin bonus applied). `OnThreshold` is true when the intensity equals `minGrudgeIntensity`, `hostileAt`, `rallyAt` or `huntAt`, or the closeness equals `grudgeClosenessThreshold` (within 0.001).
   - `SocialSimulation.cs`. The constructor takes `(SocialTuning, FactionRelationTable, FactionId player)`. Public:
     - `Registry`, `Communities`, `Tribes`, `Relations`, `Damage`, `Deaths`, `Knowledge`, `Grudges`
     - `event Action<Grudge> GrudgeCreated`
     - `bool Register(PersonRecord rec)`: adds the record. No bonds are written.
     - `bool RegisterCommunity(CommunityRecord rec)`: adds it. No bonds are written.
     - `FactionResolution EffectiveFaction(string personId)`: `FactionRules.Resolve(rec.SquadId != null, community faction when the community is registered, rec.OwnFaction, player)`; None for an unknown id.
     - `bool SetCommunityFaction(string communityId, FactionId faction, List<string> changedMembers)`: sets the community faction and fills `changedMembers` with the alive members whose effective faction changed. False for an unknown id. Setting the same faction gives an empty list.
     - `void SetElder(string communityId, string personId)`: sets `ElderId` only. A null person clears the elder.
     - `void MoveTo(string personId, string communityId)`: clears the person's elder role in the old community and sets the new community (null leaves).
     - `Relationship Relationship(from, to)`: derived plus stored, as in the design. `float Closeness(from, to)`.
     - `void AddOpinion(from, to, change)`, `void RaiseOpinionTo(from, to, floor)`: through `RelationshipRules`. `void SetAuthoredBond(from, to, BondFlags nonKinFlags, int opinion)`: `AddBonds` and `SetBase`.
     - `void RecordAttack(victimId, attackerId, now)`: `AddOpinion(victim, attacker, attackedOpinion)`. Ignores null ids and self-attacks.
     - `void RecordDamage(victimId, attackerId, FactionId attackerFaction, now)`.
     - `DeathRecord RecordDeath(string victimId, string directKillerId, bool hasDirectFaction, FactionId directFaction, WorldPoint at, IReadOnlyList<Witness> witnesses, double now)`. `Witness { string Id; float DistanceSq; }`; the adapter passes only live, awake people. In order:
       1. Attribute the killer (direct, else the damage log window).
       2. Compute `FeudGeneration` from grudges between killer and victim.
       3. Set `KillerWitnessed`: any witness who is not the killer and not Allied with the killer by effective faction.
       4. Set `VictimWasElder` and `ElderOfCommunityId`. Add the record and mark the victim Dead. `Communities.ClearElder(victimId)`.
       5. Remove grudges against the victim. Snapshot the victim's `Killing` grudges at or above `inheritAt` into `InheritableGrudges`, then remove every grudge the victim held.
       6. Fought-together opinion.
       7. Witnesses learn, closest first, with the per-death cap from `GrudgeCapFor`.
       8. Schedule news for every registered alive non-witness for whom `NewsRules.Delay` is true, at `now + hours`.
       9. Clear the victim's damage log.
     - `void Learn(personId, deathIndex, bool revealKiller, double now)`: the first time a person learns of a death whose victim they hold `Parent` toward ("the victim is my parent"), take over `InheritableGrudges` (`GrudgeRules.Inherit`, skipping dead targets and themself). Then, when the killer becomes newly known, evaluate a grudge (the creation rules in the design; closeness from `Relationship(holder, victim)` plus the Elder layer when `ElderOfCommunityId` is the holder's community).
     - `void Advance(double now)`: deliver due news (`revealKiller = KillerWitnessed`), then decay every grudge by `DayOf(now) - LastDecayDay` and remove forgotten ones.
     - `float EffectiveGrudge(holderId, otherId, out Grudge source)`: the direct grudge, or the strongest squad-scope grudge whose target shares `other`'s squad id.
     - `AttitudeResult AttitudeOf(string selfId, string otherId, FactionRelation factionRelation)`: faction only (reason Faction, no refusal) when a record is missing or `self.SquadId != null`. Otherwise `rel = Relationship(self, other)`; `closeKin = Relations.IsCloseKin(self, other)`; `opinion = rel.Opinion`, plus `Tribes.Get(self.TribeId, other.TribeId)` when neither direction has any effective bond flags, clamped to -100..100; `grudge = EffectiveGrudge(self, other)`; then `AttitudeRules.Resolve`.
     - `bool HoldsGrudgeAgainstSquad(holderId, squadId, float minIntensity)`.
     - `Grudge JoinFeud(friendId, Grudge callerGrudge, double now)`: intensity `max(hostileAt, caller × rallyIntensityFactor)`, the caller's generation.
     - `BloodPriceQuote QuoteBloodPrice(string payeeId)` (`{ Grudge Primary; List<Grudge> Settles; float Intensity; }`, null when the payee holds no grudge against a squad member) and `int PayBloodPrice(BloodPriceQuote q)` (removes every settled grudge, raises each holder's opinion of the target to `bloodPriceOpinionFloor`, returns the count), as in the design.
     - `void MarkGone(id)` (also clears an elder role), `void SetSquad(id, squadId)`, `void SetOwnFaction(id, faction)`.
   - Done when: Rules compiles and the step 3 tests pass.

3. **EditMode tests**: `Assets/Tests/EditMode/SocialRulesTests.cs` (DayClock, RelationshipBook, RelationshipRules, GrudgeRules, GrudgePreview, AttitudeRules, DefaultBondRules, NewsRules, TribeOpinionTable), `SocialSimulationTests.cs`, and `CommunityTests.cs` (CommunityRegistry and the community parts of `SocialSimulation`). See the test plan.

4. **PersonId wiring test**: `Assets/Tests/EditMode/PersonIdWiringTests.cs`. Read `Assets/Scenes/test1.unity` as text, collect every `personId: <value>` with a regex, and assert that the non-empty values are distinct. Also read every `.prefab` under `Assets/Prefabs/NPC/` and assert their own `personId` is empty. Plain file IO. It passes trivially until the field exists.

5. **Game wiring.**
   - New `Assets/Scripts/NPC/Social/SocialConfig.cs`: `[CreateAssetMenu(menuName = "Kanshilike/Social Config")]`, fields with tooltips grouped by `[Header]` sections, and `SocialTuning ToTuning()`:

     | Section | Field | Default |
     |---|---|---|
     | Opinion | `hostileOpinion` / `alliedOpinion` | -60 / 60 |
     | | `attackedOpinion` / `foughtTogetherOpinion` / `treatedWoundsOpinion` | -25 / 10 / 15 |
     | Bonds: default opinions | `neighbourBondOpinion` / `campBondOpinion` | 20 / 40 |
     | | `kinGroupBondOpinion` / `householdBondOpinion` / `elderBondOpinion` | 30 / 60 / 40 |
     | Bonds: closeness weights | Family / Lover / Friend / Comrade / Rival | 0.4 / 0.5 / 0.2 / 0.1 / -0.3 |
     | | Neighbour / Elder | 0.2 / 0.3 |
     | | Child / Parent / Spouse / Sibling | 0.7 / 0.6 / 0.6 / 0.5 |
     | Grudges: creation | `grudgeClosenessThreshold` | 0.35 |
     | | `defaultVengefulness` / `kinGrudgeBonus` | 0.5 / 20 |
     | Grudges: tiers | `minGrudgeIntensity` (Cold) / `hostileAt` / `rallyAt` / `huntAt` | 15 / 36 / 48 / 68 |
     | | `squadScopeAt` | 85 |
     | Grudges: decay and limits | `decayPerDay` / `forgetBelow` / `stackBonus` | 5 / 10 / 10 |
     | | `maxGrudgesPerHolder` / `maxGrudgesPerDeath` / `elderDeathGrudgeCap` | 3 / 4 / 20 |
     | | `maxFeudGeneration` / `generationFalloff` | 2 / 0.6 |
     | | `inheritAt` / `inheritFactor` | 60 / 0.75 |
     | Knowledge | `attributionWindowHours` / `witnessRadius` | 0.25 / 20 |
     | | `newsDelayHours` / `kinNewsDelayHours` | 3 / 24 |
     | Revenge: rally and hunt | `rallyRadius` / `rallyCloseness` / `rallyIntensityFactor` / `calloutCooldownSeconds` | 15 / 0.35 / 0.5 / 20 |
     | | `huntGiveUpHours` / `huntSearchRadius` / `gatherRadius` / `maxHuntParty` | 6 / 8 / 30 / 3 |
     | | `gatherBelowRatio` / `huntMinStrengthRatio` | 1.0 / 0.6 |
     | Blood price (4d) | `bloodPriceWorthPerIntensity` / `bloodPriceMinWorth` / `bloodPriceOpinionFloor` | 1.0 / 10 / -20 |
     | Barks (4c/4d) | `string[] calloutLines`, `grudgeBarksToTarget`, `kinGrudgeBarksToTarget`, `grudgeBarksAboutSquad`, `bloodPriceAcceptLines` | |

     - `OnValidate` keeps the tiers ordered (`minGrudgeIntensity <= hostileAt <= rallyAt <= huntAt`), warning when they are not.
   - New `Assets/Scripts/NPC/Social/Social.cs` (static):
     - `bool Active`, `double Now`, `SocialSimulation Sim`, `SocialTuning Tuning`
     - live map `Dictionary<string, Character>`, `Character Find(string id)`, `IEnumerable<Character> Live`
     - `void Reset(SocialTuning t)` (new `Sim` with `FactionRelations.Table` and `FactionRelations.PlayerId`), also called from `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`
     - `void Register(Character c)`:
       - give the character a fresh id on a live duplicate (log a warning);
       - build the `PersonRecord` (own faction from `c.OwnFaction`; vengefulness from `NpcBrain.Archetype` if present, else tuning; squad `"player"` if it has a `PlayerControllable`; memberships from `PersonTies` in 4b);
       - call `Sim.Register`, then `c.InvalidateFaction()`.
     - `void Unregister(Character c)`: `MarkGone` if still alive; remove from the live map.
     - `void HandleJoined(PlayerControllable p)`: `Sim.SetSquad(id, "player")`; in 4b also leave the community (step 9). `HandleOwnFactionChanged(Character c)`: `Sim.SetOwnFaction`.
     - `void ReportAttack(Character victim, GameObject attacker)`, `ReportDamaged(Character victim, GameObject source)`, `ReportDeath(Character victim, GameObject source)`. All return at once when `!Active`. Attackers are resolved with `GetComponentInParent<CombatActor>()`; a `Character` gives its `PersonId`; any actor gives its `Faction`.
     - `ReportDeath` builds the witness list from `Live` (alive, `!Stats.IsSleeping`, within `witnessRadius`, not the victim) and calls `Sim.RecordDeath` with `Now`.
   - New `Assets/Scripts/NPC/Social/SocialAttitudeProvider.cs` (`IAttitudeProvider`, as in the design) and `SocialCommunityFactionSource.cs` (`ICommunityFactionSource`: the person's registered community's faction and name; false otherwise).
   - New `Assets/Scripts/NPC/Social/SocialWorld.cs` (MonoBehaviour):
     - `[SerializeField] SocialConfig config`.
     - `Awake`: `Social.Reset(config.ToTuning())`, then register every enabled `Community` (4b; skip until it exists), then every `Character` already enabled. `FindObjectsByType` once each is fine here.
     - `OnEnable`: `Social.Active = true`; install `Attitude.Provider` and `CharacterFaction.CommunitySource`; subscribe to `TimeManager.OnTimeChanged`, `PlayerControllable.Joined` and `CharacterFaction.OwnFactionChanged`.
     - `OnDisable`: undo those (set the provider and source back to null) and set `Active = false`.
     - Time handler: `clock.Advance(hour)`, set `Social.Now = clock.TotalHours`, and call `Sim.Advance(Now)` when `floor(TotalHours)` changed.
     - With no config, log a warning and use `SocialTuning.Default`.
   - `Assets/Scripts/Character/Character.cs` (hooks, listed in the hooks table):
     - add `[SerializeField, HideInInspector] private string personId;` and `public string PersonId => personId;`, plus `internal void AssignNewPersonId()`.
     - add `[SerializeField] private Sex sex;` and `public Sex Sex => sex;`. `Social.Register` copies it into the `PersonRecord`.
     - add `#if UNITY_EDITOR void OnValidate()` as in the design (clear on prefab assets, fill when empty).
     - `protected override void Awake() { base.Awake(); if empty, assign; }`
     - `void OnEnable() => Social.Register(this);` (idempotent: re-registering the same object is a no-op).
     - `OnDestroy`: add `Social.Unregister(this)`.
     - `Start`: also subscribe `stats.OnDamaged += HandleDamaged` (unsubscribe in `OnDestroy`); `HandleDamaged` calls `Social.ReportDamaged(this, source)`.
     - `HandleAttacked`: first line `Social.ReportAttack(this, attacker)`.
     - `HandleDied`: first line `Social.ReportDeath(this, killer)`.
   - Done when: all existing EditMode and PlayMode tests still pass (no `SocialWorld`, so `Active` is false and no provider or source is installed) and step 7 passes.

6. **Tools.**
   - New `Assets/Scripts/NPC/Social/SocialDebugProvider.cs`: registers itself in `NpcDebugReport.Providers` (static constructor, triggered from `Social.Reset`) and adds the sections listed in the design when `Social.Active`; otherwise one line "Social system inactive (no SocialWorld)".
   - New checks in `Assets/Scripts/Editor/Validation/`: `PersonIdCheck` (duplicate ids in the scene: error; empty: warning "select it once to assign"; non-empty on an NPC prefab asset: error), `SocialWorldCheck`.
   - New `Assets/Scripts/Editor/SocialConfigEditor.cs` (`[CustomEditor(typeof(SocialConfig))]`): draws the default inspector (the sections come from the headers), then a "Preview" foldout with the vengefulness slider and a read-only table from `GrudgePreview.Rows(target.ToTuning(), v)` and `Rows(..., defaultVengefulness)`: columns Bond, Closeness, Intensity, Tier, one pair per vengefulness, flagged rows in a warning colour.
   - Done when: the preview shows the design's table for the default asset, and the step 7 debug report test passes.

7. **Tests for 4a wiring**: PlayMode `Assets/Tests/PlayMode/SocialRecordingTests.cs` and `SocialDebugReportTests.cs`. Use the shared `TestWorld` and `TestFactions` helpers plus a `MakeSocialWorld(SocialTuning)` helper that sets `config` by reflection. EditMode: `Assets/Tests/EditMode/SocialSetupValidatorTests.cs` for the 4a checks. See the test plan.

8. **(Editor) Config and scene.**
   - Create `Assets/NPC/Social/SocialConfig.asset` with the defaults. Check the preview: it must match the design's table with no flagged row.
   - In `test1`, add an empty `SocialWorld` GameObject with the component and the config assigned.
   - Select Jonttuli and Keppana once so `OnValidate` gives them ids, then save the scene.
   - Done when: both show a non-empty `personId` in the Debug inspector, step 4 passes, and the validator reports no errors.

### Phase 4b: knowledge, bonds and earned opinion (needs human-npcs phase 2 and 4a)

9. **Social structure assets and components, `PersonTies` and vengefulness.**
   - New `Assets/Scripts/NPC/Social/TribeDefinition.cs`: `[CreateAssetMenu(menuName = "Kanshilike/Social/Tribe")] public class TribeDefinition : DefinitionAsset` with tooltips:
     - `List<SkillLeaning> skillLeanings`, `[Serializable] class SkillLeaning { SkillType skill; [Min(0)] float xp; }`
     - `string[] maleNames`, `string[] femaleNames`, `string[] barkLines` (optional)
     - `List<TribeOpinion> opinionOf`, `[Serializable] class TribeOpinion { TribeDefinition other; [Range(-30, 30)] int opinion; }`
     - `string PickName(Sex sex)`: random from the matching list, or null when it is empty.
   - New `Assets/Scripts/NPC/Social/KinGroup.cs` and `Household.cs`: `[CreateAssetMenu(menuName = "Kanshilike/Social/Kin Group")]` and `".../Household"`, both `: DefinitionAsset` with no extra fields (a description tooltip: "suku: extended lineage across villages" and "talo: close family under one roof").
   - New `Assets/Scripts/NPC/Social/Community.cs` (scene component, `[DisallowMultipleComponent]`):
     - fields `string id` (empty uses the GameObject name lower-cased), `string displayName`, `CommunityKind kind`, `FactionDefinition faction`, `Character elder`.
     - `OnEnable`: `Social.RegisterCommunity(this)` (idempotent). `public string Id`, `public FactionDefinition Faction`.
     - `public void SetFaction(FactionDefinition f)`: sets the field and calls `Social.SetCommunityFaction(this, f)`. This is the elder's decision hook; nothing calls it in the demo except the play-test.
     - `#if UNITY_EDITOR OnValidate`: in Play mode, when `faction` differs from the registered one, call `SetFaction(faction)`, so the inspector can flip a village's faction during play-tests.
   - New `Assets/Scripts/NPC/Social/PersonTies.cs`, `[RequireComponent(typeof(Character))]`, implements `INpcTalkSource`: `TribeDefinition tribe`, `Community community`, `KinGroup kinGroup`, `Household household`, `List<AuthoredBond> bonds` (`[Serializable] class AuthoredBond { Character other; BondFlags flags; [Range(-100,100)] int opinion; bool mutual = true; }`). `flags` may include one kin flag, read as "other is my …" (Child means other is my son or daughter). Public read-only accessors for each membership.
     - `Start` applies the authored bonds through `Social.Sim.SetAuthoredBond` (non-kin flags and opinion), both ways when `mutual`, and any kin flag through `Relations.SetKin` (always mirrored, whatever `mutual` says). It skips a null or self `other` with a warning, and a bond with two kin flags with a warning.
     - `Start` also applies the tribe's skill leanings once (a private `leaningsApplied` flag): for each leaning, `GetComponent<SkillSet>()?.GainXP(skill, xp)`. `SkillSet.Awake` has run by then. No `SkillSet` means no leanings.
     - `CollectLines`: the tribe's `barkLines` at priority 0 with `Hold = true`.
   - `Social` additions:
     - `Register` reads `PersonTies` for tribe, community, kin group and household ids (`Id` of each asset or component; null when unset). If the tribe is set and not yet known, `RegisterTribe(tribe)` copies its `opinionOf` into `Sim.Tribes`. If `characterName` is empty, it takes `tribe.PickName(sex)`.
     - `RegisterCommunity(Community com)`: `FactionRelations.Register(com.Faction)`, builds the `CommunityRecord` (`Faction = com.Faction.FactionId`, `ElderId = com.elder?.PersonId`), calls `Sim.RegisterCommunity`, then `CharacterFaction.InvalidateAll()`.
     - `SetCommunityFaction(Community com, FactionDefinition f)`: `FactionRelations.Register(f)`, `Sim.SetCommunityFaction(id, f.FactionId, changed)`, `CharacterFaction.InvalidateAll()`, then the mid-fight rule: for every live non-squad character whose `CurrentOrder` is an `AttackOrder` where the attacker or the target is in `changed`, `GiveOrder(null)` when `Attitude.Of(attacker, target)` is Allied.
     - `HandleJoined`: after `SetSquad`, `Sim.MoveTo(id, null)` when the person has a community, then `CharacterFaction.InvalidateAll()`.
   - `Assets/Scripts/NPC/Humans/NpcArchetype.cs` (hook): add `[Range(0,1)] public float vengefulness = 0.6f;` with a tooltip.
   - New checks `PersonTiesCheck`, `CommunityCheck`, `SocialDefinitionCheck` in `Assets/Scripts/Editor/Validation/`, as listed in the design; missing references are detected with `SerializedProperty.objectReferenceValue == null && objectReferenceInstanceIDValue != 0`.
   - Done when: two townsfolk with the same `Household` asset have Family at +60 to each other after Play starts (with no stored entry), and a village member's `Faction` matches its `Community` (step 14 tests).

10. **Knowledge wiring.**
    - Witnesses and news are already in `Sim.RecordDeath` and `Sim.Advance` (step 2). Check that `Social.ReportDeath` passes the witness list, and that `SocialWorld` advances on whole hours.
    - `Assets/Scripts/Character/Corpse.cs` (hook): in `ExecuteInteraction`, before raising `LootRequested`, call `Social.ReportCorpseInspected(interactor, GetComponent<Character>())`. In `Social`, find the victim's latest `DeathRecord` and call `Sim.Learn(looterId, index, revealKiller: record.KillerWitnessed, Now)`.
    - Done when: the step 14 knowledge tests pass.

11. **Earned opinion.**
    - Attacked: already in `ReportAttack` (step 5).
    - Fought together: already in `Sim.RecordDeath`.
    - `Assets/Scripts/Character/Orders/TreatWoundsOrder.cs` (hook): after `patientStats.Heal(...)`, if `!self && gained > 0`, call `Social.ReportTreated(character, patient)`, which adds `treatedWoundsOpinion` from patient to healer.
    - Done when: the step 14 opinion tests pass.

12. **Squad holder feedback.** In `SocialWorld`, subscribe to `Sim.GrudgeCreated`. When the holder's live character has a `PlayerControllable`, show `ShowFloatingText($"{holder} swears vengeance on {target}", 4f)` and log it. Unsubscribe in `OnDisable`.

13. **EditMode validator tests for 4b**: extend `SocialSetupValidatorTests` with the 4b checks (see the test plan).

14. **PlayMode tests**: `Assets/Tests/PlayMode/KnowledgeSpreadTests.cs` and `Assets/Tests/PlayMode/CommunityTests.cs`. Build `Community` and `PersonTies` in code and set their fields by reflection; make `TribeDefinition`, `KinGroup`, `Household` and `SkillDefinition` with `ScriptableObject.CreateInstance`. See the test plan.

15. **(Editor) Content and scene ties.**
    - Tribes: create `Assets/NPC/Social/Tribes/Hamalaiset.asset` (display name "Hämäläiset") and `Karjalaiset.asset` ("Karjalaiset"), each with 6-8 period Finnic names per sex, 2-3 bark lines, a small skill leaning (open question 6; for example Hämäläiset XP in a farming or axe skill, Karjalaiset in a hunting or bow skill, whatever `SkillType` holds), and `opinionOf` the other tribe at -10.
    - Kin groups and households: `Assets/NPC/Social/KinGroups/AinonSuku.asset` (display "Ainon suku"); `Assets/NPC/Social/Households/AinonTalo.asset` ("Ainon talo"), `IlmarinTalo.asset` ("Ilmarin talo"), `LempinTalo.asset` ("Lempin talo").
    - Village **Kuusikylä**: an empty GameObject `Kuusikyla` with `Community` (id `kuusikyla`, name "Kuusikylä", Village, faction Townsfolk, elder = the Townsperson_NoRecruit instance, renamed "Heikki"). The elder cannot be recruited, so the elder-death path stays testable.
    - Add `PersonTies` to `Assets/Prefabs/NPC/Townsperson.prefab` (the variant inherits it), empty.
    - `PersonTies` on the Kuusikylä townsfolk: tribe Hämäläiset, community Kuusikylä. Aino and the second townsperson share household AinonTalo and kin group AinonSuku. Make the second one Aino's son Veikko (`sex: Male`; Aino's bond to him has `Child`), so killing him tests the kin path.
    - Place one more Townsperson in Kuusikylä, "Lempi" (`sex: Female`, tribe Hämäläiset, community Kuusikylä, kin group AinonSuku, household LempinTalo), with her own bed anchor, so a kin-group relative (Hostile tier) can be play-tested.
    - Second village **Koivula**, minimal, to test cross-village kin news and kin in an enemy village: an empty GameObject `Koivula` with `Community` (id `koivula`, name "Koivula", Village, faction Townsfolk, elder Ilmari) at least 80 m from the Kuusikylä anchors (beyond `witnessRadius` and the 60 m `anchorSearchRadius`), on the NavMesh, with its own Village, Stall, Campfire and Bed anchors. One Townsperson instance "Ilmari" (`sex: Male`, tribe Hämäläiset, community Koivula, kin group AinonSuku, household IlmarinTalo), with an authored bond to Aino with `Sibling`. If the NavMesh has no room 80 m away, place it as far as it allows and note the distance in the scene object name.
    - Keep Jonttuli and Keppana without memberships (no tribe, community, kin group or household), so their skills and faction do not change.
    - Add one authored bond: Keppana and Aino are Friend at +50, mutual (`PersonTies` on Keppana with only the bond). This lets the squad-holder path be play-tested.
    - Done when: the play-test items for 4b work, `PersonIdWiringTests` still passes, and the validator reports no errors.

### Phase 4c: RevengeRole and parley (needs human-npcs phases 2 and 3, and 4b)

16. **Rules for hunting.** New `Assets/Scripts/Rules/Social/HuntRules.cs`:
    - `enum HuntDecision { Wait = 0, Hunt = 1, Gather = 2 }`
    - `static float Strength(float health, float damage) => max(0, health) * max(0.1f, damage)`
    - `static HuntDecision Decide(float own, IReadOnlyList<float> friendStrengths, float target, SocialTuning t, out int friendsToGather)`. Friends are taken strongest first, up to `maxHuntParty`, until `own + party >= target × gatherBelowRatio`. If that is reached with 0 friends, Hunt. If it is reached with friends, Gather. If not reached, Gather when the party sum is at least `target × huntMinStrengthRatio`, else Wait.
    - `static bool HuntExpired(double startedHours, double now, SocialTuning t)`.

17. **EditMode tests**: `Assets/Tests/EditMode/HuntRulesTests.cs`.

18. **Minimal perception** (the decision is in the design). New `Assets/Scripts/NPC/Perception/ActorPerception.cs`:
    - `[Serializable] public struct PerceptionSettings { public float range; public float fieldOfViewAngle; public float eyeHeight; public LayerMask mask; public bool requireLineOfSight; }`
    - `public static bool CanSee(Transform self, Transform target, in PerceptionSettings s)`:
      - range and half-angle FOV tests as in `AnimalPerceptionUtility`;
      - when `requireLineOfSight`, a raycast from the eye to the target collider's `bounds.center` within `mask` must hit the target or its child first.
    - Do **not** change `AnimalPerceptionUtility` or AnimalAI.
    - Done when: it compiles; the step 24 tests cover it.

19. **Archetype fields.** `Assets/Scripts/NPC/Humans/NpcArchetype.cs`: add a `PerceptionSettings sight` (default range 25, fieldOfViewAngle 110, eyeHeight 1.6, mask: Default and the character layer, `requireLineOfSight` true) and `revengeChaseDistance` (25). No brain or role-interface change is needed: `StillWants` and `RoleMayReplace` exist since human-npcs phase 2.

20. **`Social` queries for roles.** Add to `Social`:
    - `IReadOnlyList<Grudge> GrudgesHeldBy(Character)`
    - `IEnumerable<Character> SquadMembersAlive()`, from `PlayerControllable.All`
    - `Grudge JoinFeud(Character friend, Grudge caller)`
    - `void SetLastKnown(Grudge g, Vector3 p)`
    - `float Closeness(Character from, Character to)`

21. **Parley.** New `Assets/Scripts/NPC/Social/Parley.cs` (static): `const string OfferActionId = "bloodprice"`; `static bool IsEnvoyTo(Character envoy, Character holder)` exactly as defined in the design (`envoy.CurrentOrder is InteractOrder io && io.ActionId == OfferActionId && io.Target is Component c && c != null && c.gameObject == holder.gameObject`, envoy alive and in the squad).

22. **`RevengeRole`.** New `Assets/Scripts/NPC/Humans/RevengeRole.cs`, `[RequireComponent(typeof(NpcBrain))]`, implements `INpcRole`:
    - Runtime state: `nextCallout`, `huntTargetId`, `huntStartedHours`, `Character huntLeader`, `double followUntilHours`.
    - `TryGetReaction(brain, out order)`:
      1. Return false if `!Social.Active`, dead or asleep.
      2. Build the candidates (design: Targets; tier Hostile or above; skip any `c` with `Parley.IsEnvoyTo(c, character)`). Pick the nearest visible one with `ActorPerception.CanSee(transform, c.transform, archetype.sight)`.
      3. **Visible:**
         - `SetLastKnown`.
         - If the current order is an `AttackOrder` on that target, return false.
         - If the tier is Rally or higher and `Time.time >= nextCallout`: show a filled call-out bark, call `Rally(target, grudge)`, and set `nextCallout`.
         - `order = new AttackOrder(target, archetype.revengeChaseDistance)`, return true.
      4. **Not visible, following a hunt leader** (`huntLeader` alive and `Social.Now < followUntilHours`): if not already following, `order = new FollowOrder(huntLeader)`, return true.
      5. **Not visible, top grudge tier Hunt with a live target:**
         - if there is no hunt yet, start one: compute strengths, then `HuntRules.Decide`. On Gather, pick friends (alive, `RevengeRole`, closeness at least `rallyCloseness`, within `gatherRadius`, `!brain.InCombat`, strongest first), call `JoinFeud` for each and set their `huntLeader = this` with `followUntilHours = Now + huntGiveUpHours`. On Wait, return false.
         - if the hunt has expired, clear it and return false.
         - if already wandering there, return false; otherwise `order = new WanderOrder(lastKnown, huntSearchRadius, archetype.wanderPauseSeconds)`, return true.
      6. Otherwise return false.
    - `StillWants(brain, order)`:
      - `AttackOrder`: the target is alive, is still a candidate, and is not an envoy to this NPC.
      - `WanderOrder`: the hunt is not expired and the grudge is still Hunt.
      - `FollowOrder`: within `followUntilHours`, and the leader is alive.
    - `Rally(target, grudge)`: as in the design.
    - Done when: the step 24 tests pass.

23. **Interaction behaviour check.** No code: `NpcInteractable` and `Recruitable` already refuse through `Attitude` (human-npcs). Confirm with the step 24 interaction tests that Cold holders refuse and Hostile-by-grudge holders show Talk, Recruit and Attack.

24. **PlayMode tests**: `Assets/Tests/PlayMode/RevengeRoleTests.cs`, and additions to `NpcInteractionTests`. Use `sight.requireLineOfSight = false`. See the test plan.

25. **(Editor) Content.**
    - Add `RevengeRole` to `Assets/Prefabs/NPC/Townsperson.prefab` (the variant inherits it).
    - Set the Townsperson archetype's `vengefulness` (0.6) and `sight`.
    - Fill `SocialConfig.calloutLines` with 3 era lines, for example "{target}! {victim}'s blood calls for yours!"
    - Done when: the 4c play-test items work.

### Phase 4d: barks and blood price (needs human-npcs phase 3 and 4c)

26. **Rules.** New `Assets/Scripts/Rules/Social/BloodPriceRules.cs`:
    - `enum BloodPriceBlock { None = 0, Dead = 1, InCombat = 2, TooFar = 3, NotEnoughValuables = 4 }`
    - `static int RequiredWorth(float intensity, float worthPerIntensity, int minWorth)`: `max(minWorth, ceil(intensity × worthPerIntensity))`; a `worthPerIntensity` of 0 or less gives `max(1, minWorth)`.
    - `struct ValuableStack { int Key; int Worth; int Count; }` and `struct ValuableTake { int Key; int Count; }`. `Key` is the caller's index into its own list of distinct items, so Rules never sees an `Item`.
    - `static int TotalWorth(IReadOnlyList<ValuableStack> stacks)`: sum of `Worth × Count`, ignoring stacks with worth or count of 0 or less.
    - `static bool TrySelect(IReadOnlyList<ValuableStack> stacks, int requiredWorth, List<ValuableTake> into)`: the greedy rule exactly as in the design (clear `into` first; false and empty when the total is short; stable sort by worth descending; take `min(count, ceil(remaining / worth))` per stack until `total >= requiredWorth`). A `requiredWorth` of 0 or less returns true with nothing taken.
    - `static BloodPriceBlock Check(bool alive, bool inCombat, bool inReach, int carriedWorth, int requiredWorth)`, checked in enum order.
    - `static string Reason(BloodPriceBlock b)`: "Dead", "Too busy fighting", "Too far away", "Wants valuables worth {n}". Fill `{n}` in the caller.

    New `Assets/Scripts/Rules/Social/BarkFormat.cs`: `static string Fill(string line, string victim, string target, string kin = null)`. It replaces `{victim}`, `{target}` and `{kin}`, leaves other text untouched, and treats null as "...".
    - EditMode tests: `Assets/Tests/EditMode/BloodPriceRulesTests.cs`.

27. **Valuable items.**
    - `Assets/Scripts/Inventory/Item.cs`: add, with tooltips,
      - `[Tooltip("Abstract barter worth of one item. Never shown to the player as a price; there is no money.")] [Min(0)] public int worth;`
      - `[Tooltip("Counts as a valuable for blood price: swords, furs, silver.")] public bool valuable;`
      - Existing assets keep the defaults (0, false) with no data migration.
    - New check `ValuableWorthCheck` (items with `valuable` and `worth` 0: error).

28. **`GrudgeTalk` and `BloodPriceOffer`.**
    - New `Assets/Scripts/NPC/Social/GrudgeTalk.cs` (`INpcTalkSource`, `[RequireComponent(typeof(Character))]`): when `Social.Active` and `Social.Sim.EffectiveGrudge(npc, interactor)` is at least `minGrudgeIntensity`: one `grudgeBarksToTarget` line (or `kinGrudgeBarksToTarget` when the victim was the holder's close kin) at priority 10 with `Hold = false`. Otherwise, if the NPC holds a grudge against another squad member: one `grudgeBarksAboutSquad` line at priority 10 with `Hold = true`. Lines are filled with `BarkFormat.Fill` from registry names and `KinTerms`.
    - New `Assets/Scripts/NPC/Social/BloodPriceOffer.cs` (`INpcInteractionSource`, `[RequireComponent(typeof(Character))]`):
      - `AddOptions`: when `Social.Active` and `Sim.QuoteBloodPrice(npc)` is not null, add `new InteractionOption(Parley.OfferActionId, "Offer blood price", primaryOnly: true)`. (`NpcInteractable` only asks sources when the NPC is not Hostile by faction.)
      - `TryExecute(bloodprice)`:
        1. `quote = Sim.QuoteBloodPrice(npc)`; none: show "..." and return true.
        2. `required = BloodPriceRules.RequiredWorth(quote.Intensity, bloodPriceWorthPerIntensity, bloodPriceMinWorth)`.
        3. Build the stacks from the payer's `CarriedInventory.Entries`: one stack per distinct `Item` with `valuable && worth > 0`, counts summed, `Key` = index in a local `List<Item>`, in first-seen order. `carriedWorth = TotalWorth(stacks)`.
        4. Check with `inCombat = brain.InCombat` and `inReach = InventoryActions.InReach`.
        5. On a block, show the reason above the NPC (`NotEnoughValuables` with `{n}` = `required`).
        6. Otherwise `TrySelect`, remove each `ValuableTake` with `RemoveOneOf(items[take.Key])` `take.Count` times, call `Sim.PayBloodPrice(quote)`, and show an accept line.
    - Done when: the step 29 tests pass.

29. **Tests for 4d**: PlayMode `Assets/Tests/PlayMode/BloodPriceTests.cs` (including parley), and the `ValuableWorthCheck` case in `SocialSetupValidatorTests`. See the test plan.

30. **(Editor) Content.**
    - Add `GrudgeTalk` and `BloodPriceOffer` to `Assets/Prefabs/NPC/Townsperson.prefab`, after `Recruitable` (so the option order is Talk, Recruit, Offer blood price, Attack).
    - Tag the valuables:
      - `Assets/Items/Weapons/Sword.asset`: `worth 30`, `valuable` on.
      - `Assets/Items/Misc/WolfPelt.asset` (the fur): `worth 10`, `valuable` on.
      - New `Assets/Items/Misc/SilverArmRing.asset` (MiscItem): `itemName` "Silver arm-ring", description that it is a twisted silver ring valued by its weight, `weight 0.1`, `maxStack 5`, `size 1x1`, an existing placeholder icon, `worth 40`, `valuable` on.
      - Every other item stays at worth 0, not valuable.
    - In `SocialConfig.asset`, check `bloodPriceWorthPerIntensity` (1.0) and `bloodPriceMinWorth` (10), and fill `grudgeBarksToTarget`, `kinGrudgeBarksToTarget`, `grudgeBarksAboutSquad` and `bloodPriceAcceptLines` (2-3 each, Iron Age tone, no crossbows, no coin).
    - In `test1`, give Keppana (the intended payer) two silver arm-rings and three wolf pelts, so a Hunt grudge (80) can be paid and a second payment falls short.
    - Done when: the 4d play-test items work and the validator reports no errors.

### Phase 4e: feud UI (needs 4d)

31. **Rules: reveal, events, view model.**
    - `Grudge`: append `bool Revealed`. `GrudgeBook.AddOrMerge` keeps `Revealed` when either side had it.
    - `SocialSimulation`: add `bool Reveal(string holderId, string targetId)`, `event Action<Grudge> GrudgeRevealed`, `event Action<Grudge, GrudgeTier> GrudgeTierChanged`, `event Action<Grudge, GrudgeEnd> GrudgeRemoved`, and `enum GrudgeEnd { TargetDied = 0, Paid = 1, Forgotten = 2, HolderDied = 3 }`. Raise `GrudgeRemoved` from every place a grudge is removed (death of target, death of holder, `PayBloodPrice`, decay in `Advance`) and `GrudgeTierChanged` from merge, `JoinFeud` and decay when the tier differs. `JoinFeud` copies the caller's `Revealed`.
    - `SocialTuning`: add `bloodPriceFairFrom` 30, `bloodPriceHeavyFrom` 50, `bloodPriceGreatFrom` 75, `killingsShownDays` 10.
    - `BloodPriceRules`: add `enum BloodPriceBand { Small = 0, Fair = 1, Heavy = 2, Great = 3 }` and `static BloodPriceBand Band(int requiredWorth, SocialTuning t)`.
    - New `Assets/Scripts/Rules/Social/FeudReport.cs`: `FeudRow`, `HeldRow`, `KillingRow`, `FeudReport { List<FeudRow> Against; List<HeldRow> Held; List<KillingRow> Killings; }` and `static void Build(...)` as in the design. `Build` clears the lists first and allocates nothing else, because the panel calls it twice a second.
    - New `Assets/Scripts/Rules/Social/KinReport.cs`: `KinRow` and `static void Build(SocialSimulation sim, string personId, KinReport into)`.
    - EditMode tests: `FeudReportTests.cs`, plus cases in `BloodPriceRulesTests.cs` and `SocialSimulationTests.cs`. See the test plan.
    - Done when: Rules compiles and the tests pass.

32. **Reveal hooks.**
    - `GrudgeTalk` (step 28): after showing a grudge bark to a squad member, `Social.Sim.Reveal(npcId, grudge.TargetId)`.
    - `RevengeRole` (4c): on a call-out, reveal the holder's grudge; when it issues an `AttackOrder` on a squad member, reveal the grudge that chose that target.
    - `BloodPriceOffer.TryExecute`: reveal every grudge in the quote (`Primary` and `Settles`) before the checks.
    - Done when: the reveal tests in step 36 pass.

33. **Message feed.**
    - New `Assets/Scripts/UI/GameMessages.cs` (static): `enum MessageKind { Info = 0, Warning = 1, Good = 2 }`, `static event Action<string, MessageKind> Posted`, `static void Post(string text, MessageKind kind = MessageKind.Info)`. `Post` ignores null or empty text, creates the `MessageFeed` on first use when the application is playing, then raises `Posted`. Reset the event in `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
    - New `Assets/Scripts/UI/MessageFeed.cs` (MonoBehaviour, self-created like `SelectionHud.Instance`): its own overlay canvas (sorting order 60, same scaler settings as `SelectionHud`), no `GraphicRaycaster` (it must never block clicks), 5 `UIFactory.Text` lines at the top left. A new message pushes older ones up; each line fades over its last second and is cleared after `messageSeconds` (serialized, 8). Colours: Info `TextColor`, Warning `WarningColor`, Good a light green.
    - Done when: calling `GameMessages.Post("test")` in Play mode shows a line that fades.

34. **`FeudHud`, markers and panel.**
    - `SocialWorld`: add `[SerializeField] bool showFeudUi = true;` and `[SerializeField] KeyCode feudPanelKey = KeyCode.F;`. In `OnEnable`, after `Social.Active` is set, create a `FeudHud` child when `showFeudUi`; destroy it in `OnDisable`.
    - `SocialConfig`: a new section "Feud messages" with the message lines from the design table (one string each, with `{holder}`, `{target}`, `{victim}`, `{kin}`, `{killer}`, `{community}`), the tuning fields from step 31 in the "Blood price" section, and `offerTimeoutSeconds` (20).
    - New `Assets/Scripts/NPC/Social/UI/FeudHud.cs`:
      - `Awake`: overlay canvas, sorting order 90, scaler as in `SelectionHud`, a `GraphicRaycaster`.
      - Subscribes to `Sim.GrudgeCreated/Revealed/TierChanged/Removed` and to the death report (`Social.DeathRecorded`, a new Game-side event raised by `Social.ReportDeath` with the record) and posts the messages in the design table. Paid: post once per `PayBloodPrice` call, not per removed grudge (remember the frame of the last Paid message). Unsubscribes in `OnDestroy`.
      - `Update`: toggle the panel on `feudPanelKey` (not while an inventory window is open: `InventoryScreen.IsOpen`); close it on Escape; every 0.5 s rebuild the report (`FeudReport.Build` with squad id `"player"`), then refresh the markers and, if open, the panel.
    - New `Assets/Scripts/NPC/Social/UI/GrudgeMarkers.cs`: a pool of small `UIFactory.Panel` squares rotated 45 degrees, one per distinct holder in `report.Against` (the highest tier wins when a holder has several rows). Each `LateUpdate`: position at `WorldToScreenPoint(holder.transform.position + Vector3.up * 2.2f)` using `Camera.main`, hide when z is negative or the holder has no live `Character` (`Social.Find`). Colour by tier as in the design; green while `Parley.IsEnvoyTo(anySquadMember, holder)`; Hunt scales between 1 and 1.25 on a sine. `raycast: false`.
    - New `Assets/Scripts/NPC/Social/UI/FeudPanel.cs`: built with `UIFactory.Panel`, `Text` and `Button` (header with title and close button, two tab buttons, a body of text lines). `Show(FeudReport, KinReport, Character primary)` writes the lines exactly as in the design; text only, no icons. The "carries enough" line needs the primary's stacks: move the stack-building loop of `BloodPriceOffer` (step 28) into a shared `BloodPriceOffer.BuildStacks(Inventory, List<Item>, List<ValuableStack>)` and use it in both places. The primary selected member comes from `SelectionManager.PrimarySelected`; with none selected the Kin tab says "Select someone." and the carries line is left out.
    - Click-through: check how `SelectionManager` skips world clicks over the inventory windows and apply the same guard for this canvas and the dialog. If it has no such guard, add `EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()` at the top of its left and right mouse handlers.
    - New check `FeudUiSetupCheck` (`ISetupCheck`): a `SocialWorld` with `showFeudUi` and no `EventSystem` in the scene is an error (buttons would not work); an empty feud message line is a warning.
    - Done when: the 4e play-test items for messages, markers and the panel work.

35. **Blood price confirm dialog.**
    - `Parley`: add `static void BeginOffer(Character envoy, Character holder)`, `static void EndOffer()`, and make `IsEnvoyTo` also true for the pending pair. Only one offer can be pending.
    - `BloodPriceOffer`: add `public static Func<BloodPriceOffer, Character, BloodPriceQuote, bool> Confirm;` (returns true when it took over). In `TryExecute`, after the checks pass and before taking anything: if `Confirm != null && Confirm(this, payer, quote)`, return true without paying. Move the "select, remove items, `PayBloodPrice`, accept line" part into `public bool Pay(Character payer, BloodPriceQuote quote)`, which re-runs the checks and `TrySelect` and returns false with the reason shown when they fail.
    - New `Assets/Scripts/NPC/Social/UI/BloodPriceDialog.cs` (owned by `FeudHud`, which sets `BloodPriceOffer.Confirm` in `OnEnable` and clears it in `OnDisable`):
      - `Open(offer, payer, quote)`: `Parley.BeginOffer`, `holderBrain.HoldFor(offerTimeoutSeconds, payer.transform)`, show the text with the household or holder name, the band word, the victim, and the list from `TrySelect` grouped as "2 x Silver arm-ring".
      - **Pay**: `offer.Pay(payer, quote)`, then close.
      - **Walk away**, Escape, or any auto-close condition from the design (checked each `Update`: `payer.CurrentOrder != null`, either dead, `!InventoryActions.InReach`, `holderBrain.InCombat`, timeout): close.
      - Close: `Parley.EndOffer()`, and if the holder's current order is still the hold, end it so the brain returns to its schedule.
    - Done when: the dialog tests in step 36 pass.

36. **Tests for 4e**: EditMode `FeudReportTests.cs`; PlayMode `Assets/Tests/PlayMode/FeudUiTests.cs`; the `FeudUiSetupCheck` case in `SocialSetupValidatorTests`. See the test plan.

37. **(Editor) Content.**
    - Fill the "Feud messages" lines in `SocialConfig.asset` with the texts from the design table (Iron Age tone, no coin).
    - Check `test1` has an `EventSystem` (the inventory windows already need one) and that `SocialWorld.showFeudUi` is on.
    - Done when: the 4e play-test items work and the validator reports no errors.

## Test plan

- **Automated: EditMode** (`Assets/Tests/EditMode`, assembly `Kanshilike.Tests.EditMode`; everything tested lives in `Kanshilike.Rules`, except the validator tests, which use `Kanshilike.Game.Editor`)
  - (4a) `SocialRulesTests`:
    - `DayClock`:
      - the first call seeds, Day 0.
      - 23 then 1 gives Day 1 and TotalHours 25.
      - forward moves keep the day.
      - two wraps give Day 2.
    - `RelationshipBook`:
      - a missing entry is the default stored relationship.
      - it is directional (`a→b` does not change `b→a`).
      - an entry back at the default is removed (`Entries` is empty).
      - `BondFlags` numeric values are pinned, including `Neighbour == 512` and `Elder == 1024`.
      - `SetKin(a, b, Child)` gives Child on a→b and Parent on b→a; Sibling and Spouse are set both ways; `SetBonds` and `AddBonds` with a kin flag throw; with `Elder` or `Neighbour` they do not.
      - a kin entry with no base and delta 0 is not removed.
      - `IsCloseKin` is true for each of the four kin flags and false for Family alone.
    - `RelationshipRules`:
      - `Effective`: flags are the union; with no stored base the opinion is derived + delta; with a stored base it is base + delta; no derived and no stored is (0, None); the result is clamped at ±100.
      - `DeltaAfterChange`: base 60, delta 0, change +60 gives delta 40 (effective 100, clamped). Base 20, delta -110 (effective -90), change -25 gives delta -120 (effective -100); a further -25 leaves the delta at -120 (no pile-up past the clamp); a following +15 then gives effective -85.
      - `DeltaAfterRaise`: raises a lower effective opinion to the floor and leaves a higher one unchanged.
    - `GrudgeRules`:
      - `Closeness`: comrade +40 is 0.5; neighbour +20 is 0.4; Family +30 is 0.7; family +60 is 1.0 (clamped); Neighbour + Elder at +40 is 0.9 (and 1.0 with Family); rival at +20 is 0; opinion -100 is 0; Child at opinion 0 is 0.7.
      - `Intensity` with kin: Child at closeness 1.0 and vengefulness 0.6 is 80 (Hunt); Friend at the same closeness is 60 (Rally).
      - `Intensity` with generations 0, 1 and 2 (falloff).
      - `Tier` edges at 14.9/15 (None/Cold), 35.9/36 (Cold/Hostile), 47.9/48 (Hostile/Rally) and 67.9/68 (Rally/Hunt); `(int)GrudgeTier.Cold == 1`, `Hunt == 4`.
      - `Merge` adds `stackBonus` and caps at 100.
      - `Decayed`: 0 days unchanged, 3 days -15, negative days treated as 0, never below 0.
      - `Forgotten` below 10.
      - `DaysUntilForgotten`: 24 gives 3; 10 gives 1; 9 gives 0; `decayPerDay` 0 gives `int.MaxValue`.
      - `GrudgeCapFor`: 20 for an elder, 4 otherwise.
    - `GrudgePreview.Rows` with `SocialTuning.Default`:
      - at vengefulness 0.6: neighbour 0.4/24/Cold, comrade 0.5/30/Cold, kin-group relative 0.7/42/Hostile, elder 0.9/54/Rally, household member 1.0/60/Rally, parent, child, sibling and spouse 1.0/80/Hunt.
      - at 0.5: 20 Cold, 25 Cold, 35 Cold, 45 Hostile, 50 Rally, 70 Hunt.
      - no row has `OnThreshold` at 0.6 or 0.5.
      - setting `hostileAt` to 42 flags the kin-group row at 0.6; setting `grudgeClosenessThreshold` to 0.45 marks the neighbour row `BelowCloseness`.
    - `KinTerms`: Child+Male is son, Child+Female is daughter, Parent+Female is mother, Sibling+Male is brother, Spouse+Female is wife, Friend is null.
    - `AttitudeRules.Resolve`:
      - faction Hostile always wins without close kin, even at opinion +100.
      - a grudge at 36 makes Neutral Hostile (Grudge); 35.9 does not.
      - a grudge at 15 leaves Neutral as Neutral (Faction) with `RefusesDealings` true; 14.9 gives `RefusesDealings` false.
      - opinion -60 makes Allied (same faction) Hostile (Opinion), refusing dealings.
      - opinion +60 makes Neutral Allied (Opinion); +59 stays Neutral.
      - opinion 0 and no grudge returns the faction relation with reason Faction and no refusal.
      - close kin: faction Hostile, opinion 0, no grudge gives Neutral (Kin); with a grudge of 36 Hostile (Grudge); with a grudge of 20 Neutral (Kin) refusing dealings; with opinion -60 Hostile (Opinion); with opinion +60 Allied (Opinion).
      - close kin with faction Neutral or Allied returns the faction relation with reason Faction (the kin rule only removes faction hostility).
    - `DefaultBondRules.Between`:
      - same household: Family, +60.
      - same kin group only: Family, +30.
      - same village: Neighbour, +20. Same camp: Comrade, +40.
      - member to elder: Neighbour | Elder, +40. Elder to member: Neighbour, +20 (one way).
      - household + kin group + village: Family | Neighbour, +60 (union and max).
      - nothing shared, or only the tribe: false.
      - null and empty ids never match (two people with no household get nothing).
      - `from == to`: false.
    - `NewsRules.Delay`: same household gives 3; same community gives 3; same kin group in another community gives 24; same kin group and same community gives 3; nothing shared gives false; empty ids give false; the victim gives false.
    - `TribeOpinionTable`: directional; missing gives 0; same tribe gives 0; empty id gives 0.
  - (4a) `SocialSimulationTests`, using a fresh `SocialSimulation(SocialTuning.Default, table, playerId)` per test (table from the five test faction ids) with helper people:
    - Register and derived bonds:
      - same household gives an effective Family +60 both ways, with **no stored entry** (`Relations.Entries` empty).
      - same camp gives Comrade +40; same village gives Neighbour +20.
      - same kin group in different communities gives Family +30.
      - 50 villagers registered in one village: every pair has Neighbour +20 and `Relations.Entries` is still empty.
      - a duplicate id returns false.
      - an authored bond (Friend +50) between housemates gives flags Family | Neighbour | Friend and opinion 50 (base replaced, flags added).
      - retuning: a Sim built with `neighbourBondOpinion` 30 gives neighbours +30 with no other change.
      - earned opinion is a delta: a neighbour attacked once is at -5 (20 - 25); after `MoveTo` away from the village it is -25 (the Neighbour base is gone, the delta stays) and has no Neighbour flag.
    - Attribution:
      - a direct killer is recorded with its faction.
      - a null source within 0.25 h credits the last damager.
      - at 0.26 h the killer is unknown.
      - an animal source gives a null `KillerId` and the wildlife id.
    - Grudge creation:
      - a witnessing household member at vengefulness 0.6 gets 60, tier Rally.
      - a witnessing neighbour at 0.6 gets 24, tier Cold.
      - a stranger gets none.
      - the killer never holds one against itself.
      - the victim never holds one.
      - none against an animal.
      - none when `KillerId` is null.
    - Knowledge:
      - a news recipient learns after exactly `newsDelayHours` (not at 2.9 h, yes at 3.0 h).
      - an unwitnessed death gives news without the killer and no grudge.
      - looting the corpse of an unwitnessed death reveals the death but not the killer, and makes no grudge.
      - a killer's faction-Allied comrade does not make `KillerWitnessed` true.
      - learning the same death twice creates the grudge only once.
      - a paid-off grudge does not come back when later news arrives.
      - a kin-group member in another community learns at 24 h (not at 23.9 h) and gets a Hostile grudge (42 at vengefulness 0.6).
      - a kin-group member in the squad (no community) also learns at 24 h.
      - a person sharing only the tribe never hears.
    - Resolution:
      - the target dies, so every grudge against it is removed.
      - the holder dies, so its grudges are removed.
    - Inheritance:
      - a holder with an 80 grudge dies; their child learns of the death through news and holds 60 against the same target, same generation, `Inherited` true.
      - a 59 grudge (below `inheritAt`) is not inherited.
      - the holder's friend, spouse and sibling inherit nothing.
      - the child inherits even when the holder's own death was unwitnessed (killer unknown).
      - the child inherits nothing until they learn of the death, and only once.
      - a target who died before the child learns is skipped; a child who is the target is skipped.
      - inheriting merges with the child's own grudge against the same target (max + `stackBonus`).
      - hand-over twice: 80 → 60 → 45, and the grandchild of a 60 gets nothing more (45 < 60 is not passed on).
      - `GrudgeCause` serialized value: `Killing` is 0.
    - Mutual kill: A and B record each other as killer in the same `now`, in both orders. Afterwards no grudges remain.
    - Feud chain:
      1. Gen 0 murder.
      2. A holder kills the killer: gen 1, and the killer's friends get grudges ×0.6.
      3. One of them kills the avenger: gen 2.
      4. A friend kills that one: gen 3, so no new grudges.
      - The total grudge count never exceeds `maxGrudgesPerDeath` per death.
    - Caps:
      - a fourth target replaces the weakest when stronger, and is dropped when weaker.
      - per-death cap: 6 household witnesses give 4 grudges, closest first.
    - Merge: a second killing by the same target stacks to `max + 10`.
    - Decay:
      - `Advance` across 1 day lowers by 5.
      - a single `Advance` that jumps 10 days lowers by 50 and removes forgotten ones.
      - news due during a jump starts decaying from its due day.
    - Squad scope:
      - a grudge of 90 against a squad member makes `AttitudeOf(holder, otherSquadMember)` Hostile (Grudge); 80 does not.
      - after the target dies, the scope ends.
    - Squad holder: `AttitudeOf(squadHolder, target)` returns the faction relation with no refusal even with a grudge of 100.
    - Close kin and tribe in `AttitudeOf`:
      - a mother in a Townsfolk village and her son in a Bandits camp: `AttitudeOf(mother, son, Hostile)` is Neutral (Kin), both ways.
      - the same pair where the son killed the mother's husband (a grudge of 80): Hostile (Grudge). After the grudge is paid: Neutral (Kin) again.
      - strangers of tribes at -10: an effective opinion of -50 gives Hostile (Opinion); -49 does not. With any effective bond flag between them (for example the same village) the tribe term is ignored. Without tribes the term is 0.
    - `EffectiveFaction`: uses `FactionRules.Resolve`; a squad member is Player even with a community.
    - Opinion:
      - `RecordAttack` lowers by 25 each time.
      - fought-together gives +10 both ways to two co-damagers, and nothing to a Hostile pair.
    - `JoinFeud` gives the caller's generation and at least `hostileAt`.
    - Blood price:
      - `QuoteBloodPrice` picks the payee's strongest grudge against a squad member; a payee with no such grudge gives null.
      - household settlement: payee (Rally 60) and a housemate (Hunt 80) with grudges against the same target for the same victim: the quote settles both and its intensity is 80; `PayBloodPrice` removes both and raises both opinions to -20 (but does not lower a higher one).
      - a housemate's grudge for a different victim and a non-household villager's grudge for the same victim are not settled.
      - a payee with no household settles only their own grudge.
    - `HoldsGrudgeAgainstSquad` true for a Cold grudge against a squad member, false for one against a non-member.
    - `GrudgeCreated` fires once per new grudge and not on a merge.
  - (4a) `CommunityTests`:
    - `CommunityRegistry`: `Add` rejects a duplicate and an empty id; `TryGet`; `SetFaction` false for an unknown id; `ClearElder` clears only communities with that elder; `(int)CommunityKind.Village == 0`, `Camp == 1`.
    - `EffectiveFaction`: a member gets the community faction even when its own is different (source Community); a person with no community gets its own; a person whose community id is not registered gets its own, and the community faction once it registers.
    - `SetCommunityFaction`: every alive member's effective faction changes, dead members are not listed, non-members are unchanged, the same faction gives an empty list, an unknown id returns false.
    - `RegisterCommunity` after its members: derived bonds and the elder bond apply at once, with no stored entries.
    - `SetElder`: members gain `Elder` and at least +40 toward the new elder and lose it toward the old one; a member with an authored base of +70 toward the new elder keeps +70; an earned delta toward the old elder stays; null clears.
    - `MoveTo`: leaving gives the own faction back, keeps kin, kin-group and household bonds, and drops Neighbour; joining another community gives its derived bonds; an elder who moves away leaves `ElderId` null.
    - Elder death: an elder with 6 villagers who did not witness; after 3 h every villager holds a Rally grudge (54) against the killer (6 grudges, more than `maxGrudgesPerDeath`), `VictimWasElder` is true, `ElderOfCommunityId` is the village and `ElderId` is null. An ordinary villager's death with the same setup gives at most 4 grudges.
    - Elder despawned (`MarkGone`): `ElderId` null, no grudges.
    - A person with no tribe, community, kin group or household registers, gets no derived bonds and resolves attitudes from faction.
  - (4a) `PersonIdWiringTests`: no duplicate `personId` in `test1.unity`; NPC prefab assets have an empty `personId`.
  - (4a, extended in 4b and 4d) `SocialSetupValidatorTests` (temporary additive scene built in code):
    - (4a) two characters with the same `personId`: one error; an empty id: a warning; two `SocialWorld`s: an error.
    - (4b) `PersonTies` with an authored bond to itself, a bond with two kin flags, a community with no elder (warning), a community with no faction (error), two communities with the same id (error), two `Household` assets with the same id (error): each reported once with its object.
    - (4d) a `MiscItem` with `valuable` on and worth 0: an error.
    - `test1.unity` opened additively: no errors.
  - (4c) `HuntRulesTests`:
    - an equal match hunts.
    - weaker with enough friends gathers the minimum number, strongest first.
    - weaker with friends who are too weak still hunts at a party ratio of 0.6 or more, and waits below it.
    - `maxHuntParty` is respected.
    - `HuntExpired` at exactly 6 h.
  - (4d) `BloodPriceRulesTests`:
    - `RequiredWorth`: 24 gives 24, 59.2 gives 60, 5 gives 10 (minimum), `worthPerIntensity` 0.5 at 60 gives 30, `worthPerIntensity` 0 gives `minWorth`.
    - `TotalWorth`: sums `worth × count`; zero-worth and zero-count stacks are ignored.
    - `TrySelect`, with stacks given as (key, worth, count):
      - required 30 from [(0, 10, 3), (1, 40, 1)]: takes key 1 ×1 only (highest first, overpay allowed).
      - required 60 from [(0, 10, 3), (1, 30, 1)]: takes key 1 ×1 and key 0 ×3.
      - required 80 from [(0, 40, 2)]: takes key 0 ×2.
      - required 50 from [(0, 40, 1), (1, 10, 3)]: takes key 0 ×1 and key 1 ×1.
      - required 100 from [(0, 30, 1), (1, 10, 3)]: false, `into` empty.
      - equal worths keep input order: required 10 from [(0, 10, 1), (1, 10, 1)] takes key 0.
      - required 0 returns true with nothing taken; an empty list with required 10 returns false.
      - `into` is cleared on entry.
    - `Check` order: dead before in combat, in combat before too far, too far before not enough; `carriedWorth == requiredWorth` passes.
    - `Reason` is non-empty for every block, and `NotEnoughValuables` contains `{n}`.
    - `BarkFormat.Fill` replaces all three tokens, more than once, and handles null names.
  - (4e) `FeudReportTests`:
    - an unrevealed grudge against a squad member is not in `Against`; after `Reveal` it is. `Reveal` returns true once, then false, and raises `GrudgeRevealed` once.
    - a merge of a revealed and an unrevealed grudge is revealed; `JoinFeud` copies the caller's flag.
    - two household members with grudges for the same victim and target share a `GroupKey`, and both rows carry the band of the stronger grudge.
    - sorting: Hunt before Rally before Cold; equal tiers by intensity.
    - `SquadWide` at 85 and not at 84.9; `KinTerm` is "son" for a mother's grudge over a male child and null for a neighbour.
    - a dead or Gone holder is not listed; a grudge against a non-squad person is not listed.
    - `Held` lists a squad member's grudge whether revealed or not.
    - `Killings`: a squad kill within 10 days is listed with its `Witnessed` flag; one 11 days old is not; an animal's or a villager's kill is not.
    - `Build` on a reused report clears the old rows.
    - `KinReport`: lists Parent, Child, Sibling, Spouse, Friend and Lover rows with alive or dead; leaves out derived neighbours and kin-group relatives.
  - (4e) `BloodPriceRulesTests`: `Band` edges at 29/30, 49/50 and 74/75.
  - (4e) `SocialSimulationTests`: `GrudgeRemoved` fires with Paid, Forgotten, TargetDied and HolderDied; `GrudgeTierChanged` fires on a merge that raises the tier and on decay that lowers it, and not when the tier stays.
- **Automated: PlayMode** (`Assets/Tests/PlayMode`, assembly `Kanshilike.Tests.PlayMode`; code-built characters, no NavMesh; `TestFactions.Install()` and `Social.Reset(SocialTuning.Default)` in SetUp and TearDown)
  - (4a) `SocialRecordingTests` (TimeManager plus SocialWorld; time is moved by setting `TimeManager.currentTime`):
    - `NoSocialWorld_NothingRecorded`: one character kills another; `Social.Sim.Deaths.All` is empty, `Attitude.Provider` and `CharacterFaction.CommunitySource` are null, and `FriendlyAttack_DefenderStaysIdle` still holds.
    - `Kill_RecordsVictimKillerFactionAndPosition`.
    - `NullSourceDeathSoonAfterAttack_CreditsAttacker`: B hits A, then `A.Stats.ReceiveDamage(1000, null)` in the same game minute; `KillerId == B.PersonId`.
    - `NullSourceDeathAfterWindow_KillerUnknown`: advance `currentTime` by 0.5 h first.
    - `WolfKill_NoKillerId_WildlifeFaction`: an AnimalAI is not needed; a `Character` with faction Wildlife has an id, so build a minimal `CombatActor` stub subclass in the test assembly with the wildlife faction id and use it as the source.
    - `PersonId_AssignedAtRuntime_AndDuplicateFixed`: two characters with the same id set by reflection before enabling. The second gets a new id, and a warning is logged (`LogAssert.Expect`).
    - `AttackLowersOpinion_BeforeDefending`: the NPC has +70 toward the attacker (authored base). After the first blow it is 45 (Neutral), and it fights back on that blow.
    - `FriendAtPlus100_FirstBlowNotAnswered_SecondIs`.
    - `SocialWorldDisabled_ProviderAndSourceRemoved`: disabling the `SocialWorld` restores plain faction behaviour.
  - (4a) `SocialDebugReportTests`: with a SocialWorld, an NPC who witnessed a killing has sections for Memberships, Relationships (the victim listed with closeness), Grudges (target, tier, days left) and Deaths known; the Attitude line toward the killer names the grudge; without a SocialWorld the report says "Social system inactive".
  - (4b) `KnowledgeSpreadTests`:
    - `Witness_LearnsInstantly_AndHoldsGrudge`: a household member within 20 m.
    - `SleepingWitness_DoesNotLearn`.
    - `Household_LearnsAfterNewsDelay`: away from the scene (100 m), no grudge at +2.9 h, a grudge at +3 h.
    - `UnwitnessedKill_HouseholdLearnsDeathButNoGrudge`.
    - `HolderKilled_ChildInheritsHardGrudge`: Veikko holds an 80 grudge against a stranger and has a son in his household; Veikko is killed with nobody watching; after +3 h the son holds 60 against the stranger and no grudge against Veikko's unknown killer.
    - `LootingCorpse_LearnsDeath`.
    - `AuthoredBond_OverridesDerivedOpinion_AddsFlags`.
    - `TreatWounds_RaisesPatientOpinionOfHealer`: run a `TreatWoundsOrder` to completion with `HealingConfig` set as `CharacterBehaviourTests` does.
    - `SquadHolder_GetsGrudge_NeverActs`: Keppana-like squad member with a Friend bond to the victim; a grudge exists; their order stays null over 1 s.
  - (4b) `CommunityTests` (PlayMode):
    - `CommunityMember_FactionComesFromCommunity`: an NPC with own faction Townsfolk in a community set to Traders has `Faction == traders` (source Community) after one frame; `SetOwnFaction(Bandits)` changes its own faction but it is still Traders.
    - `VillageFactionChange_PropagatesToMembers`: `Community.SetFaction(Bandits)` changes every member's `Character.Faction` in the same frame; a non-member is unchanged; a Player character now counts as Hostile to them (`Attitude.Of`).
    - `FactionChangeToAllied_EndsFightWithAlly`: a member fights a Traders NPC; the village is set to Traders; within one think tick neither has an `AttackOrder` and the member's order is a schedule order.
    - `FactionChangeMidFight_HostileFightContinues`: a member fights a squad member; the village is set to Bandits; the member's `AttackOrder` continues.
    - `ElderKilled_WholeVillageHoldsGrudge`: an elder and five villagers 100 m away; the elder is killed by a witnessed killer; after +3 h all five hold a Rally grudge against the killer, and the community's `ElderId` is null.
    - `Recruit_LeavesCommunity_FactionPlayer_KinBondRemains`: recruit a villager whose mother is in the village; the recruit's `CommunityId` is null, its faction is Player after a later `Community.SetFaction(Bandits)`, `IsCloseKin(mother, recruit)` is still true, and the Neighbour flag toward another villager is gone.
    - `KinInEnemyVillage_DoesNotAttack`: Aino's brother in a second community set to Bandits, both within sight with `RevengeRole` (4c, add then); neither gets an `AttackOrder` over 1 s.
    - `TribeLeanings_GiveXpOnce`: a character with a `SkillSet` (one `SkillDefinition` made in code) and a tribe with a leaning; after `Start` the XP or level has increased by the leaning; disabling and re-enabling the object does not add it again.
    - `EmptyName_TakesTribeName`: an empty `characterName` and a tribe with one female name gives that name.
    - `TribeBarks_MixedIntoTalk`: an NPC whose archetype has no barks and whose tribe has one line shows that line on Talk.
  - (4c) `RevengeRoleTests` (archetype `sight.requireLineOfSight = false`, think interval 0.05):
    - `ColdGrudge_DoesNotAttack`: a grudge of 24; the target in sight for 1 s gets no `AttackOrder`.
    - `HostileGrudge_AttacksTargetOnSight`.
    - `HostileGrudge_IgnoresTargetOutOfRange`.
    - `RallyGrudge_RalliesCloseFriend`: a friend within 15 m with closeness 0.5 gets a grudge of at least `hostileAt` with the same generation and attacks.
    - `HuntGrudge_TargetUnseen_HuntsLastKnown`: the order is a `WanderOrder`, and its center is the death position.
    - `HuntGrudge_Weaker_GathersFriends`: a friend gets a `FollowOrder` on the hunter.
    - `HuntGrudge_TooWeakNoFriends_Waits`: no order from the role.
    - `Hunt_GivesUpAfterHours`: advance 6 h; the schedule order returns.
    - `LowHealth_FleeOutranksRevenge`.
    - `SelfDefence_NotOverriddenByRevenge`: attacked by a third party, the NPC keeps that AttackOrder.
    - `SquadScope_AttacksOtherSquadMember`: a grudge of 90 against squad member A; squad member B in sight is attacked.
    - `TargetDies_RoleReleasesOrder`.
    - `Asleep_NoReaction`.
  - (4c) `NpcInteractionTests` additions:
    - `Options_ColdHolder_NormalOptions_TalkNoHold_RecruitUnwilling`: with a grudge of 24 against the interactor, Talk, Recruit and Attack are present; Talk does not hold; `TryRecruit` fails with "Won't share a fire with your people".
    - `Options_GrudgeHostileHolder_TalkRecruitAttack_RecruitRefused`: with a grudge of 40, all three options are present and `TryRecruit` fails with Hostile.
    - `Recruit_HoldsGrudgeAgainstOtherSquadMember_Unwilling`.
  - (4d) `BloodPriceTests` (items made with `ScriptableObject.CreateInstance<MiscItem>()`, `worth` and `valuable` set in code):
    - `Option_ShownForAnyTier_OnlyWithGrudgeAgainstSquad`: a Cold grudge (20) shows it; no grudge does not.
    - `Pay_FullPrice_RemovesGrudgeAndValuables`: a grudge of 60; the payer carries a 30-worth item and three 10-worth items; after paying the grudge is gone and all four items are gone.
    - `Pay_PicksHighestWorthFirst`: a grudge of 24; the payer carries a 40-worth item and three 10-worth items; only the 40-worth item is taken.
    - `Pay_SettlesHousehold_PriceIsStrongest`: payee at 42 and housemate at 80 for the same victim; the refusal with 60 worth carried says 80; with 80 both grudges are gone.
    - `Pay_NotEnoughValuables_KeepsItemsAndGrudge`: the refusal text contains the required worth.
    - `Pay_IgnoresNonValuables`: a payer with plenty of food (worth set but `valuable` off) is refused.
    - `Pay_WhileNpcFighting_Refused`.
    - `Talk_ByTarget_ShowsGrudgeBarkNoHold`: assert the order is not an IdleAtOrder.
    - `Talk_ByOtherMember_HoldsAndBarks`.
    - `Parley_HolderDoesNotAttackEnvoy`: the target walks toward a Hostile holder with an `InteractOrder(bloodprice)`; the holder gets no Role `AttackOrder` on it while the order runs; a second holder still attacks it.
    - `Parley_EndsWithOrder`: the envoy's order is replaced by a `MoveOrder`; within two think ticks the holder attacks.
    - `Parley_RoleAttackInProgress_Stops`: the holder is attacking the target by role; the target starts the offer order; the holder's `AttackOrder` ends at the next think.
  - (4e) `FeudUiTests` (a `SocialWorld` with `showFeudUi` on, built in code; messages captured from `GameMessages.Posted`):
    - `GrudgeBark_RevealsAndPostsMessage`: Talk to a Cold holder; one Warning message naming holder, target and victim; talking again posts nothing new.
    - `HiddenGrudge_NoMarkerNoRow`: a grudge that was never shown has no marker and no panel row.
    - `RevealedGrudge_HasMarker_ColouredByTier`, and the marker is green while `Parley.IsEnvoyTo` is true.
    - `SquadKill_PostsKillingMessage_WithWitnessNote`.
    - `Panel_ToggleKey_OpensAndCloses`, and it does not open while an inventory window is open.
    - `Offer_OpensDialog_NothingTakenYet`; `Dialog_Pay_TakesItems_RemovesGrudges_PostsPaidOnce` (a household of two holders gives one Paid message).
    - `Dialog_WalkAway_TakesNothing_EndsParley`.
    - `Dialog_HolderHoldsFire_WhileOpen`: the envoy is the grudge target; the holder's order is not an `AttackOrder` on the envoy while the dialog is open.
    - `Dialog_ClosesOnNewOrder`, `Dialog_ClosesOnTimeout` (set `offerTimeoutSeconds` to 0.2).
    - `Dialog_Pay_AfterValuablesRemoved_Refuses`: remove the items while it is open; Pay shows the refusal and takes nothing.
    - `NoFeudUi_PaysImmediately`: `showFeudUi` off; the 4d flow is unchanged.
- **Manual play-test** in `Assets/Scenes/test1.unity` (keep the Console and the NPC Debugger open; `SocialWorld` logs each death and grudge):
  - [ ] (4a) Jonttuli and Keppana keep the same `personId` after leaving Play mode and reopening the scene.
  - [ ] (4a) A wolf kills a townsperson: the log shows killer faction Wildlife, no killer id and no grudges.
  - [ ] (4a) `SocialConfig` inspector: the preview table shows neighbour Cold, comrade Cold, kin-group relative Hostile, elder Rally, household member Rally, kin Hunt; drag `hostileAt` to 42 and the kin-group row is flagged at once; undo.
  - [ ] (4a) Kanshilike > Validate NPC Setup: Ctrl+D a townsperson and validate: a duplicate person id error until the copy is selected (OnValidate) or deleted.
  - [ ] (4b) Jonttuli kills Aino in front of Veikko (her son, same household): Veikko immediately holds a Hunt grudge (80) against Jonttuli (log and debugger). Heikki (the elder, 100 m away) gets a Cold grudge (24) and Lempi (kin group) a Hostile one (42) about 3 in-game hours later.
  - [ ] (4b) Kill a townsperson far from everyone (no witness): the household learns of the death after 3 h, but nobody holds a grudge.
  - [ ] (4b) Keppana (Friend of Aino) is near when Aino dies: "Keppana swears vengeance on …" shows, and Keppana does nothing on her own.
  - [ ] (4b) Wounded townspeople can't be treated by right-click (only allies can). Recruit a wounded townsperson, bind its wounds, and check the log for the opinion gain.
  - [ ] (4b) The NPC Debugger shows Kuusikylä townsfolk as "Townsfolk (community Kuusikylä)", their memberships, and derived relationships (Family/Neighbour) with closeness. The townsfolk who were given tribe leanings show the extra XP in the skills panel; Jonttuli and Keppana are unchanged.
  - [ ] (4b) During play, set Kuusikylä's `faction` to Bandits in the inspector: every villager's faction changes at once (debugger), and they now attack the squad on sight only if a role makes them (4c) or fight back when attacked. Set it back to Townsfolk while a villager is fighting a squad member: the fight continues (Neutral); set it to Player: the villager stops attacking.
  - [ ] (4b) Kill Aino with a witness from Kuusikylä present: Ilmari in Koivula (her brother, same kin group) does not react at 3 h, and gets a Hunt grudge (sibling) at about 24 in-game hours (log).
  - [ ] (4b) Set Koivula to Bandits: Ilmari and Aino do not fight each other when they meet (Attitude Kin). Kill Ilmari's sister with Jonttuli while Ilmari watches: he turns on Jonttuli, not on the rest of Kuusikylä.
  - [ ] (4b) Kill the elder Heikki with Jonttuli in front of one villager: about 3 h later every Kuusikylä villager holds a Rally grudge against Jonttuli (log), and Kuusikylä's elder is now none (debugger).
  - [ ] (4b) Recruit a villager: the log shows it left Kuusikylä; the debugger no longer lists Neighbour bonds for it; changing Kuusikylä's faction afterwards no longer changes the recruit's faction.
  - [ ] (4c) Heikki (Cold) does not attack Jonttuli, but right-click with Jonttuli: Talk gives a grudge line with no pause, and Recruit says "Won't share a fire with your people".
  - [ ] (4c) Lempi (Hostile) attacks Jonttuli on sight, but ignores Keppana. Right-click with Jonttuli: Talk gives "You killed Aino!", and Recruit is refused.
  - [ ] (4c) Veikko (Hunt, which includes Rally) shouts a call-out, and a nearby close villager joins the attack.
  - [ ] (4c) After the elder's death, several villagers call out and rally against Jonttuli on sight.
  - [ ] (4c) With Jonttuli out of sight, Veikko walks to the death spot and searches, with a friend following when he is weaker. He gives up after 6 in-game hours.
  - [ ] (4c) Hurt the hunter below 35%: it flees instead of pressing on.
  - [ ] (4c) Kill the hunter with Jonttuli: its friends get weaker grudges (generation 1). Keep killing down the chain: after generation 2 no new grudges appear.
  - [ ] (4c) Raise one grudge above 85 (two killings by Jonttuli of one family): the holder now also attacks Keppana.
  - [ ] (4d) Parley: select only Jonttuli (Veikko's target) and choose "Offer blood price" on Veikko: Veikko does not attack Jonttuli while he walks up; Lempi still does. Give Jonttuli a move order mid-way: Veikko attacks again.
  - [ ] (4d) Send Keppana to Veikko: "Offer blood price" is offered. The price is 80 (the strongest grudge in Aino's household). With her two silver arm-rings and three wolf pelts she pays; the two arm-rings are taken (highest worth) and the pelts stay; the grudge is cleared, and Veikko no longer attacks Jonttuli on sight.
  - [ ] (4d) Offer blood price to Lempi (Hostile 42) with only the three pelts left: "Wants valuables worth 42" shows and nothing is taken. Give Keppana a sword and it succeeds (the sword and two pelts are taken).
  - [ ] (4d) Heikki (Cold): "Offer blood price" is offered even though he would not attack; paying 24 clears it and Talk returns to normal barks.
  - [ ] (4d) No price, coin or worth number appears in the inventory UI or tooltips.
  - [ ] (4d) Raise `TimeManager.currentTime` across midnight several times: Cold grudges fade (log; the debugger's days-left counts down) and the holders become talkable again.

  - [ ] (4e) Kill Veikko with Jonttuli in front of a villager: the feed shows "Jonttuli has killed Veikko of Kuusikylä. There were witnesses." No marker appears yet. Press F: *Blood spilled* lists it and *Against us* is empty.
  - [ ] (4e) Walk Keppana to Aino and Talk: the grudge bark shows, a Warning message appears, a red marker appears over Aino, and the panel now shows "Aino's household, Kuusikylä: Hunt" with "for her son Veikko", the days until it fades, "Asks a great blood price" and whether Keppana carries enough.
  - [ ] (4e) Heikki (Cold) gets a grey marker after he is talked to; Lempi (Hostile) an orange one after she first attacks.
  - [ ] (4e) Choose "Offer blood price" on Aino with Jonttuli: her marker turns green as he walks up, the dialog lists what will be handed over, and she stands still. Wait 20 seconds: it closes and she attacks. Do it again and press Pay: the items are gone, one "blood price is paid" message shows, and the markers on Aino's household disappear.
  - [ ] (4e) The panel and dialog never show a number for worth, and clicking on them does not move or deselect the squad.
  - [ ] (4e) Select a recruit with kin and open the Kin tab: tribe, village, kin group, household and each kin with the right word (son, mother, brother) and "(dead)" where it applies.
  - [ ] (4e) Turn `showFeudUi` off on `SocialWorld`: no panel, markers or dialog; blood price is paid at once; other `GameMessages` still show.

## Out of scope
- Bandits: BanditRole, groups, camps, raids, perception extraction (`FindNearestHostile` and moving AnimalAI onto `ActorPerception`; this plan only adds `CanSee`). The `Camp` community kind and its Comrade layer exist for them, but no camp is placed.
- Traders: trade UI and barter (goods for goods; there is no money in this setting). `Item.worth` is the hook for it, and Cold refusal of trade is the attitude hook; showing worth anywhere in the UI is part of that later work.
- Player reputation per faction, and faction-wide reactions such as the whole village turning hostile after the player attacks a townsperson or guards responding. Grudges are personal, per holder; an elder's death only gives every villager a personal grudge.
- Dialogue trees, quests, voice, and NPC names generated at runtime beyond picking from the tribe's name list when `characterName` is empty.
- Household-owned beds or anchors (looking up a bed by household).
- Grudges against animals, or species-wide revenge such as hunting the wolf pack.
- Gossip beyond household, community and kin group, line of sight for witnesses, and witnesses' opinion of strangers' killings (reputation).
- Earning bonds in play (becoming Friends or Lovers), opinion decaying over time, authored Rival behaviour beyond its closeness weight, and removing a derived flag by authoring.
- Squad members acting on grudges, and a mood system. Only the record and the floating text hook exist.
- Feud UI beyond 4e: a map or list of villages, tribes and factions and their relations; a family tree view; opinions of people who hold no grudge; clicking a row to move the camera to the holder; icons and portraits (4e is text and coloured markers only); a message history window; choosing which valuables to hand over in the confirm dialog.
- Partial blood price, haggling, paying on behalf of a third party who is not in the squad, choosing which valuables to hand over, minimising overpay, giving change, and a parley grace period after a refused offer.
- A weapon drawn/sheathed state (the parley rule notes where it would plug in).
- Multi-day time skips in `TimeManager`. The social rules already handle them.
- Saving and loading social data. It is id-keyed and plain so a later save can write it.
- Politics: the elder deciding the village faction on their own, persuading an elder, elder succession (a dead elder leaves `ElderId` null), a kin-group head, factions as alliances of communities, and a chieftain emerging as the strongest elder. Only the `Community.SetFaction` API exists.
- Tribe politics or tribe-wide reactions; marriage and births changing kin groups or households at runtime; people moving between villages on their own.
- **Multi-scene worlds:** `Social`, `NpcAnchor.All` and the installed `Attitude.Provider`/`CharacterFaction.CommunitySource` are static and global, which is fine for one loaded scene. Additive or streamed multi-scene worlds will need them scoped per world or scene.

## Open questions
1. **Roadmap.** `docs/PLAN.md` and `docs/architecture.md` are missing. Are feuds and kinship (tribes, communities, kin groups, elders, grudges, blood price) in the vertical slice demo, or after it? Is there an architecture order this should wait for?
2. **Parley edge.** The parley ends the moment the offer order ends, with no grace period, so an envoy who is also a target and whose offer is refused is attacked at once. Keep this (send the right goods, or send someone who is not a target), or add a short grace period?
3. ~~**Inherited grudges.**~~ **Decided:** children take over a dead parent's `Killing` grudges of 60 or more at × 0.75, same generation, when they learn of the death. Lesser grudges (and later causes such as theft) are not inherited, and friends inherit nothing. See Grudges.
4. ~~**Unwitnessed killings.**~~ **Decided:** a killing nobody outside the killer's side saw causes no grudge. News and corpse looting reveal the death but not the killer. No setting.
5. **Friends do not answer the first blow.** At opinion +60 or more, an NPC treats the first attack as an accident and fights back from the next one, because each attack costs -25. Keep this, or have high opinion affect only talk and recruiting, and always fight back?
6. **Tribe skill leaning sizes.** Leanings are XP added once at spawn. How large should they be: a nudge (about one level in one or two skills) or a clear identity (two or three levels)? And which skills fit Hämäläiset and Karjalaiset in the current `SkillType` list?
7. **Should tribe affect attitude at all?** The plan adds a small cultural bias between strangers of different tribes (-10 between Hämäläiset and Karjalaiset), applied only to people with no bond. Keep it, set it to 0 for the demo, or drop the term?
8. **Elder death scale.** Killing an elder gives every villager a Rally grudge (54 at default vengefulness; up to 20 grudges, `elderDeathGrudgeCap`), paid once per household. Is a whole-village feud the intended cost, or should only the elder's household and kin group feud while the rest of the village only lowers its opinion?
9. **Hidden grudges.** The feud UI shows a grudge only after its holder has shown it (bark, call-out, attack, or being offered a blood price), so an avenger the player never met can arrive unannounced. Keep this, or show every grudge against the squad as soon as it exists?
10. **Blood price in words.** The panel and dialog describe the price as small, fair, heavy or great and say whether the selected member carries enough, with no number. The 4d refusal still says "Wants valuables worth {n}". Change the refusal to words too ("Wants a heavy blood price"), or show the number everywhere?
