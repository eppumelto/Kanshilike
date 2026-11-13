using NUnit.Framework;
using UnityEngine;

namespace Kanshilike.Tests.EditMode
{
	public class FactionRelationsTests
	{
		static readonly FactionId Player = new FactionId("player");
		static readonly FactionId Wildlife = new FactionId("wildlife");
		static readonly FactionId Townsfolk = new FactionId("townsfolk");
		static readonly FactionId Traders = new FactionId("traders");
		static readonly FactionId Bandits = new FactionId("bandits");
		static readonly FactionId[] All = { Player, Wildlife, Townsfolk, Traders, Bandits };

		static FactionRelationTable Shipped()
		{
			var t = new FactionRelationTable();
			t.Define(Player, FactionRelation.Neutral);
			t.Define(Wildlife, FactionRelation.Hostile);
			t.Define(Townsfolk, FactionRelation.Neutral);
			t.Define(Traders, FactionRelation.Neutral);
			t.Define(Bandits, FactionRelation.Hostile);
			return t;
		}

		[TearDown]
		public void TearDown() => FactionRelations.ResetToDefault();

		[Test]
		public void EachFaction_AlliedWithItself()
		{
			var t = Shipped();
			foreach (FactionId f in All)
				Assert.AreEqual(FactionRelation.Allied, t.Get(f, f), f.ToString());
		}

		[Test]
		public void Wildlife_HostileToEveryone()
		{
			var t = Shipped();
			foreach (FactionId f in new[] { Player, Townsfolk, Traders, Bandits })
				Assert.AreEqual(FactionRelation.Hostile, t.Get(Wildlife, f), f.ToString());
		}

		[Test]
		public void Bandits_HostileToPeople()
		{
			var t = Shipped();
			foreach (FactionId f in new[] { Player, Townsfolk, Traders })
				Assert.AreEqual(FactionRelation.Hostile, t.Get(Bandits, f), f.ToString());
		}

		[Test]
		public void PlayerTradersTownsfolk_NeutralToEachOther()
		{
			var t = Shipped();
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Player, Townsfolk));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Player, Traders));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Traders, Townsfolk));
		}

		[Test]
		public void Relations_AreSymmetric()
		{
			var t = Shipped();
			t.Set(Player, Traders, FactionRelation.Allied);
			foreach (FactionId a in All)
				foreach (FactionId b in All)
					Assert.AreEqual(t.Get(a, b), t.Get(b, a), $"{a} vs {b}");
		}

		[Test]
		public void Set_OverridesStances_BothWays_AndClearRestores()
		{
			var t = Shipped();
			t.Set(Wildlife, Player, FactionRelation.Neutral);
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Wildlife, Player));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Player, Wildlife));

			t.Clear(Player, Wildlife);
			Assert.AreEqual(FactionRelation.Hostile, t.Get(Wildlife, Player));
		}

		[Test]
		public void ExplicitSelfEntry_OverridesAllied()
		{
			var t = Shipped();
			t.Set(Bandits, Bandits, FactionRelation.Hostile);
			Assert.AreEqual(FactionRelation.Hostile, t.Get(Bandits, Bandits));
		}

		[Test]
		public void DifferentFactions_MoreHostileStanceWins()
		{
			var t = Shipped();
			var allies = new FactionId("allies");
			t.Define(allies, FactionRelation.Allied);
			Assert.AreEqual(FactionRelation.Hostile, t.Get(Wildlife, Bandits));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Townsfolk, allies));
		}

		[Test]
		public void UndefinedId_ActsNeutral_AndDefineOverwrites()
		{
			var t = Shipped();
			var stranger = new FactionId("stranger");
			Assert.IsFalse(t.IsDefined(stranger));
			Assert.AreEqual(FactionRelation.Neutral, t.DefaultStanceOf(stranger));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(stranger, Townsfolk));

			t.Define(stranger, FactionRelation.Hostile);
			Assert.AreEqual(FactionRelation.Hostile, t.Get(stranger, Townsfolk));
			t.Define(stranger, FactionRelation.Allied);
			Assert.AreEqual(FactionRelation.Neutral, t.Get(stranger, Townsfolk));
		}

		[Test]
		public void None_IsNeutralToEverything()
		{
			var t = Shipped();
			Assert.AreEqual(FactionRelation.Neutral, t.Get(FactionId.None, Wildlife));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Bandits, FactionId.None));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(FactionId.None, FactionId.None));
		}

		[Test]
		public void Explain_GivesSource()
		{
			var t = Shipped();
			t.Set(Player, Traders, FactionRelation.Allied);

			t.Explain(Player, Player, out FactionRelationSource s);
			Assert.AreEqual(FactionRelationSource.Same, s);
			t.Explain(Traders, Player, out s);
			Assert.AreEqual(FactionRelationSource.Explicit, s);
			t.Explain(Wildlife, Player, out s);
			Assert.AreEqual(FactionRelationSource.DefaultStance, s);
			t.Explain(FactionId.None, Player, out s);
			Assert.AreEqual(FactionRelationSource.Unaffiliated, s);
		}

		[Test]
		public void FactionId_Equality()
		{
			Assert.AreEqual(new FactionId("abc"), new FactionId("abc"));
			Assert.AreEqual(new FactionId("abc").GetHashCode(), new FactionId("abc").GetHashCode());
			Assert.AreNotEqual(new FactionId("abc"), new FactionId("ABC"));
			Assert.IsTrue(default(FactionId).IsNone);
			Assert.IsTrue(new FactionId("").IsNone);
			Assert.IsTrue(new FactionId(null) == FactionId.None);
			Assert.IsTrue(new FactionId("") == default(FactionId));
		}

		[Test]
		public void Facade_MatchesTable()
		{
			var t = Shipped();
			FactionRelations.Configure(t, null, null);
			foreach (FactionId a in All)
				foreach (FactionId b in All)
				{
					FactionRelation r = t.Get(a, b);
					Assert.AreEqual(r == FactionRelation.Hostile, FactionRelations.AreHostile(a, b));
					Assert.AreEqual(r == FactionRelation.Allied, FactionRelations.AreAllied(a, b));
					Assert.AreEqual(r != FactionRelation.Allied, FactionRelations.FightsBack(a, b));
				}
		}

		[Test]
		public void SerializedNumbers_NeverChange()
		{
			Assert.AreEqual(0, (int)FactionRelation.Hostile);
			Assert.AreEqual(1, (int)FactionRelation.Neutral);
			Assert.AreEqual(2, (int)FactionRelation.Allied);
			Assert.AreEqual(0, (int)FactionSource.Own);
			Assert.AreEqual(1, (int)FactionSource.Squad);
			Assert.AreEqual(2, (int)FactionSource.Community);
		}

		[Test]
		public void DefinitionAsset_IdFallsBackToAssetName()
		{
			var f = FactionDefinition.Create("Iron Wolves", FactionRelation.Hostile);
			try
			{
				Assert.AreEqual("iron-wolves", f.Id);
				Assert.AreEqual("Iron Wolves", f.DisplayName);
				Assert.AreEqual(new FactionId("iron-wolves"), f.FactionId);
			}
			finally
			{
				Object.DestroyImmediate(f);
			}
		}
	}

	public class FactionRulesTests
	{
		static readonly FactionId Player = new FactionId("player");
		static readonly FactionId Village = new FactionId("kuusikyla");
		static readonly FactionId Own = new FactionId("townsfolk");

		[Test]
		public void SquadMember_IsPlayer_OverCommunityAndOwn()
		{
			FactionResolution r = FactionRules.Resolve(true, Village, Own, Player);
			Assert.AreEqual(Player, r.Id);
			Assert.AreEqual(FactionSource.Squad, r.Source);
		}

		[Test]
		public void SquadMember_WithNoPlayerFaction_FallsThrough()
		{
			Assert.AreEqual(FactionSource.Community, FactionRules.Resolve(true, Village, Own, FactionId.None).Source);
			Assert.AreEqual(FactionSource.Own, FactionRules.Resolve(true, FactionId.None, Own, FactionId.None).Source);
		}

		[Test]
		public void Community_WinsOverOwn()
		{
			FactionResolution r = FactionRules.Resolve(false, Village, Own, Player);
			Assert.AreEqual(Village, r.Id);
			Assert.AreEqual(FactionSource.Community, r.Source);
		}

		[Test]
		public void Own_WhenNoCommunity()
		{
			FactionResolution r = FactionRules.Resolve(false, FactionId.None, Own, Player);
			Assert.AreEqual(Own, r.Id);
			Assert.AreEqual(FactionSource.Own, r.Source);
		}

		[Test]
		public void AllNone_GivesNone()
		{
			Assert.IsTrue(FactionRules.Resolve(false, FactionId.None, FactionId.None, FactionId.None).Id.IsNone);
		}
	}
}
