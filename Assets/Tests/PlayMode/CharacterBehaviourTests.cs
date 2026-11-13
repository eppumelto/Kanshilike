using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>
	/// Orders, self-defence, item use and corpses on characters built in code, with no scene, NavMesh
	/// or UI. Characters stand within unarmed reach of each other, so nobody needs to walk.
	/// </summary>
	public class CharacterBehaviourTests
	{
		const float Reach = 1f; // inside the default unarmed range of 1.4

		readonly List<Object> made = new List<Object>();
		CombatFormulaConfig config;
		TestFactions factions;

		[SetUp]
		public void SetUp()
		{
			factions = new TestFactions();
			factions.Install();
			config = Track(ScriptableObject.CreateInstance<CombatFormulaConfig>());
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var obj in made)
				if (obj != null)
					Object.Destroy(obj);
			made.Clear();
			factions.Uninstall();
		}

		// ── Orders ──────────────────────────────────────────

		[Test]
		public void Orders_SayWhetherSelfDefenceMayReplaceThem()
		{
			Assert.IsFalse(new MoveOrder(Vector3.zero).YieldsToSelfDefence, "walking away");
			Assert.IsFalse(new AttackOrder(null).YieldsToSelfDefence, "already fighting");
			Assert.IsTrue(new InteractOrder(null, "x").YieldsToSelfDefence, "busy with a task");
			Assert.IsTrue(new SleepOrder(8f).YieldsToSelfDefence, "asleep");
			Assert.IsTrue(new TreatWoundsOrder(null, null, null).StopsWhenAttacked, "binding wounds");

			// NPC routine orders: a townsperson walking to work still defends itself; fleeing does not stop to fight.
			Assert.IsTrue(new WanderOrder(Vector3.zero, 5f, Vector2.one).YieldsToSelfDefence, "wandering");
			Assert.IsTrue(new PatrolOrder(new[] { Vector3.zero, Vector3.one }, 1f).YieldsToSelfDefence, "patrolling");
			Assert.IsTrue(new IdleAtOrder(Vector3.zero).YieldsToSelfDefence, "idling");
			Assert.IsTrue(new FollowOrder(null).YieldsToSelfDefence, "following");
			Assert.IsTrue(new SleepOrder(8f, Vector3.zero).YieldsToSelfDefence, "going to bed");
			Assert.IsFalse(new FleeOrder(null, 10f).YieldsToSelfDefence, "fleeing");
		}

		[UnityTest]
		public IEnumerator GiveOrder_EndsThePreviousOrderOnce()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player);
			yield return null;

			var first = new RecordingOrder();
			var second = new RecordingOrder();
			c.GiveOrder(first);
			c.GiveOrder(second);

			Assert.AreEqual(1, first.EndCount);
			Assert.AreEqual(0, second.EndCount);
			Assert.AreSame(second, c.CurrentOrder);

			c.GiveOrder(null);
			Assert.AreEqual(1, second.EndCount);
			Assert.IsNull(c.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator FinishedOrder_EndsAndLeavesTheCharacterIdle()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player);
			yield return null;

			var order = new RecordingOrder { Frames = 2 };
			c.GiveOrder(order);
			yield return WaitFor(() => c.CurrentOrder == null, 1f);

			Assert.IsNull(c.CurrentOrder);
			Assert.AreEqual(1, order.EndCount);
		}

		// ── Fighting and self-defence ───────────────────────

		[UnityTest]
		public IEnumerator HostileAttack_IdleDefenderFightsBack()
		{
			Character defender = MakeCharacter("Defender", Vector3.zero, factions.Player);
			Character attacker = MakeCharacter("Attacker", Vector3.right * Reach, factions.Wildlife);
			yield return null;

			attacker.AttackTarget(defender);
			yield return WaitFor(() => IsAttacking(defender, attacker), 5f);

			Assert.IsTrue(IsAttacking(defender, attacker), "defender should fight back");
		}

		[UnityTest]
		public IEnumerator FriendlyAttack_DefenderStaysIdle()
		{
			Character defender = MakeCharacter("Defender", Vector3.zero, factions.Player);
			Character attacker = MakeCharacter("Friend", Vector3.right * Reach, factions.Player);
			int attacks = 0;
			defender.Stats.OnAttacked += (_, __) => attacks++;
			yield return null;

			attacker.AttackTarget(defender);
			yield return WaitFor(() => attacks >= 2, 5f);

			Assert.GreaterOrEqual(attacks, 2, "the friend should have swung");
			Assert.IsNull(defender.CurrentOrder, "friendly fire must not start a fight");
		}

		[UnityTest]
		public IEnumerator OrderThatDoesNotYield_IsKeptWhenAttacked()
		{
			Character defender = MakeCharacter("Defender", Vector3.zero, factions.Player);
			Character attacker = MakeCharacter("Attacker", Vector3.right * Reach, factions.Wildlife);
			int attacks = 0;
			defender.Stats.OnAttacked += (_, __) => attacks++;
			yield return null;

			var walking = new RecordingOrder { Yields = false };
			defender.GiveOrder(walking);
			attacker.AttackTarget(defender);
			yield return WaitFor(() => attacks >= 1, 5f);

			Assert.GreaterOrEqual(attacks, 1);
			Assert.AreSame(walking, defender.CurrentOrder, "walking away should not be interrupted");
		}

		[UnityTest]
		public IEnumerator Sleeper_WakesAndFightsBackWhenAttacked()
		{
			MakeTimeManager();
			Character sleeper = MakeCharacter("Sleeper", Vector3.zero, factions.Player);
			Character attacker = MakeCharacter("Attacker", Vector3.right * Reach, factions.Wildlife);
			yield return null;

			sleeper.Sleep();
			Assert.IsInstanceOf<SleepOrder>(sleeper.CurrentOrder);
			Assert.IsTrue(sleeper.Stats.IsSleeping);

			attacker.AttackTarget(sleeper);
			yield return WaitFor(() => IsAttacking(sleeper, attacker), 5f);

			Assert.IsFalse(sleeper.Stats.IsSleeping, "should wake up");
			Assert.IsTrue(IsAttacking(sleeper, attacker), "should fight back");
		}

		[UnityTest]
		public IEnumerator TargetDies_AttackOrderEnds()
		{
			Character attacker = MakeCharacter("Attacker", Vector3.zero, factions.Player);
			Character target = MakeCharacter("Target", Vector3.right * Reach, factions.Wildlife);
			yield return null;

			attacker.AttackTarget(target);
			Assert.IsInstanceOf<AttackOrder>(attacker.CurrentOrder);

			target.Stats.ReceiveDamage(1000f, null);
			yield return WaitFor(() => attacker.CurrentOrder == null, 3f);

			Assert.IsNull(attacker.CurrentOrder);
		}

		// ── Corpses ─────────────────────────────────────────

		[UnityTest]
		public IEnumerator Death_LeavesACorpseAndEndsTheOrder()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player);
			yield return null;

			var order = new RecordingOrder { Frames = 1000 };
			c.GiveOrder(order);
			c.Stats.ReceiveDamage(1000f, null);

			Assert.IsNotNull(c.GetComponent<Corpse>());
			Assert.IsNull(c.CurrentOrder);
			Assert.AreEqual(1, order.EndCount);
		}

		// ── Item use ────────────────────────────────────────

		[UnityTest]
		public IEnumerator UsingFood_EatsOneFromTheStack()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player, withInventory: true);
			yield return null;

			var food = MakeItem<FoodItem>("Berries", maxStack: 5);
			food.nutritionValue = 20f;
			c.GetComponent<Inventory>().Add(food, 2);
			c.Stats.hunger = 50f;

			Assert.IsTrue(c.TryUseItem(c.GetComponent<Inventory>().FindFirst<FoodItem>(), out _));
			Assert.AreEqual(70f, c.Stats.hunger, 0.01f);
			Assert.AreEqual(1, c.GetComponent<Inventory>().FindFirst<FoodItem>().count);
		}

		[UnityTest]
		public IEnumerator UsingADrink_KeepsTheEmptyContainer()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player, withInventory: true);
			yield return null;

			var empty = MakeItem<MiscItem>("Empty waterskin", maxStack: 1);
			var water = MakeItem<DrinkItem>("Waterskin", maxStack: 1);
			water.hydrationValue = 30f;
			water.emptyContainer = empty;
			c.GetComponent<Inventory>().Add(water, 1);
			c.Stats.thirst = 50f;

			Assert.IsTrue(c.TryUseItem(c.GetComponent<Inventory>().FindFirst<DrinkItem>(), out _));
			Assert.AreEqual(80f, c.Stats.thirst, 0.01f);
			Assert.IsNull(c.GetComponent<Inventory>().FindFirst<DrinkItem>());
			Assert.IsNotNull(c.GetComponent<Inventory>().FindFirst<MiscItem>(), "the empty waterskin should stay");
		}

		[UnityTest]
		public IEnumerator UsingAnItemWithNoUse_FailsWithAReason()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player, withInventory: true);
			yield return null;

			var stone = MakeItem<MiscItem>("Stone", maxStack: 1);
			c.GetComponent<Inventory>().Add(stone, 1);

			Assert.IsFalse(c.TryUseItem(c.GetComponent<Inventory>().FindFirst<MiscItem>(), out string reason));
			StringAssert.Contains("Stone", reason);
		}

		[UnityTest]
		public IEnumerator BindingWounds_IsInterruptedByAnyAttack()
		{
			Character patient = MakeCharacter("Patient", Vector3.zero, factions.Player, withInventory: true);
			Character friend = MakeCharacter("Friend", Vector3.right * Reach, factions.Player);
			SetField(patient.Stats, "healingConfig", Track(ScriptableObject.CreateInstance<HealingConfig>()));
			yield return null;

			var moss = MakeItem<HealingItem>("Moss dressing", maxStack: 5);
			moss.useSeconds = 30f;
			patient.GetComponent<Inventory>().Add(moss, 1);
			patient.Stats.currentHealth = 50f;

			Assert.IsTrue(patient.TryUseItem(patient.GetComponent<Inventory>().FindFirst<HealingItem>(), out string reason), reason);
			Assert.IsInstanceOf<TreatWoundsOrder>(patient.CurrentOrder);

			friend.AttackTarget(patient);
			yield return WaitFor(() => !(patient.CurrentOrder is TreatWoundsOrder), 5f);

			Assert.IsNotInstanceOf<TreatWoundsOrder>(patient.CurrentOrder, "any attack interrupts treatment");
			Assert.IsNotNull(patient.GetComponent<Inventory>().FindFirst<HealingItem>(), "an interrupted dressing is not used up");
		}

		[UnityTest]
		public IEnumerator TreatingASquadmate_HealsThemAndUsesTheHealersDressing()
		{
			Character healer = MakeCharacter("Healer", Vector3.zero, factions.Player, withInventory: true);
			Character patient = MakeCharacter("Patient", Vector3.right * Reach, factions.Player);
			var healing = Track(ScriptableObject.CreateInstance<HealingConfig>());
			healing.minTreatmentSeconds = 0f;
			SetField(healer.Stats, "healingConfig", healing);
			yield return null;

			var moss = MakeItem<HealingItem>("Moss dressing", maxStack: 5);
			moss.useSeconds = 0.1f;
			moss.healAmount = 25f;
			healer.GetComponent<Inventory>().Add(moss, 1);
			patient.Stats.currentHealth = 50f;

			Assert.IsTrue(healer.TryTreatWounds(patient, out string reason), reason);
			Assert.AreSame(patient, ((TreatWoundsOrder)healer.CurrentOrder).Patient);
			yield return WaitFor(() => healer.CurrentOrder == null, 3f);

			Assert.AreEqual(75f, patient.Stats.currentHealth, 0.01f, "the patient is healed");
			Assert.AreEqual(100f, healer.Stats.currentHealth, 0.01f);
			Assert.IsNull(healer.GetComponent<Inventory>().FindFirst<HealingItem>(), "the healer's dressing is used up");
		}

		[UnityTest]
		public IEnumerator Treating_BarelyWounded_IsRefusedAndKeepsTheDressing()
		{
			Character c = MakeCharacter("A", Vector3.zero, factions.Player, withInventory: true);
			SetField(c.Stats, "healingConfig", Track(ScriptableObject.CreateInstance<HealingConfig>()));
			yield return null;

			var moss = MakeItem<HealingItem>("Moss dressing", maxStack: 5);
			moss.healAmount = 25f;
			c.GetComponent<Inventory>().Add(moss, 1);
			c.Stats.currentHealth = 95f; // missing 5 of a 25 heal

			Assert.IsFalse(c.TryUseItem(c.GetComponent<Inventory>().FindFirst<HealingItem>(), out string reason));
			Assert.AreEqual("Not wounded enough", reason);
			Assert.IsNotNull(c.GetComponent<Inventory>().FindFirst<HealingItem>());
		}

		[UnityTest]
		public IEnumerator Treating_AHostile_IsRefused()
		{
			Character healer = MakeCharacter("Healer", Vector3.zero, factions.Player, withInventory: true);
			Character wolf = MakeCharacter("Wolf", Vector3.right * Reach, factions.Wildlife);
			SetField(healer.Stats, "healingConfig", Track(ScriptableObject.CreateInstance<HealingConfig>()));
			yield return null;

			healer.GetComponent<Inventory>().Add(MakeItem<HealingItem>("Moss dressing", maxStack: 5), 1);
			wolf.Stats.currentHealth = 10f;

			Assert.IsFalse(healer.TryTreatWounds(wolf, out _));
		}

		// ── Helpers ─────────────────────────────────────────

		Character MakeCharacter(string name, Vector3 position, FactionDefinition faction, bool withInventory = false)
		{
			var go = Track(new GameObject(name));
			go.SetActive(false);
			go.transform.position = position;
			go.AddComponent<CapsuleCollider>();
			go.AddComponent<CharacterStats>();
			if (withInventory)
				go.AddComponent<Inventory>();
			var character = go.AddComponent<Character>();
			character.characterName = name;
			SetField(character, "ownFaction", faction);
			SetField(character, "combatFormulaConfig", config);
			go.SetActive(true);
			return character;
		}

		void MakeTimeManager()
		{
			var go = Track(new GameObject("Time"));
			go.SetActive(false);
			var time = go.AddComponent<TimeManager>();
			time.sun = go.AddComponent<Light>();
			time.ambientColor = new Gradient();
			time.sunIntensity = AnimationCurve.Constant(0f, 1f, 1f);
			go.SetActive(true);
		}

		T MakeItem<T>(string name, int maxStack) where T : Item
		{
			var item = Track(ScriptableObject.CreateInstance<T>());
			item.name = name;
			item.itemName = name;
			item.maxStack = maxStack;
			return item;
		}

		T Track<T>(T obj) where T : Object
		{
			made.Add(obj);
			return obj;
		}

		static bool IsAttacking(Character who, CombatActor target)
		{
			return who.CurrentOrder is AttackOrder order && order.Target == target;
		}

		static IEnumerator WaitFor(Func<bool> condition, float seconds)
		{
			float until = Time.time + seconds;
			while (!condition() && Time.time < until)
				yield return null;
		}

		static void SetField(object target, string field, object value)
		{
			for (Type type = target.GetType(); type != null; type = type.BaseType)
			{
				FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (info != null)
				{
					info.SetValue(target, value);
					return;
				}
			}
			Assert.Fail($"No field {field} on {target.GetType().Name}");
		}

		/// <summary>A stand-in order that runs for a set number of frames and counts how often it was ended.</summary>
		class RecordingOrder : CharacterOrder
		{
			public int Frames = int.MaxValue;
			public bool Yields = true;
			public int EndCount;

			public override bool YieldsToSelfDefence => Yields;

			public override IEnumerator Run(Character character)
			{
				for (int i = 0; i < Frames; i++)
					yield return null;
			}

			public override void End(Character character) => EndCount++;
		}
	}
}
