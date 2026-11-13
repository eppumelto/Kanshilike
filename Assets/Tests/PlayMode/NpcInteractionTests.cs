using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>Right-click options on NPCs, talking, and recruiting into the squad.</summary>
	public class NpcInteractionTests
	{
		TestWorld world;
		NpcArchetype archetype;

		[SetUp]
		public void SetUp()
		{
			world = new TestWorld();
			world.MakeTimeManager(10f);
			archetype = world.MakeArchetype(TestWorld.Entry(6f, world.MakeAsset<WanderActivity>("Wander")));
			archetype.barkLines = new[] { "The pike are biting." };
			archetype.talkPauseSeconds = 4f;
		}

		[TearDown]
		public void TearDown() => world.Dispose();

		// ── Options ─────────────────────────────────────────

		[UnityTest]
		public IEnumerator Options_NeutralWithRecruitable_TalkRecruitAttackInOrder()
		{
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return null;

			List<InteractionOption> options = Interactable(npc).GetAvailableInteractions(jonttuli);
			CollectionAssert.AreEqual(new[] { "talk", "recruit", "attack" }, options.Select(o => o.id).ToArray());
			Assert.IsTrue(options[0].primaryOnly);
			Assert.IsTrue(options[1].primaryOnly);
			Assert.AreEqual(InteractionKind.Attack, options[2].kind);
			Assert.IsFalse(options[2].primaryOnly);
		}

		[UnityTest]
		public IEnumerator Options_HostileNpc_OnlyAttack()
		{
			NpcBrain bandit = MakeNpc("Bandit", recruitable: true, faction: world.Factions.Bandits);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right * 20f);
			yield return null;

			List<InteractionOption> options = Interactable(bandit).GetAvailableInteractions(jonttuli);
			CollectionAssert.AreEqual(new[] { "attack" }, options.Select(o => o.id).ToArray());
		}

		[UnityTest]
		public IEnumerator Options_DeadNpc_None()
		{
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return null;

			npc.Character.Stats.ReceiveDamage(1000f, null);
			Assert.IsEmpty(Interactable(npc).GetAvailableInteractions(jonttuli));
		}

		[UnityTest]
		public IEnumerator Options_SourceComponent_AddsOptionBeforeAttack()
		{
			TestSource source = null;
			NpcBrain npc = MakeNpc("Aino", recruitable: true, extra: go => source = go.AddComponent<TestSource>());
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return null;

			NpcInteractable interactable = Interactable(npc);
			CollectionAssert.AreEqual(new[] { "talk", "recruit", "test", "attack" },
				interactable.GetAvailableInteractions(jonttuli).Select(o => o.id).ToArray());

			interactable.ExecuteInteraction(jonttuli, "test");
			Assert.AreSame(jonttuli, source.ExecutedBy);
		}

		// ── Talk ────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Talk_ShowsBarkAndHoldsNpc()
		{
			NpcBrain npc = MakeNpc("Aino");
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return TestWorld.WaitFor(() => npc.Character.CurrentOrder is WanderOrder, 1f);

			Interactable(npc).ExecuteInteraction(jonttuli, "talk");
			var hold = npc.Character.CurrentOrder as IdleAtOrder;
			Assert.IsNotNull(hold, "the NPC stops to talk");
			Assert.AreEqual(jonttuli.transform, hold.LookAt);
			Assert.AreEqual(NpcPriority.Social, npc.Diagnostics.IssuedPriority);

			// The schedule doesn't take over while talking.
			yield return new WaitForSeconds(0.3f);
			Assert.AreSame(hold, npc.Character.CurrentOrder);
		}

		[UnityTest]
		public IEnumerator Talk_HigherPriorityTalkSourceWins_NoHoldWhenLineSaysSo()
		{
			TestTalk talk = null;
			NpcBrain npc = MakeNpc("Aino", extra: go => talk = go.AddComponent<TestTalk>());
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return TestWorld.WaitFor(() => npc.Character.CurrentOrder is WanderOrder, 1f);

			Interactable(npc).ExecuteInteraction(jonttuli, "talk");
			Assert.IsTrue(talk.Collected);
			Assert.IsNotInstanceOf<IdleAtOrder>(npc.Character.CurrentOrder, "the priority 10 line says don't hold");

			var lines = new List<NpcTalkLine> { new NpcTalkLine("low", 0, true), new NpcTalkLine("high", 10, false) };
			Assert.AreEqual("high", NpcInteractable.Pick(lines).Text);
			Assert.AreEqual("...", NpcInteractable.Pick(new List<NpcTalkLine>()).Text);
		}

		// ── Recruit ─────────────────────────────────────────

		[UnityTest]
		public IEnumerator Recruit_JoinsSquad()
		{
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			npc.Character.Stats.hasNeeds = false;
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return TestWorld.WaitFor(() => npc.Character.CurrentOrder is WanderOrder, 1f);
			CharacterOrder brainOrder = npc.Character.CurrentOrder;

			PlayerControllable joined = null;
			System.Action<PlayerControllable> handler = p => joined = p;
			PlayerControllable.Joined += handler;
			bool ok;
			try
			{
				ok = npc.GetComponent<Recruitable>().TryRecruit(jonttuli, out string reason);
				Assert.IsTrue(ok, reason);
			}
			finally
			{
				PlayerControllable.Joined -= handler;
			}

			Character aino = npc.Character;
			Assert.IsNotNull(joined);
			Assert.AreSame(aino, joined.Character);
			CollectionAssert.Contains(PlayerControllable.All.ToList(), joined);
			Assert.AreEqual(world.Factions.Player.FactionId, aino.Faction);
			Assert.AreEqual(FactionSource.Squad, aino.FactionResolution.Source);
			Assert.AreSame(world.Factions.Player, aino.OwnFaction);
			Assert.IsFalse(npc.enabled, "brain off");
			Assert.AreNotSame(brainOrder, aino.CurrentOrder, "the brain's order ended");
			Assert.IsTrue(aino.Stats.hasNeeds);

			yield return null;
			Assert.IsNull(aino.GetComponent<NpcInteractable>());
			Assert.IsNull(aino.GetComponent<Recruitable>());
		}

		[UnityTest]
		public IEnumerator Recruit_WhileInCombat_Refused()
		{
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			Character wolf = world.MakeCharacter("Wolf", Vector3.left, world.Factions.Wildlife);
			yield return null;

			wolf.AttackTarget(npc.Character);
			yield return TestWorld.WaitFor(() => npc.InCombat, 3f);
			Assert.IsTrue(npc.InCombat);

			Assert.IsFalse(npc.GetComponent<Recruitable>().TryRecruit(jonttuli, out string reason));
			Assert.AreEqual(RecruitRules.Reason(RecruitBlock.InCombat), reason);
			Assert.AreEqual(world.Factions.Townsfolk.FactionId, npc.Character.Faction);
		}

		[UnityTest]
		public IEnumerator Recruit_CostTakenOnlyOnSuccess()
		{
			var fish = world.MakeItem<MiscItem>("Dried pike", 5);
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			var recruitable = npc.GetComponent<Recruitable>();
			TestWorld.SetField(recruitable, "costItem", fish);
			TestWorld.SetField(recruitable, "costCount", 2);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right, withInventory: true);
			var bag = jonttuli.GetComponent<Inventory>();
			yield return null;

			bag.Add(fish, 1);
			Assert.IsFalse(recruitable.TryRecruit(jonttuli, out string reason));
			Assert.AreEqual(RecruitRules.Reason(RecruitBlock.CantAfford), reason);
			Assert.AreEqual(1, Count(bag, fish));

			bag.Add(fish, 1);
			Assert.IsTrue(recruitable.TryRecruit(jonttuli, out reason), reason);
			Assert.AreEqual(0, Count(bag, fish));
		}

		[UnityTest]
		public IEnumerator Recruit_TooFar_Refused()
		{
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right * 30f);
			yield return null;

			Assert.IsFalse(npc.GetComponent<Recruitable>().TryRecruit(jonttuli, out string reason));
			Assert.AreEqual(RecruitRules.Reason(RecruitBlock.TooFar), reason);
		}

		[UnityTest]
		public IEnumerator Recruit_ProviderRefusesSquad_Unwilling()
		{
			NpcBrain npc = MakeNpc("Aino", recruitable: true);
			Character jonttuli = MakeSquad("Jonttuli", Vector3.right);
			yield return null;

			Attitude.Provider = new RefusingProvider();
			Assert.IsFalse(npc.GetComponent<Recruitable>().TryRecruit(jonttuli, out string reason));
			Assert.AreEqual("Won't share a fire with your people", reason);
		}

		// ── Helpers ─────────────────────────────────────────

		NpcBrain MakeNpc(string name, bool recruitable = false, FactionDefinition faction = null, System.Action<GameObject> extra = null)
		{
			NpcBrain brain = null;
			world.MakeCharacter(name, Vector3.zero, faction ?? world.Factions.Townsfolk, beforeEnable: go =>
			{
				brain = go.AddComponent<NpcBrain>();
				TestWorld.SetField(brain, "archetype", archetype);
				go.AddComponent<NpcInteractable>();
				if (recruitable)
					go.AddComponent<Recruitable>();
				extra?.Invoke(go);
			});
			return brain;
		}

		Character MakeSquad(string name, Vector3 position, bool withInventory = false) =>
			world.MakeCharacter(name, position, world.Factions.Player, withInventory, go => go.AddComponent<PlayerControllable>());

		static NpcInteractable Interactable(NpcBrain npc) => npc.GetComponent<NpcInteractable>();

		static int Count(Inventory bag, Item item) => bag.Entries.Where(e => e.item == item).Sum(e => e.count);

		class TestSource : MonoBehaviour, INpcInteractionSource
		{
			public Character ExecutedBy;

			public void AddOptions(Character interactor, AttitudeResult attitude, List<InteractionOption> into) =>
				into.Add(new InteractionOption("test", "Test"));

			public bool TryExecute(Character interactor, string actionId)
			{
				if (actionId != "test")
					return false;
				ExecutedBy = interactor;
				return true;
			}
		}

		class TestTalk : MonoBehaviour, INpcTalkSource
		{
			public bool Collected;

			public void CollectLines(Character interactor, List<NpcTalkLine> into)
			{
				Collected = true;
				into.Add(new NpcTalkLine("Not now.", 10, false));
			}
		}

		class RefusingProvider : IAttitudeProvider
		{
			public bool TryExplain(CombatActor self, CombatActor other, FactionRelation factionRelation, out AttitudeResult result)
			{
				result = default;
				return false;
			}

			public bool RefusesSquad(CombatActor self) => true;
		}
	}
}
