using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything an activity needs to build its order.</summary>
public readonly struct NpcActivityContext
{
	public readonly NpcBrain Brain;
	/// <summary>The anchor in use, or null when running at home.</summary>
	public readonly NpcAnchor Anchor;
	public readonly Vector3 Position;
	public readonly Quaternion? Facing;
	/// <summary>Wander radius: the anchor's, or the archetype's at home.</summary>
	public readonly float Radius;
	public readonly IReadOnlyList<Vector3> Waypoints;
	/// <summary>Hours until the next schedule entry, or -1 with no clock.</summary>
	public readonly float HoursUntilNext;

	public NpcActivityContext(NpcBrain brain, NpcAnchor anchor, Vector3 position, Quaternion? facing, float radius,
		IReadOnlyList<Vector3> waypoints, float hoursUntilNext)
	{
		Brain = brain;
		Anchor = anchor;
		Position = position;
		Facing = facing;
		Radius = radius;
		Waypoints = waypoints;
		HoursUntilNext = hoursUntilNext;
	}

	public NpcArchetype Archetype => Brain != null ? Brain.Archetype : null;
}

/// <summary>
/// One thing an NPC can do on its schedule. New activities ("work", "eat") are new subclasses plus
/// an asset; the brain and schedule don't change.
/// </summary>
public abstract class NpcActivity : ScriptableObject
{
	[Tooltip("Whether this activity happens at an anchor named by the schedule entry's tag.")]
	public bool usesAnchor = true;
	[Tooltip("Waypoints the anchor needs (Patrol: 2). Fewer counts as a missing anchor.")]
	[Min(0)] public int minWaypoints;
	[Tooltip("What to do when the anchor is missing: the same activity at home, or wander around home.")]
	public MissingAnchorFallback missingAnchorFallback = MissingAnchorFallback.RunAtHome;

	/// <summary>False makes the NPC wander at home instead (Follow with no leader).</summary>
	public virtual bool PrerequisitesMet(NpcBrain brain) => true;

	public abstract CharacterOrder CreateOrder(in NpcActivityContext ctx);
}
