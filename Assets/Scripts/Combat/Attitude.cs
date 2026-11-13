using UnityEngine;

/// <summary>Overrides how one actor regards another (installed by the social system).</summary>
public interface IAttitudeProvider
{
	/// <summary>Returns true and a result to override the faction relation for this pair.</summary>
	bool TryExplain(CombatActor self, CombatActor other, FactionRelation factionRelation, out AttitudeResult result);

	/// <summary>Whether self refuses to deal with anyone in the player's squad.</summary>
	bool RefusesSquad(CombatActor self);
}

/// <summary>
/// How one actor regards another. Callers that decide fighting back, talking or recruiting ask
/// this rather than the faction table, so personal feelings can be added later without changing them.
/// Directional: Of(a, b) is how a regards b.
/// </summary>
public static class Attitude
{
	public static IAttitudeProvider Provider { get; set; }

	public static AttitudeResult Explain(CombatActor self, CombatActor other)
	{
		FactionRelation relation = self != null && other != null
			? FactionRelations.Get(self.Faction, other.Faction)
			: FactionRelation.Neutral;

		if (Provider != null && self != null && other != null
			&& Provider.TryExplain(self, other, relation, out AttitudeResult overridden))
			return overridden;

		return new AttitudeResult(relation, AttitudeReason.Faction);
	}

	public static FactionRelation Of(CombatActor self, CombatActor other) => Explain(self, other).Relation;

	/// <summary>Anyone not allied fights back when attacked.</summary>
	public static bool FightsBack(CombatActor self, CombatActor other) => Of(self, other) != FactionRelation.Allied;

	public static bool RefusesDealings(CombatActor self, CombatActor other) => Explain(self, other).RefusesDealings;

	public static bool RefusesSquad(CombatActor self) => Provider != null && self != null && Provider.RefusesSquad(self);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics() => Provider = null;
}
