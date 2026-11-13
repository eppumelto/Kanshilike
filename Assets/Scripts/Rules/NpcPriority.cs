/// <summary>How much an NPC order matters; higher replaces lower. Serialized: never renumber.</summary>
public enum NpcPriority
{
	/// <summary>Idle: no order.</summary>
	None = 0,
	/// <summary>The daily routine.</summary>
	Schedule = 10,
	/// <summary>A short pause to talk.</summary>
	Social = 20,
	/// <summary>A role's reaction (revenge, later bandit raids).</summary>
	Role = 30,
	/// <summary>Anything the brain didn't give, such as self-defence.</summary>
	External = 40,
	/// <summary>Running for its life.</summary>
	Flee = 50
}

/// <summary>Where a schedule activity runs. Serialized: never renumber.</summary>
public enum ActivityPlacement
{
	/// <summary>At the anchor the entry names.</summary>
	Normal = 0,
	/// <summary>The same activity, at the NPC's home position.</summary>
	AtHome = 1,
	/// <summary>Wandering around home instead.</summary>
	WanderAtHome = 2
}

/// <summary>What an activity does when its anchor is missing. Serialized: never renumber.</summary>
public enum MissingAnchorFallback
{
	RunAtHome = 0,
	WanderAtHome = 1
}
