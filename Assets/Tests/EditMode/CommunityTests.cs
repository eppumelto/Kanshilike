using System.Collections.Generic;
using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>CommunityRegistry and the community parts of SocialSimulation: faction, elder, moving.</summary>
	public class CommunityTests
	{
		const float Tol = 0.01f;
		SimWorld w;
		SocialSimulation Sim => w.Sim;

		[SetUp]
		public void SetUp() => w = new SimWorld();

		[Test]
		public void Registry_Basics()
		{
			var reg = new CommunityRegistry();
			Assert.IsTrue(reg.Add(new CommunityRecord { Id = "kuusi", ElderId = "heikki" }));
			Assert.IsTrue(reg.Add(new CommunityRecord { Id = "koivu", ElderId = "ilmari" }));
			Assert.IsFalse(reg.Add(new CommunityRecord { Id = "kuusi" }));
			Assert.IsFalse(reg.Add(new CommunityRecord { Id = "" }));
			Assert.IsTrue(reg.TryGet("kuusi", out _));
			Assert.IsFalse(reg.SetFaction("nowhere", SimWorld.Bandits));
			CollectionAssert.AreEqual(new[] { "kuusi" }, reg.ClearElder("heikki"));
			Assert.AreEqual("ilmari", reg.Get("koivu").ElderId);
			Assert.AreEqual(0, (int)CommunityKind.Village);
			Assert.AreEqual(1, (int)CommunityKind.Camp);
		}

		[Test]
		public void EffectiveFaction_FromCommunity()
		{
			w.Village("kuusi");
			w.Person("a", "kuusi", faction: SimWorld.Traders);
			w.Person("b", faction: SimWorld.Traders);
			w.Person("c", "later", faction: SimWorld.Traders);
			Assert.AreEqual(SimWorld.Townsfolk, Sim.EffectiveFaction("a").Id);
			Assert.AreEqual(FactionSource.Community, Sim.EffectiveFaction("a").Source);
			Assert.AreEqual(SimWorld.Traders, Sim.EffectiveFaction("b").Id);
			Assert.AreEqual(SimWorld.Traders, Sim.EffectiveFaction("c").Id, "community not registered yet");
			w.Village("later", faction: SimWorld.Bandits);
			Assert.AreEqual(SimWorld.Bandits, Sim.EffectiveFaction("c").Id);
		}

		[Test]
		public void SetCommunityFaction_ListsChangedAliveMembers()
		{
			w.Village("kuusi");
			w.Person("a", "kuusi");
			w.Person("b", "kuusi");
			w.Person("dead", "kuusi");
			w.Person("outsider");
			w.Kill("dead", null);

			var changed = new List<string>();
			Assert.IsTrue(Sim.SetCommunityFaction("kuusi", SimWorld.Bandits, changed));
			CollectionAssert.AreEquivalent(new[] { "a", "b" }, changed);
			Assert.AreEqual(SimWorld.Townsfolk, Sim.EffectiveFaction("outsider").Id);

			changed.Clear();
			Assert.IsTrue(Sim.SetCommunityFaction("kuusi", SimWorld.Bandits, changed));
			Assert.IsEmpty(changed);
			Assert.IsFalse(Sim.SetCommunityFaction("nowhere", SimWorld.Bandits, changed));
		}

		[Test]
		public void RegisterCommunityAfterMembers_BondsApplyAtOnce()
		{
			w.Person("a", "kuusi");
			w.Person("heikki", "kuusi");
			Assert.AreEqual(BondFlags.None, Sim.Relationship("a", "heikki").Bonds);
			w.Village("kuusi", elder: "heikki");
			Assert.AreEqual(new Relationship(40, BondFlags.Neighbour | BondFlags.Elder), Sim.Relationship("a", "heikki"));
			Assert.AreEqual(0, Sim.Relations.Count);
		}

		[Test]
		public void SetElder_BondsMove()
		{
			w.Village("kuusi", elder: "old");
			w.Person("a", "kuusi");
			w.Person("b", "kuusi");
			w.Person("old", "kuusi");
			w.Person("new", "kuusi");
			Sim.SetAuthoredBond("b", "new", BondFlags.None, 70);
			Sim.AddOpinion("a", "old", -10);

			Sim.SetElder("kuusi", "new");
			Assert.AreEqual(new Relationship(40, BondFlags.Neighbour | BondFlags.Elder), Sim.Relationship("a", "new"));
			Assert.AreEqual(70, Sim.Relationship("b", "new").Opinion);
			Assert.AreEqual(new Relationship(10, BondFlags.Neighbour), Sim.Relationship("a", "old"), "earned -10 stays");

			Sim.SetElder("kuusi", null);
			Assert.AreEqual(BondFlags.Neighbour, Sim.Relationship("a", "new").Bonds);
		}

		[Test]
		public void MoveTo_LeavingAndJoining()
		{
			w.Village("kuusi", elder: "heikki");
			w.Village("koivu");
			w.Person("heikki", "kuusi", faction: SimWorld.Traders);
			w.Person("a", "kuusi", "suku", "talo", faction: SimWorld.Traders);
			w.Person("b", "kuusi", "suku", "talo");
			w.Person("c", "koivu");
			Sim.Relations.SetKin("a", "b", BondFlags.Sibling);

			Sim.MoveTo("a", null);
			Assert.AreEqual(SimWorld.Traders, Sim.EffectiveFaction("a").Id);
			Assert.AreEqual(BondFlags.Family | BondFlags.Sibling, Sim.Relationship("a", "b").Bonds);
			Assert.AreEqual(60, Sim.Relationship("a", "b").Opinion);

			Sim.MoveTo("a", "koivu");
			Assert.AreEqual(new Relationship(20, BondFlags.Neighbour), Sim.Relationship("a", "c"));

			Sim.MoveTo("heikki", "koivu");
			Assert.IsNull(Sim.Communities.Get("kuusi").ElderId);
		}

		[Test]
		public void ElderDeath_WholeVillageRallies()
		{
			w.Village("kuusi", elder: "heikki");
			w.Person("heikki", "kuusi");
			for (int i = 0; i < 6; i++)
				w.Person("v" + i, "kuusi");
			w.Person("onlooker");
			w.Squad("j");

			DeathRecord d = w.Kill("heikki", "j", "onlooker");
			Assert.IsTrue(d.VictimWasElder);
			Assert.AreEqual("kuusi", d.ElderOfCommunityId);
			Assert.IsNull(Sim.Communities.Get("kuusi").ElderId);

			w.Advance(3);
			for (int i = 0; i < 6; i++)
			{
				Grudge g = w.G("v" + i, "j");
				Assert.IsNotNull(g, "v" + i);
				Assert.AreEqual(54f, g.Intensity, Tol);
				Assert.AreEqual(GrudgeTier.Rally, GrudgeRules.Tier(g.Intensity, w.T));
			}
			Assert.AreEqual(6, d.GrudgesCreated);
		}

		[Test]
		public void OrdinaryDeath_CappedAtFour()
		{
			w.Village("kuusi");
			w.Person("victim", "kuusi");
			for (int i = 0; i < 6; i++)
				w.Person("v" + i, "kuusi");
			w.Person("onlooker");
			w.Squad("j");
			DeathRecord d = w.Kill("victim", "j", "onlooker");
			w.Advance(3);
			Assert.AreEqual(4, d.GrudgesCreated);
		}

		[Test]
		public void ElderGone_NoGrudges()
		{
			w.Village("kuusi", elder: "heikki");
			w.Person("heikki", "kuusi");
			w.Person("v", "kuusi");
			Sim.MarkGone("heikki");
			Assert.IsNull(Sim.Communities.Get("kuusi").ElderId);
			Assert.AreEqual(PersonState.Gone, Sim.Registry.Get("heikki").State);
			w.Advance(5);
			Assert.AreEqual(0, Sim.Grudges.Count);
		}

		[Test]
		public void Loner_FactionOnly()
		{
			w.Person("loner");
			w.Person("other");
			Assert.AreEqual(BondFlags.None, Sim.Relationship("loner", "other").Bonds);
			AttitudeResult r = Sim.AttitudeOf("loner", "other", FactionRelation.Neutral);
			Assert.AreEqual(FactionRelation.Neutral, r.Relation);
			Assert.AreEqual(AttitudeReason.Faction, r.Reason);
		}
	}
}
