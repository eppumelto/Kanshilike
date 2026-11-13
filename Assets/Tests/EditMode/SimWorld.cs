using System.Collections.Generic;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>A fresh SocialSimulation with the five test factions and short helpers for people and deaths.</summary>
	public sealed class SimWorld
	{
		public static readonly FactionId Player = new FactionId("player");
		public static readonly FactionId Wildlife = new FactionId("wildlife");
		public static readonly FactionId Townsfolk = new FactionId("townsfolk");
		public static readonly FactionId Traders = new FactionId("traders");
		public static readonly FactionId Bandits = new FactionId("bandits");

		public readonly SocialSimulation Sim;
		public readonly List<Grudge> Created = new List<Grudge>();
		public double Now;

		public SimWorld(SocialTuning tuning = null)
		{
			var table = new FactionRelationTable();
			table.Define(Player, FactionRelation.Neutral);
			table.Define(Wildlife, FactionRelation.Hostile);
			table.Define(Townsfolk, FactionRelation.Neutral);
			table.Define(Traders, FactionRelation.Neutral);
			table.Define(Bandits, FactionRelation.Hostile);
			Sim = new SocialSimulation(tuning ?? SocialTuning.Default, table, Player);
			Sim.GrudgeCreated += g => Created.Add(g);
		}

		public SocialTuning T => Sim.Tuning;

		public PersonRecord Person(string id, string community = null, string kin = null, string household = null,
			float vengefulness = 0.6f, FactionId? faction = null, string tribe = null, Sex sex = Sex.Male)
		{
			var r = new PersonRecord
			{
				Id = id,
				Name = id,
				CommunityId = community,
				KinGroupId = kin,
				HouseholdId = household,
				Vengefulness = vengefulness,
				OwnFaction = faction ?? Townsfolk,
				TribeId = tribe,
				Sex = sex
			};
			Sim.Register(r);
			return r;
		}

		/// <summary>A player squad member (Player faction).</summary>
		public PersonRecord Squad(string id, string household = null)
		{
			PersonRecord r = Person(id, household: household, vengefulness: 0.5f, faction: Player);
			Sim.SetSquad(id, SocialSimulation.PlayerSquadId);
			return r;
		}

		public CommunityRecord Village(string id, string elder = null, FactionId? faction = null, CommunityKind kind = CommunityKind.Village)
		{
			var c = new CommunityRecord { Id = id, Name = id, Kind = kind, Faction = faction ?? Townsfolk, ElderId = elder };
			Sim.RegisterCommunity(c);
			return c;
		}

		/// <summary>Killer kills victim now; witnesses in order are 1 m, 2 m, 3 m... away.</summary>
		public DeathRecord Kill(string victim, string killer, params string[] witnesses)
		{
			var list = new List<Witness>();
			for (int i = 0; i < witnesses.Length; i++)
				list.Add(new Witness(witnesses[i], (i + 1) * (i + 1)));
			return Sim.RecordDeath(victim, killer, false, default, default, list, Now);
		}

		public void Advance(double hours)
		{
			Now += hours;
			Sim.Advance(Now);
		}

		public Grudge Give(string holder, string target, float intensity, int generation = 0)
		{
			int day = DayClock.DayOf(Now);
			return Sim.Grudges.AddOrMerge(new Grudge
			{
				HolderId = holder,
				TargetId = target,
				VictimId = "someone",
				DeathIndex = -1,
				Intensity = intensity,
				CreatedDay = day,
				LastDecayDay = day,
				Generation = generation
			}, T, out _);
		}

		public Grudge G(string holder, string target) => Sim.Grudges.Get(holder, target);
	}
}
