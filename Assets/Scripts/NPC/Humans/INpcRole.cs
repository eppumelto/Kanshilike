/// <summary>
/// A behaviour that can take over an NPC above its routine (revenge, later bandit raids). Put it on
/// the same GameObject as the NpcBrain; the brain asks it every think tick.
/// </summary>
public interface INpcRole
{
	/// <summary>
	/// Return true with an order to react at Role priority. Return false when your current order
	/// already does the job, or the brain would restart it every tick.
	/// </summary>
	bool TryGetReaction(NpcBrain brain, out CharacterOrder order);

	/// <summary>Asked each think while this role's order runs. Return false to hand control back to the schedule.</summary>
	bool StillWants(NpcBrain brain, CharacterOrder order);
}
