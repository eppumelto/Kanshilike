/// <summary>Where a character's faction came from. Serialized by number: only append.</summary>
public enum FactionSource
{
	Own = 0,
	Squad = 1,
	Community = 2
}

public readonly struct FactionResolution
{
	public readonly FactionId Id;
	public readonly FactionSource Source;

	public FactionResolution(FactionId id, FactionSource source)
	{
		Id = id;
		Source = source;
	}
}

public static class FactionRules
{
	/// <summary>
	/// The one rule for a character's faction: squad members are the player's faction, then a
	/// community's faction, then the character's own.
	/// </summary>
	public static FactionResolution Resolve(bool inSquad, FactionId community, FactionId own, FactionId player)
	{
		if (inSquad && !player.IsNone)
			return new FactionResolution(player, FactionSource.Squad);
		if (!community.IsNone)
			return new FactionResolution(community, FactionSource.Community);
		return new FactionResolution(own, FactionSource.Own);
	}
}
