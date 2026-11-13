/// <summary>
/// Personal feelings (opinion, grudges, close kin) over faction relations. Installed in
/// Attitude.Provider by SocialWorld. Animals and squad members defer to plain faction relations:
/// squad members' grudges are recorded but never change who they fight.
/// </summary>
public sealed class SocialAttitudeProvider : IAttitudeProvider
{
	public bool TryExplain(CombatActor self, CombatActor other, FactionRelation factionRelation, out AttitudeResult result)
	{
		result = default;
		if (!Social.Active || !(self is Character a) || !(other is Character b) || Social.InSquad(a))
			return false;
		result = Social.Sim.AttitudeOf(a.PersonId, b.PersonId, factionRelation);
		return true;
	}

	public bool RefusesSquad(CombatActor self)
	{
		if (!Social.Active || !(self is Character c) || Social.InSquad(c))
			return false;
		return Social.Sim.HoldsGrudgeAgainstSquad(c.PersonId, SocialSimulation.PlayerSquadId, Social.Tuning.minGrudgeIntensity);
	}
}

/// <summary>Gives a person their registered community's faction. Installed by SocialWorld.</summary>
public sealed class SocialCommunityFactionSource : ICommunityFactionSource
{
	public bool TryGetCommunityFaction(Character character, out FactionId faction, out string communityName)
	{
		faction = FactionId.None;
		communityName = null;
		if (character == null || Social.Sim == null || !Social.Sim.Registry.TryGet(character.PersonId, out PersonRecord r)
			|| !Social.Sim.Communities.TryGet(r.CommunityId, out CommunityRecord community) || community.Faction.IsNone)
			return false;
		faction = community.Faction;
		communityName = community.Name;
		return true;
	}
}
