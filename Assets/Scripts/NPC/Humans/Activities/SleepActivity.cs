using UnityEngine;

/// <summary>Walk to the bed and sleep until the next schedule entry. With no clock, just stand there.</summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Activities/Sleep", fileName = "Sleep")]
public class SleepActivity : NpcActivity
{
	private const float MinHours = 0.1f;

	public override CharacterOrder CreateOrder(in NpcActivityContext ctx)
	{
		if (ctx.HoursUntilNext < MinHours)
			return new IdleAtOrder(ctx.Position, ctx.Facing);
		return new SleepOrder(ctx.HoursUntilNext, ctx.Position);
	}
}
