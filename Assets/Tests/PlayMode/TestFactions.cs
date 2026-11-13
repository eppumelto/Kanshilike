using UnityEngine;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>
	/// The shipped factions made in code, so PlayMode tests don't depend on the Resources asset.
	/// Install() in SetUp, Uninstall() in TearDown.
	/// </summary>
	public sealed class TestFactions
	{
		public readonly FactionDefinition Player = FactionDefinition.Create("Player", FactionRelation.Neutral);
		public readonly FactionDefinition Wildlife = FactionDefinition.Create("Wildlife", FactionRelation.Hostile);
		public readonly FactionDefinition Townsfolk = FactionDefinition.Create("Townsfolk", FactionRelation.Neutral);
		public readonly FactionDefinition Traders = FactionDefinition.Create("Traders", FactionRelation.Neutral);
		public readonly FactionDefinition Bandits = FactionDefinition.Create("Bandits", FactionRelation.Hostile);

		public FactionDefinition[] All => new[] { Player, Wildlife, Townsfolk, Traders, Bandits };

		public void Install()
		{
			var table = new FactionRelationTable();
			foreach (FactionDefinition f in All)
				table.Define(f.FactionId, f.DefaultStance);
			FactionRelations.Configure(table, Player, All);
		}

		public void Uninstall()
		{
			FactionRelations.ResetToDefault();
			CharacterFaction.CommunitySource = null;
			Attitude.Provider = null;
			foreach (FactionDefinition f in All)
				if (f != null)
					Object.Destroy(f);
		}
	}
}
