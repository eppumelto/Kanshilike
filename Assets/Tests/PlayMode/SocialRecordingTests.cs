using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>Characters report attacks, damage and deaths to the social simulation while a SocialWorld is on.</summary>
	public class SocialRecordingTests
	{
		const float Reach = 1.2f;
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

		Character Townsperson(string name, Vector3 at) => world.MakeCharacter(name, at, world.Factions.Townsfolk);

		Character Squad(string name, Vector3 at) =>
			world.MakeCharacter(name, at, world.Factions.Player, beforeEnable: go => go.AddComponent<PlayerControllable>());

		[UnityTest]
		public IEnumerator NoSocialWorld_NothingRecorded()
		{
			Character aino = Townsperson("Aino", Vector3.zero);
			Character jonttuli = Squad("Jonttuli", Vector3.right);
			yield return null;

			aino.Stats.ReceiveDamage(1000f, jonttuli.gameObject);
			Assert.IsEmpty(Social.Sim.Deaths.All);
			Assert.IsNull(Attitude.Provider);
			Assert.IsNull(CharacterFaction.CommunitySource);
			Assert.IsFalse(Social.Active);
		}

		[UnityTest]
		public IEnumerator Kill_RecordsVictimKillerFactionAndPosition()
		{
			Character aino = Townsperson("Aino", new Vector3(3f, 0f, 4f));
			Character jonttuli = Squad("Jonttuli", Vector3.zero);
			world.MakeSocialWorld();
			yield return null;

			aino.Stats.ReceiveDamage(1000f, jonttuli.gameObject);
			DeathRecord d = Social.Sim.Deaths.Get(0);
			Assert.IsNotNull(d);
			Assert.AreEqual(aino.PersonId, d.VictimId);
			Assert.AreEqual(jonttuli.PersonId, d.KillerId);
			Assert.AreEqual(world.Factions.Player.FactionId, d.KillerFaction);
			Assert.AreEqual(3f, d.Position.X, 0.01f);
			Assert.AreEqual(4f, d.Position.Z, 0.01f);
			Assert.AreEqual(PersonState.Dead, Social.Sim.Registry.Get(aino.PersonId).State);
		}

		[UnityTest]
		public IEnumerator NullSourceDeathSoonAfterAttack_CreditsAttacker()
		{
			world.MakeTimeManager(8f);
			Character aino = Townsperson("Aino", Vector3.zero);
			Character jonttuli = Squad("Jonttuli", Vector3.right);
			world.MakeSocialWorld();
			yield return null;
			yield return null;

			aino.Stats.ReceiveDamage(5f, jonttuli.gameObject);
			aino.Stats.ReceiveDamage(1000f, null);
			Assert.AreEqual(jonttuli.PersonId, Social.Sim.Deaths.Get(0).KillerId);
		}

		[UnityTest]
		public IEnumerator NullSourceDeathAfterWindow_KillerUnknown()
		{
			TimeManager time = world.MakeTimeManager(8f);
			Character aino = Townsperson("Aino", Vector3.zero);
			Character jonttuli = Squad("Jonttuli", Vector3.right);
			world.MakeSocialWorld();
			yield return null;
			yield return null;

			aino.Stats.ReceiveDamage(5f, jonttuli.gameObject);
			time.currentTime = 8.5f;
			yield return null;
			Assert.AreEqual(8.5, Social.Now, 0.01);
			aino.Stats.ReceiveDamage(1000f, null);
			Assert.IsNull(Social.Sim.Deaths.Get(0).KillerId);
		}

		[UnityTest]
		public IEnumerator WolfKill_NoKillerId_WildlifeFaction()
		{
			Character aino = Townsperson("Aino", Vector3.zero);
			var wolfGo = world.Track(new GameObject("Wolf"));
			var wolf = wolfGo.AddComponent<StubAnimal>();
			wolf.FactionValue = world.Factions.Wildlife.FactionId;
			world.MakeSocialWorld();
			yield return null;

			aino.Stats.ReceiveDamage(1000f, wolfGo);
			DeathRecord d = Social.Sim.Deaths.Get(0);
			Assert.IsNull(d.KillerId);
			Assert.IsTrue(d.KillerFactionKnown);
			Assert.AreEqual(world.Factions.Wildlife.FactionId, d.KillerFaction);
			Assert.AreEqual(0, Social.Sim.Grudges.Count);
		}

		[UnityTest]
		public IEnumerator PersonId_AssignedAtRuntime_AndDuplicateFixed()
		{
			Character fresh = Townsperson("Fresh", Vector3.zero);
			Assert.IsFalse(string.IsNullOrEmpty(fresh.PersonId));

			Character a = world.MakeCharacter("A", Vector3.zero, world.Factions.Townsfolk, beforeEnable: go =>
				TestWorld.SetField(go.GetComponent<Character>(), "personId", "copied"));
			LogAssert.Expect(LogType.Warning, new Regex("same person id"));
			Character b = world.MakeCharacter("B", Vector3.zero, world.Factions.Townsfolk, beforeEnable: go =>
				TestWorld.SetField(go.GetComponent<Character>(), "personId", "copied"));
			yield return null;

			Assert.AreEqual("copied", a.PersonId);
			Assert.AreNotEqual("copied", b.PersonId);
			Assert.AreSame(b, Social.Find(b.PersonId));
			Assert.AreSame(a, Social.Find("copied"));
		}

		[UnityTest]
		public IEnumerator AttackLowersOpinion_BeforeDefending()
		{
			Character aino = Townsperson("Aino", Vector3.zero);
			Character jonttuli = Squad("Jonttuli", Vector3.right * Reach);
			world.MakeSocialWorld();
			yield return null;
			Social.Sim.SetAuthoredBond(aino.PersonId, jonttuli.PersonId, BondFlags.None, 70);

			int firstOpinion = int.MinValue;
			bool foughtBackOnFirst = false;
			aino.Stats.OnAttacked += (_, __) =>
			{
				if (firstOpinion != int.MinValue)
					return;
				firstOpinion = Social.Sim.Relationship(aino.PersonId, jonttuli.PersonId).Opinion;
				foughtBackOnFirst = aino.CurrentOrder is AttackOrder;
			};

			jonttuli.AttackTarget(aino);
			yield return TestWorld.WaitFor(() => firstOpinion != int.MinValue, 5f);
			Assert.AreEqual(45, firstOpinion);
			Assert.IsTrue(foughtBackOnFirst, "+45 is Neutral, so the first blow is answered");
		}

		[UnityTest]
		public IEnumerator FriendAtPlus100_FirstBlowNotAnswered_SecondIs()
		{
			Character aino = Townsperson("Aino", Vector3.zero);
			Character jonttuli = Squad("Jonttuli", Vector3.right * Reach);
			world.MakeSocialWorld();
			yield return null;
			Social.Sim.SetAuthoredBond(aino.PersonId, jonttuli.PersonId, BondFlags.Friend, 100);

			int attacks = 0;
			bool answeredFirst = false, answeredSecond = false;
			aino.Stats.OnAttacked += (_, __) =>
			{
				attacks++;
				if (attacks == 1) answeredFirst = aino.CurrentOrder is AttackOrder;
				if (attacks == 2) answeredSecond = aino.CurrentOrder is AttackOrder;
			};

			jonttuli.AttackTarget(aino);
			yield return TestWorld.WaitFor(() => attacks >= 2, 8f);
			Assert.GreaterOrEqual(attacks, 2);
			Assert.IsFalse(answeredFirst, "75 is still Allied");
			Assert.IsTrue(answeredSecond, "50 is Neutral");
		}

		[UnityTest]
		public IEnumerator SocialWorldDisabled_ProviderAndSourceRemoved()
		{
			SocialWorld social = world.MakeSocialWorld();
			yield return null;
			Assert.IsNotNull(Attitude.Provider);
			Assert.IsNotNull(CharacterFaction.CommunitySource);
			Assert.IsTrue(Social.Active);

			social.enabled = false;
			Assert.IsNull(Attitude.Provider);
			Assert.IsNull(CharacterFaction.CommunitySource);
			Assert.IsFalse(Social.Active);
		}

		[UnityTest]
		public IEnumerator Witness_HoldsGrudge_SquadWitnessSwearsVengeance()
		{
			Character aino = Townsperson("Aino", Vector3.zero);
			Character veikko = Townsperson("Veikko", Vector3.forward * 3f);
			Character keppana = Squad("Keppana", Vector3.back * 3f);
			Character stranger = Townsperson("Stranger", Vector3.left * 3f);
			Character jonttuli = Squad("Jonttuli", Vector3.right);
			world.MakeSocialWorld();
			yield return null;
			Social.Sim.Relations.SetKin(veikko.PersonId, aino.PersonId, BondFlags.Parent);
			Social.Sim.SetAuthoredBond(keppana.PersonId, aino.PersonId, BondFlags.Friend, 60);
			Social.Sim.SetAuthoredBond(veikko.PersonId, aino.PersonId, BondFlags.None, 100);

			aino.Stats.ReceiveDamage(1000f, jonttuli.gameObject);

			Grudge g = Social.Sim.Grudges.Get(veikko.PersonId, jonttuli.PersonId);
			Assert.IsNotNull(g, "her son saw it");
			Assert.AreEqual(GrudgeTier.Hunt, GrudgeRules.Tier(g.Intensity, Social.Tuning));
			Assert.IsNotNull(Social.Sim.Grudges.Get(keppana.PersonId, jonttuli.PersonId), "squad members hold grudges too");
			Assert.IsNull(Social.Sim.Grudges.Get(stranger.PersonId, jonttuli.PersonId));

			// Veikko is now Hostile to the killer; Keppana, a squad member, is not.
			Assert.AreEqual(FactionRelation.Hostile, Attitude.Of(veikko, jonttuli));
			Assert.AreEqual(AttitudeReason.Grudge, Attitude.Explain(veikko, jonttuli).Reason);
			Assert.AreNotEqual(FactionRelation.Hostile, Attitude.Of(keppana, jonttuli));
		}

		[UnityTest]
		public IEnumerator SleepingWitness_DoesNotLearn()
		{
			world.MakeTimeManager(22f);
			Character aino = Townsperson("Aino", Vector3.zero);
			Character veikko = Townsperson("Veikko", Vector3.forward * 3f);
			Character jonttuli = Squad("Jonttuli", Vector3.right);
			world.MakeSocialWorld();
			yield return null;
			Social.Sim.Relations.SetKin(veikko.PersonId, aino.PersonId, BondFlags.Parent);
			veikko.Stats.Sleep(8f);
			Assume.That(veikko.Stats.IsSleeping);

			aino.Stats.ReceiveDamage(1000f, jonttuli.gameObject);
			Assert.IsFalse(Social.Sim.Knowledge.Knows(veikko.PersonId, 0));
			Assert.AreEqual(0, Social.Sim.Grudges.Count);
		}

		/// <summary>A minimal non-person combat actor, like an animal.</summary>
		class StubAnimal : CombatActor
		{
			public FactionId FactionValue;
			public override FactionId Faction => FactionValue;
			public override bool CanUseEquipment => false;
			public override bool CanBlock => false;
			public override bool CanDodge => false;
			public override bool CanCounter => false;
		}
	}
}
