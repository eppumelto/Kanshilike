using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>The NPC brain on code-built NPCs with no NavMesh: orders arrive instantly.</summary>
	public class NpcBrainTests
	{
		TestWorld world;
		NpcAnchorTag stall;
		NpcAnchorTag market;
		WanderActivity wander;
		IdleAtActivity idleAt;
		SleepActivity sleep;

		[SetUp]
		public void SetUp()
		{
			world = new TestWorld();
			stall = world.MakeAsset<NpcAnchorTag>("Stall");
			market = world.MakeAsset<NpcAnchorTag>("Market");
			wander = world.MakeAsset<WanderActivity>("Wander");
			idleAt = world.MakeAsset<IdleAtActivity>("IdleAt");
			sleep = world.MakeAsset<SleepActivity>("Sleep");
		}

		[TearDown]
		public void TearDown() => world.Dispose();

		NpcArchetype DayArchetype() =>
			world.MakeArchetype(TestWorld.Entry(6f, wander), TestWorld.Entry(9f, idleAt, stall));

		[UnityTest]
		public IEnumerator Brain_DoesNotChangeFaction()
		{
			world.MakeTimeManager(10f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			yield return null;
			Assert.AreEqual(world.Factions.Townsfolk.FactionId, brain.Character.Faction);
		}

		[UnityTest]
		public IEnumerator Brain_IdleNpc_GetsScheduleOrderForCurrentHour()
		{
			world.MakeTimeManager(10f);
			NpcAnchor anchor = world.MakeAnchor("Stall", new Vector3(3f, 0f, 0f), stall);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != null, 1f);

			var order = brain.Character.CurrentOrder as IdleAtOrder;
			Assert.IsNotNull(order, Describe(brain));
			Assert.AreEqual(anchor.Position, order.Point);
		}

		[UnityTest]
		public IEnumerator Brain_HourChange_SwitchesScheduleOrder()
		{
			TimeManager time = world.MakeTimeManager(7f);
			world.MakeAnchor("Stall", Vector3.right, stall);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is WanderOrder, 1f);
			Assert.IsInstanceOf<WanderOrder>(brain.Character.CurrentOrder);

			time.currentTime = 9.5f;
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is IdleAtOrder, 1f);
			Assert.IsInstanceOf<IdleAtOrder>(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator Brain_TimeJumpOverSeveralEntries_UsesCurrentEntryOnly()
		{
			var counting = world.MakeAsset<CountingActivity>("Counting");
			NpcArchetype archetype = world.MakeArchetype(
				TestWorld.Entry(6f, wander), TestWorld.Entry(12f, idleAt), TestWorld.Entry(17f, wander), TestWorld.Entry(21f, counting));
			archetype.schedule.entries[1].anchorTag = null;
			TimeManager time = world.MakeTimeManager(8f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, archetype);
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is WanderOrder, 1f);

			time.currentTime = 22f;
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is MarkerOrder, 1f);
			yield return new WaitForSeconds(0.3f);
			Assert.IsInstanceOf<MarkerOrder>(brain.Character.CurrentOrder);
			Assert.AreEqual(1, counting.Created, "the 22:00 entry's order, issued once");
		}

		[UnityTest]
		public IEnumerator Brain_MissingAnchor_FallsBackAndKeepsRunning()
		{
			world.MakeTimeManager(10f);
			LogAssert.Expect(LogType.Warning, new Regex("no anchor tagged 'Market'"));
			NpcBrain brain = world.MakeNpc("Aino", new Vector3(5f, 0f, 5f), world.MakeArchetype(TestWorld.Entry(9f, idleAt, market)));
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != null, 1f);

			var order = brain.Character.CurrentOrder as IdleAtOrder;
			Assert.IsNotNull(order, Describe(brain));
			Assert.AreEqual(brain.HomePosition, order.Point);

			// Still running at home a few ticks later, with no second warning.
			yield return new WaitForSeconds(0.3f);
			Assert.IsInstanceOf<IdleAtOrder>(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator Brain_NullActivity_WandersAtHome_WarnsOnce()
		{
			world.MakeTimeManager(10f);
			LogAssert.Expect(LogType.Warning, new Regex("has no activity"));
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, world.MakeArchetype(TestWorld.Entry(9f, null)));
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != null, 1f);
			yield return new WaitForSeconds(0.3f);

			var order = brain.Character.CurrentOrder as WanderOrder;
			Assert.IsNotNull(order, Describe(brain));
			Assert.AreEqual(brain.HomePosition, order.Center);
		}

		[UnityTest]
		public IEnumerator Brain_CustomActivityClass_IsUsed()
		{
			world.MakeTimeManager(10f);
			var counting = world.MakeAsset<CountingActivity>("Counting");
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, world.MakeArchetype(TestWorld.Entry(9f, counting)));
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != null, 1f);
			Assert.IsInstanceOf<MarkerOrder>(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator Brain_DoesNotOverrideSelfDefence()
		{
			world.MakeTimeManager(10f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			Character wolf = world.MakeCharacter("Wolf", Vector3.right, world.Factions.Wildlife);
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != null, 1f);

			wolf.AttackTarget(brain.Character);
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is AttackOrder, 3f);
			Assert.IsInstanceOf<AttackOrder>(brain.Character.CurrentOrder);

			for (int i = 0; i < 6; i++)
			{
				yield return new WaitForSeconds(0.05f);
				if (brain.Character.Stats.currentHealth / brain.Character.Stats.maxHealth < brain.Archetype.fleeHealthFraction)
					break;
				Assert.IsInstanceOf<AttackOrder>(brain.Character.CurrentOrder, "tick " + i);
			}
		}

		[UnityTest]
		public IEnumerator Brain_FleesBelowThreshold()
		{
			world.MakeTimeManager(10f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			Character wolf = world.MakeCharacter("Wolf", Vector3.right, world.Factions.Wildlife);
			yield return null;

			brain.Character.Stats.currentHealth = 30f;
			brain.Character.Stats.ReceiveDamage(1f, wolf.gameObject);
			Assert.IsInstanceOf<FleeOrder>(brain.Character.CurrentOrder);
			Assert.AreEqual(wolf.transform, ((FleeOrder)brain.Character.CurrentOrder).Threat);
		}

		[UnityTest]
		public IEnumerator Brain_ResumesScheduleAfterFightEnds()
		{
			world.MakeTimeManager(7f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			Character wolf = world.MakeCharacter("Wolf", Vector3.right, world.Factions.Wildlife);
			wolf.Stats.maxHealth = 1f;
			wolf.Stats.currentHealth = 1f;
			yield return null;

			wolf.AttackTarget(brain.Character);
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is AttackOrder, 3f);
			yield return TestWorld.WaitFor(() => !wolf.IsAlive, 5f);
			Assert.IsFalse(wolf.IsAlive, "the NPC should have killed the weak wolf");

			// AttackOrder notices the death after its attack interval.
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is WanderOrder, 4f);
			Assert.IsInstanceOf<WanderOrder>(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator SleepingNpc_WakesAndDefends()
		{
			world.MakeTimeManager(22f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, world.MakeArchetype(TestWorld.Entry(6f, wander), TestWorld.Entry(21f, sleep)));
			Character wolf = world.MakeCharacter("Wolf", Vector3.right, world.Factions.Wildlife);
			yield return TestWorld.WaitFor(() => brain.Character.Stats.IsSleeping, 1f);
			Assert.IsInstanceOf<SleepOrder>(brain.Character.CurrentOrder, Describe(brain));
			Assert.IsTrue(brain.Character.Stats.IsSleeping);

			wolf.AttackTarget(brain.Character);
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is AttackOrder, 3f);
			Assert.IsFalse(brain.Character.Stats.IsSleeping);
			Assert.IsInstanceOf<AttackOrder>(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator NeutralAttacked_FightsBack()
		{
			world.MakeTimeManager(10f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			Character player = world.MakeCharacter("Jonttuli", Vector3.right, world.Factions.Player);
			yield return null;

			player.AttackTarget(brain.Character);
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is AttackOrder a && a.Target == player, 3f);
			Assert.IsTrue(brain.Character.CurrentOrder is AttackOrder order && order.Target == player, Describe(brain));
		}

		[UnityTest]
		public IEnumerator NpcDies_MidOrder_BrainStops()
		{
			world.MakeTimeManager(7f);
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, DayArchetype());
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is WanderOrder, 1f);

			brain.Character.Stats.ReceiveDamage(1000f, null);
			yield return null;
			Assert.IsNull(brain.Character.CurrentOrder);
			Assert.IsFalse(brain.enabled);
			Assert.IsNotNull(brain.GetComponent<Corpse>());

			yield return new WaitForSeconds(0.5f);
			Assert.IsNull(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator Role_StillWantsFalse_ReturnsToSchedule()
		{
			world.MakeTimeManager(7f);
			NpcBrain brain = MakeNpcWithRole(out TestRole role);
			role.React = true;
			yield return TestWorld.WaitFor(() => brain.IsRoleOrder(brain.Character.CurrentOrder), 1f);
			Assert.IsTrue(brain.IsRoleOrder(brain.Character.CurrentOrder));

			role.React = false;
			role.Wants = false;
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder is WanderOrder, 0.3f);
			Assert.IsInstanceOf<WanderOrder>(brain.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator Role_CanReplaceItsOwnRoleOrder()
		{
			world.MakeTimeManager(7f);
			NpcBrain brain = MakeNpcWithRole(out TestRole role);
			role.React = true;
			yield return TestWorld.WaitFor(() => brain.IsRoleOrder(brain.Character.CurrentOrder), 1f);
			CharacterOrder first = brain.Character.CurrentOrder;

			role.React = true; // reacts again with a new order
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != first, 0.5f);
			Assert.AreNotSame(first, brain.Character.CurrentOrder);
			Assert.IsTrue(brain.IsRoleOrder(brain.Character.CurrentOrder));
		}

		// ── Helpers ─────────────────────────────────────────

		NpcBrain MakeNpcWithRole(out TestRole role)
		{
			TestRole made = null;
			NpcArchetype archetype = DayArchetype();
			world.MakeCharacter("Aino", Vector3.zero, world.Factions.Townsfolk, beforeEnable: go =>
			{
				made = go.AddComponent<TestRole>();
				var brain = go.AddComponent<NpcBrain>();
				TestWorld.SetField(brain, "archetype", archetype);
			});
			role = made;
			return made.GetComponent<NpcBrain>();
		}

		static string Describe(NpcBrain brain) =>
			string.Join(" | ", NpcDebugReport.Build(brain.Character).Find("Brain")?.Lines ?? new System.Collections.Generic.List<string>());
	}

	/// <summary>An order that does nothing until replaced.</summary>
	public class MarkerOrder : CharacterOrder
	{
		public override IEnumerator Run(Character character)
		{
			while (true)
				yield return null;
		}
	}

	/// <summary>A test-only activity: proves new activity classes need no brain change.</summary>
	public class CountingActivity : NpcActivity
	{
		public int Created;

		public CountingActivity()
		{
			usesAnchor = false;
		}

		public override CharacterOrder CreateOrder(in NpcActivityContext ctx)
		{
			Created++;
			return new MarkerOrder();
		}
	}

	/// <summary>A role the test drives by hand: React makes one reaction, Wants answers StillWants.</summary>
	public class TestRole : MonoBehaviour, INpcRole
	{
		public bool React;
		public bool Wants = true;

		public bool TryGetReaction(NpcBrain brain, out CharacterOrder order)
		{
			order = null;
			if (!React)
				return false;
			React = false;
			order = new MarkerOrder();
			return true;
		}

		public bool StillWants(NpcBrain brain, CharacterOrder order) => Wants;
	}
}
