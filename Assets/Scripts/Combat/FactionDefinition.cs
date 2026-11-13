using UnityEngine;

/// <summary>
/// One faction: the player, wildlife, a village, a bandit band, a trading house. Adding a faction
/// is creating one of these; relations to other factions come from its default stance unless the
/// FactionRelations asset lists the pair.
/// </summary>
[CreateAssetMenu(menuName = "Kanshilike/Factions/Faction", fileName = "NewFaction")]
public class FactionDefinition : DefinitionAsset
{
	[Tooltip("How this faction treats any faction it has no entry with in the relations asset. Wildlife and bandits: Hostile.")]
	[SerializeField] private FactionRelation defaultStance = FactionRelation.Neutral;

	public FactionRelation DefaultStance => defaultStance;
	public FactionId FactionId => new FactionId(Id);

	/// <summary>Makes a faction in code (tests, or factions founded during play). The asset name becomes its id.</summary>
	public static FactionDefinition Create(string name, FactionRelation defaultStance)
	{
		var definition = CreateInstance<FactionDefinition>();
		definition.name = name;
		definition.defaultStance = defaultStance;
		return definition;
	}
}
