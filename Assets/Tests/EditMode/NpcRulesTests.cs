using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	public class NpcScheduleRulesTests
	{
		static readonly float[] Day = { 6f, 9f, 17f, 21f };

		[Test]
		public void ResolveIndex_EmptyAndSingle()
		{
			Assert.AreEqual(-1, NpcScheduleRules.ResolveIndex(new float[0], 10f));
			Assert.AreEqual(-1, NpcScheduleRules.ResolveIndex(null, 10f));
			foreach (float h in new[] { 0f, 5f, 12f, 23.9f })
				Assert.AreEqual(0, NpcScheduleRules.ResolveIndex(new[] { 9f }, h), "hour " + h);
		}

		[TestCase(5.9f, 3)]
		[TestCase(6f, 0)]
		[TestCase(8.99f, 0)]
		[TestCase(9f, 1)]
		[TestCase(23.5f, 3)]
		[TestCase(0f, 3)]
		public void ResolveIndex_Day(float hour, int expected)
		{
			Assert.AreEqual(expected, NpcScheduleRules.ResolveIndex(Day, hour));
		}

		[Test]
		public void ResolveIndex_Unsorted()
		{
			Assert.AreEqual(1, NpcScheduleRules.ResolveIndex(new[] { 21f, 6f }, 7f));
			Assert.AreEqual(0, NpcScheduleRules.ResolveIndex(new[] { 21f, 6f }, 3f), "wraps to the latest start");
		}

		[Test]
		public void ResolveIndex_DuplicateStart_LowestIndex()
		{
			Assert.AreEqual(0, NpcScheduleRules.ResolveIndex(new[] { 9f, 9f }, 10f));
			Assert.AreEqual(1, NpcScheduleRules.ResolveIndex(new[] { 20f, 9f, 9f }, 10f));
		}

		[Test]
		public void Hours_AreNormalised()
		{
			Assert.AreEqual(0f, NpcScheduleRules.NormalizeHour(24f));
			Assert.AreEqual(23f, NpcScheduleRules.NormalizeHour(-1f));
			Assert.AreEqual(0, NpcScheduleRules.ResolveIndex(Day, 24f + 6f));
			Assert.AreEqual(3, NpcScheduleRules.ResolveIndex(Day, -1f));
		}

		[Test]
		public void HoursUntilNext()
		{
			Assert.AreEqual(8f, NpcScheduleRules.HoursUntilNext(new[] { 21f, 6f }, 22f), 1e-4f);
			Assert.AreEqual(1f, NpcScheduleRules.HoursUntilNext(new[] { 21f, 6f }, 5f), 1e-4f);
			Assert.AreEqual(24f, NpcScheduleRules.HoursUntilNext(new[] { 9f }, 9f), 1e-4f);
			Assert.AreEqual(18f, NpcScheduleRules.HoursUntilNext(new[] { 9f }, 15f), 1e-4f);
			Assert.AreEqual(0f, NpcScheduleRules.HoursUntilNext(new float[0], 5f));
		}

		[Test]
		public void ResolvePlacement()
		{
			Assert.AreEqual(ActivityPlacement.AtHome,
				NpcScheduleRules.ResolvePlacement(true, false, 0, 0, true, MissingAnchorFallback.RunAtHome));
			Assert.AreEqual(ActivityPlacement.WanderAtHome,
				NpcScheduleRules.ResolvePlacement(true, false, 0, 0, true, MissingAnchorFallback.WanderAtHome));
			Assert.AreEqual(ActivityPlacement.WanderAtHome,
				NpcScheduleRules.ResolvePlacement(true, true, 1, 2, true, MissingAnchorFallback.WanderAtHome), "patrol, one waypoint");
			Assert.AreEqual(ActivityPlacement.Normal,
				NpcScheduleRules.ResolvePlacement(true, true, 2, 2, true, MissingAnchorFallback.WanderAtHome), "patrol, two waypoints");
			Assert.AreEqual(ActivityPlacement.WanderAtHome,
				NpcScheduleRules.ResolvePlacement(false, false, 0, 0, false, MissingAnchorFallback.RunAtHome), "follow with no leader");
			Assert.AreEqual(ActivityPlacement.Normal,
				NpcScheduleRules.ResolvePlacement(false, false, 0, 0, true, MissingAnchorFallback.RunAtHome));
		}

		[Test]
		public void SerializedNumbers_NeverChange()
		{
			Assert.AreEqual(0, (int)MissingAnchorFallback.RunAtHome);
			Assert.AreEqual(1, (int)MissingAnchorFallback.WanderAtHome);
			Assert.AreEqual(0, (int)ActivityPlacement.Normal);
			Assert.AreEqual(2, (int)ActivityPlacement.WanderAtHome);
			Assert.AreEqual(10, (int)NpcPriority.Schedule);
			Assert.AreEqual(50, (int)NpcPriority.Flee);
		}
	}

	public class NpcBrainRulesTests
	{
		[Test]
		public void ShouldReplace_PriorityLadder()
		{
			Assert.IsTrue(NpcBrainRules.ShouldReplace(NpcPriority.External, NpcPriority.Flee));
			Assert.IsFalse(NpcBrainRules.ShouldReplace(NpcPriority.External, NpcPriority.Schedule));
			Assert.IsTrue(NpcBrainRules.ShouldReplace(NpcPriority.Social, NpcPriority.Role));
			Assert.IsTrue(NpcBrainRules.ShouldReplace(NpcPriority.None, NpcPriority.Schedule));
			Assert.IsFalse(NpcBrainRules.ShouldReplace(NpcPriority.Role, NpcPriority.Role));
		}

		[Test]
		public void RoleMayReplace()
		{
			Assert.IsTrue(NpcBrainRules.RoleMayReplace(NpcPriority.None));
			Assert.IsTrue(NpcBrainRules.RoleMayReplace(NpcPriority.Schedule));
			Assert.IsTrue(NpcBrainRules.RoleMayReplace(NpcPriority.Social));
			Assert.IsTrue(NpcBrainRules.RoleMayReplace(NpcPriority.Role));
			Assert.IsFalse(NpcBrainRules.RoleMayReplace(NpcPriority.External));
			Assert.IsFalse(NpcBrainRules.RoleMayReplace(NpcPriority.Flee));
		}

		[Test]
		public void CurrentPriority()
		{
			Assert.AreEqual(NpcPriority.None, NpcBrainRules.CurrentPriority(true, false, NpcPriority.Role));
			Assert.AreEqual(NpcPriority.Role, NpcBrainRules.CurrentPriority(false, true, NpcPriority.Role));
			Assert.AreEqual(NpcPriority.External, NpcBrainRules.CurrentPriority(false, false, NpcPriority.Schedule));
		}

		[Test]
		public void ShouldFlee()
		{
			Assert.IsTrue(NpcBrainRules.ShouldFlee(34f, 100f, 0.35f, true));
			Assert.IsFalse(NpcBrainRules.ShouldFlee(35f, 100f, 0.35f, true), "at the threshold");
			Assert.IsFalse(NpcBrainRules.ShouldFlee(10f, 100f, 0f, true), "never flees");
			Assert.IsFalse(NpcBrainRules.ShouldFlee(10f, 100f, 0.35f, false), "no threat");
			Assert.IsFalse(NpcBrainRules.ShouldFlee(10f, 0f, 0.35f, true), "no max health");
		}
	}
}
