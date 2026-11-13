using System.Collections.Generic;
using UnityEngine;

/// <summary>What the brain last decided, for the debug view.</summary>
public readonly struct NpcBrainDiagnostics
{
	public readonly NpcPriority CurrentPriority;
	public readonly NpcPriority IssuedPriority;
	public readonly CharacterOrder IssuedOrder;
	public readonly INpcRole IssuedRole;
	public readonly float Hour;
	public readonly bool HasClock;
	public readonly int EntryIndex;
	public readonly NpcActivity Activity;
	public readonly NpcAnchorTag AnchorTag;
	public readonly NpcAnchor Anchor;
	public readonly ActivityPlacement Placement;
	public readonly CombatActor Threat;
	public readonly float ThreatSecondsLeft;

	public NpcBrainDiagnostics(NpcPriority currentPriority, NpcPriority issuedPriority, CharacterOrder issuedOrder, INpcRole issuedRole,
		float hour, bool hasClock, int entryIndex, NpcActivity activity, NpcAnchorTag anchorTag, NpcAnchor anchor,
		ActivityPlacement placement, CombatActor threat, float threatSecondsLeft)
	{
		CurrentPriority = currentPriority;
		IssuedPriority = issuedPriority;
		IssuedOrder = issuedOrder;
		IssuedRole = issuedRole;
		Hour = hour;
		HasClock = hasClock;
		EntryIndex = entryIndex;
		Activity = activity;
		AnchorTag = anchorTag;
		Anchor = anchor;
		Placement = placement;
		Threat = threat;
		ThreatSecondsLeft = threatSecondsLeft;
	}
}

/// <summary>
/// Runs an NPC's day: follows its schedule, lets roles react, and runs from danger when badly hurt.
/// It only gives orders (Character.GiveOrder); fighting stays with the character's self-defence, and
/// it never changes the faction. Higher priority replaces lower, so a routine never cuts a fight short.
/// </summary>
[RequireComponent(typeof(Character))]
public class NpcBrain : MonoBehaviour
{
	private const float NoClockHour = 12f;

	[Tooltip("Tuning and schedule for this kind of NPC.")]
	[SerializeField] private NpcArchetype archetype;
	[Tooltip("This NPC's own places (its bed, its stall). Used before searching the scene for a tag.")]
	[SerializeField] private NpcAnchor[] assignedAnchors = new NpcAnchor[0];
	[Tooltip("Who to follow for a Follow activity.")]
	[SerializeField] private Character leader;

	private Character character;
	private CharacterStats stats;
	private INpcRole[] roles = new INpcRole[0];
	private Vector3 homePosition;
	private TimeManager time;

	private CharacterOrder issuedOrder;
	private NpcPriority issuedPriority;
	private int issuedEntryIndex = -1;
	private INpcRole issuedRole;

	private CombatActor threat;
	private float threatTime = float.NegativeInfinity;
	private float nextThink;
	private readonly HashSet<int> warnedMissing = new HashSet<int>();

	private int lastEntryIndex = -1;
	private NpcAnchor lastAnchor;
	private ActivityPlacement lastPlacement;

	public NpcArchetype Archetype => archetype;
	public Vector3 HomePosition => homePosition;
	public Character Character => character;
	public Character Leader => leader;
	public IReadOnlyList<NpcAnchor> AssignedAnchors => assignedAnchors ?? new NpcAnchor[0];

	/// <summary>Fighting, fleeing, or recently attacked by someone still alive.</summary>
	public bool InCombat
	{
		get
		{
			CharacterOrder current = character != null ? character.CurrentOrder : null;
			if (current is AttackOrder || current is FleeOrder)
				return true;
			return HasLivingThreat;
		}
	}

	public bool IsRoleOrder(CharacterOrder order) => order != null && order == issuedOrder && issuedRole != null;

	public NpcBrainDiagnostics Diagnostics
	{
		get
		{
			NpcSchedule.Entry entry = archetype != null && archetype.schedule != null ? archetype.schedule.Get(lastEntryIndex) : null;
			float memory = archetype != null ? archetype.threatMemorySeconds : 0f;
			float left = threat != null ? Mathf.Max(0f, threatTime + memory - Time.time) : 0f;
			return new NpcBrainDiagnostics(CurrentPriority(), issuedPriority, issuedOrder, issuedRole, CurrentHour(), time != null,
				lastEntryIndex, entry?.activity, entry?.anchorTag, lastAnchor, lastPlacement, threat, left);
		}
	}

	private void Awake()
	{
		character = GetComponent<Character>();
		stats = GetComponent<CharacterStats>();
		roles = GetComponents<INpcRole>();
		homePosition = transform.position;

		if (archetype == null)
		{
			Debug.LogWarning($"[NpcBrain] '{name}' has no archetype. Disabling.", this);
			enabled = false;
		}
	}

	private void OnEnable()
	{
		if (archetype == null)
		{
			enabled = false;
			return;
		}

		time = FindFirstObjectByType<TimeManager>();
		if (stats != null)
		{
			stats.OnDamaged += HandleDamaged;
			stats.OnDied += HandleDied;
		}
		nextThink = 0f;
	}

	private void OnDisable()
	{
		if (stats != null)
		{
			stats.OnDamaged -= HandleDamaged;
			stats.OnDied -= HandleDied;
		}

		if (character != null && issuedOrder != null && character.CurrentOrder == issuedOrder)
			character.GiveOrder(null);
		ClearIssued();
	}

	private void Update()
	{
		if (Time.time < nextThink)
			return;
		nextThink = Time.time + archetype.thinkIntervalSeconds;
		Think();
	}

	private void Think()
	{
		if (!character.IsAlive)
		{
			enabled = false;
			return;
		}

		if (TryFlee())
			return;

		NpcPriority cur = CurrentPriority();

		// A role that no longer wants its order hands control back to the schedule.
		if (cur == NpcPriority.Role && issuedRole != null && !issuedRole.StillWants(this, issuedOrder))
		{
			character.GiveOrder(null);
			ClearIssued();
			cur = NpcPriority.None;
		}

		foreach (INpcRole role in roles)
		{
			if (role == null || !NpcBrainRules.RoleMayReplace(cur))
				continue;
			if (role.TryGetReaction(this, out CharacterOrder reaction) && reaction != null)
			{
				Issue(reaction, NpcPriority.Role, -1, role);
				return;
			}
		}

		ThinkSchedule(cur);
	}

	private void ThinkSchedule(NpcPriority cur)
	{
		NpcSchedule schedule = archetype.schedule;
		if (schedule == null || schedule.entries.Count == 0)
		{
			if (cur == NpcPriority.None)
				Issue(WanderActivity.AtHome(this), NpcPriority.Schedule, -1, null);
			return;
		}

		int index = schedule.ResolveIndex(CurrentHour());
		if (cur == NpcPriority.None || (cur == NpcPriority.Schedule && index != issuedEntryIndex))
			Issue(BuildScheduleOrder(index), NpcPriority.Schedule, index, null);
	}

	/// <summary>Stands still facing someone for a while (after talking). Does nothing while fighting or busy with something more important.</summary>
	public void HoldFor(float seconds, Transform lookAt)
	{
		if (!enabled || !NpcBrainRules.ShouldReplace(CurrentPriority(), NpcPriority.Social))
			return;
		Issue(new IdleAtOrder(transform.position, null, seconds, lookAt), NpcPriority.Social, -1, null);
	}

	private NpcPriority CurrentPriority()
	{
		CharacterOrder current = character != null ? character.CurrentOrder : null;
		return NpcBrainRules.CurrentPriority(current == null, current != null && current == issuedOrder, issuedPriority);
	}

	private float CurrentHour() => time != null ? time.currentTime : NoClockHour;

	private bool HasLivingThreat =>
		threat != null && threat.IsAlive && archetype != null && Time.time - threatTime <= archetype.threatMemorySeconds;

	private void HandleDamaged(float amount, GameObject source)
	{
		CombatActor attacker = source != null ? source.GetComponentInParent<CombatActor>() : null;
		if (attacker != null && attacker != character && attacker.IsAlive)
		{
			threat = attacker;
			threatTime = Time.time;
		}
		if (enabled && character.IsAlive)
			TryFlee();
	}

	private void HandleDied(GameObject killer) => enabled = false;

	private bool TryFlee()
	{
		if (character.CurrentOrder is FleeOrder || stats == null)
			return false;
		if (!NpcBrainRules.ShouldFlee(stats.currentHealth, stats.maxHealth, archetype.fleeHealthFraction, HasLivingThreat))
			return false;

		Issue(new FleeOrder(threat.transform, archetype.fleeDistance), NpcPriority.Flee, -1, null);
		return true;
	}

	private CharacterOrder BuildScheduleOrder(int index)
	{
		NpcSchedule schedule = archetype.schedule;
		NpcSchedule.Entry entry = schedule.Get(index);
		lastEntryIndex = index;
		lastAnchor = null;

		NpcActivity activity = entry != null ? entry.activity : null;
		if (activity == null)
		{
			lastPlacement = ActivityPlacement.WanderAtHome;
			if (warnedMissing.Add(index))
				Debug.LogWarning($"[NpcBrain] '{name}': schedule entry {index} has no activity. Wandering at home.", this);
			return WanderActivity.AtHome(this);
		}

		NpcAnchor anchor = null;
		if (activity.usesAnchor && entry.anchorTag != null)
			anchor = FindAnchor(entry.anchorTag);

		int waypointCount = anchor != null ? anchor.GetWaypoints().Count : 0;
		ActivityPlacement placement = NpcScheduleRules.ResolvePlacement(activity.usesAnchor, anchor != null, waypointCount,
			activity.minWaypoints, activity.PrerequisitesMet(this), activity.missingAnchorFallback);
		lastPlacement = placement;
		lastAnchor = placement == ActivityPlacement.Normal ? anchor : null;

		if (activity.usesAnchor && entry.anchorTag != null && anchor == null && warnedMissing.Add(index))
			Debug.LogWarning($"[NpcBrain] '{name}': no anchor tagged '{entry.anchorTag.DisplayName}' for schedule entry {index}. Running it at home.", this);

		if (placement == ActivityPlacement.WanderAtHome)
			return WanderActivity.AtHome(this);

		float hoursUntilNext = time != null ? schedule.HoursUntilNext(CurrentHour()) : -1f;
		NpcActivityContext ctx = placement == ActivityPlacement.Normal && anchor != null
			? new NpcActivityContext(this, anchor, anchor.Position, anchor.Facing, anchor.radius, anchor.GetWaypoints(), hoursUntilNext)
			: new NpcActivityContext(this, null, homePosition, null, archetype.wanderRadius, new Vector3[0], hoursUntilNext);
		return activity.CreateOrder(ctx) ?? WanderActivity.AtHome(this);
	}

	private NpcAnchor FindAnchor(NpcAnchorTag tag)
	{
		if (assignedAnchors != null)
			foreach (NpcAnchor a in assignedAnchors)
				if (a != null && a.isActiveAndEnabled && a.anchorTag == tag)
					return a;
		return NpcAnchor.FindNearest(tag, transform.position, archetype.anchorSearchRadius);
	}

	private void Issue(CharacterOrder order, NpcPriority priority, int entryIndex, INpcRole role)
	{
		issuedOrder = order;
		issuedPriority = priority;
		issuedEntryIndex = entryIndex;
		issuedRole = role;
		character.GiveOrder(order);
	}

	private void ClearIssued()
	{
		issuedOrder = null;
		issuedPriority = NpcPriority.None;
		issuedEntryIndex = -1;
		issuedRole = null;
	}
}
