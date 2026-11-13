using System.Linq;
using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>SocialSimulation: derived bonds, deaths, knowledge, grudges, inheritance and attitude.</summary>
	public class SocialSimulationTests
	{
		const float Tol = 0.01f;
		SimWorld w;
		SocialSimulation Sim => w.Sim;

		[SetUp]
		public void SetUp() => w = new SimWorld();

		// ── Register and derived bonds ──────────────────────

		[Test]
		public void Household_DerivedFamilyBothWays_NothingStored()
		{
			w.Person("aino", household: "h");
			w.Person("veikko", household: "h");
			Assert.AreEqual(new Relationship(60, BondFlags.Family), Sim.Relationship("aino", "veikko"));
			Assert.AreEqual(new Relationship(60, BondFlags.Family), Sim.Relationship("veikko", "aino"));
			Assert.AreEqual(0, Sim.Relations.Count);
		}

		[Test]
		public void CampVillageAndKinGroup()
		{
			w.Village("camp", kind: CommunityKind.Camp);
			w.Village("kuusi");
			w.Person("b1", "camp");
			w.Person("b2", "camp");
			w.Person("v1", "kuusi", "suku");
			w.Person("v2", "kuusi");
			w.Person("k1", "koivu", "suku");
			Assert.AreEqual(new Relationship(40, BondFlags.Comrade), Sim.Relationship("b1", "b2"));
			Assert.AreEqual(new Relationship(20, BondFlags.Neighbour), Sim.Relationship("v1", "v2"));
			Assert.AreEqual(new Relationship(30, BondFlags.Family), Sim.Relationship("v1", "k1"));
		}

		[Test]
		public void FiftyVillagers_NoStoredEntries()
		{
			w.Village("kuusi");
			for (int i = 0; i < 50; i++)
				w.Person("v" + i, "kuusi");
			Assert.AreEqual(new Relationship(20, BondFlags.Neighbour), Sim.Relationship("v3", "v41"));
			Assert.AreEqual(0, Sim.Relations.Count);
			Assert.IsFalse(Sim.Register(new PersonRecord { Id = "v3" }), "duplicate id");
		}

		[Test]
		public void AuthoredBond_ReplacesBaseAddsFlags()
		{
			w.Village("kuusi");
			w.Person("a", "kuusi", household: "h");
			w.Person("b", "kuusi", household: "h");
			Sim.SetAuthoredBond("a", "b", BondFlags.Friend, 50);
			Assert.AreEqual(new Relationship(50, BondFlags.Family | BondFlags.Neighbour | BondFlags.Friend), Sim.Relationship("a", "b"));
		}

		[Test]
		public void Retuning_ChangesDerivedOpinion()
		{
			SocialTuning t = SocialTuning.Default;
			t.neighbourBondOpinion = 30;
			w = new SimWorld(t);
			w.Village("kuusi");
			w.Person("a", "kuusi");
			w.Person("b", "kuusi");
			Assert.AreEqual(new Relationship(30, BondFlags.Neighbour), Sim.Relationship("a", "b"));
		}

		[Test]
		public void EarnedOpinion_IsDelta_SurvivesMove()
		{
			w.Village("kuusi");
			w.Person("a", "kuusi");
			w.Person("b", "kuusi");
			Sim.RecordAttack("a", "b", 0);
			Assert.AreEqual(-5, Sim.Relationship("a", "b").Opinion);
			Sim.MoveTo("a", null);
			Assert.AreEqual(new Relationship(-25, BondFlags.None), Sim.Relationship("a", "b"));
		}

		// ── Attribution ─────────────────────────────────────

		[Test]
		public void Attribution_DirectWindowAndAnimal()
		{
			w.Person("v1");
			w.Squad("j");
			DeathRecord d = Sim.RecordDeath("v1", "j", true, SimWorld.Player, default, null, 0);
			Assert.AreEqual("j", d.KillerId);
			Assert.AreEqual(SimWorld.Player, d.KillerFaction);

			w.Person("v2");
			Sim.RecordDamage("v2", "j", SimWorld.Player, 10);
			d = Sim.RecordDeath("v2", null, false, default, default, null, 10.25);
			Assert.AreEqual("j", d.KillerId, "within 0.25 h");

			w.Person("v3");
			Sim.RecordDamage("v3", "j", SimWorld.Player, 20);
			d = Sim.RecordDeath("v3", null, false, default, default, null, 20.26);
			Assert.IsNull(d.KillerId, "at 0.26 h unknown");
			Assert.IsFalse(d.KillerFactionKnown);

			w.Person("v4");
			d = Sim.RecordDeath("v4", null, true, SimWorld.Wildlife, default, null, 30);
			Assert.IsNull(d.KillerId);
			Assert.AreEqual(SimWorld.Wildlife, d.KillerFaction);
		}

		// ── Grudge creation ─────────────────────────────────

		[Test]
		public void Witnesses_GetGrudgesByCloseness()
		{
			w.Village("kuusi");
			w.Person("v", "kuusi", household: "h");
			w.Person("mate", household: "h");
			w.Person("neighbour", "kuusi");
			w.Person("stranger");
			w.Squad("j");
			w.Kill("v", "j", "mate", "neighbour", "stranger", "j", "v");

			Assert.AreEqual(60f, w.G("mate", "j").Intensity, Tol);
			Assert.AreEqual(GrudgeTier.Rally, GrudgeRules.Tier(w.G("mate", "j").Intensity, w.T));
			Assert.AreEqual(24f, w.G("neighbour", "j").Intensity, Tol);
			Assert.AreEqual(GrudgeTier.Cold, GrudgeRules.Tier(w.G("neighbour", "j").Intensity, w.T));
			Assert.IsNull(w.G("stranger", "j"));
			Assert.IsNull(w.G("j", "j"));
			Assert.IsEmpty(Sim.Grudges.HeldBy("v"));
			Assert.AreEqual(2, Sim.Deaths.Get(0).GrudgesCreated);
		}

		[Test]
		public void NoGrudge_AgainstAnimalOrUnknown()
		{
			w.Person("v", household: "h");
			w.Person("mate", household: "h");
			Sim.RecordDeath("v", null, true, SimWorld.Wildlife, default, new[] { new Witness("mate", 1) }, 0);
			w.Person("v2", household: "h");
			Sim.RecordDeath("v2", null, false, default, default, new[] { new Witness("mate", 1) }, 0);
			Assert.AreEqual(0, Sim.Grudges.Count);
			Assert.IsTrue(Sim.Knowledge.Knows("mate", 0));
		}

		// ── Knowledge ───────────────────────────────────────

		[Test]
		public void News_ArrivesAfterExactlyThreeHours()
		{
			w.Person("v", household: "h");
			w.Person("mate", household: "h");
			w.Person("onlooker");
			w.Squad("j");
			w.Kill("v", "j", "onlooker");
			w.Advance(2.9);
			Assert.IsFalse(Sim.Knowledge.Knows("mate", 0));
			w.Advance(0.1);
			Assert.IsTrue(Sim.Knowledge.KnowsKiller("mate", 0));
			Assert.AreEqual(60f, w.G("mate", "j").Intensity, Tol);
		}

		[Test]
		public void Unwitnessed_NewsWithoutKiller_NoGrudge()
		{
			w.Person("v", household: "h");
			w.Person("mate", household: "h");
			w.Squad("j");
			DeathRecord d = w.Kill("v", "j");
			Assert.IsFalse(d.KillerWitnessed);
			w.Advance(3);
			Assert.IsTrue(Sim.Knowledge.Knows("mate", 0));
			Assert.IsFalse(Sim.Knowledge.KnowsKiller("mate", 0));
			Assert.AreEqual(0, Sim.Grudges.Count);

			// Looting the body tells of the death, not the killer.
			w.Person("looter", household: "h");
			Sim.Learn("looter", 0, d.KillerWitnessed, w.Now);
			Assert.IsTrue(Sim.Knowledge.Knows("looter", 0));
			Assert.AreEqual(0, Sim.Grudges.Count);
		}

		[Test]
		public void KillersComrade_DoesNotMakeItWitnessed()
		{
			w.Person("v");
			w.Squad("j");
			w.Squad("k");
			Assert.IsFalse(w.Kill("v", "j", "k").KillerWitnessed);
			w.Person("v2");
			w.Person("onlooker");
			Assert.IsTrue(w.Kill("v2", "j", "onlooker").KillerWitnessed);
		}

		[Test]
		public void LearningTwice_OneGrudge_PaidOffStaysGone()
		{
			w.Person("v", household: "h");
			w.Person("mate", household: "h");
			w.Squad("j");
			w.Kill("v", "j", "mate");
			Sim.Learn("mate", 0, true, 0);
			Assert.AreEqual(1, w.Created.Count);
			Assert.AreEqual(60f, w.G("mate", "j").Intensity, Tol);

			Sim.Grudges.Remove(w.G("mate", "j"));
			Sim.Learn("mate", 0, true, 1);
			Assert.IsNull(w.G("mate", "j"), "a paid-off grudge does not come back");
		}

		[Test]
		public void KinGroup_LearnsAtTwentyFourHours()
		{
			w.Village("kuusi");
			w.Village("koivu");
			w.Person("v", "kuusi", "suku");
			w.Person("cousin", "koivu", "suku");
			w.Squad("recruit");
			Sim.Registry.Get("recruit").KinGroupId = "suku";
			w.Person("tribesman", "koivu", tribe: "hame");
			Sim.Registry.Get("v").TribeId = "hame";
			w.Person("onlooker");
			w.Squad("j");
			w.Kill("v", "j", "onlooker");

			w.Advance(23.9);
			Assert.IsFalse(Sim.Knowledge.Knows("cousin", 0));
			w.Advance(0.1);
			Assert.IsTrue(Sim.Knowledge.Knows("cousin", 0));
			Assert.IsTrue(Sim.Knowledge.Knows("recruit", 0));
			Assert.AreEqual(42f, w.G("cousin", "j").Intensity, Tol);
			Assert.AreEqual(GrudgeTier.Hostile, GrudgeRules.Tier(w.G("cousin", "j").Intensity, w.T));
			w.Advance(100);
			Assert.IsFalse(Sim.Knowledge.Knows("tribesman", 0));
		}

		// ── Resolution ──────────────────────────────────────

		[Test]
		public void TargetOrHolderDies_GrudgesRemoved()
		{
			w.Person("a");
			w.Person("b");
			w.Squad("j");
			w.Give("a", "j", 50);
			w.Give("b", "j", 50);
			w.Kill("j", null);
			Assert.AreEqual(0, Sim.Grudges.Count);

			w.Squad("k");
			w.Give("a", "k", 40);
			w.Kill("a", null);
			Assert.IsEmpty(Sim.Grudges.HeldBy("a"));
		}

		// ── Inheritance ─────────────────────────────────────

		// Veikko holds `intensity` against Jonttuli; Aatu is his son in his household.
		void Family(float intensity)
		{
			w.Person("veikko", household: "talo");
			w.Person("aatu", household: "talo");
			Sim.Relations.SetKin("veikko", "aatu", BondFlags.Child);
			w.Squad("j");
			w.Give("veikko", "j", intensity, generation: 1);
		}

		[Test]
		public void Child_InheritsHardGrudge_WhenNewsArrives_EvenUnwitnessed()
		{
			Family(80);
			DeathRecord d = w.Kill("veikko", null);
			Assert.AreEqual(1, d.InheritableGrudges.Count);
			Assert.IsNull(w.G("aatu", "j"), "nothing until the news arrives");

			w.Advance(3);
			Grudge g = w.G("aatu", "j");
			Assert.IsNotNull(g);
			Assert.AreEqual(60f, g.Intensity, Tol);
			Assert.AreEqual(1, g.Generation, "same generation");
			Assert.IsTrue(g.Inherited);
			Assert.AreEqual(GrudgeCause.Killing, g.Cause);

			Sim.Learn("aatu", d.Index, false, w.Now);
			Assert.AreEqual(1, w.Created.Count, "only once");
		}

		[Test]
		public void SoftGrudge_NotInherited()
		{
			Family(59);
			w.Kill("veikko", null);
			w.Advance(3);
			Assert.IsNull(w.G("aatu", "j"));
		}

		[Test]
		public void FriendSpouseSibling_InheritNothing()
		{
			Family(80);
			w.Person("wife", household: "talo");
			w.Person("brother", household: "talo");
			w.Person("friend", household: "talo");
			Sim.Relations.SetKin("veikko", "wife", BondFlags.Spouse);
			Sim.Relations.SetKin("veikko", "brother", BondFlags.Sibling);
			Sim.SetAuthoredBond("friend", "veikko", BondFlags.Friend, 80);
			w.Kill("veikko", null);
			w.Advance(3);
			Assert.IsNull(w.G("wife", "j"));
			Assert.IsNull(w.G("brother", "j"));
			Assert.IsNull(w.G("friend", "j"));
			Assert.IsNotNull(w.G("aatu", "j"));
		}

		[Test]
		public void DeadTargetSkipped_ChildTargetSkipped()
		{
			Family(80);
			w.Give("veikko", "aatu", 70);
			w.Kill("veikko", null);
			w.Kill("j", null);
			w.Advance(3);
			Assert.AreEqual(0, Sim.Grudges.Count);
		}

		[Test]
		public void Inherit_MergesWithOwnGrudge()
		{
			Family(80);
			w.Give("aatu", "j", 30);
			w.Kill("veikko", null);
			w.Advance(3);
			Assert.AreEqual(70f, w.G("aatu", "j").Intensity, Tol);
			Assert.IsFalse(w.G("aatu", "j").Inherited, "his own grudge was not inherited");
		}

		[Test]
		public void HandOverTwice_ThenStops()
		{
			Family(80);
			w.Person("ilmari", household: "talo");
			w.Person("onni", household: "talo");
			Sim.Relations.SetKin("aatu", "ilmari", BondFlags.Child);
			Sim.Relations.SetKin("ilmari", "onni", BondFlags.Child);

			w.Kill("veikko", null);
			Sim.Learn("aatu", 0, false, w.Now);
			Assert.AreEqual(60f, w.G("aatu", "j").Intensity, Tol);

			w.Kill("aatu", null);
			Sim.Learn("ilmari", 1, false, w.Now);
			Assert.AreEqual(45f, w.G("ilmari", "j").Intensity, Tol);

			DeathRecord d = w.Kill("ilmari", null);
			Assert.IsEmpty(d.InheritableGrudges, "45 is below 60");
			Sim.Learn("onni", 2, false, w.Now);
			Assert.IsNull(w.G("onni", "j"));
		}

		// ── Mutual kill, chains, caps, merge, decay ─────────

		[Test]
		public void MutualKill_NoGrudgesEitherOrder([Values(true, false)] bool aFirst)
		{
			w.Person("a", household: "ha");
			w.Person("fa", household: "ha");
			w.Squad("b", household: "hb");
			w.Squad("fb", household: "hb");
			if (aFirst)
			{
				w.Kill("a", "b", "fa", "fb");
				w.Kill("b", "a", "fa", "fb");
			}
			else
			{
				w.Kill("b", "a", "fa", "fb");
				w.Kill("a", "b", "fa", "fb");
			}
			Assert.AreEqual(0, Sim.Grudges.Count);
		}

		[Test]
		public void FeudChain_StopsAfterGenerationTwo()
		{
			w.Person("v", household: "h");
			w.Person("h1", household: "h");
			w.Person("h2", household: "h");
			w.Squad("j", household: "s");
			w.Squad("k", household: "s");
			w.Squad("k2", household: "s");
			Sim.Registry.Get("k").Vengefulness = 0.6f;

			DeathRecord d0 = w.Kill("v", "j", "h1");
			Assert.AreEqual(0, d0.FeudGeneration);
			Assert.AreEqual(60f, w.G("h1", "j").Intensity, Tol);

			DeathRecord d1 = w.Kill("j", "h1", "k");
			Assert.AreEqual(1, d1.FeudGeneration);
			Assert.AreEqual(36f, w.G("k", "h1").Intensity, Tol, "× 0.6");

			DeathRecord d2 = w.Kill("h1", "k", "h2");
			Assert.AreEqual(2, d2.FeudGeneration);
			Assert.AreEqual(21.6f, w.G("h2", "k").Intensity, Tol);

			DeathRecord d3 = w.Kill("k", "h2", "k2");
			Assert.AreEqual(3, d3.FeudGeneration);
			Assert.AreEqual(0, d3.GrudgesCreated);
			Assert.IsNull(w.G("k2", "h2"));
			Assert.IsTrue(Sim.Deaths.All.All(d => d.GrudgesCreated <= w.T.maxGrudgesPerDeath));
		}

		[Test]
		public void PerHolderCap_ReplacesWeakest()
		{
			w.Person("a");
			foreach (string t in new[] { "t1", "t2", "t3", "t4", "t5" })
				w.Person(t);
			w.Give("a", "t1", 20);
			w.Give("a", "t2", 30);
			w.Give("a", "t3", 40);
			Assert.IsNotNull(w.Give("a", "t4", 50));
			Assert.IsNull(w.G("a", "t1"));
			Assert.IsNull(w.Give("a", "t5", 25), "weaker than the weakest is dropped");
			Assert.AreEqual(3, Sim.Grudges.HeldBy("a").Count);
		}

		[Test]
		public void PerDeathCap_ClosestFirst()
		{
			w.Person("v", household: "h");
			string[] mates = { "m1", "m2", "m3", "m4", "m5", "m6" };
			foreach (string m in mates)
				w.Person(m, household: "h");
			w.Squad("j");
			DeathRecord d = w.Kill("v", "j", mates);
			Assert.AreEqual(4, d.GrudgesCreated);
			Assert.IsNotNull(w.G("m4", "j"));
			Assert.IsNull(w.G("m5", "j"));
			Assert.IsNull(w.G("m6", "j"));
		}

		[Test]
		public void SecondKilling_Stacks_NoNewEvent()
		{
			w.Person("v1", household: "h");
			w.Person("v2", household: "h");
			w.Person("mate", household: "h");
			w.Squad("j");
			w.Kill("v1", "j", "mate");
			w.Kill("v2", "j", "mate");
			Assert.AreEqual(70f, w.G("mate", "j").Intensity, Tol);
			Assert.AreEqual("v2", w.G("mate", "j").VictimId);
			Assert.AreEqual(1, w.Created.Count, "GrudgeCreated fires on the new grudge, not the merge");
		}

		[Test]
		public void Decay_DaysAndJumps()
		{
			w.Person("a");
			w.Squad("j");
			w.Give("a", "j", 60);
			w.Advance(24);
			Assert.AreEqual(55f, w.G("a", "j").Intensity, Tol);
			w.Advance(24 * 10);
			Assert.IsNull(w.G("a", "j"), "60 - 5 - 50 is forgotten");
		}

		[Test]
		public void NewsDuringJump_DecaysFromItsDueDay()
		{
			w.Person("v", household: "h");
			w.Person("mate", household: "h");
			w.Person("onlooker");
			w.Squad("j");
			w.Kill("v", "j", "onlooker");
			w.Advance(49);
			Assert.AreEqual(50f, w.G("mate", "j").Intensity, Tol, "due on day 0, decayed for days 1 and 2");
		}

		// ── Squad scope and squad holders ───────────────────

		[Test]
		public void SquadScope_At85()
		{
			w.Person("a");
			w.Person("b");
			w.Squad("j");
			w.Squad("k");
			w.Give("a", "j", 90);
			w.Give("b", "j", 80);
			AttitudeResult r = Sim.AttitudeOf("a", "k", FactionRelation.Neutral);
			Assert.AreEqual(FactionRelation.Hostile, r.Relation);
			Assert.AreEqual(AttitudeReason.Grudge, r.Reason);
			Assert.AreEqual(FactionRelation.Neutral, Sim.AttitudeOf("b", "k", FactionRelation.Neutral).Relation);

			w.Kill("j", null);
			Assert.AreEqual(FactionRelation.Neutral, Sim.AttitudeOf("a", "k", FactionRelation.Neutral).Relation);
		}

		[Test]
		public void SquadHolder_UsesFactionOnly()
		{
			w.Squad("k");
			w.Person("aino");
			w.Give("k", "aino", 100);
			AttitudeResult r = Sim.AttitudeOf("k", "aino", FactionRelation.Neutral);
			Assert.AreEqual(FactionRelation.Neutral, r.Relation);
			Assert.AreEqual(AttitudeReason.Faction, r.Reason);
			Assert.IsFalse(r.RefusesDealings);
		}

		// ── Close kin and tribe ─────────────────────────────

		[Test]
		public void CloseKin_AcrossHostileFactions()
		{
			w.Village("kuusi");
			w.Village("camp", faction: SimWorld.Bandits, kind: CommunityKind.Camp);
			w.Person("mother", "kuusi");
			w.Person("son", "camp");
			Sim.Relations.SetKin("mother", "son", BondFlags.Child);
			Assert.AreEqual(FactionRelation.Hostile, Sim.FactionBetween("mother", "son"));

			AttitudeResult r = Sim.AttitudeOf("mother", "son", FactionRelation.Hostile);
			Assert.AreEqual(FactionRelation.Neutral, r.Relation);
			Assert.AreEqual(AttitudeReason.Kin, r.Reason);
			Assert.AreEqual(AttitudeReason.Kin, Sim.AttitudeOf("son", "mother", FactionRelation.Hostile).Reason);

			Grudge g = w.Give("mother", "son", 80);
			Assert.AreEqual(AttitudeReason.Grudge, Sim.AttitudeOf("mother", "son", FactionRelation.Hostile).Reason);
			Sim.Grudges.Remove(g);
			Assert.AreEqual(AttitudeReason.Kin, Sim.AttitudeOf("mother", "son", FactionRelation.Hostile).Reason);
		}

		[Test]
		public void TribeTerm_OnlyBetweenStrangers()
		{
			Sim.Tribes.Set("hame", "karjala", -10);
			w.Person("a", tribe: "hame");
			w.Person("b", tribe: "karjala");
			w.Person("c", tribe: "hame");
			w.Person("d", tribe: "karjala");
			Sim.AddOpinion("a", "b", -50);
			Sim.AddOpinion("c", "d", -49);
			AttitudeResult r = Sim.AttitudeOf("a", "b", FactionRelation.Neutral);
			Assert.AreEqual(FactionRelation.Hostile, r.Relation, "-50 - 10 = -60");
			Assert.AreEqual(AttitudeReason.Opinion, r.Reason);
			Assert.AreEqual(FactionRelation.Neutral, Sim.AttitudeOf("c", "d", FactionRelation.Neutral).Relation);

			// Any shared bond and the tribe term no longer applies.
			w.Village("kuusi");
			Sim.MoveTo("a", "kuusi");
			Sim.MoveTo("b", "kuusi");
			Sim.AddOpinion("a", "b", -20); // neighbour 20 + delta -70 = -50
			Assert.AreEqual(-50, Sim.Relationship("a", "b").Opinion);
			Assert.AreEqual(FactionRelation.Neutral, Sim.AttitudeOf("a", "b", FactionRelation.Neutral).Relation);

			w.Person("e");
			w.Person("f");
			Sim.AddOpinion("e", "f", -59);
			Assert.AreEqual(FactionRelation.Neutral, Sim.AttitudeOf("e", "f", FactionRelation.Neutral).Relation, "no tribes, no term");
		}

		// ── Faction, opinion, rally, blood price ────────────

		[Test]
		public void EffectiveFaction_SquadBeatsCommunity()
		{
			w.Village("kuusi");
			w.Person("a", "kuusi", faction: SimWorld.Traders);
			Assert.AreEqual(SimWorld.Townsfolk, Sim.EffectiveFaction("a").Id);
			Sim.SetSquad("a", SocialSimulation.PlayerSquadId);
			Assert.AreEqual(SimWorld.Player, Sim.EffectiveFaction("a").Id);
			Assert.AreEqual(FactionSource.Squad, Sim.EffectiveFaction("a").Source);
		}

		[Test]
		public void Opinion_AttacksAndFightingTogether()
		{
			w.Person("v");
			w.Squad("j");
			w.Squad("k");
			w.Person("bandit", faction: SimWorld.Bandits);
			Sim.RecordAttack("v", "j", 0);
			Sim.RecordAttack("v", "j", 0);
			Assert.AreEqual(-50, Sim.Relationship("v", "j").Opinion);

			Sim.RecordDamage("v", "j", SimWorld.Player, 0);
			Sim.RecordDamage("v", "k", SimWorld.Player, 0);
			Sim.RecordDamage("v", "bandit", SimWorld.Bandits, 0);
			w.Kill("v", "j");
			Assert.AreEqual(10, Sim.Relationship("j", "k").Opinion);
			Assert.AreEqual(10, Sim.Relationship("k", "j").Opinion);
			Assert.AreEqual(0, Sim.Relationship("j", "bandit").Opinion, "a hostile pair gets nothing");
		}

		[Test]
		public void JoinFeud_CallerGenerationAtLeastHostile()
		{
			w.Person("caller");
			w.Person("friend");
			w.Squad("j");
			Grudge caller = w.Give("caller", "j", 60, generation: 1);
			Grudge g = Sim.JoinFeud("friend", caller, 0);
			Assert.AreEqual(36f, g.Intensity, Tol);
			Assert.AreEqual(1, g.Generation);
		}

		[Test]
		public void BloodPrice_SettlesHousehold()
		{
			w.Person("payee", household: "h");
			w.Person("mate", household: "h");
			w.Person("villager");
			w.Squad("j");
			Grudge primary = w.Give("payee", "j", 60);
			Grudge mates = w.Give("mate", "j", 80);
			Grudge other = w.Give("villager", "j", 70);
			Sim.SetAuthoredBond("mate", "j", BondFlags.None, 10);
			Sim.AddOpinion("payee", "j", -80);

			BloodPriceQuote q = Sim.QuoteBloodPrice("payee");
			Assert.AreSame(primary, q.Primary);
			CollectionAssert.AreEquivalent(new[] { primary, mates }, q.Settles);
			Assert.AreEqual(80f, q.Intensity, Tol);

			Assert.AreEqual(2, Sim.PayBloodPrice(q));
			Assert.IsNull(w.G("payee", "j"));
			Assert.IsNull(w.G("mate", "j"));
			Assert.AreSame(other, w.G("villager", "j"));
			Assert.AreEqual(-20, Sim.Relationship("payee", "j").Opinion);
			Assert.AreEqual(10, Sim.Relationship("mate", "j").Opinion, "never lowered");
		}

		[Test]
		public void BloodPrice_OtherVictimNotSettled_NoHousehold_NoGrudge()
		{
			w.Person("payee", household: "h");
			w.Person("mate", household: "h");
			w.Person("loner");
			w.Squad("j");
			w.Give("payee", "j", 60);
			Grudge different = w.Give("mate", "j", 80);
			different.VictimId = "another";
			Assert.AreEqual(1, Sim.QuoteBloodPrice("payee").Settles.Count);

			w.Give("loner", "j", 50);
			Assert.AreEqual(1, Sim.QuoteBloodPrice("loner").Settles.Count);
			Assert.IsNull(Sim.QuoteBloodPrice("j"));
		}

		[Test]
		public void HoldsGrudgeAgainstSquad()
		{
			w.Person("a");
			w.Person("b");
			w.Person("outsider");
			w.Squad("j");
			w.Give("a", "j", 20);
			w.Give("b", "outsider", 90);
			Assert.IsTrue(Sim.HoldsGrudgeAgainstSquad("a", SocialSimulation.PlayerSquadId, w.T.minGrudgeIntensity));
			Assert.IsFalse(Sim.HoldsGrudgeAgainstSquad("b", SocialSimulation.PlayerSquadId, w.T.minGrudgeIntensity));
		}
	}
}
