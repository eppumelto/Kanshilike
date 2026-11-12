using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	public class HealingRulesTests
	{
		const float Eps = 1e-4f;

		// RegenPerHour
		[Test]
		public void RegenPerHour_Awake_ReturnsNaturalRate()
		{
			Assert.AreEqual(3f, HealingRules.RegenPerHour(0f, false, false, 3f, 8f), Eps);
		}

		[Test]
		public void RegenPerHour_Asleep_ReturnsSleepRateNotSum()
		{
			Assert.AreEqual(8f, HealingRules.RegenPerHour(1f, false, false, 3f, 8f), Eps);
		}

		[Test]
		public void RegenPerHour_PartlyAsleep_BlendsByShareOfHour()
		{
			Assert.AreEqual(5.5f, HealingRules.RegenPerHour(0.5f, false, false, 3f, 8f), Eps);
			Assert.AreEqual(3.5f, HealingRules.RegenPerHour(0.1f, false, false, 3f, 8f), Eps);
		}

		[TestCase(-1f, 3f)]
		[TestCase(2f, 8f)]
		public void RegenPerHour_FractionOutOfRange_Clamps(float fraction, float expected)
		{
			Assert.AreEqual(expected, HealingRules.RegenPerHour(fraction, false, false, 3f, 8f), Eps);
		}

		[TestCase(1f, true, false)]
		[TestCase(1f, false, true)]
		[TestCase(1f, true, true)]
		[TestCase(0f, true, false)]
		[TestCase(0f, false, true)]
		[TestCase(0f, true, true)]
		public void RegenPerHour_StarvingOrDehydrated_ReturnsZero(float asleep, bool starving, bool dehydrated)
		{
			Assert.AreEqual(0f, HealingRules.RegenPerHour(asleep, starving, dehydrated, 3f, 8f), Eps);
		}

		[Test]
		public void RegenPerHour_NegativeRates_ClampToZero()
		{
			Assert.AreEqual(0f, HealingRules.RegenPerHour(0f, false, false, -2f, 8f), Eps);
			Assert.AreEqual(0f, HealingRules.RegenPerHour(1f, false, false, 3f, -2f), Eps);
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
