using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>Pins the combat maths, using the default values from CombatFormulaConfig where it matters.</summary>
	public class CombatRulesTests
	{
		const float Eps = 0.0001f;

		[TestCase(1f, 0f)]
		[TestCase(2f, 1f)]
		[TestCase(1.5f, 0.5f)]
		[TestCase(0.5f, 0f)]
		public void SkillProgress_IsBonusMinusOneAndNeverNegative(float bonus, float expected)
		{
			Assert.AreEqual(expected, CombatRules.SkillProgress(bonus), Eps);
		}

		[Test]
		public void AttackRating_UntrainedIsBaseRating()
		{
			Assert.AreEqual(10f, CombatRules.AttackRating(10f, 1f, 0f, 1f, 0f, 0.25f), Eps);
		}

		[Test]
		public void AttackRating_MasterOfWeaponAndStrengthAddsBothScales()
		{
			Assert.AreEqual(22.5f, CombatRules.AttackRating(10f, 1f, 1f, 1f, 1f, 0.25f), Eps);
		}

		[Test]
		public void AttackRating_NaturalAttackMultiplierApplies()
		{
			Assert.AreEqual(15f, CombatRules.AttackRating(10f, 1.5f, 0f, 1f, 0f, 0.25f), Eps);
		}

		[Test]
		public void AttackRating_NeverBelowMinimum()
		{
			Assert.AreEqual(0.1f, CombatRules.AttackRating(0f, 1f, 0f, 1f, 0f, 0f), Eps);
		}

		[Test]
		public void Damage_ScalesWithStrengthAndWeaponSkill()
		{
			Assert.AreEqual(5f, CombatRules.Damage(5f, 0f, 0.5f, 0f, 0.5f), Eps);
			Assert.AreEqual(10f, CombatRules.Damage(5f, 1f, 0.5f, 1f, 0.5f), Eps);
		}

		[Test]
		public void Damage_NeverBelowOne()
		{
			Assert.AreEqual(1f, CombatRules.Damage(0.2f, 0f, 0.5f, 0f, 0.5f), Eps);
		}

		[Test]
		public void AttackInterval_FasterWithSpeedAndInitiative()
		{
			Assert.AreEqual(1.2f, CombatRules.AttackInterval(1.2f, 0.2f, 1f, 1f), Eps);
			Assert.AreEqual(0.6f, CombatRules.AttackInterval(1.2f, 0.2f, 2f, 1f), Eps);
			Assert.AreEqual(0.6f, CombatRules.AttackInterval(1.2f, 0.2f, 1f, CombatRules.Initiative(1f, 0.5f, 1f, 0.5f)), Eps);
		}

		[Test]
		public void AttackInterval_NeverBelowMinimum()
		{
			Assert.AreEqual(0.2f, CombatRules.AttackInterval(1.2f, 0.2f, 100f, 1f), Eps);
		}

		[Test]
		public void DefenseRating_ZeroWhenReactionUnavailable()
		{
			Assert.AreEqual(0f, CombatRules.DefenseRating(false, 10f, 1f, 1f), Eps);
		}

		[Test]
		public void DefenseRating_ScalesWithSkillAndShield()
		{
			Assert.AreEqual(10f, CombatRules.DefenseRating(true, 10f, 0f, 1f), Eps);
			Assert.AreEqual(20f, CombatRules.DefenseRating(true, 10f, 1f, 1f), Eps);
			Assert.AreEqual(32f, CombatRules.DefenseRating(true, 10f, 1f, 1f, 1.6f), Eps);
		}

		[TestCase(10f, 10f, 0.5f)]
		[TestCase(20f, 10f, 2f / 3f)]
		[TestCase(0f, 10f, 0f)]
		public void DefenseChance_IsDefenseOverDefensePlusAttack(float defense, float attack, float expected)
		{
			Assert.AreEqual(expected, CombatRules.DefenseChance(defense, attack), Eps);
		}

		[Test]
		public void ReactionWeight_ZeroWhenUnavailableAndDoubledForMaster()
		{
			Assert.AreEqual(0f, CombatRules.ReactionWeight(false, 1f, 1f), Eps);
			Assert.AreEqual(2f, CombatRules.ReactionWeight(true, 1f, 1f), Eps);
			Assert.AreEqual(4f, CombatRules.ReactionWeight(true, 1f, 1f, 2f), Eps);
		}

		[TestCase(0f, CombatReactionType.Block)]
		[TestCase(0.24f, CombatReactionType.Block)]
		[TestCase(0.26f, CombatReactionType.Dodge)]
		[TestCase(0.74f, CombatReactionType.Dodge)]
		[TestCase(0.76f, CombatReactionType.Counter)]
		[TestCase(0.999f, CombatReactionType.Counter)]
		public void ChooseReaction_FollowsTheWeights(float roll, CombatReactionType expected)
		{
			Assert.AreEqual(expected, CombatRules.ChooseReaction(1f, 2f, 1f, roll));
		}

		[Test]
		public void ChooseReaction_NoneWhenNothingIsAvailable()
		{
			Assert.AreEqual(CombatReactionType.None, CombatRules.ChooseReaction(0f, 0f, 0f, 0.5f));
		}

		[Test]
		public void ChooseReaction_SkipsUnavailableReactions()
		{
			Assert.AreEqual(CombatReactionType.Dodge, CombatRules.ChooseReaction(0f, 1f, 0f, 0f));
			Assert.AreEqual(CombatReactionType.Dodge, CombatRules.ChooseReaction(0f, 1f, 0f, 0.999f));
		}

		[Test]
		public void FailedBlockDamage_TakesTheShieldsReductionOff()
		{
			Assert.AreEqual(7f, CombatRules.FailedBlockDamage(10f, 0.3f), Eps);
		}

		[Test]
		public void CounterDamage_IsAFractionAndNeverBelowOne()
		{
			Assert.AreEqual(5f, CombatRules.CounterDamage(10f, 0.5f), Eps);
			Assert.AreEqual(1f, CombatRules.CounterDamage(1f, 0.5f), Eps);
		}

		[Test]
		public void BowSpread_MasterIsTighterThanUntrained()
		{
			Assert.AreEqual(3f, CombatRules.BowSpread(0f, 0f, 0.25f, 3f, 0.75f, 1f, 0f, 5f), Eps);
			Assert.AreEqual(0.75f, CombatRules.BowSpread(1f, 1f, 0.25f, 3f, 0.75f, 1f, 0f, 5f), Eps);
		}

		[Test]
		public void BowSpread_AimSkillOnlyCountsForItsShare()
		{
			// Skill is 0.25 of the way from untrained to master.
			Assert.AreEqual(2.4375f, CombatRules.BowSpread(0f, 1f, 0.25f, 3f, 0.75f, 0f, 0f, 5f), Eps);
		}

		[Test]
		public void BowSpread_MovingTargetWidensItUpToTheLimit()
		{
			Assert.AreEqual(3.5f, CombatRules.BowSpread(0f, 0f, 0.25f, 3f, 0.75f, 1f, 2.5f, 5f), Eps);
			Assert.AreEqual(4f, CombatRules.BowSpread(0f, 0f, 0.25f, 3f, 0.75f, 1f, 50f, 5f), Eps);
		}

		[Test]
		public void BowHitChance_CertainWithNoSpread()
		{
			Assert.AreEqual(1f, CombatRules.BowHitChance(0f, 20f, 0.5f), Eps);
		}

		[Test]
		public void BowHitChance_DropsWithDistanceAndNeverExceedsOne()
		{
			float near = CombatRules.BowHitChance(3f, 5f, 0.5f);
			float far = CombatRules.BowHitChance(3f, 25f, 0.5f);
			Assert.AreEqual(1f, near, Eps);
			Assert.Less(far, near);
			Assert.Greater(far, 0f);
		}

		[TestCase(true, true, true, true)]
		[TestCase(false, true, true, false)]
		[TestCase(true, false, true, false)]
		[TestCase(true, true, false, false)]
		public void ShouldDefendSelf_NeedsLifeAYieldingOrderAndAnEnemy(bool alive, bool orderYields, bool hostile, bool expected)
		{
			Assert.AreEqual(expected, CombatRules.ShouldDefendSelf(alive, orderYields, hostile));
		}
	}
}
