using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kanshilike.Tests.PlayMode
{
	public class NpcDebugReportTests
	{
		TestWorld world;
		Action<Character, NpcDebugReport> extra;

		[SetUp]
		public void SetUp() => world = new TestWorld();

		[TearDown]
		public void TearDown()
		{
			if (extra != null)
				NpcDebugReport.Providers.Remove(extra);
			world.Dispose();
		}

		[UnityTest]
		public IEnumerator Npc_ReportShowsFactionAttitudeAndBrain()
		{
			world.MakeTimeManager(7f);
			var wander = world.MakeAsset<WanderActivity>("Wander");
			NpcBrain brain = world.MakeNpc("Aino", Vector3.zero, world.MakeArchetype(TestWorld.Entry(6f, wander)));
			Character jonttuli = world.MakeCharacter("Jonttuli", Vector3.right * 5f, world.Factions.Townsfolk, beforeEnable: go => go.AddComponent<PlayerControllable>());
			Character keppana = world.MakeCharacter("Keppana", Vector3.left * 5f, world.Factions.Townsfolk, beforeEnable: go => go.AddComponent<PlayerControllable>());
			yield return TestWorld.WaitFor(() => brain.Character.CurrentOrder != null, 1f);

			NpcDebugReport report = NpcDebugReport.Build(brain.Character);

			string faction = string.Join("\n", report.Find("Faction").Lines);
			StringAssert.Contains("Townsfolk", faction);
			StringAssert.Contains("own faction", faction);

			var attitude = report.Find("Attitude").Lines;
			Assert.IsTrue(attitude.Any(l => l.Contains("Jonttuli")), string.Join("\n", attitude));
			Assert.IsTrue(attitude.Any(l => l.Contains("Keppana")), string.Join("\n", attitude));

			string brainText = string.Join("\n", report.Find("Brain").Lines);
			StringAssert.Contains("WanderOrder", brainText);
			StringAssert.Contains("Schedule entry 0", brainText);
		}

		[UnityTest]
		public IEnumerator SquadMember_FactionSaysSquad()
		{
			Character jonttuli = world.MakeCharacter("Jonttuli", Vector3.zero, world.Factions.Townsfolk, beforeEnable: go => go.AddComponent<PlayerControllable>());
			yield return null;
			StringAssert.Contains("squad", string.Join("\n", NpcDebugReport.Build(jonttuli).Find("Faction").Lines));
		}

		[UnityTest]
		public IEnumerator Provider_AddsSection()
		{
			extra = (c, r) => r.Add("Extra").Add("hello " + c.characterName);
			NpcDebugReport.Providers.Add(extra);
			Character c = world.MakeCharacter("Aino", Vector3.zero, world.Factions.Townsfolk);
			yield return null;
			Assert.AreEqual("hello Aino", NpcDebugReport.Build(c).Find("Extra").Lines[0]);
		}
	}
}
