using UnityEngine;

/// <summary>Walk the anchor's waypoints in a loop. Needs at least two; otherwise wanders at home.</summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Activities/Patrol", fileName = "Patrol")]
public class PatrolActivity : NpcActivity
{
	// Defaults for new assets and CreateInstance; saved values override them.
	public PatrolActivity()
	{
		minWaypoints = 2;
		missingAnchorFallback = MissingAnchorFallback.WanderAtHome;
	}

	public override CharacterOrder CreateOrder(in NpcActivityContext ctx)
	{
		NpcArchetype a = ctx.Archetype;
		return new PatrolOrder(ctx.Waypoints, a != null ? a.patrolPauseSeconds : 3f);
	}
}
