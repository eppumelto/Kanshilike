/// <summary>Who an actor sides with. Add a value here for each new group (bandits, traders...).</summary>
public enum Faction
{
	Player,
	Wildlife
}

public static class FactionRelations
{
	/// <summary>
	/// Whether these factions treat each other as threats. For now that is simply any other
	/// faction. Replace this with a relations table once factions can be neutral or allied.
	/// </summary>
	public static bool AreHostile(Faction a, Faction b) => a != b;
}
