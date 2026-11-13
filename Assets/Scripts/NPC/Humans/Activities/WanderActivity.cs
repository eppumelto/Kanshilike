using UnityEngine;

/// <summary>Stroll around the anchor within its radius.</summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Activities/Wander", fileName = "Wander")]
public class WanderActivity : NpcActivity
{
	public override CharacterOrder CreateOrder(in NpcActivityContext ctx)
	{
		NpcArchetype a = ctx.Archetype;
		return new WanderOrder(ctx.Position, ctx.Radius, a != null ? a.wanderPauseSeconds : new Vector2(2f, 6f));
	}

	/// <summary>Wander around the NPC's home: the fallback when nothing else fits.</summary>
	public static CharacterOrder AtHome(NpcBrain brain)
	{
		NpcArchetype a = brain.Archetype;
		return new WanderOrder(brain.HomePosition, a != null ? a.wanderRadius : 8f, a != null ? a.wanderPauseSeconds : new Vector2(2f, 6f));
	}
}
