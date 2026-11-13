using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>The social sections of the NPC Debugger report.</summary>
	public class SocialDebugReportTests
	{
		TestWorld world;

		[SetUp]
		public void SetUp()
		{
			Social.Reset(SocialTuning.Default);
			world = new TestWorld();
		}

		[TearDown]
		public void TearDown()
		{
			world.Dispose();
			Social.Reset(SocialTuning.Default);
		}

		[UnityTest]
		public IEnumerator Witness_ReportShowsMembershipsRelationshipsGrudgesAndDeaths()
		{
			Character aino = world.MakeCharacter("Aino", Vector3.zero, world.Factions.Townsfolk,
				beforeEnable: go => TestWorld.SetField(go.GetComponent<Character>(), "sex", Sex.Female));
			Character veikko = world.MakeCharacter("Veikko", Vector3.forward * 3f, world.Factions.Townsfolk);
			Character jonttuli = world.MakeCharacter("Jonttuli", Vector3.right, world.Factions.Player,
				beforeEnable: go => go.AddComponent<PlayerControllable>());
			world.MakeSocialWorld();
			yield return null;
			Social.Sim.Relations.SetKin(veikko.PersonId, aino.PersonId, BondFlags.Parent);

			aino.Stats.ReceiveDamage(1000f, jonttuli.gameObject);
			NpcDebugReport report = NpcDebugReport.Build(veikko);

			StringAssert.Contains("Veikko", Text(report, "Memberships"));
			string relationships = Text(report, "Relationships");
			StringAssert.Contains("Aino (Dead)", relationships);
			StringAssert.Contains("closeness", relationships);
			StringAssert.Contains("mother", relationships);
			string grudges = Text(report, "Grudges");
			StringAssert.Contains("Against Jonttuli", grudges);
			StringAssert.Contains("Rally", grudges); // a parent outside the household: 0.6 × 0.5 × 100 + 20 = 50
			StringAssert.Contains("day(s) left", grudges);
			StringAssert.Contains("Aino, killed by Jonttuli", Text(report, "Deaths known"));
			StringAssert.Contains("Grudge", Text(report, "Attitude"));
			StringAssert.Contains("Rally grudge", Text(report, "Feelings toward the squad"));
		}

		[UnityTest]
		public IEnumerator NoSocialWorld_SaysInactive()
		{
			Character aino = world.MakeCharacter("Aino", Vector3.zero, world.Factions.Townsfolk);
			yield return null;
			StringAssert.Contains("Social system inactive", Text(NpcDebugReport.Build(aino), "Social"));
		}

		static string Text(NpcDebugReport report, string section)
		{
			NpcDebugReport.Section s = report.Find(section);
			Assert.IsNotNull(s, "no section " + section);
			return string.Join("\n", s.Lines);
		}
	}
}
