using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	public class RecruitRulesTests
	{
		static RecruitBlock Check(bool alive = true, FactionRelation relation = FactionRelation.Neutral, bool inCombat = false,
			bool inReach = true, int carried = 0, int cost = 0, bool unwilling = false) =>
			RecruitRules.Check(alive, relation, inCombat, inReach, carried, cost, unwilling);

		[Test]
		public void Willing_WhenEverythingPasses() => Assert.AreEqual(RecruitBlock.None, Check());

		[Test]
		public void EachBlock()
		{
			Assert.AreEqual(RecruitBlock.Dead, Check(alive: false));
			Assert.AreEqual(RecruitBlock.Hostile, Check(relation: FactionRelation.Hostile));
			Assert.AreEqual(RecruitBlock.InCombat, Check(inCombat: true));
			Assert.AreEqual(RecruitBlock.TooFar, Check(inReach: false));
			Assert.AreEqual(RecruitBlock.CantAfford, Check(carried: 1, cost: 2));
			Assert.AreEqual(RecruitBlock.Unwilling, Check(unwilling: true));
		}

		[Test]
		public void Precedence_FollowsEnumOrder()
		{
			Assert.AreEqual(RecruitBlock.Dead, Check(alive: false, relation: FactionRelation.Hostile));
			Assert.AreEqual(RecruitBlock.Hostile, Check(relation: FactionRelation.Hostile, inCombat: true));
			Assert.AreEqual(RecruitBlock.InCombat, Check(inCombat: true, inReach: false));
			Assert.AreEqual(RecruitBlock.TooFar, Check(inReach: false, cost: 1));
			Assert.AreEqual(RecruitBlock.CantAfford, Check(cost: 1, unwilling: true));
		}

		[Test]
		public void Cost()
		{
			Assert.AreEqual(RecruitBlock.None, Check(cost: 0));
			Assert.AreEqual(RecruitBlock.None, Check(cost: -3));
			Assert.AreEqual(RecruitBlock.None, Check(carried: 2, cost: 2));
		}

		[Test]
		public void Unwilling_OnlyWhenEarlierChecksPass()
		{
			Assert.AreEqual(RecruitBlock.TooFar, Check(inReach: false, unwilling: true));
			Assert.AreEqual(RecruitBlock.Unwilling, Check(carried: 1, cost: 1, unwilling: true));
		}

		[Test]
		public void SerializedNumbers_AndReasons()
		{
			Assert.AreEqual(5, (int)RecruitBlock.CantAfford);
			Assert.AreEqual(6, (int)RecruitBlock.Unwilling);
			foreach (RecruitBlock b in System.Enum.GetValues(typeof(RecruitBlock)))
				Assert.IsNotEmpty(RecruitRules.Reason(b), b.ToString());
			Assert.AreEqual("Won't share a fire with your people", RecruitRules.Reason(RecruitBlock.Unwilling));
		}
	}
}
