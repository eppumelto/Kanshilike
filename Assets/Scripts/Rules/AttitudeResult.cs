/// <summary>Why someone regards another the way they do. Append only (the feuds plan adds reasons).</summary>
public enum AttitudeReason
{
	Faction = 0,
	/// <summary>Personal opinion crossed the hostile or allied line.</summary>
	Opinion = 1,
	/// <summary>A grudge of the Hostile tier or above.</summary>
	Grudge = 2,
	/// <summary>Close kin are never hostile over faction alone.</summary>
	Kin = 3
}

/// <summary>How one actor regards another: the relation, why, and whether they refuse to deal with them.</summary>
public readonly struct AttitudeResult
{
	public readonly FactionRelation Relation;
	public readonly AttitudeReason Reason;
	/// <summary>Refuses small talk, recruiting and (later) trade, without necessarily attacking.</summary>
	public readonly bool RefusesDealings;

	public AttitudeResult(FactionRelation relation, AttitudeReason reason, bool refusesDealings = false)
	{
		Relation = relation;
		Reason = reason;
		RefusesDealings = refusesDealings;
	}
}
