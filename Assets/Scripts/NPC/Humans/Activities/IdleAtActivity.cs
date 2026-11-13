using UnityEngine;

/// <summary>Stand at the anchor, facing the way it faces.</summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Activities/Idle At", fileName = "IdleAt")]
public class IdleAtActivity : NpcActivity
{
	public override CharacterOrder CreateOrder(in NpcActivityContext ctx) => new IdleAtOrder(ctx.Position, ctx.Facing);
}
