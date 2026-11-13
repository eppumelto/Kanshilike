using UnityEngine;

/// <summary>Follow the brain's leader. With no living leader, wanders at home.</summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Activities/Follow", fileName = "Follow")]
public class FollowActivity : NpcActivity
{
	// Defaults for new assets and CreateInstance; saved values override them.
	public FollowActivity()
	{
		usesAnchor = false;
		missingAnchorFallback = MissingAnchorFallback.WanderAtHome;
	}

	public override bool PrerequisitesMet(NpcBrain brain) => brain != null && brain.Leader != null && brain.Leader.IsAlive;

	public override CharacterOrder CreateOrder(in NpcActivityContext ctx) => new FollowOrder(ctx.Brain.Leader);
}
