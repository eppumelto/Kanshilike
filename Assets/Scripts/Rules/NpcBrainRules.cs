/// <summary>When an NPC brain may replace what the character is doing. Pure.</summary>
public static class NpcBrainRules
{
	public static bool ShouldReplace(NpcPriority current, NpcPriority incoming) => incoming > current;

	/// <summary>A role may replace anything up to and including a role order (switch from attacking to hunting).</summary>
	public static bool RoleMayReplace(NpcPriority current) => current <= NpcPriority.Role;

	/// <summary>Idle is None; the brain's own order keeps its priority; anything else (self-defence) is External.</summary>
	public static NpcPriority CurrentPriority(bool idle, bool isOwnOrder, NpcPriority ownPriority)
	{
		if (idle)
			return NpcPriority.None;
		return isOwnOrder ? ownPriority : NpcPriority.External;
	}

	public static bool ShouldFlee(float health, float maxHealth, float fleeFraction, bool hasLivingThreat)
	{
		if (!hasLivingThreat || fleeFraction <= 0f || maxHealth <= 0f)
			return false;
		return health / maxHealth < fleeFraction;
	}
}
