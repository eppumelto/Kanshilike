using System;
using System.Linq;
using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>The pure social rules: clocks, relationships, grudges, attitude, default bonds and news.</summary>
	public class SocialRulesTests
	{
		static SocialTuning T => SocialTuning.Default;
		const float Tol = 0.01f;

		// ── DayClock ────────────────────────────────────────

		[Test]
		public void DayClock_SeedsWrapsAndCounts()
		{
			var c = new DayClock();
			c.Advance(23f);
			Assert.AreEqual(0, c.Day);
			c.Advance(23.5f);
			Assert.AreEqual(0, c.Day, "forward keeps the day");
			c.Advance(1f);
			Assert.AreEqual(1, c.Day);
			Assert.AreEqual(25.0, c.TotalHours, 1e-6);
			c.Advance(0.5f);
			Assert.AreEqual(2, c.Day, "two wraps");
			Assert.AreEqual(2, DayClock.DayOf(48.0));
			Assert.AreEqual(1, DayClock.DayOf(47.9));
		}

		// ── RelationshipBook ────────────────────────────────

		[Test]
		public void Book_DirectionalDefaultAndRemoval()
		{
			var b = new RelationshipBook();
			Assert.IsTrue(b.GetStored("a", "b").IsDefault);
			b.SetDelta("a", "b", -25);
			Assert.AreEqual(-25, b.GetStored("a", "b").Delta);
			Assert.AreEqual(0, b.GetStored("b", "a").Delta);
			b.SetDelta("a", "b", 0);
			Assert.AreEqual(0, b.Entries.Count(), "back at the default is removed");
		}

		[Test]
		public void BondFlags_ValuesPinned()
		{
			Assert.AreEqual(1, (int)BondFlags.Family);
			Assert.AreEqual(16, (int)BondFlags.Rival);
			Assert.AreEqual(32, (int)BondFlags.Parent);
			Assert.AreEqual(64, (int)BondFlags.Child);
			Assert.AreEqual(256, (int)BondFlags.Spouse);
			Assert.AreEqual(512, (int)BondFlags.Neighbour);
			Assert.AreEqual(1024, (int)BondFlags.Elder);
		}

		[Test]
		public void Book_KinMirrorsAndIsProtected()
		{
			var b = new RelationshipBook();
			b.SetKin("aino", "veikko", BondFlags.Child);
			Assert.AreEqual(BondFlags.Child, b.KinOf("aino", "veikko"));
			Assert.AreEqual(BondFlags.Parent, b.KinOf("veikko", "aino"));
			b.SetKin("a", "b", BondFlags.Sibling);
			b.SetKin("c", "d", BondFlags.Spouse);
			Assert.AreEqual(BondFlags.Sibling, b.KinOf("b", "a"));
			Assert.AreEqual(BondFlags.Spouse, b.KinOf("d", "c"));

			Assert.Throws<ArgumentException>(() => b.SetBonds("a", "b", BondFlags.Child));
			Assert.Throws<ArgumentException>(() => b.AddBonds("a", "b", BondFlags.Spouse | BondFlags.Friend));
			Assert.DoesNotThrow(() => b.AddBonds("a", "b", BondFlags.Elder | BondFlags.Neighbour));
			b.SetBonds("a", "b", BondFlags.None);
			Assert.AreEqual(BondFlags.Sibling, b.KinOf("a", "b"), "SetBonds keeps kin");

			Assert.IsTrue(b.GetStored("aino", "veikko").Bonds == BondFlags.Child, "a kin entry with no base and delta 0 stays");
			foreach (BondFlags k in new[] { BondFlags.Parent, BondFlags.Child, BondFlags.Sibling, BondFlags.Spouse })
			{
				var book = new RelationshipBook();
				book.SetKin("x", "y", k);
				Assert.IsTrue(book.IsCloseKin("x", "y"), k.ToString());
			}
			b.AddBonds("e", "f", BondFlags.Family);
			Assert.IsFalse(b.IsCloseKin("e", "f"));
		}

		// ── RelationshipRules ───────────────────────────────

		[Test]
		public void Effective_UnionBaseAndClamp()
		{
			var derived = new Relationship(20, BondFlags.Neighbour);
			var stored = new StoredRelationship { Bonds = BondFlags.Friend, Delta = 5 };
			Relationship r = RelationshipRules.Effective(true, derived, stored);
			Assert.AreEqual(BondFlags.Neighbour | BondFlags.Friend, r.Bonds);
			Assert.AreEqual(25, r.Opinion);

			stored.HasBase = true;
			stored.Base = 50;
			Assert.AreEqual(55, RelationshipRules.Effective(true, derived, stored).Opinion);
			Assert.AreEqual(new Relationship(0, BondFlags.None), RelationshipRules.Effective(false, default, default));
			Assert.AreEqual(100, RelationshipRules.Effective(true, new Relationship(90, BondFlags.Family), new StoredRelationship { Delta = 40 }).Opinion);
		}

		[Test]
		public void DeltaAfterChange_NoPileUpPastClamp()
		{
			Assert.AreEqual(40, RelationshipRules.DeltaAfterChange(60, 0, 60));
			int d = RelationshipRules.DeltaAfterChange(20, -110, -25);
			Assert.AreEqual(-120, d);
			d = RelationshipRules.DeltaAfterChange(20, d, -25);
			Assert.AreEqual(-120, d);
			d = RelationshipRules.DeltaAfterChange(20, d, 15);
			Assert.AreEqual(-85, RelationshipRules.Clamp(20 + d));
		}

		[Test]
		public void DeltaAfterRaise_RaisesOnly()
		{
			Assert.AreEqual(-40, RelationshipRules.DeltaAfterRaise(20, -70, -20));
			Assert.AreEqual(5, RelationshipRules.DeltaAfterRaise(20, 5, -20));
		}

		// ── GrudgeRules ─────────────────────────────────────

		[Test]
		public void Closeness()
		{
			Assert.AreEqual(0.5f, C(40, BondFlags.Comrade), Tol);
			Assert.AreEqual(0.4f, C(20, BondFlags.Neighbour), Tol);
			Assert.AreEqual(0.7f, C(30, BondFlags.Family), Tol);
			Assert.AreEqual(1.0f, C(60, BondFlags.Family), Tol);
			Assert.AreEqual(0.9f, C(40, BondFlags.Neighbour | BondFlags.Elder), Tol);
			Assert.AreEqual(1.0f, C(40, BondFlags.Neighbour | BondFlags.Elder | BondFlags.Family), Tol);
			Assert.AreEqual(0f, C(20, BondFlags.Rival), Tol);
			Assert.AreEqual(0f, C(-100, BondFlags.None), Tol);
			Assert.AreEqual(0.7f, C(0, BondFlags.Child), Tol);
		}

		static float C(int opinion, BondFlags flags) => GrudgeRules.Closeness(new Relationship(opinion, flags), T);

		[Test]
		public void Intensity_KinAndGenerations()
		{
			Assert.AreEqual(80f, GrudgeRules.Intensity(1f, 0.6f, BondFlags.Child, 0, T), Tol);
			Assert.AreEqual(GrudgeTier.Hunt, GrudgeRules.Tier(GrudgeRules.Intensity(1f, 0.6f, BondFlags.Child, 0, T), T));
			Assert.AreEqual(60f, GrudgeRules.Intensity(1f, 0.6f, BondFlags.None, 0, T), Tol);
			Assert.AreEqual(36f, GrudgeRules.Intensity(1f, 0.6f, BondFlags.None, 1, T), Tol);
			Assert.AreEqual(21.6f, GrudgeRules.Intensity(1f, 0.6f, BondFlags.None, 2, T), Tol);
		}

		[Test]
		public void Tier_Edges()
		{
			Assert.AreEqual(GrudgeTier.None, GrudgeRules.Tier(14.9f, T));
			Assert.AreEqual(GrudgeTier.Cold, GrudgeRules.Tier(15f, T));
			Assert.AreEqual(GrudgeTier.Cold, GrudgeRules.Tier(35.9f, T));
			Assert.AreEqual(GrudgeTier.Hostile, GrudgeRules.Tier(36f, T));
			Assert.AreEqual(GrudgeTier.Hostile, GrudgeRules.Tier(47.9f, T));
			Assert.AreEqual(GrudgeTier.Rally, GrudgeRules.Tier(48f, T));
			Assert.AreEqual(GrudgeTier.Rally, GrudgeRules.Tier(67.9f, T));
			Assert.AreEqual(GrudgeTier.Hunt, GrudgeRules.Tier(68f, T));
			Assert.AreEqual(1, (int)GrudgeTier.Cold);
			Assert.AreEqual(4, (int)GrudgeTier.Hunt);
		}

		[Test]
		public void MergeDecayForget()
		{
			Assert.AreEqual(70f, GrudgeRules.Merge(60f, 40f, T), Tol);
			Assert.AreEqual(100f, GrudgeRules.Merge(95f, 10f, T), Tol);
			Assert.AreEqual(30f, GrudgeRules.Decayed(30f, 0, T), Tol);
			Assert.AreEqual(15f, GrudgeRules.Decayed(30f, 3, T), Tol);
			Assert.AreEqual(30f, GrudgeRules.Decayed(30f, -2, T), Tol);
			Assert.AreEqual(0f, GrudgeRules.Decayed(4f, 3, T), Tol);
			Assert.IsTrue(GrudgeRules.Forgotten(9.9f, T));
			Assert.IsFalse(GrudgeRules.Forgotten(10f, T));
		}

		[Test]
		public void DaysUntilForgotten_AndCaps()
		{
			Assert.AreEqual(3, GrudgeRules.DaysUntilForgotten(24f, T));
			Assert.AreEqual(1, GrudgeRules.DaysUntilForgotten(10f, T));
			Assert.AreEqual(0, GrudgeRules.DaysUntilForgotten(9f, T));
			SocialTuning t = T;
			t.decayPerDay = 0f;
			Assert.AreEqual(int.MaxValue, GrudgeRules.DaysUntilForgotten(24f, t));
			Assert.AreEqual(20, GrudgeRules.GrudgeCapFor(true, T));
			Assert.AreEqual(4, GrudgeRules.GrudgeCapFor(false, T));
		}

		[Test]
		public void Inheritable_OnlyHardKillingGrudges()
		{
			Assert.AreEqual(0, (int)GrudgeCause.Killing);
			Assert.IsTrue(GrudgeRules.Inheritable(new Grudge { Intensity = 60f }, T));
			Assert.IsTrue(GrudgeRules.Inheritable(new Grudge { Intensity = 80f * 0.75f }, T), "80 × 0.75 counts as 60");
			Assert.IsFalse(GrudgeRules.Inheritable(new Grudge { Intensity = 59f }, T));
			Assert.AreEqual(60f, GrudgeRules.Inherit(80f, T), Tol);
		}

		// ── GrudgePreview ───────────────────────────────────

		[Test]
		public void Preview_DefaultTable()
		{
			var at06 = GrudgePreview.Rows(T, 0.6f);
			float[] closeness = { 0.4f, 0.5f, 0.7f, 0.9f, 1f, 1f, 1f, 1f, 1f };
			float[] i06 = { 24, 30, 42, 54, 60, 80, 80, 80, 80 };
			GrudgeTier[] t06 = { GrudgeTier.Cold, GrudgeTier.Cold, GrudgeTier.Hostile, GrudgeTier.Rally, GrudgeTier.Rally,
				GrudgeTier.Hunt, GrudgeTier.Hunt, GrudgeTier.Hunt, GrudgeTier.Hunt };
			Assert.AreEqual(9, at06.Count);
			for (int i = 0; i < 9; i++)
			{
				Assert.AreEqual(closeness[i], at06[i].Closeness, Tol, at06[i].Bond);
				Assert.AreEqual(i06[i], at06[i].Intensity, Tol, at06[i].Bond);
				Assert.AreEqual(t06[i], at06[i].Tier, at06[i].Bond);
				Assert.IsFalse(at06[i].OnThreshold, at06[i].Bond);
			}

			var at05 = GrudgePreview.Rows(T, 0.5f);
			float[] i05 = { 20, 25, 35, 45, 50, 70 };
			GrudgeTier[] t05 = { GrudgeTier.Cold, GrudgeTier.Cold, GrudgeTier.Cold, GrudgeTier.Hostile, GrudgeTier.Rally, GrudgeTier.Hunt };
			for (int i = 0; i < 6; i++)
			{
				Assert.AreEqual(i05[i], at05[i].Intensity, Tol, at05[i].Bond);
				Assert.AreEqual(t05[i], at05[i].Tier, at05[i].Bond);
			}
			Assert.IsFalse(at05.Any(r => r.OnThreshold));
		}

		[Test]
		public void Preview_FlagsThresholds()
		{
			SocialTuning t = T;
			t.hostileAt = 42f;
			Assert.IsTrue(GrudgePreview.Rows(t, 0.6f)[2].OnThreshold);
			t = T;
			t.grudgeClosenessThreshold = 0.45f;
			Assert.IsTrue(GrudgePreview.Rows(t, 0.6f)[0].BelowCloseness);
			Assert.AreEqual(GrudgeTier.None, GrudgePreview.Rows(t, 0.6f)[0].Tier);
		}

		// ── KinTerms ────────────────────────────────────────

		[Test]
		public void KinTerms_Words()
		{
			Assert.AreEqual("son", KinTerms.Term(BondFlags.Child, Sex.Male));
			Assert.AreEqual("daughter", KinTerms.Term(BondFlags.Child, Sex.Female));
			Assert.AreEqual("mother", KinTerms.Term(BondFlags.Parent, Sex.Female));
			Assert.AreEqual("brother", KinTerms.Term(BondFlags.Sibling, Sex.Male));
			Assert.AreEqual("wife", KinTerms.Term(BondFlags.Spouse, Sex.Female));
			Assert.IsNull(KinTerms.Term(BondFlags.Friend, Sex.Male));
		}

		// ── AttitudeRules ───────────────────────────────────

		static AttitudeResult A(FactionRelation f, bool kin = false, int opinion = 0, float grudge = 0f) =>
			AttitudeRules.Resolve(f, kin, opinion, grudge, T);

		[Test]
		public void Attitude_Order()
		{
			AssertResult(A(FactionRelation.Hostile, opinion: 100), FactionRelation.Hostile, AttitudeReason.Faction, true);
			AssertResult(A(FactionRelation.Neutral, grudge: 36f), FactionRelation.Hostile, AttitudeReason.Grudge, true);
			AssertResult(A(FactionRelation.Neutral, grudge: 35.9f), FactionRelation.Neutral, AttitudeReason.Faction, true);
			AssertResult(A(FactionRelation.Neutral, grudge: 15f), FactionRelation.Neutral, AttitudeReason.Faction, true);
			AssertResult(A(FactionRelation.Neutral, grudge: 14.9f), FactionRelation.Neutral, AttitudeReason.Faction, false);
			AssertResult(A(FactionRelation.Allied, opinion: -60), FactionRelation.Hostile, AttitudeReason.Opinion, true);
			AssertResult(A(FactionRelation.Neutral, opinion: 60), FactionRelation.Allied, AttitudeReason.Opinion, false);
			AssertResult(A(FactionRelation.Neutral, opinion: 59), FactionRelation.Neutral, AttitudeReason.Faction, false);
			AssertResult(A(FactionRelation.Allied), FactionRelation.Allied, AttitudeReason.Faction, false);
		}

		[Test]
		public void Attitude_CloseKin()
		{
			AssertResult(A(FactionRelation.Hostile, true), FactionRelation.Neutral, AttitudeReason.Kin, false);
			AssertResult(A(FactionRelation.Hostile, true, grudge: 36f), FactionRelation.Hostile, AttitudeReason.Grudge, true);
			AssertResult(A(FactionRelation.Hostile, true, grudge: 20f), FactionRelation.Neutral, AttitudeReason.Kin, true);
			AssertResult(A(FactionRelation.Hostile, true, opinion: -60), FactionRelation.Hostile, AttitudeReason.Opinion, true);
			AssertResult(A(FactionRelation.Hostile, true, opinion: 60), FactionRelation.Allied, AttitudeReason.Opinion, false);
			AssertResult(A(FactionRelation.Neutral, true), FactionRelation.Neutral, AttitudeReason.Faction, false);
			AssertResult(A(FactionRelation.Allied, true), FactionRelation.Allied, AttitudeReason.Faction, false);
		}

		static void AssertResult(AttitudeResult r, FactionRelation relation, AttitudeReason reason, bool refuses)
		{
			Assert.AreEqual(relation, r.Relation);
			Assert.AreEqual(reason, r.Reason);
			Assert.AreEqual(refuses, r.RefusesDealings, "RefusesDealings");
		}

		// ── DefaultBondRules ────────────────────────────────

		static PersonRecord P(string id, string community = null, string kin = null, string household = null) =>
			new PersonRecord { Id = id, CommunityId = community, KinGroupId = kin, HouseholdId = household };

		static readonly CommunityRecord Village = new CommunityRecord { Id = "kuusi", Kind = CommunityKind.Village, ElderId = "elder" };
		static readonly CommunityRecord Camp = new CommunityRecord { Id = "camp", Kind = CommunityKind.Camp };

		static Relationship Between(PersonRecord a, PersonRecord b, CommunityRecord c)
		{
			Assert.IsTrue(DefaultBondRules.Between(a, b, c, T, out Relationship r));
			return r;
		}

		[Test]
		public void DefaultBonds_Layers()
		{
			Assert.AreEqual(new Relationship(60, BondFlags.Family), Between(P("a", household: "h"), P("b", household: "h"), null));
			Assert.AreEqual(new Relationship(30, BondFlags.Family), Between(P("a", kin: "k"), P("b", kin: "k"), null));
			Assert.AreEqual(new Relationship(20, BondFlags.Neighbour), Between(P("a", "kuusi"), P("b", "kuusi"), Village));
			Assert.AreEqual(new Relationship(40, BondFlags.Comrade), Between(P("a", "camp"), P("b", "camp"), Camp));
			Assert.AreEqual(new Relationship(40, BondFlags.Neighbour | BondFlags.Elder), Between(P("a", "kuusi"), P("elder", "kuusi"), Village));
			Assert.AreEqual(new Relationship(20, BondFlags.Neighbour), Between(P("elder", "kuusi"), P("a", "kuusi"), Village));
			Assert.AreEqual(new Relationship(60, BondFlags.Family | BondFlags.Neighbour),
				Between(P("a", "kuusi", "k", "h"), P("b", "kuusi", "k", "h"), Village));
		}

		[Test]
		public void DefaultBonds_NothingShared()
		{
			var a = P("a");
			a.TribeId = "hame";
			var b = P("b");
			b.TribeId = "hame";
			Assert.IsFalse(DefaultBondRules.Between(a, b, null, T, out _), "only the tribe");
			Assert.IsFalse(DefaultBondRules.Between(P("a", household: ""), P("b", household: ""), null, T, out _), "empty ids");
			Assert.IsFalse(DefaultBondRules.Between(P("a"), P("b"), null, T, out _), "null ids");
			Assert.IsFalse(DefaultBondRules.Between(P("a", household: "h"), P("a", household: "h"), null, T, out _), "self");
			Assert.IsFalse(DefaultBondRules.Between(P("a", "kuusi"), P("b", "kuusi"), null, T, out _), "unregistered community");
		}

		// ── NewsRules ───────────────────────────────────────

		[Test]
		public void News_Delays()
		{
			Assert.IsTrue(NewsRules.Delay(P("v", household: "h"), P("r", household: "h"), T, out double h));
			Assert.AreEqual(3.0, h, 1e-6);
			Assert.IsTrue(NewsRules.Delay(P("v", "kuusi"), P("r", "kuusi"), T, out h));
			Assert.AreEqual(3.0, h, 1e-6);
			Assert.IsTrue(NewsRules.Delay(P("v", "kuusi", "k"), P("r", "koivu", "k"), T, out h));
			Assert.AreEqual(24.0, h, 1e-6);
			Assert.IsTrue(NewsRules.Delay(P("v", "kuusi", "k"), P("r", "kuusi", "k"), T, out h));
			Assert.AreEqual(3.0, h, 1e-6);
			Assert.IsFalse(NewsRules.Delay(P("v", "kuusi"), P("r", "koivu"), T, out _));
			Assert.IsFalse(NewsRules.Delay(P("v", ""), P("r", ""), T, out _));
			Assert.IsFalse(NewsRules.Delay(P("v", "kuusi"), P("v", "kuusi"), T, out _));
		}

		// ── TribeOpinionTable ───────────────────────────────

		[Test]
		public void TribeTable()
		{
			var t = new TribeOpinionTable();
			t.Set("hame", "karjala", -10);
			Assert.AreEqual(-10, t.Get("hame", "karjala"));
			Assert.AreEqual(0, t.Get("karjala", "hame"));
			Assert.AreEqual(0, t.Get("hame", "hame"));
			Assert.AreEqual(0, t.Get("", "karjala"));
			Assert.AreEqual(0, t.Get(null, "karjala"));
		}
	}
}
