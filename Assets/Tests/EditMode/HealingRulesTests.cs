using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	public class HealingRulesTests
	{
		const float Eps = 1e-4f;

		// WorthTreating
		[TestCase(80f, false)]   // missing 20 of a 50 heal: less than half
		[TestCase(75f, true)]    // missing exactly half
		[TestCase(30f, true)]
		public void WorthTreating_NeedsHalfTheHealMissing(float current, bool expected)
		{
			Assert.AreEqual(expected, HealingRules.WorthTreating(current, 100f, 50f, 0.5f));
		}

		[Test]
		public void WorthTreating_ZeroShare_AnyWoundCounts()
		{
			Assert.IsTrue(HealingRules.WorthTreating(99f, 100f, 50f, 0f));
		}

		[TestCase(100f)]
		[TestCase(0f)]
		[TestCase(-5f)]
		public void WorthTreating_FullHealthOrDead_False(float current)
		{
			Assert.IsFalse(HealingRules.WorthTreating(current, 100f, 50f, 0f));
		}

		[Test]
		public void WorthTreating_HealBiggerThanMax_UsesMaxInstead()
		{
			// A 200 HP dressing on a 40 HP creature: missing half of 40 is enough.
			Assert.IsTrue(HealingRules.WorthTreating(20f, 40f, 200f, 0.5f));
		}

		// HealthGained
		[Test]
		public void HealthGained_BelowMax_ReturnsFullAmount()
		{
			Assert.AreEqual(10f, HealingRules.HealthGained(50f, 100f, 10f), Eps);
		}

		[Test]
		public void HealthGained_WouldExceedMax_ClampsAtMax()
		{
			Assert.AreEqual(5f, HealingRules.HealthGained(95f, 100f, 25f), Eps);
		}

		[Test]
		public void HealthGained_AtFullHealth_ReturnsZero()
		{
			Assert.AreEqual(0f, HealingRules.HealthGained(100f, 100f, 25f), Eps);
		}

		[TestCase(0f)]
		[TestCase(-5f)]
		public void HealthGained_DeadOrBelowZero_ReturnsZero(float current)
		{
			Assert.AreEqual(0f, HealingRules.HealthGained(current, 100f, 25f), Eps);
		}

		[Test]
		public void HealthGained_NegativeAmount_ReturnsZero()
		{
			Assert.AreEqual(0f, HealingRules.HealthGained(50f, 100f, -10f), Eps);
		}

		[Test]
		public void HealthGained_ZeroAmount_ReturnsZero()
		{
			Assert.AreEqual(0f, HealingRules.HealthGained(50f, 100f, 0f), Eps);
		}

		// CanTreat
		[Test]
		public void CanTreat_AtFullHealth_ReturnsFalse()
		{
			Assert.IsFalse(HealingRules.CanTreat(100f, 100f));
		}

		[TestCase(0f)]
		[TestCase(-1f)]
		public void CanTreat_Dead_ReturnsFalse(float current)
		{
			Assert.IsFalse(HealingRules.CanTreat(current, 100f));
		}

		[TestCase(1f)]
		[TestCase(50f)]
		[TestCase(99.9f)]
		public void CanTreat_Wounded_ReturnsTrue(float current)
		{
			Assert.IsTrue(HealingRules.CanTreat(current, 100f));
		}

		// TreatmentHeal
		[Test]
		public void TreatmentHeal_BonusOne_ReturnsBaseHeal()
		{
			Assert.AreEqual(25f, HealingRules.TreatmentHeal(25f, 1f, 1f), Eps);
		}

		[Test]
		public void TreatmentHeal_Bonus125_Returns3125()
		{
			Assert.AreEqual(31.25f, HealingRules.TreatmentHeal(25f, 1.25f, 1f), Eps);
		}

		[Test]
		public void TreatmentHeal_ScaleZero_IgnoresSkill()
		{
			Assert.AreEqual(25f, HealingRules.TreatmentHeal(25f, 2f, 0f), Eps);
		}

		// TreatmentSeconds
		[Test]
		public void TreatmentSeconds_BonusOne_ReturnsBaseSeconds()
		{
			Assert.AreEqual(4f, HealingRules.TreatmentSeconds(4f, 1f, 1f, 1.5f), Eps);
		}

		[Test]
		public void TreatmentSeconds_Bonus125_Returns3Point2()
		{
			Assert.AreEqual(3.2f, HealingRules.TreatmentSeconds(4f, 1.25f, 1f, 1.5f), Eps);
		}

		[Test]
		public void TreatmentSeconds_HugeBonus_NeverBelowMinimum()
		{
			Assert.AreEqual(1.5f, HealingRules.TreatmentSeconds(4f, 50f, 1f, 1.5f), Eps);
		}

		[Test]
		public void TreatmentSeconds_ScaleZero_IgnoresSkill()
		{
			Assert.AreEqual(4f, HealingRules.TreatmentSeconds(4f, 2f, 0f, 1.5f), Eps);
		}

		[Test]
		public void TreatmentSeconds_DegenerateBonus_StaysFinite()
		{
			float s = HealingRules.TreatmentSeconds(4f, 0f, 5f, 1.5f);
			Assert.IsFalse(float.IsInfinity(s) || float.IsNaN(s));
			Assert.GreaterOrEqual(s, 1.5f);
		}

		// TreatmentXP
		[Test]
		public void TreatmentXP_ScalesWithHealthGained()
		{
			Assert.AreEqual(50f, HealingRules.TreatmentXP(25f, 2f), Eps);
			Assert.AreEqual(10f, HealingRules.TreatmentXP(5f, 2f), Eps);
		}

		[Test]
		public void TreatmentXP_ZeroGained_ReturnsZero()
		{
			Assert.AreEqual(0f, HealingRules.TreatmentXP(0f, 2f), Eps);
		}

		[Test]
		public void TreatmentXP_NegativeGained_ReturnsZero()
		{
			Assert.AreEqual(0f, HealingRules.TreatmentXP(-5f, 2f), Eps);
		}
	}
}
