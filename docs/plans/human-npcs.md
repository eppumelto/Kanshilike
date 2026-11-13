# Human NPCs (phases 1-3): faction relations, NPC brain and routines, talk and recruit

**Roadmap item:** not checked. `docs/PLAN.md` and `docs/architecture.md` do not exist in the repo (checked again 2026-11-12 for this revision). The scope and design below come from the design agreed with the user. Before starting, confirm that human NPCs are in the vertical slice demo scope (open question 2). Relationships, kinship and feuds were phase 4 of this plan and are now a separate plan, `docs/plans/feuds-and-kinship.md`, which builds on the seams listed at the end of the design.
**Goal:** Townsfolk live in `test1` on a daily schedule: they wander, work at a stall, sleep in a bed and run from danger. The player can talk to them, recruit one into the squad, or attack them. Factions are data: a faction is an asset with a default stance, relations between factions are listed in one asset, and adding bandits, traders or a new village is a matter of creating assets, not changing code.

## Design

### Codebase facts this plan relies on (checked against current code, 2026-11-12)
- The asmdef split already exists: `Assets/Scripts/Kanshilike.Game.asmdef` (game code, references `Kanshilike.Rules`, TextMeshPro and UGUI) and `Assets/Scripts/Rules/Kanshilike.Rules.asmdef` (`noEngineReferences: true`, no references). Both test asmdefs reference both, so EditMode tests can also test Game code. All pure logic in this plan goes into `Kanshilike.Rules`. There is **no editor code and no editor assembly** in the project yet (no `using UnityEditor`, no `MenuItem`). Menu items, the migration tool, the debug window and the validator need one small editor-only asmdef (step 5). That is not a project-wide split.
- `Faction` is a two-value enum (`Player, Wildlife`, no explicit numbers) in `Assets/Scripts/Combat/Faction.cs`, in the Game assembly, next to `FactionRelations.AreHostile(a, b) => a != b`.
- `CombatActor.Faction` is `public abstract Faction Faction { get; }` (CombatActor.cs:20). `Character` overrides it from `[SerializeField] private Faction faction = Faction.Player;` (Character.cs:30, 85) and `AnimalAI` from `[SerializeField] private Faction faction = Faction.Wildlife;` (AnimalAI.cs:23, 64).
- **Serialized faction data today** (enum stored as an int under the key `faction`):
  - `Assets/Scenes/test1.unity`: Jonttuli `faction: 0` (line 4587), the wolf (`AnimalAI`) `faction: 1` (4983), Keppana `faction: 0` (5305).
  - `Assets/Prefabs/Animals/Wolf.prefab` and `Deer.prefab`: the `AnimalAI` block has **no** `faction` key (serialized before the field existed), so they use the code default Wildlife.
  - `Assets/Scenes/Iides_map.unity`: one stale `Character` (Keppana) with **no** `faction` key, so it uses the code default Player. `lansi.unity` has no characters or animals.
  - No other prefab or asset contains a faction.
- `FactionRelations.AreHostile` has four callers: `Character.DefendAgainst` (Character.cs:178), `Character.TryTreatWounds` (:474), `SelectionManager.TryOrderTreatment` (:343), and `AnimalPerceptionUtility.DetectNearestTarget` (:35, the one AnimalAI uses; its parameter is `Faction selfFaction`).
- The only test that uses the enum is `Assets/Tests/PlayMode/CharacterBehaviourTests.cs`: `MakeCharacter(string, Vector3, Faction, bool)` sets the private field `"faction"` by reflection (line 327); 22 call sites pass `Faction.Player` or `Faction.Wildlife`.
- `PlayerControllable` keeps a static `All` list, fires a static `Joined` event from `OnEnable` (so also for recruits), and caches `Character` in `Awake`.
- `SelectionManager.HandleRightMouse` always picks `options[0]` ("For now, auto-pick the first action"). No in-game context menu exists. Talk, Recruit and Attack on one NPC need a way to choose, so phase 3 adds a minimal one (step 19). In `test1` today every interactable offers exactly one option, so the menu only appears when there are two or more and current behaviour does not change.
- `InteractOrder` reads `GetInteractionPoint()` once, then walks to that point. Against a wandering NPC the squad member would walk to where the NPC used to be. Step 18 makes it follow a moving target. Its `target` and `actionId` are private; step 18 exposes them read-only.
- **There is no money in this game.** It is set in the Iron Age, before coinage. Every cost is paid in goods. Recruiting costs an item and a count. Never add coins, prices, shops or a currency field. Recruit cost defaults to free (open question 1).
- The player's characters in `test1` (Jonttuli, Keppana) are scene objects, not prefabs. The only `CombatEntityInteractable` in the scene is on the wolf.
- `TimeManager.OnTimeChanged` fires every frame with a float hour that wraps at 24. `CharacterStats.Sleep` needs a `TimeManager` in the scene.
- `Assets/Resources/` exists (it holds an unrelated tool config).
- The working tree has uncommitted healing changes to `Character.cs`, `SelectionManager.cs`, `TimeManager.cs` and others. Build on top of them; do not revert them.

### Phase 1: faction relations as data

#### Types
- **Rules sees only ids.** `FactionId` is a small `readonly struct` wrapping a string (ordinal, case-sensitive equality and hash; `default` and the empty string are `None`). Rules never sees an asset.
- `FactionRelation { Hostile = 0, Neutral = 1, Allied = 2 }` (explicit numbers: it is serialized in assets).
- **`FactionRelationTable`** (Rules) is still an instance, so tests use their own copies with no static state, and it still has a runtime `Set(a, b, relation)`. It is keyed by `FactionId`:
  - `Define(id, defaultStance)`: the faction's stance toward every faction it has no explicit entry with.
  - `Set(a, b, relation)`: an explicit, symmetric pair entry. `Clear(a, b)` removes it.
  - `Get(a, b)`, in this order:
    1. either side is `None`: Neutral (an actor with no faction is unaffiliated, even toward another unaffiliated one);
    2. an explicit entry for the pair: that value (this also lets an explicit self-entry override Allied);
    3. `a == b`: **Allied** (a faction is always allied with itself);
    4. otherwise the more hostile of the two default stances (`min` by enum value). An undefined id has stance Neutral.
  - `Explain(a, b)` returns the relation plus its source (`FactionRelationSource { Unaffiliated = 0, Explicit = 1, Same = 2, DefaultStance = 3 }`) for the debug view.
- **`FactionDefinition`** (Game, ScriptableObject, `Kanshilike/Factions/Faction`): `id`, `displayName`, `defaultStance` (default Neutral). Every village, band or trading house can have its own asset. **Adding a faction is creating an asset**; no code changes.
- **`FactionRelationSet`** (Game, ScriptableObject, one instance at `Assets/Resources/Factions/FactionRelations.asset`): `player` (the faction squad members belong to), `legacyEnumOrder` (index = old enum number: `[Player, Wildlife]`), an optional `factions` list (catalog for lookups and the debug view), and `relations`, a list of `{ FactionDefinition a; FactionDefinition b; FactionRelation relation }`. `BuildTable()` defines the stance of every listed faction and of both sides of every entry, then applies the entries.
- **Shipped content** gives exactly the old intended defaults with no pair entries at all:

  | Asset | id | defaultStance |
  |---|---|---|
  | Player | `player` | Neutral |
  | Wildlife | `wildlife` | Hostile |
  | Townsfolk | `townsfolk` | Neutral |
  | Traders | `traders` | Neutral |
  | Bandits | `bandits` | Hostile |

  So Wildlife is Hostile to every other faction, Bandits are Hostile to every other faction, Player, Traders and Townsfolk are Neutral to each other, and each is Allied with itself. Wolf and deer stay Allied with each other (both Wildlife), as today.
- **Ids.** All definition assets in this plan and the feuds plan derive from a small `DefinitionAsset` base: `Id` is the serialized `id` when set, otherwise the asset name lower-cased with spaces as dashes. The fallback avoids the "New Faction Definition" id that `OnValidate` would freeze at creation time. Ids must not change after content ships (saves will key on them); the validator (step 16) reports duplicates.

#### `FactionRelations` (Game, static)
- `Table`: the shared `FactionRelationTable`, built lazily from the Resources asset on first use. `Asset` (the `FactionRelationSet`), `Player` (a `FactionDefinition`), `PlayerId`, `FromLegacy(int)`, `Find(FactionId)` (catalog plus every registered definition).
- `Register(FactionDefinition)` defines a definition's stance in the table if it is not defined yet and remembers it for `Find`. Actors call it when they resolve their faction, so **a new faction asset works even if nobody adds it to the catalog**: its own stance applies to everything with no explicit entry.
- `Get`, `AreHostile` (`== Hostile`), `AreAllied` (`== Allied`), `FightsBack` (`!= Allied`), all on `FactionId`.
- `Configure(FactionRelationTable table, FactionDefinition player, IEnumerable<FactionDefinition> catalog)` and `ResetToDefault()` for tests. `ResetToDefault` also runs from `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, so statics are fresh with domain reload off.
- **Missing asset:** logs one error and uses an empty table (everyone Allied with their own faction, Neutral otherwise) and no Player faction. The validator and `FactionContentTests` catch this before play.
- **Reputation hook (not implemented):** every relation query goes through `FactionRelations.Get`, and the table has `Set` for runtime changes. A later reputation system can change the Player row, or wrap `Get`, without touching any caller. No reputation values, events or UI are added now.

#### One function decides a character's faction
- `FactionRules.Resolve(bool inSquad, FactionId community, FactionId own, FactionId player)` (Rules, pure) returns `FactionResolution { FactionId Id; FactionSource Source }` with `FactionSource { Own = 0, Squad = 1, Community = 2 }`:
  1. in the squad and `player` is not None: `player` (Squad);
  2. `community` is not None: `community` (Community);
  3. otherwise `own` (Own).
- `Character.Faction` (the `CombatActor` override everything in combat reads) returns this resolution and nothing else decides it. There is **no write-back** into a serialized field, no re-sync and no archetype setting the faction:
  - `inSquad`: the character has an enabled `PlayerControllable`.
  - `community`: from `CharacterFaction.CommunitySource` (an `ICommunityFactionSource`), which is **null in phases 1-3**, so the term is always None. The feuds plan installs its source while its social system is active and removes it when inactive, so with the social system off a character uses its own faction.
  - `own`: the `ownFaction` asset, or the legacy enum value mapped through `FactionRelations.FromLegacy` when the asset is not set (migration below).
- **Cheap:** the result is cached per character. The cache is dropped when:
  - `SetOwnFaction` is called;
  - the character's `PlayerControllable` is enabled or disabled (it calls `Character.InvalidateFaction()`);
  - `CharacterFaction.InvalidateAll()` bumps a static version number (used when the community source is installed or removed, and by the feuds plan when a community or a membership changes). Each character compares its cached version on read, so invalidating everyone costs one integer increment and no list walk.
  - So the per-read cost is a version compare; `GetComponent` and the resolver run only after an invalidation.
- `Character.SetOwnFaction(FactionDefinition)` sets the own field, invalidates the cache and raises `CharacterFaction.OwnFactionChanged` (a static event; the feuds plan's registry listens to it).
- `CharacterFaction.Explain(Character)` returns the resolution plus a readable "why" for the debug view ("Player (squad member)", "Townsfolk (own faction)", "Player (own faction, legacy field)").
- **Faction change mid-fight (phases 1-3):** the only runtime changes are recruiting (refused while in combat) and explicit `SetOwnFaction` calls. Fights in progress are not cancelled; new fights follow the new relation. The feuds plan adds the community case and its rule.

#### What the relations mean
- **Hostile:** starts fights (animals, later bandits) and fights back.
- **Neutral:** never starts a fight but **fights back when attacked**.
- **Allied:** never fights back (friendly fire), and can have wounds bound by right-click.

#### Attitude seam
- `Attitude` (Game, static) is what callers use to ask how one actor regards another: `Explain(CombatActor self, CombatActor other)` returns `AttitudeResult { FactionRelation Relation; AttitudeReason Reason; bool RefusesDealings }`; also `Of` (the relation), `FightsBack` (`Of != Allied`), `RefusesDealings`, and `RefusesSquad(CombatActor self)` (won't deal with anyone in the player squad).
- In phases 1-3 it returns `FactionRelations.Get(self.Faction, other.Faction)` with `AttitudeReason.Faction` and `RefusesDealings = false`. `AttitudeResult` and `AttitudeReason { Faction = 0 }` live in Rules (`AttitudeResult.cs`); the feuds plan appends reasons.
- An optional `Attitude.Provider` (`IAttitudeProvider`, null in phases 1-3) may override the result for a pair. The feuds plan installs one; no caller changes.
- **Callers that change on purpose:**
  1. `Character.DefendAgainst` uses `Attitude.FightsBack(this, enemy)`. Otherwise a townsperson the player punches would just stand there. The existing PlayMode test `FriendlyAttack_DefenderStaysIdle` (same faction, so Allied) still passes.
  2. `SelectionManager.TryOrderTreatment` uses `!FactionRelations.AreAllied(...)`. Otherwise right-clicking a wounded townsperson would start wound treatment instead of opening Talk/Recruit/Attack.
  3. `NpcInteractable` and `Recruitable` (phase 3) use `Attitude.Explain`.
- **Callers that stay on faction relations:** `AnimalPerceptionUtility` (`AreHostile`, its parameter becomes `FactionId`), `Character.TryTreatWounds` (`AreHostile`), and `SelectionManager.TryOrderTreatment`, so treating wounds keeps the simple faction rule.

#### Migration of the serialized `faction` fields
- **Chosen approach: keep the old int readable as a hidden legacy field, resolve it at runtime, and provide a one-off editor migration that fills real asset references.**
  - `Character`: new `[SerializeField] FactionDefinition ownFaction;` and `[SerializeField, HideInInspector, FormerlySerializedAs("faction")] int legacyFaction = 0;`.
  - `AnimalAI`: new `[SerializeField] FactionDefinition factionDefinition;` and `[SerializeField, HideInInspector, FormerlySerializedAs("faction")] int legacyFaction = 1;`.
  - The new fields need new names: an object reference field named `faction` cannot read the stored int.
  - The legacy initializers equal the old enum defaults (Player for characters, Wildlife for animals), so objects with no `faction` key (Wolf and Deer prefabs, Iides_map's Keppana) resolve exactly as today.
  - When the asset field is null, the actor resolves `FactionRelations.FromLegacy(legacyFaction)`: index into `legacyEnumOrder` (0 Player, 1 Wildlife); out of range gives None with one warning.
  - Editor menu **Kanshilike > Factions > Migrate Legacy Faction Fields** (step 5) fills the asset fields: first every prefab (Wolf, Deer and any other with `Character` or `AnimalAI`), then every scene under `Assets/Scenes` (opened additively, saved, closed), skipping objects whose field is already set. A scene instance of a migrated prefab inherits the value, so it needs no override. It is idempotent and logs a count.
- **Why this approach:**
  - `FormerlySerializedAs` reads the old data with no hand-editing of YAML, which is fragile with scene and prefab files that have uncommitted changes.
  - The runtime fallback means nothing breaks if a scene or prefab is not migrated (Iides_map, a prefab added from another branch, a wolf spawned from `Wolf.prefab` at runtime).
  - The editor step makes the inspector show the real asset, so designers stop seeing a hidden number, and the validator can require explicit references in `test1`.
  - Removing the legacy fields is a later cleanup once every scene is migrated (out of scope).

### Phase 2: NpcBrain and routines
- **The brain only issues orders.** `NpcBrain` never touches the `NavMeshAgent`. It calls `Character.GiveOrder` with `CharacterOrder` subclasses. Fighting stays in the existing self-defence (`Character.HandleAttacked`, then `AttackOrder`). The brain never sets the faction.
- **Priorities** (`NpcPriority` in Rules, higher wins): `None 0 < Schedule 10 < Social 20 (talk pause) < Role 30 < External 40 < Flee 50`.
  - The brain remembers the order it last issued, that order's priority, and the role that issued it (if any). If `Character.CurrentOrder` is null, the current priority is `None`. If it is the brain's own order, it is that order's priority. Anything else, such as self-defence `AttackOrder` started by `Character`, counts as `External`.
  - The brain replaces the current order only if `NpcBrainRules.ShouldReplace(current, incoming)` is true (`incoming > current`). There are two extra cases:
    - a Schedule order is also replaced by the next Schedule entry when the schedule entry index changes;
    - **a role may replace a Role order** (`NpcBrainRules.RoleMayReplace(current) => current <= Role`), so a role can switch from attacking to hunting without first handing control back.
  - So only Flee can interrupt combat. The brain never overrides a fight with a routine.
- **Flee:** when the NPC takes damage from a living attacker and `health / max < archetype.fleeHealthFraction`, the brain issues a `FleeOrder` away from that attacker. Starvation damage has a null source and never causes fleeing. The brain also remembers the threat for `threatMemorySeconds`. If the NPC is still hurt and the threat is near when a flee ends, it flees again. If no flee point is found on the NavMesh (cornered), the order ends at once and self-defence takes over again.
- **Role slot:** an `INpcRole` interface on components of the same GameObject, complete from the start so the feuds plan does not change it:
  - `bool TryGetReaction(NpcBrain brain, out CharacterOrder order)`: asked every think tick.
  - `bool StillWants(NpcBrain brain, CharacterOrder order)`: asked every think tick while this role's order runs. False ends the order and hands control back to the schedule.
  - Roles must return false from `TryGetReaction` when their current order already does the job (for example already attacking the visible target). Otherwise the brain would restart the order every tick.
  - No implementations ship in this plan. The feuds plan adds `RevengeRole`; bandits will add `BanditRole`.
- **Schedule:** `NpcSchedule` (ScriptableObject) holds a list of `(fromHour, activity, anchorTag)` entries. Resolution is pure, in `NpcScheduleRules`. An entry runs from its `fromHour` until the next entry's `fromHour`, wrapping past midnight. The brain resolves from the *current* hour on every tick and never replays missed entries, so time jumps of any size are safe.
- **Activities are assets, so new ones need no brain change.** `NpcActivity` is an abstract ScriptableObject with:
  - `usesAnchor`, `minWaypoints` and `missingAnchorFallback` (`RunAtHome` or `WanderAtHome`), all serialized so the fallback is data;
  - `virtual bool PrerequisitesMet(NpcBrain brain)` (default true);
  - `abstract CharacterOrder CreateOrder(in NpcActivityContext ctx)`, where the context carries the brain, the anchor (or null at home), the position and facing to use, the wander radius, the waypoints, and the hours until the next entry (or none without a `TimeManager`).
  - Shipped subclasses, one asset each: `IdleAtActivity`, `WanderActivity`, `PatrolActivity`, `SleepActivity`, `FollowActivity`. "Work", "eat" and so on are later new subclasses plus an asset; `NpcBrain` and `NpcSchedule` do not change.
- **Anchors:** `NpcAnchor` is a scene component with an `NpcAnchorTag anchorTag` (a small `DefinitionAsset`: `Village`, `Stall`, `Campfire`, `Bed`, `Guardpost`, ...), a `radius` (wander area), and optional ordered `waypoints` (patrol). **Tags are assets, not strings**, so a typo cannot silently create a new tag: the schedule and the anchor both pick the same asset from the object picker. The TagManager is untouched. All anchors register in a static `NpcAnchor.All` from OnEnable/OnDisable, the same pattern as `PlayerControllable.All`, so there are no per-tick `FindObjectsByType` calls.
  - Lookup order: first the brain's `assignedAnchors` with the matching tag (for example its own bed), then the nearest anchor in `All` with the same tag asset within `archetype.anchorSearchRadius`.
- **Missing anchor fallback** (pure, `NpcScheduleRules.ResolvePlacement(usesAnchor, anchorFound, waypointCount, minWaypoints, prerequisitesMet, fallback)` returning `ActivityPlacement { Normal = 0, AtHome = 1, WanderAtHome = 2 }`):
  1. prerequisites not met: WanderAtHome (Follow with no live leader);
  2. the activity uses an anchor and none was found, or the anchor has fewer than `minWaypoints` waypoints: the activity's own fallback (`RunAtHome` gives AtHome, `WanderAtHome` gives WanderAtHome);
  3. otherwise Normal.
  - With the shipped assets this is the same table as before:
    - Sleep: sleep at the home position.
    - IdleAt: idle at the home position.
    - Patrol: wander around home (also when the anchor has fewer than 2 waypoints).
    - Wander: wander around home.
    - Follow: no `leader` set means wander around home.
  - The home position is where the NPC spawned. A missing anchor (tag set, nothing found) logs one warning per brain per entry index. An entry with no tag runs at home without a warning (the validator reports it). An entry with no activity wanders at home and warns once.
- **Orders:** `WanderOrder`, `PatrolOrder`, `IdleAtOrder` (walk to a point and stay, optionally for a set time and facing a direction) and `FollowOrder` are new. So is `FleeOrder`, which is needed for the flee priority above. `SleepOrder` gets an optional bed position to walk to first.
  - All new orders except Flee keep the default `YieldsToSelfDefence = true`. Flee returns false. Do not use `MoveOrder` for NPC walking: it returns `YieldsToSelfDefence = false` (the player's way out of a fight), so a townsperson walking to work would not defend itself.
  - Every order must cope with `character.Agent == null`, because PlayMode tests build characters without a NavMesh. In that case treat the character as arrived.
- **NpcArchetype** (ScriptableObject) holds all tuning: schedule, flee fraction, threat memory, flee distance, think interval, wander radius and pause range, patrol pause, anchor search radius, bark lines, bark seconds, talk pause seconds, and an `aggression` placeholder that nothing reads yet (bandits will). **It has no faction**: the faction comes from the `Character`'s own field (set on the prefab) or, in the feuds plan, its community.
- **Perception: extraction deferred to the bandit phase.** Phases 1-3 never need an NPC to *notice* anyone. Fleeing and fighting react to being attacked (`OnAttacked`/`OnDamaged` carry the attacker). Extracting `AnimalPerceptionUtility` now would be untested speculation and could break AnimalAI. In the bandit phase, pull out `ActorPerception.FindNearestHostile(Transform self, FactionId, PerceptionSettings)`, where `PerceptionSettings` is a struct of range, FOV, eye height and mask that replaces `AnimalPersonality` in the signature, and have it take the hostility test as a predicate so a bandit can pass `Attitude.Of`. Then point AnimalAI at it. (The feuds plan creates `ActorPerception` early with only `CanSee`.)
- **Needs:** the Townsperson prefab sets `CharacterStats.hasNeeds = false`, because townsfolk have no way to eat and would starve over a few days. When a townsperson is recruited, `Recruitable.enableNeedsOnJoin` turns needs on, so recruits become a survival responsibility like any squad member. Hunger and thirst are made decisions, not chores.

### Phase 3: interaction, talk and recruit
- `NpcInteractable : MonoBehaviour, IInteractable` replaces `CombatEntityInteractable` on human NPCs. `CombatEntityInteractable` stays unchanged for animals.
- **Options**, all omitted when the NPC is dead (the Corpse takes over anyway). The NPC's view comes from `Attitude.Explain(npc, interactor)`:
  - if `Relation == Hostile` and `Reason == Faction`: **Attack only**;
  - otherwise **Talk** (Kind Task, primary selected character only), then the options of every `INpcInteractionSource` component on the NPC in component order (**Recruit** comes from `Recruitable`), then **Attack** (Kind Attack, the whole selection).
  - Order is Talk, sources, Attack, so a single option is never an accidental murder.
  - In phases 1-3 the only reason is Faction, so this is exactly "Talk and Recruit when the relation is not Hostile, Attack always". The feuds plan's grudge-hostile NPCs (reason Grudge or Opinion) then keep Talk and Recruit (to bark and refuse) with no change here.
- **`INpcInteractionSource`** (seam): `void AddOptions(Character interactor, AttitudeResult attitude, List<InteractionOption> into)` and `bool TryExecute(Character interactor, string actionId)`. New NPC interactions (the feuds plan's blood price, later trade) are new components, not edits to `NpcInteractable`.
- **Recruit is always offered** when those conditions hold. The checks (in combat, cost, reach, unwilling) run in `ExecuteInteraction`, and a failure shows its reason as floating text. Hiding the option instead would make `InteractOrder.CanStillPerform` silently cancel the walk.
- **Talk:** the NPC shows a bark line with `Character.ShowFloatingText` (the existing harvesting label prefab). Lines come from the archetype (priority 0, hold) plus every **`INpcTalkSource`** component (seam: `void CollectLines(Character interactor, List<NpcTalkLine> into)`, `NpcTalkLine { string Text; int Priority; bool Hold }`). The highest priority present wins; a random line among those is shown. If the chosen line has `Hold` and the attitude neither is Hostile nor `RefusesDealings`, the brain holds the NPC in place, facing the speaker, for `talkPauseSeconds` (Social priority). There are no dialogue trees.
- **Recruit:**
  1. `RecruitRules.Check` passes.
  2. The cost items are taken from the recruiter's inventory.
  3. `character.SetOwnFaction(FactionRelations.Player)`. The squad rule already makes the recruit Player once `PlayerControllable` is added; setting the own faction too keeps it Player if the component is ever removed.
  4. `NpcBrain` is disabled. Its OnDisable ends its own current order.
  5. Needs are optionally enabled.
  6. A `PlayerControllable` is added. Its OnEnable fires `Joined` (the feuds plan uses this to take the recruit out of its community), invalidates the faction cache, and `SelectionHud` rebuilds the squad bar.
  7. `NpcInteractable`, `Recruitable` and every other `INpcInteractionSource` are destroyed. `GetComponentInParent<IInteractable>` still finds *disabled* components, so disabling them is not enough.
  - `Recruitable` does not depend on faction. Bandits or traders could carry one later.
  - `RecruitRules` has `Unwilling = 6` from the start, fed by `Attitude.RefusesDealings(npc, recruiter) || Attitude.RefusesSquad(npc)`. Both are always false in phases 1-3; the feuds plan's grudges make them true without changing `RecruitRules` or `Recruitable`.
- `InteractionOption` gets a `primaryOnly` flag (default false) so SelectionManager can send Talk and Recruit to one character only. Corpse looting keeps its existing special case.

### Ease of use and visibility (phases 1-3)
- **One-step NPC setup.** `Assets/Prefabs/NPC/Townsperson.prefab` carries every component an NPC needs (`Character` with `ownFaction` Townsfolk, `CharacterStats` with needs off, `NavMeshAgent`, `NpcBrain`, `NpcInteractable`, `Recruitable`, plus inventory and equipment as on the squad). `NpcBrain`, `NpcInteractable` and `Recruitable` have `[RequireComponent(typeof(Character))]`; `NavMeshAgent` is not required by code, because PlayMode tests build NPCs without a NavMesh. Placing an NPC is: drop the prefab, pick the archetype, assign its bed anchor (and, with the feuds plan, community, household and kin group). Changing the faction is one object-picker field on `Character`.
- **NPC debugger: an editor window, not an in-game overlay** (`Kanshilike > NPC Debugger`, step 15).
  - Why a window: it follows the editor selection, so it can inspect NPCs that the game cannot select (only squad members are selectable in play); it needs no new input binding, UI canvas or build code; it works while paused and while stepping frames; and it can be docked next to the Game view. When nothing is selected in the editor it shows the primary selected squad member, so it also follows in-game selection. It works in Play mode (refreshing four times a second) and shows "Enter Play mode to inspect NPCs" otherwise.
  - Content comes from `NpcDebugReport.Build(Character)` (Game, runtime code, PlayMode-testable): a list of sections, each with a title and lines. Phases 1-3 provide: **Faction** (effective faction and why, own faction, legacy field in use), **Attitude** toward each live squad member (relation, reason, refuses dealings), and **Brain** (archetype, current order type and priority, issuing role, schedule entry index, hour, activity, anchor tag, anchor found or fallback used, home position, threat and seconds of memory left, `InCombat`).
  - New sections are added by registering a provider (`NpcDebugReport.Providers`, a list of `Action<Character, NpcDebugReport>`), so the feuds plan adds memberships, relationships, grudges and known deaths without editing the window.
- **Setup validator** (`Kanshilike > Validate NPC Setup`, step 16): checks the open scene and the relevant assets and lists issues (error or warning, with the object to ping) in a window and in the Console. Checks are classes implementing `ISetupCheck`, found with `TypeCache`, so the feuds plan adds checks as new classes. Phases 1-3 checks:
  - faction definitions with duplicate ids; the Resources `FactionRelations` asset missing, or its `player` or `legacyEnumOrder` unset; relation entries with a null side, or the same pair listed twice with different relations;
  - a `Character` or `AnimalAI` whose faction asset is unset (warning: legacy value in use, run the migration);
  - an `NpcBrain` with no archetype; an archetype with no schedule; a schedule entry with no activity (error) or with no anchor tag on an activity that uses one (warning);
  - a schedule entry whose anchor tag has no `NpcAnchor` in the scene and no matching assigned anchor (warning: it will fall back);
  - a Patrol entry whose anchors have fewer than 2 waypoints; a Follow entry on a brain with no leader;
  - an `NpcAnchor` with no tag (error);
  - an NPC with `NpcBrain` that also has `CombatEntityInteractable` or `PlayerControllable`, or lacks `NpcInteractable` (warning); an `NpcInteractable` with no `NpcBrain` (warning: no talk pause); a `Recruitable` with `costCount > 0` and no `costItem` (error).

### Seams for feuds
The feuds plan (`docs/plans/feuds-and-kinship.md`) plugs into these. Building them now means it does not change phases 1-3 code twice.

| Seam | Phase 1-3 behaviour | Feuds plan uses it to |
|---|---|---|
| `FactionRules.Resolve` + `CharacterFaction.CommunitySource` (`ICommunityFactionSource`), `InvalidateAll`, `OwnFactionChanged` | no community source: own faction, or Player in the squad | give community members their community's faction, with no write-back |
| `FactionId`, `FactionRelationTable` | faction relations | key `PersonRecord`, `CommunityRecord` and `DeathRecord` factions; the social simulation uses the same resolver and table |
| `Attitude.Explain/Of/FightsBack/RefusesDealings/RefusesSquad` + `Attitude.Provider` (`IAttitudeProvider`) | faction relation only, never refuses | add opinion, grudge, kin and Cold refusal; `DefendAgainst`, `NpcInteractable` and `Recruitable` already call it |
| `AttitudeResult.RefusesDealings`, `AttitudeReason` (append only), `RecruitBlock.Unwilling = 6` | always false | Cold and stronger grudges refuse small talk and recruiting |
| `INpcRole.TryGetReaction/StillWants`, `RoleMayReplace`, issued-role tracking in `NpcBrain` | no roles | `RevengeRole` |
| `NpcBrain` API: `HoldFor`, `InCombat`, `Character`, `Archetype`, `HomePosition`, `IsRoleOrder` | talk pause | revenge, rally and hunting |
| `INpcInteractionSource` | `Recruitable` | the "Offer blood price" option |
| `INpcTalkSource` | archetype barks only | tribe barks and grudge barks |
| `InteractOrder.Target`, `InteractOrder.ActionId` | follow moving targets | parley: a holder holds fire toward an envoy walking up to offer blood price |
| `PlayerControllable.Joined` | squad bar rebuild | recruits leave their community |
| `NpcDebugReport.Providers` | faction, attitude, brain sections | social sections |
| `ISetupCheck` | faction and NPC checks | person id, ties, community and valuable checks |
| `DefinitionAsset` | factions and anchor tags | tribes, kin groups, households |
| `NpcArchetype` | no faction field | gains `vengefulness`, `sight` and `revengeChaseDistance` (additive fields) |

`Character` gets `personId` and `sex` only in the feuds plan; that plan also adds its own lines to `Character` (registration and report hooks).

## Steps

All paths are relative to `C:\Users\Eppu\Kanshilike`. "(Editor)" marks steps that need the Unity Editor.

### Phase 1: faction relations

1. **Rules types.** New files in `Assets/Scripts/Rules/` (no `UnityEngine`, global namespace like `HourClock`):
   - `FactionId.cs`: `public readonly struct FactionId : IEquatable<FactionId>` with `Value`, `IsNone` (null or empty), `static FactionId None`, ordinal `Equals`/`GetHashCode` (`StringComparer.Ordinal`), `==`, `!=`, `ToString` (Value or "none").
   - `FactionRelationTable.cs`:
     - `public enum FactionRelation { Hostile = 0, Neutral = 1, Allied = 2 }` with the comment "Serialized by number: never renumber, only append."
     - `public enum FactionRelationSource { Unaffiliated = 0, Explicit = 1, Same = 2, DefaultStance = 3 }`
     - `public sealed class FactionRelationTable`: stance map `Dictionary<FactionId, FactionRelation>`; pair map keyed by `(FactionId, FactionId)` ordered by ordinal compare so lookups are symmetric; `Define(id, stance)` (overwrites), `IsDefined`, `DefaultStanceOf` (Neutral when undefined), `Set(a, b, relation)`, `Clear(a, b)`, `Get(a, b)` and `Explain(a, b, out FactionRelationSource)` in the order given in the design. `Define`/`Set` ignore `None`.
   - `FactionRules.cs`: `public enum FactionSource { Own = 0, Squad = 1, Community = 2 }`, `public readonly struct FactionResolution { FactionId Id; FactionSource Source; }`, `public static FactionResolution Resolve(bool inSquad, FactionId community, FactionId own, FactionId player)`.
   - `AttitudeResult.cs`: `public enum AttitudeReason { Faction = 0 }` ("append only") and `public readonly struct AttitudeResult { FactionRelation Relation; AttitudeReason Reason; bool RefusesDealings; }` with a constructor.
   - Done when: `Kanshilike.Rules` compiles without `UnityEngine`.

2. **Faction data and the static facade (Game).**
   - New `Assets/Scripts/Data/DefinitionAsset.cs`: `public abstract class DefinitionAsset : ScriptableObject` with `[SerializeField] string id`, `[SerializeField] string displayName`, `public string Id` (id, or the asset name lower-cased with spaces replaced by `-`), `public string DisplayName` (displayName, or the asset name). Tooltip on `id`: "Stable id used for lookups and saves. Leave empty to use the asset name. Never change it after content ships."
   - New `Assets/Scripts/Combat/FactionDefinition.cs`: `[CreateAssetMenu(menuName = "Kanshilike/Factions/Faction")] public class FactionDefinition : DefinitionAsset` with `[SerializeField] FactionRelation defaultStance = FactionRelation.Neutral` (tooltip: "How this faction treats any faction it has no entry with in the relations asset. Wildlife and bandits: Hostile.") and `public FactionId FactionId => new FactionId(Id)`.
   - New `Assets/Scripts/Combat/FactionRelationSet.cs`: `[CreateAssetMenu(menuName = "Kanshilike/Factions/Faction Relations")]` with the fields from the design (`player`, `legacyEnumOrder`, `factions`, `relations` with `[Serializable] class Entry { FactionDefinition a; FactionDefinition b; FactionRelation relation; }`), `BuildTable()` and `Find(FactionId)`.
   - Rename `Assets/Scripts/Combat/Faction.cs` **with its `.meta`** to `FactionRelations.cs` (`git mv` both, or the Project window) and rewrite it as `public static class FactionRelations`: **delete the `Faction` enum**, and add the members listed under "`FactionRelations` (Game, static)", loading `Resources.Load<FactionRelationSet>("Factions/FactionRelations")` lazily.
   - New `Assets/Scripts/Combat/CharacterFaction.cs` (static): `ICommunityFactionSource { bool TryGetCommunityFaction(Character c, out FactionId faction, out string communityName); }`, `CommunitySource` (setter calls `InvalidateAll`), `int Version`, `InvalidateAll()`, `event Action<Character> OwnFactionChanged`, `Explain(Character)`; reset in `SubsystemRegistration`.
   - Done when: Game compiles after step 3.

3. **Actors resolve their faction.**
   - `Assets/Scripts/Combat/CombatActor.cs`: `public abstract FactionId Faction { get; }`.
   - `Assets/Scripts/Character/Character.cs`:
     - replace the `faction` field with `ownFaction` and `legacyFaction` exactly as in the migration design (`using UnityEngine.Serialization;`), with a tooltip on `ownFaction` ("This person's own faction. Squad members always count as Player; a community member uses its community's faction.").
     - `public FactionDefinition OwnFaction` (the asset, or the legacy mapping), `public bool UsesLegacyFaction`.
     - `public override FactionId Faction`: cached `FactionRules.Resolve(...)` as in the design; on recompute also `FactionRelations.Register(OwnFaction)`.
     - `internal void InvalidateFaction()`, `public FactionResolution FactionResolution` (for the debug view).
     - `public void SetOwnFaction(FactionDefinition value)`: set, invalidate, raise `CharacterFaction.OwnFactionChanged`.
   - `Assets/Scripts/Selection/PlayerControllable.cs`: in `OnEnable` and `OnDisable`, call `Character?.InvalidateFaction()`.
   - `Assets/Scripts/NPC/Animals/AnimalAI.cs`: replace the field with `factionDefinition` and `legacyFaction = 1`; `public override FactionId Faction` returns the asset's id or the legacy mapping (registering the definition once, cached in `Awake`).
   - `Assets/Scripts/NPC/Animals/AnimalPerceptionUtility.cs`: parameter `FactionId selfFaction`; the body is unchanged.
   - Done when: the project compiles, and in Play mode the wolf still attacks Jonttuli (legacy fallback, before step 6).

4. **Attitude seam and callers.**
   - New `Assets/Scripts/Combat/Attitude.cs` (Game, static): `IAttitudeProvider { bool TryExplain(CombatActor self, CombatActor other, FactionRelation factionRelation, out AttitudeResult result); bool RefusesSquad(CombatActor self); }`, `Provider`, `Explain`, `Of`, `FightsBack`, `RefusesDealings`, `RefusesSquad`. With no provider, or when it returns false, the result is the faction relation with reason Faction and no refusal. Reset `Provider` in `SubsystemRegistration`.
   - `Character.DefendAgainst`: `bool fightsBack = enemy != null && enemy != this && enemy.IsAlive && Attitude.FightsBack(this, enemy);` and pass it to `CombatRules.ShouldDefendSelf`.
   - `SelectionManager.TryOrderTreatment`: replace `FactionRelations.AreHostile(...)` with `!FactionRelations.AreAllied(primary.Faction, patient.Faction)`.
   - Leave `Character.TryTreatWounds` and `AnimalPerceptionUtility` on `AreHostile`.
   - Done when: the existing EditMode and PlayMode tests pass after the step 7 test updates.

5. **Editor assembly and the migration tool.**
   - New `Assets/Scripts/Editor/Kanshilike.Game.Editor.asmdef`: `includePlatforms: ["Editor"]`, references `Kanshilike.Game` and `Kanshilike.Rules`, `autoReferenced: false`. (A folder named `Editor` inside `Assets/Scripts` would otherwise belong to `Kanshilike.Game`.)
   - Add `"Kanshilike.Game.Editor"` to the references of `Assets/Tests/EditMode/Kanshilike.Tests.EditMode.asmdef`. Do not add it to the PlayMode test asmdef.
   - New `Assets/Scripts/Editor/FactionMigration.cs`: the menu item and the order of work from the migration design. Use `SerializedObject` on each `Character`/`AnimalAI`; read `legacyFaction`; set `ownFaction`/`factionDefinition` from `legacyEnumOrder`; `PrefabUtility.SavePrefabAsset` for prefabs; `EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)`, `SaveScene`, `CloseScene` for scenes that are not already open (save open ones in place). Report "migrated N objects in M files".
   - Done when: it compiles and the menu item appears.

6. **(Editor) Faction content and migration.**
   - Create `Assets/Factions/Player.asset`, `Wildlife.asset`, `Townsfolk.asset`, `Traders.asset`, `Bandits.asset` (FactionDefinition) with the ids and stances from the design table and display names.
   - Create `Assets/Resources/Factions/FactionRelations.asset` (FactionRelationSet): `player` = Player, `legacyEnumOrder` = [Player, Wildlife], `factions` = all five, `relations` empty.
   - Run **Kanshilike > Factions > Migrate Legacy Faction Fields**. Review the diff: `test1.unity` (Jonttuli, Keppana, wolf), `Iides_map.unity` (Keppana), `Wolf.prefab`, `Deer.prefab` gain asset references; nothing else changes. Commit the healing work first or review carefully, because the scene has uncommitted changes.
   - Done when: Jonttuli shows Own Faction = Player and the wolf Wildlife in the inspector, and `FactionWiringTests` and `FactionContentTests` pass.

7. **Tests for phase 1.** EditMode `Assets/Tests/EditMode/FactionRelationsTests.cs`, `FactionRulesTests.cs`, `FactionContentTests.cs`, `FactionWiringTests.cs` (namespace `Kanshilike.Tests.EditMode`); PlayMode: new shared helper `Assets/Tests/PlayMode/TestFactions.cs`, update `CharacterBehaviourTests`, new `FactionResolutionTests.cs`. See the test plan.
   - `TestFactions`: creates `Player`, `Wildlife`, `Townsfolk`, `Traders`, `Bandits` definitions with `ScriptableObject.CreateInstance` (names and stances as the content), `Install()` calls `FactionRelations.Configure(table, Player, all)`, and `Uninstall()` calls `ResetToDefault()` and destroys them.
   - `CharacterBehaviourTests`: `MakeCharacter(string, Vector3, FactionDefinition, bool)` sets `"ownFaction"`; replace every `Faction.Player`/`Faction.Wildlife` with `factions.Player`/`factions.Wildlife`; `Install()` in `SetUp`, `Uninstall()` in `TearDown`.

### Phase 2: NPC brain and routines

8. **Pure schedule and brain rules.**
   - New file `Assets/Scripts/Rules/NpcPriority.cs`: `NpcPriority` with explicit ints `None 0, Schedule 10, Social 20, Role 30, External 40, Flee 50`; `ActivityPlacement { Normal = 0, AtHome = 1, WanderAtHome = 2 }`; `MissingAnchorFallback { RunAtHome = 0, WanderAtHome = 1 }` (serialized: never renumber).
   - New file `Assets/Scripts/Rules/NpcScheduleRules.cs`:
     - `static float NormalizeHour(float hour)` returns `[0,24)`, so 24 becomes 0 and -1 becomes 23.
     - `static int ResolveIndex(IReadOnlyList<float> fromHours, float hour)`:
       - returns -1 for an empty list.
       - otherwise returns the index whose `fromHour` is the largest one `<= hour`.
       - if no `fromHour` is `<= hour`, it wraps: the index with the largest `fromHour` overall.
       - ties go to the lowest index.
       - works on unsorted lists.
     - `static float HoursUntilNext(IReadOnlyList<float> fromHours, float hour)`: hours until the next *different* start time after `hour`, wrapping. Returns 24 for a single entry and 0 for an empty list.
     - `static ActivityPlacement ResolvePlacement(bool usesAnchor, bool anchorFound, int waypointCount, int minWaypoints, bool prerequisitesMet, MissingAnchorFallback fallback)` as in the design.
   - New file `Assets/Scripts/Rules/NpcBrainRules.cs`:
     - `static bool ShouldReplace(NpcPriority current, NpcPriority incoming) => incoming > current;`
     - `static bool RoleMayReplace(NpcPriority current) => current <= NpcPriority.Role;`
     - `static NpcPriority CurrentPriority(bool idle, bool isOwnOrder, NpcPriority ownPriority)`
     - `static bool ShouldFlee(float health, float maxHealth, float fleeFraction, bool hasLivingThreat)`: false when `fleeFraction <= 0`, `maxHealth <= 0` or there is no threat.
   - Done when: Rules compiles and the step 9 tests pass.

9. **EditMode tests** for step 8 (`Assets/Tests/EditMode/NpcScheduleRulesTests.cs`, `NpcBrainRulesTests.cs`).

10. **Data asset classes.** In `Assets/Scripts/NPC/Humans/`:
    - `NpcAnchorTag.cs`: `[CreateAssetMenu(menuName = "Kanshilike/NPC/Anchor Tag")] public class NpcAnchorTag : DefinitionAsset {}`.
    - `NpcSchedule.cs`:
      - `[CreateAssetMenu(menuName = "Kanshilike/NPC/Schedule")] public class NpcSchedule : ScriptableObject`
      - `[Serializable] public class Entry { [Range(0f, 24f)] public float fromHour; public NpcActivity activity; public NpcAnchorTag anchorTag; }`
      - `public List<Entry> entries`
      - `public int ResolveIndex(float hour)`, which builds a cached `float[]` of the hours and calls `NpcScheduleRules.ResolveIndex`. Rebuild the cache in `OnValidate`.
      - `public float HoursUntilNext(float hour)`.
    - `Activities/NpcActivity.cs`: the abstract ScriptableObject and `NpcActivityContext` struct from the design, with tooltips on `usesAnchor`, `minWaypoints`, `missingAnchorFallback`.
    - `Activities/IdleAtActivity.cs`, `WanderActivity.cs`, `PatrolActivity.cs`, `SleepActivity.cs`, `FollowActivity.cs`, each `[CreateAssetMenu(menuName = "Kanshilike/NPC/Activities/...")]`, with these defaults and orders:

      | Class | usesAnchor | minWaypoints | fallback | `CreateOrder` |
      |---|---|---|---|---|
      | IdleAt | true | 0 | RunAtHome | `IdleAtOrder(ctx.Position, ctx.Facing)` |
      | Wander | true | 0 | RunAtHome | `WanderOrder(ctx.Position, ctx.Radius, archetype.wanderPauseSeconds)` (radius: anchor radius, or `archetype.wanderRadius` at home) |
      | Patrol | true | 2 | WanderAtHome | `PatrolOrder(ctx.Waypoints, archetype.patrolPauseSeconds)` |
      | Sleep | true | 0 | RunAtHome | no clock or `ctx.HoursUntilNext < 0.1`: `IdleAtOrder(ctx.Position)`; otherwise `SleepOrder(hours, ctx.Position)` |
      | Follow | false | 0 | WanderAtHome | `FollowOrder(brain.Leader)`; `PrerequisitesMet`: leader set and alive |

      - `WanderActivity` also has `public static CharacterOrder AtHome(NpcBrain brain)` (wander around home with the archetype radius), which the brain uses for `WanderAtHome`.
    - `NpcArchetype.cs`: `[CreateAssetMenu(menuName = "Kanshilike/NPC/Archetype")]`, fields with tooltips and defaults (no faction):

      | Field | Default |
      |---|---|
      | `NpcSchedule schedule` | |
      | `[Range(0,1)] fleeHealthFraction` | 0.35 |
      | `threatMemorySeconds` | 10 |
      | `fleeDistance` | 15 |
      | `thinkIntervalSeconds` | 0.5 |
      | `wanderRadius` | 8 |
      | `Vector2 wanderPauseSeconds` | (2, 6) |
      | `patrolPauseSeconds` | 3 |
      | `anchorSearchRadius` | 60 |
      | `string[] barkLines` | |
      | `barkSeconds` | 3 |
      | `talkPauseSeconds` | 4 |
      | `[Range(0,1)] aggression` | 0 |

      - Comment on `aggression`: "Placeholder, unused until bandits."
    - Done when: all appear in the Create menu.

11. **`NpcAnchor`.** New file `Assets/Scripts/NPC/Humans/NpcAnchor.cs`:
    - `public NpcAnchorTag anchorTag`
    - `[Min(0)] public float radius = 5f`
    - `public Transform[] waypoints` (if empty, use the anchor's children in hierarchy order)
    - static `List<NpcAnchor> all` and `public static IReadOnlyList<NpcAnchor> All`, registered in OnEnable/OnDisable
    - `public Vector3 Position`, `public Quaternion Facing`
    - `public IReadOnlyList<Vector3> GetWaypoints()`
    - `static NpcAnchor FindNearest(NpcAnchorTag tag, Vector3 from, float maxDistance)`: reference compare, ignores a null tag.
    - `OnDrawGizmos`: a wire sphere at `radius`, lines between waypoints, and the tag's display name as a label (`Handles.Label` inside `#if UNITY_EDITOR`).

12. **New orders** in `Assets/Scripts/Character/Orders/`:
    - First add a shared helper `OrderMovement.cs` (`internal static class`):
      - `static bool HasArrived(Character c, Vector3 point, float slack)`: true when the agent is null or disabled. Otherwise use the same test as `MoveOrder`: not `pathPending`, and either no path or `remainingDistance <= stoppingDistance + slack`. Also accept a flat distance below `slack + stoppingDistance`.
      - `static IEnumerator WalkTo(Character c, Vector3 point, float slack)`: calls `c.MoveTo`, then yields until it has arrived.
      - `static bool TrySampleNavMesh(Vector3 point, float maxDistance, out Vector3 result)`, which wraps `NavMesh.SamplePosition`.
      - Make `MoveOrder` use `HasArrived` without changing its behaviour.
    - `IdleAtOrder(Vector3 point, Quaternion? facing = null, float seconds = float.PositiveInfinity, Transform lookAt = null)`:
      - walk there, stop, turn to `facing` or `lookAt` using `CombatFacing.TurnToward`, then wait `seconds` (forever by default).
      - `End` calls `StopMoving`.
    - `WanderOrder(Vector3 center, float radius, Vector2 pauseSeconds)`, which loops until replaced:
      1. Pick a random point in the circle and sample the NavMesh. On failure, wait 1 s and retry.
      2. Walk there.
      3. Wait `Random.Range(pause.x, pause.y)`.
      - `End` calls `StopMoving`. Exposes `Center`.
    - `PatrolOrder(IReadOnlyList<Vector3> waypoints, float pauseSeconds)`:
      - start at the nearest waypoint, loop through them in order, pause at each.
      - fewer than 2 waypoints means the order ends at once (the brain's fallback should prevent this).
    - `FollowOrder(Character leader, float keepDistance = 3f)`:
      - while the leader is alive and not null: if farther than `keepDistance + 1`, move to the leader's position; if within `keepDistance`, stop.
      - ends when the leader dies or is destroyed.
      - Exposes `Leader`.
    - `FleeOrder(Transform threat, float fleeDistance)`:
      - `YieldsToSelfDefence => false`.
      - Find a point `fleeDistance` away from the threat. Try the straight-away direction, then ±45° and ±90°, sampling the NavMesh for each. With no agent, use the raw point.
      - No point found: `yield break` (cornered).
      - Walk there. The order ends on arrival, or when the threat is null or dead, or once the threat is farther than `fleeDistance`.
      - Exposes `Threat`.
      - `End` calls `StopMoving`.
    - `SleepOrder.cs`: add an optional second constructor parameter `Vector3? bedPosition = null`. When it is set, first `yield return OrderMovement.WalkTo(...)`, then the existing sleep. The player's `Character.Sleep()` does not change.
    - Done when: it compiles, and `Orders_SayWhetherSelfDefenceMayReplaceThem` is extended (see test plan).

13. **`INpcRole`.** New file `Assets/Scripts/NPC/Humans/INpcRole.cs` with both methods and doc comments: `TryGetReaction`: "Return true with an order to react at Role priority. Return false when your current order already does the job." `StillWants`: "Asked each think while this role's order runs. Return false to hand control back to the schedule."

14. **`NpcBrain`.** New file `Assets/Scripts/NPC/Humans/NpcBrain.cs`, `[RequireComponent(typeof(Character))]`.
    - Serialized fields:
      - `NpcArchetype archetype`
      - `NpcAnchor[] assignedAnchors`
      - `Character leader` (for the Follow activity)
    - Private state:
      - `issuedOrder`, `issuedPriority`, `issuedEntryIndex` (-1 when none), `INpcRole issuedRole`
      - `homePosition`
      - `TimeManager time`, `float currentHour`
      - `CombatActor threat`, `float threatTime`
      - `INpcRole[] roles`
      - `float nextThink`
      - `HashSet<int> warnedMissing`
      - last schedule diagnostics for the debug view: entry index, anchor used, placement.
    - `Awake`:
      - cache the Character, stats and roles (`GetComponents<INpcRole>()`).
      - `homePosition = transform.position`.
      - if there is no archetype, log a warning and set `enabled = false`. (No faction is set here.)
    - `OnEnable`:
      - `time = FindFirstObjectByType<TimeManager>()`, subscribe to `OnTimeChanged` (cache the hour), seed `currentHour = time.currentTime`.
      - subscribe to `stats.OnDamaged` and `stats.OnDied`.
    - `OnDisable`:
      - unsubscribe everything.
      - if `character.CurrentOrder == issuedOrder` and the order is not null, call `character.GiveOrder(null)`.
      - clear the issued state.
    - `HandleDied`: `enabled = false`.
    - `HandleDamaged(amount, source)`:
      - resolve `source.GetComponentInParent<CombatActor>()`; if it is a living actor other than self, store `threat` and `threatTime`.
      - then call `TryFlee()` straight away, without waiting for the next think tick.
    - `Update`: every `thinkIntervalSeconds`, call `Think()`:
      1. If dead: `enabled = false`, return.
      2. `cur = NpcBrainRules.CurrentPriority(character.CurrentOrder == null, character.CurrentOrder == issuedOrder, issuedPriority)`.
      3. `TryFlee()`, which does nothing if the current order is already a FleeOrder.
      4. If `cur == Role` and `issuedRole != null` and `!issuedRole.StillWants(this, issuedOrder)`: `character.GiveOrder(null)`, clear the issued state, `cur = None`.
      5. For each role: if `RoleMayReplace(cur)` and `TryGetReaction(this, out order)`: `Issue(order, Role, -1, role)`, return.
      6. Schedule:
         - with no schedule, wander around home.
         - `idx = archetype.schedule.ResolveIndex(currentHour)`.
         - if `cur == None`, or `cur == Schedule && idx != issuedEntryIndex`: `Issue(BuildScheduleOrder(idx), Schedule, idx, null)`.
    - `TryFlee()`:
      - `hasThreat = threat != null && threat.IsAlive && Time.time - threatTime <= threatMemorySeconds`.
      - if `NpcBrainRules.ShouldFlee(...)` holds and the current order is not a FleeOrder: `Issue(new FleeOrder(threat.transform, archetype.fleeDistance), Flee, -1, null)`.
    - `BuildScheduleOrder(idx)`:
      1. Read the entry. No activity: warn once for this idx, return `WanderActivity.AtHome(this)`.
      2. If `activity.usesAnchor` and the entry has a tag, find its anchor (assigned anchors first, then `NpcAnchor.FindNearest`).
      3. `placement = NpcScheduleRules.ResolvePlacement(activity.usesAnchor, anchor != null, waypointCount, activity.minWaypoints, activity.PrerequisitesMet(this), activity.missingAnchorFallback)`. Warn once per idx when a tag was set and no anchor was found.
      4. `WanderAtHome`: `WanderActivity.AtHome(this)`. Otherwise build the context (anchor and its position/facing/radius/waypoints for Normal; null anchor, home position, archetype wander radius for AtHome; `HoursUntilNext` from the schedule when `time != null`) and return `activity.CreateOrder(ctx)`.
    - `Issue(order, priority, idx, role)`: set the issued fields, then `character.GiveOrder(order)`.
    - Public API for phase 3 and the feuds plan:
      - `public void HoldFor(float seconds, Transform lookAt)`: if `ShouldReplace(cur, Social)`, issue `IdleAtOrder(transform.position, null, seconds, lookAt)` at Social priority. Otherwise do nothing; a fighting NPC does not stop to chat.
      - `public bool InCombat`: the current order is an AttackOrder or FleeOrder, or `Time.time - threatTime < threatMemorySeconds` with a living threat.
      - `public NpcArchetype Archetype`, `public Vector3 HomePosition`, `public Character Character`, `public Character Leader`
      - `public bool IsRoleOrder(CharacterOrder o)`
      - `public NpcBrainDiagnostics Diagnostics` (read-only struct for the debug view).
    - Done when: the step 17 tests pass and the play-test checklist works.

15. **NPC debugger.**
    - New `Assets/Scripts/NPC/Debug/NpcDebugReport.cs` (Game): `NpcDebugReport { List<Section> Sections; Section Add(string title); }`, `Section { string Title; List<string> Lines; }`, `static List<Action<Character, NpcDebugReport>> Providers`, `static NpcDebugReport Build(Character c)`. Register the three phase 1-3 providers (Faction, Attitude, Brain, content as in the design) from a static constructor so they exist in every play session and test.
    - New `Assets/Scripts/Editor/NpcDebuggerWindow.cs`: `EditorWindow`, menu **Kanshilike > NPC Debugger**. Target: the `Character` on or above `Selection.activeGameObject`, else the primary selected squad member (`PlayerControllable.All` with `IsSelected`). A "Pin" toggle keeps the current target. Draw each section as a foldout with selectable labels. `Repaint` four times a second in Play mode (`EditorApplication.update` with a timer). Outside Play mode show "Enter Play mode to inspect NPCs".
    - Done when: in Play mode, selecting a townsperson in the Hierarchy shows its faction, attitude toward Jonttuli and Keppana, current order and schedule entry, and `NpcDebugReportTests` pass.

16. **Setup validator.**
    - New `Assets/Scripts/Editor/Validation/ISetupCheck.cs`: `interface ISetupCheck { string Name { get; } void Run(SetupContext ctx, List<SetupIssue> issues); }`, `SetupIssue { Severity (Error, Warning); string Message; Object Context; }`, `SetupContext { Scene Scene; IReadOnlyList<T> FindInScene<T>(bool includeInactive = true); IReadOnlyList<T> FindAssets<T>(); }`.
    - New `Assets/Scripts/Editor/Validation/SetupValidator.cs`: `static List<SetupIssue> Validate(Scene scene)` runs every `ISetupCheck` found with `TypeCache.GetTypesDerivedFrom<ISetupCheck>()`; menu **Kanshilike > Validate NPC Setup** validates the active scene, logs each issue with its context object, and opens a small results window (click to ping).
    - Checks, one class each in `Assets/Scripts/Editor/Validation/`: `FactionSetupCheck`, `ActorFactionCheck`, `NpcBrainCheck` (archetype, schedule, entries, anchors, waypoints, leader), `NpcAnchorCheck`, `NpcComponentCheck` (including the phase 3 `NpcInteractable` and `Recruitable` checks; add those when step 21-22 land). Contents as listed in the design.
    - New EditMode tests `Assets/Tests/EditMode/SetupValidatorTests.cs` (see the test plan).
    - Done when: the validator reports the deliberate problems in `SetupValidatorTests` and nothing at error level in `test1` (step 28).

17. **PlayMode tests** for the brain (`Assets/Tests/PlayMode/NpcBrainTests.cs`) and the debug report (`NpcDebugReportTests.cs`). See the test plan. Build NPCs with the same code-only helpers as `CharacterBehaviourTests`: move `MakeCharacter`, `SetField`, `WaitFor`, `MakeTimeManager` and `MakeItem` into a shared `TestWorld` helper class in the PlayMode folder and use it from all PlayMode files. Archetypes, schedules, activities and anchor tags are made with `ScriptableObject.CreateInstance`. Set `thinkIntervalSeconds` to 0.05.

### Phase 3: interaction, talk and recruit

18. **`InteractOrder` follows moving targets.** In `Assets/Scripts/Character/Orders/InteractOrder.cs`:
    - inside the walking loop, re-read `target.GetInteractionPoint()` every 0.25 s. If it has moved more than 1 m from `destination`, update `destination` and call `MoveTo` again. Static targets behave as before.
    - add `public IInteractable Target => target;` and `public string ActionId => actionId;`.
    - Done when: a squad member walks to and reaches a wandering NPC.

19. **Option chooser.**
    - `Assets/Scripts/Interaction/InteractionOption.cs`: add `public bool primaryOnly;` and an optional constructor parameter `bool primaryOnly = false`.
    - New file `Assets/Scripts/Interaction/InteractionMenu.cs`: a lazily created singleton with its own ScreenSpaceOverlay canvas, `sortingOrder 60`, `CanvasScaler` settings like `SelectionHud`, and a `GraphicRaycaster`.
      - `Show(Vector2 screenPos, string title, List<InteractionOption> options, Action<InteractionOption> onChosen)`: a title panel plus one `UIFactory.Button` per option, at the mouse position and clamped to the screen.
      - `Hide()`.
      - `IsOpen`.
      - It hides on Escape, on any mouse button pressed outside it (in `Update`, check `EventSystem.current.IsPointerOverGameObject()` before hiding), and after a choice is made.
    - `Assets/Scripts/Selection/SelectionManager.cs`, `HandleRightMouse`:
      1. Close an open menu first.
      2. One option: same as today.
      3. More than one option and no corpse: `InteractionMenu.Instance.Show(Input.mousePosition, interactable.GetName(), options, chosen => Dispatch(interactable, chosen))`.
      - Factor out `Dispatch`: if the target is a corpse or `option.primaryOnly`, send it only to `selectedCharacters[0]`; otherwise send it to every selected character. Re-read the selection inside the callback, and do nothing if it is empty.
      - Update the class doc comment: "right-click: … (several options: pick one from a menu)".
    - Done when: right-clicking the wolf still attacks at once, and right-clicking a townsperson opens a 3-button menu.

20. **Pure recruit rules.** New file `Assets/Scripts/Rules/RecruitRules.cs`:
    - `public enum RecruitBlock { None = 0, Dead = 1, Hostile = 2, InCombat = 3, TooFar = 4, CantAfford = 5, Unwilling = 6 }` ("append only").
    - `public static RecruitBlock Check(bool alive, FactionRelation relation, bool inCombat, bool inReach, int carried, int cost, bool unwilling)`. Checks run in the enum's order. A cost of 0 or less is always affordable.
    - `public static string Reason(RecruitBlock b)` returns short player-facing text: "Dead", "Won't listen to you", "Too busy fighting", "Too far away", "Can't pay", "Won't share a fire with your people".
    - EditMode tests: `Assets/Tests/EditMode/RecruitRulesTests.cs`.

21. **`NpcInteractable` and the interaction seams.**
    - New `Assets/Scripts/Interaction/INpcInteractionSource.cs` and `INpcTalkSource.cs` (with `NpcTalkLine`), as in the design.
    - New file `Assets/Scripts/Interaction/NpcInteractable.cs`, `[RequireComponent(typeof(Character))]`:
      - `GetName` returns `character.characterName`, or the GameObject name if that is empty.
      - `GetAvailableInteractions(interactor)`: empty if the NPC is dead; otherwise the option rule from the design, using `Attitude.Explain(character, interactor)` and `GetComponents<INpcInteractionSource>()`. `talk` is `primaryOnly`; `attack` is kind Attack.
      - `ExecuteInteraction`:
        - `talk`: collect lines (archetype `barkLines` at priority 0 with `Hold = true`, then each `INpcTalkSource`), pick as in the design (no lines: "..."), `character.ShowFloatingText(line, archetype.barkSeconds)`, then `brain?.HoldFor(archetype.talkPauseSeconds, interactor.transform)` when the line holds and the attitude is not Hostile and does not refuse dealings.
        - `attack`: routed by `Character.InteractWith` and never reaches here, but handle it the same way `CombatEntityInteractable` does.
        - anything else: the first source whose `TryExecute` returns true.
      - `GetInteractionPoint` returns `transform.position`.
    - Done when: it compiles and the step 24 tests pass.

22. **`Recruitable`.** New file `Assets/Scripts/NPC/Humans/Recruitable.cs`, `[RequireComponent(typeof(Character))]`, implements `INpcInteractionSource`:
    - Fields:
      - `Item costItem` (null means free)
      - `[Min(0)] int costCount`
      - `bool enableNeedsOnJoin = true`
      - `string[] acceptLines`
      - `string[] refuseLines` (if empty, use `RecruitRules.Reason`)
    - `AddOptions`: adds `new InteractionOption("recruit", "Recruit", primaryOnly: true)`. `TryExecute("recruit")` calls `TryRecruit(interactor, out _)` (it shows its own refusal) and returns true.
    - `public bool TryRecruit(Character recruiter, out string reason)`:
      1. `inCombat` is `brain != null ? brain.InCombat : character.CurrentOrder is AttackOrder`.
      2. `inReach = InventoryActions.InReach(recruiter, character)`.
      3. `carried` is the sum of `count` over `recruiter.CarriedInventory.Entries` where `item == costItem`.
      4. `relation = Attitude.Of(character, recruiter)`; `unwilling = Attitude.RefusesDealings(character, recruiter) || Attitude.RefusesSquad(character)`.
      5. If `RecruitRules.Check` returns anything other than None, show a refusal line above the NPC and return false.
      6. Otherwise remove `costCount` of the item with `RemoveOneOf` in a loop, then call `Join(recruiter)`.
    - `private void Join(Character recruiter)`:
      1. `character.SetOwnFaction(FactionRelations.Player)`.
      2. If there is a brain, set `brain.enabled = false`.
      3. If `enableNeedsOnJoin`, set `stats.hasNeeds = true`.
      4. If there is no `PlayerControllable`, call `gameObject.AddComponent<PlayerControllable>()`.
      5. Show an accept line.
      6. Destroy `NpcInteractable`, every other `INpcInteractionSource` component, and `this`.
      - `CarriedInventory` is internal. That is fine: same assembly.

23. **Validator checks for phase 3.** Add the `NpcInteractable` and `Recruitable` checks from the design to `NpcComponentCheck` and their cases to `SetupValidatorTests`.

24. **PlayMode tests** for interaction and recruit (`Assets/Tests/PlayMode/NpcInteractionTests.cs`). See the test plan.

### Scene and content setup

25. **(Editor) Assets.**
    - Anchor tags in `Assets/NPC/AnchorTags/`: `Village`, `Stall`, `Campfire`, `Bed`, `Guardpost`, and `Market` (used only by the broken-anchor test schedule; no anchor carries it).
    - Activities in `Assets/NPC/Activities/`: `IdleAt.asset`, `Wander.asset`, `Patrol.asset`, `Sleep.asset`, `Follow.asset` with the defaults from step 10.
    - Create `Assets/NPC/Schedules/TownspersonDay.asset` (NpcSchedule) with these entries:

      | fromHour | activity | anchorTag |
      |---|---|---|
      | 6 | Wander | Village |
      | 9 | IdleAt | Stall |
      | 17 | IdleAt | Campfire |
      | 21 | Sleep | Bed |

    - Create `Assets/NPC/Archetypes/Townsperson.asset` (NpcArchetype): schedule TownspersonDay, the default tuning values, and 4-6 era-appropriate bark lines. Iron Age Finnish village talk: fishing, the harvest, spirits of the forest, wolves. No modern references and no crossbows.
    - Done when: all assets have their fields filled in.

26. **(Editor) Townsperson prefab.**
    1. In `Assets/Scenes/test1.unity`, duplicate Jonttuli.
    2. Remove `PlayerControllable` and any `SelectionRing`.
    3. Set `characterName` (for example "Aino"), **`Character.ownFaction` = Townsfolk**, and `CharacterStats.hasNeeds = false`. Keep `CombatFormulaConfig` and `harvestingLabelPrefab` assigned; barks need the label prefab.
    4. Add `NpcBrain` (archetype Townsperson), `NpcInteractable` and `Recruitable` (cost: see open question 1; leave it free if undecided).
    5. Clear any starting gear you don't want townsfolk to have. Keep the same layer as the squad so wolves' `detectionMask` sees them.
    6. Drag the result to `Assets/Prefabs/NPC/Townsperson.prefab`, then delete the scene copy.
    7. Make a variant `Townsperson_NoRecruit.prefab` without `Recruitable`.
    - Done when: the prefab has no `PlayerControllable` or `CombatEntityInteractable`, its own faction is Townsfolk, and dropping a fresh instance into an empty test scene gives no validator errors, only "no anchor" warnings.

27. **(Editor) Scene.**
    - Add anchors in `test1` on the NavMesh, near the existing bed (GenericInteractable "Sleep") and the well:
      - an empty GameObject `NpcAnchors`
      - children with `NpcAnchor`: Village (radius 10), Stall, Campfire, and two Bed anchors.
    - Place two Townsperson instances, with each one's `assignedAnchors` set to its own bed. Also place one Townsperson_NoRecruit.
    - For the edge-case play-test, add a fourth instance whose schedule (a copy of the asset, `TownspersonBrokenAnchor.asset`) uses tag `Market`, which no anchor has. Delete it before committing, or keep it disabled.
    - Done when: entering Play mode makes the townsfolk walk to the activity for the current hour (the clock starts at 8:00, which is Wander).

28. **Validator run on `test1`.** Run **Kanshilike > Validate NPC Setup** on `test1`: no errors (the Market warning appears only while the broken-anchor NPC is enabled). Add the `test1` case to `SetupValidatorTests`. Done when it passes.

## Test plan

- **Automated: EditMode** (`Assets/Tests/EditMode`, assembly `Kanshilike.Tests.EditMode`; Rules logic, plus Game and editor code through the existing `Kanshilike.Game` reference and the new `Kanshilike.Game.Editor` reference)
  - `FactionRelationsTests` (table instances with code-defined ids `player`, `wildlife`, `townsfolk`, `traders`, `bandits` and the content stances):
    - each faction Allied with itself.
    - Wildlife vs Player, Townsfolk, Traders and Bandits is Hostile.
    - Bandits vs Player, Townsfolk and Traders is Hostile.
    - Player vs Townsfolk, Player vs Traders and Traders vs Townsfolk are Neutral.
    - symmetry over every pair of the five ids.
    - `Set` is symmetric and overrides the stances (Wildlife vs Player set Neutral is Neutral both ways); `Clear` restores the stance result.
    - an explicit self-entry overrides Allied.
    - two different Hostile-stance factions are Hostile; a Neutral and an Allied stance give Neutral (the more hostile wins).
    - an undefined id behaves as stance Neutral; `Define` overwrites a stance.
    - `None` vs anything, and `None` vs `None`, is Neutral (Unaffiliated).
    - `Explain` returns Same, Explicit, DefaultStance and Unaffiliated in the matching cases.
    - `FactionId`: ordinal and case-sensitive equality, equal hash for equal values, `default` and `""` are `None`.
    - `FactionRelations.AreHostile`, `AreAllied` and `FightsBack` match `Get` (after `Configure` with a test table; `ResetToDefault` in TearDown).
    - `(int)FactionRelation.Hostile == 0`, `Neutral == 1`, `Allied == 2`, guarding serialized data.
  - `FactionRulesTests`: `Resolve` gives Player (Squad) for a squad member even with a community and another own faction; a squad member with `player` None falls through to community or own; community over own (Community); own when no community (Own); everything None gives None.
  - `FactionContentTests`: `Resources/Factions/FactionRelations` loads; `player` has id `player`; `legacyEnumOrder` is [player, wildlife]; its built table gives the defaults listed under `FactionRelationsTests`; every `FactionDefinition` asset in the project has a unique id.
  - `FactionWiringTests` (scene and prefab text, like `HealingWiringTests`): every `Character` block in `test1.unity` and `Iides_map.unity` has a non-null `ownFaction` reference; every `AnimalAI` block in `test1.unity`, `Wolf.prefab` and `Deer.prefab` has a non-null `factionDefinition`; Jonttuli's reference is the Player asset's guid and the wolf's the Wildlife asset's. Passes after step 6.
  - `NpcScheduleRulesTests`:
    - `ResolveIndex`: empty returns -1; a single entry gives index 0 at any hour.
    - With `[6, 9, 17, 21]`:

      | hour | expected index |
      |---|---|
      | 5.9 | 3 (wrap) |
      | 6 | 0 |
      | 8.99 | 0 |
      | 9 | 1 |
      | 23.5 | 3 |
      | 0 | 3 |

    - unsorted `[21, 6]` at 7 returns the index of 6.
    - a duplicate `fromHour` resolves to the lowest index.
    - hour 24 and hour -1 are normalised.
    - `HoursUntilNext`: `[21, 6]` at 22 gives 8, at 5 gives 1; single entry gives 24.
    - `ResolvePlacement`: an anchor activity with no anchor gives AtHome with `RunAtHome` and WanderAtHome with `WanderAtHome`; Patrol-like (min 2) with 1 waypoint gives WanderAtHome; with 2 gives Normal; prerequisites unmet (Follow with no leader) gives WanderAtHome even when `usesAnchor` is false; `usesAnchor` false with prerequisites met gives Normal; `(int)MissingAnchorFallback.RunAtHome == 0`.
  - `NpcBrainRulesTests`:
    - `ShouldReplace` over the priority ladder: Flee beats External; Schedule never beats External; Role beats Social; equal priorities do not replace.
    - `RoleMayReplace`: true for None, Schedule, Social and Role; false for External and Flee.
    - `CurrentPriority` for idle, own order and foreign order.
    - `ShouldFlee`: threshold edges, `fleeFraction` 0, no threat, `maxHealth` 0.
  - `RecruitRulesTests`:
    - each block, in order of precedence (dead and hostile reports Dead).
    - a cost of 0 is free.
    - `carried == cost` passes.
    - `Unwilling` is reported only when every earlier check passes.
    - `(int)RecruitBlock.CantAfford == 5`, `Unwilling == 6`.
    - `Reason` is non-empty for every value.
  - `SetupValidatorTests` (`EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)`, built in code, closed in TearDown):
    - an `NpcBrain` with no archetype, an archetype with no schedule, an entry with no activity, an entry whose tag has no anchor, an `NpcAnchor` with no tag, a Patrol anchor with 1 waypoint, a `Character` with no faction asset, a `Recruitable` with a count and no item: each gives exactly its issue with the right severity and context object.
    - two `FactionDefinition` instances with the same id passed to `FactionSetupCheck` give a duplicate-id error.
    - a correct NPC with an anchor gives no issues.
    - (step 28) `test1.unity` opened additively gives no errors.
- **Automated: PlayMode** (`Assets/Tests/PlayMode`, assembly `Kanshilike.Tests.PlayMode`; code-built characters, no NavMesh; `TestFactions.Install()` in every SetUp)
  - `CharacterBehaviourTests`: all existing tests pass with `TestFactions` (including `FriendlyAttack_DefenderStaysIdle` and `Treating_AHostile_IsRefused`).
  - `CharacterBehaviourTests.Orders_SayWhetherSelfDefenceMayReplaceThem`:
    - `WanderOrder`, `PatrolOrder`, `IdleAtOrder`, `FollowOrder` and `SleepOrder(…, bed)` yield.
    - `FleeOrder` does not.
  - `FactionResolutionTests`:
    - `LegacyField_ResolvesThroughLegacyOrder`: `ownFaction` null and `legacyFaction` 0 gives Player; 1 gives Wildlife; 7 gives None and logs one warning.
    - `OwnFactionAsset_WinsOverLegacy`.
    - `SquadMember_IsPlayer`: own Townsfolk plus an added `PlayerControllable` gives Player (Squad); destroying the component gives Townsfolk again after one frame.
    - `SetOwnFaction_ChangesFactionAndRaisesEvent`.
    - `CommunitySource_UsedAndInvalidated`: a stub `ICommunityFactionSource` returning Traders makes a non-squad character Traders (Community); after the stub changes its answer, `InvalidateAll` makes the next read return the new value; setting `CommunitySource = null` returns the own faction.
    - `NewFactionAsset_WorksWithoutCatalog`: a definition created in code with stance Hostile and not in the catalog makes `FactionRelations.AreHostile` true against Townsfolk.
    - `AttitudeProvider_OverridesDefence`: a stub provider that returns Allied for one pair makes the defender stay idle when attacked by a Wildlife character; with the provider removed it fights back.
  - `NpcBrainTests`:
    - `Brain_DoesNotChangeFaction`: an NPC with own faction Townsfolk is still Townsfolk after `Awake`.
    - `Brain_IdleNpc_GetsScheduleOrderForCurrentHour`: TimeManager `startTime` 10, schedule `[6 Wander, 9 IdleAt Stall]` with a matching anchor; the order is `IdleAtOrder`.
    - `Brain_HourChange_SwitchesScheduleOrder`: set `currentTime` past the next entry; the order changes type.
    - `Brain_TimeJumpOverSeveralEntries_UsesCurrentEntryOnly`: jump from 8 to 22 in one frame; the order is the 22:00 entry's, issued once.
    - `Brain_MissingAnchor_FallsBackAndKeepsRunning`: IdleAt with a tag no anchor has gives an IdleAtOrder at home and the warning is logged once. Use `LogAssert.Expect`.
    - `Brain_NullActivity_WandersAtHome_WarnsOnce`.
    - `Brain_CustomActivityClass_IsUsed`: a test-only `NpcActivity` subclass (in the test assembly) returning a marker order is run by the brain with no brain change.
    - `Brain_DoesNotOverrideSelfDefence`: a hostile (Wildlife) character attacks a Townsfolk NPC at full health. The NPC's order becomes AttackOrder and stays one across several think ticks.
    - `Brain_FleesBelowThreshold`: set NPC health to 30/100 and `fleeHealthFraction` to 0.35; the attacker hits; the order becomes FleeOrder.
    - `Brain_ResumesScheduleAfterFightEnds`: kill the attacker; within 1 s the order is a schedule order again.
    - `SleepingNpc_WakesAndDefends`: the Sleep entry is active and the order is SleepOrder; an attack makes `IsSleeping` false and the order an AttackOrder.
    - `NeutralAttacked_FightsBack`: a Player character attacks a Townsfolk NPC, and the NPC fights back. Proves the `FightsBack` change.
    - `NpcDies_MidOrder_BrainStops`: kill the NPC during a WanderOrder. `CurrentOrder` is null, the brain is disabled, a Corpse exists, and no new order arrives over the next 0.5 s.
    - `Role_StillWantsFalse_ReturnsToSchedule`: a test role that issues an IdleAtOrder and then returns false from `StillWants`. Within 2 ticks the order is a schedule order.
    - `Role_CanReplaceItsOwnRoleOrder`: the test role returns a different order on the second tick, and it replaces the first.
  - `NpcDebugReportTests`: a Townsfolk NPC's report has a Faction section containing "Townsfolk" and "own faction", an Attitude line for each live squad member, and a Brain section naming the current order and schedule entry; a squad member's Faction section says "squad"; a provider added to `Providers` appears in the report (removed in TearDown).
  - `NpcInteractionTests`:
    - `Options_NeutralWithRecruitable_TalkRecruitAttackInOrder`.
    - `Options_HostileNpc_OnlyAttack`: an NPC with own faction Bandits against a Player interactor.
    - `Options_DeadNpc_None`.
    - `Options_SourceComponent_AddsOptionBeforeAttack`: a test `INpcInteractionSource` adds `"test"`; the order is Talk, Recruit, test, Attack, and executing `"test"` reaches the source.
    - `Talk_ShowsBarkAndHoldsNpc`: the order becomes an IdleAtOrder at Social priority. If a label prefab is needed, assert on the order only.
    - `Talk_HigherPriorityTalkSourceWins_NoHoldWhenLineSaysSo`: a test `INpcTalkSource` at priority 10 with `Hold = false`; the order is not an IdleAtOrder.
    - `Recruit_JoinsSquad`:
      - `PlayerControllable.Joined` fires.
      - `PlayerControllable.All` contains the NPC.
      - its faction is Player, with source Squad, and its own faction is Player.
      - the brain is disabled and the brain's order has ended.
      - `hasNeeds` is true.
      - `NpcInteractable` and `Recruitable` are gone after one frame.
    - `Recruit_WhileInCombat_Refused`: the NPC is fighting a wolf; `TryRecruit` returns false and the faction is unchanged.
    - `Recruit_CostTakenOnlyOnSuccess`: costItem ×2. With one carried it is refused and still holds one; with two it succeeds and holds zero.
    - `Recruit_TooFar_Refused`.
    - `Recruit_ProviderRefusesSquad_Unwilling`: a stub `IAttitudeProvider` whose `RefusesSquad` is true; `TryRecruit` fails with "Won't share a fire with your people".
    - Clean up: destroy the created objects in TearDown so `PlayerControllable.All` does not leak between tests. It removes itself in OnDisable. Reset `Attitude.Provider` and `CharacterFaction.CommunitySource` to null in TearDown.
- **Manual play-test** in `Assets/Scenes/test1.unity`:
  - [ ] After the migration: Jonttuli and Keppana show Own Faction = Player, the wolf Faction Definition = Wildlife; the wolf still hunts the squad, and the squad fights back.
  - [ ] At 8:00 the townsfolk wander around the Village anchor and pause between walks.
  - [ ] Speed time up (raise `TimeManager.timeMultiplier`). At 9:00 they walk to the stall and face its forward direction, at 17:00 they go to the campfire, and at 21:00 each walks to its own bed and sleeps (label "Sleeping...").
  - [ ] Set `currentTime` from 8 to 23 in the inspector during play. They go straight to bed and do not run the stall or campfire entries on the way.
  - [ ] Townsperson with the `Market` (missing) tag: one warning in the console, and it idles or wanders at home instead of freezing. The validator lists the same entry as a warning.
  - [ ] Wolves now attack townsfolk. A townsperson fights back, and below about 35% health runs away. If cornered, it fights again.
  - [ ] Attack a sleeping townsperson. It wakes and fights back.
  - [ ] Right-click the wolf: an immediate attack, with no menu (regression check).
  - [ ] Right-click a townsperson: a menu with Talk, Recruit and Attack. Escape or clicking elsewhere closes it.
  - [ ] Talk: one squad member walks up even while the NPC wanders. The NPC stops, faces them and shows a bark line, then goes back to its routine.
  - [ ] Recruit while a wolf is attacking the townsperson: "Too busy fighting" appears and nothing changes.
  - [ ] Recruit (with a cost set: without the item you see "Can't pay"; with it the item is used up). The NPC shows an accept line and appears in the squad bar. It can be selected, moved and given inventory items, and it no longer follows its schedule. The NPC Debugger shows "Player (squad member)".
  - [ ] Attack a townsperson with the whole squad selected. Everyone attacks and the townsperson fights back. Kill it; right-click the body and it offers Loot, not Talk.
  - [ ] A wounded townsperson: right-click opens the menu and does not start wound treatment.
  - [ ] A wounded squad member: right-click still binds wounds (regression check).
  - [ ] Townsperson_NoRecruit offers only Talk and Attack.
  - [ ] NPC Debugger (Kanshilike > NPC Debugger): select a townsperson in the Hierarchy during play. It shows "Townsfolk (own faction)", Neutral toward Jonttuli and Keppana, its current order, schedule entry and anchor; it updates when the hour changes and when the NPC flees. With nothing selected in the editor it follows the selected squad member.
  - [ ] New faction with no code change: create a FactionDefinition "Hiidenväki" with default stance Hostile, assign it as a townsperson's own faction in play. The debugger shows Hostile toward the squad, right-click offers only Attack, and a wolf now attacks it on sight. Set it back to Townsfolk: Talk and Recruit return.
  - [ ] Add a relations entry Townsfolk–Player = Allied in `FactionRelations.asset` (then remove it): after re-entering Play mode, a wounded townsperson can have wounds bound by right-click, and a townsperson hit by the squad does not fight back.
  - [ ] Kanshilike > Validate NPC Setup on `test1`: no errors. Clear a townsperson's archetype: one error that pings the object. Undo.
  - [ ] Drop a fresh Townsperson prefab into the scene, set only its archetype and bed anchor: it follows the schedule in play with no other setup.

## Out of scope
- Bandits: BanditRole, groups, camps, raids, perception extraction (`FindNearestHostile` and moving AnimalAI onto `ActorPerception`), the `aggression` behaviour.
- Relationships, kinship, communities, grudges and blood price: `docs/plans/feuds-and-kinship.md`.
- Traders: trade UI and barter (goods for goods; there is no money in this setting).
- Player reputation per faction, and faction-wide reactions such as the whole village turning hostile after the player attacks a townsperson or guards responding.
- Dialogue trees, quests, voice, and NPC names generated at runtime.
- Bed and stall reservation (two NPCs picking the same unassigned bed); assign beds explicitly for now.
- NPC needs (eating, drinking) while not recruited; they have `hasNeeds = false`. "Work" and "eat" activities (the activity assets make them a later addition with no brain change).
- Dismissing a recruit back to NPC life; a squad size cap.
- Running speed while fleeing (`Character.Update` resets `agent.speed` every frame).
- Townsfolk fighting or hunting animals on their own initiative, or helping each other in fights.
- A Treat wounds option for neutral NPCs.
- NPC spawning, saving and loading of NPC state.
- An in-game debug overlay for players; the debugger is an editor window.
- Removing the legacy `legacyFaction` int fields, once every scene and prefab has been migrated.
- **Multi-scene worlds:** `NpcAnchor.All`, `FactionRelations`, `CharacterFaction` and `Attitude.Provider` are static, global registries, which is fine for one loaded scene. Additive or streamed multi-scene worlds will need them scoped per world or scene.

## Open questions
1. **Recruit cost.** There is no currency. Options:
   - (a) free for the demo, with only the non-hostile and not-in-combat checks.
   - (b) an item cost from existing content, for example a food item ×N, which makes recruiting a survival trade-off (one more mouth to feed, paid in food).
   - (c) a new barter good such as furs or livestock.
   - The plan supports all three through `Recruitable.costItem/costCount`. Which should the demo townsperson use? Recruit cost stays an item and a count either way; it could later be paid in valuables by worth like the blood price in the feuds plan.
2. **Roadmap.** `docs/PLAN.md` and `docs/architecture.md` are missing. Are human NPCs (phases 1-3) confirmed for the vertical slice demo? Is there an architecture order this should wait for?
3. **Neutrals fight back.** The plan makes Neutral NPCs, and the player's squad, fight back when attacked by a neutral (the `FightsBack` change). Confirm this is the intended meaning of Neutral rather than "flee only".
