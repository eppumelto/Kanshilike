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
	/// <summary>How a Character works out its faction: own asset, legacy field, squad, community, attitude.</summary>
	public class FactionResolutionTests
	{
		readonly List<Object> made = new List<Object>();
		TestFactions factions;
		CombatFormulaConfig config;

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

		[Test]
		public void LegacyField_ResolvesThroughLegacyOrder()
		{
			InstallWithLegacyOrder();

			Assert.AreEqual(factions.Player.FactionId, MakeCharacter("A", null, legacy: 0).Faction);
			Assert.AreEqual(factions.Wildlife.FactionId, MakeCharacter("B", null, legacy: 1).Faction);

			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("legacy value 7"));
			Character unknown = MakeCharacter("C", null, legacy: 7);
			Assert.IsTrue(unknown.Faction.IsNone);
			Assert.IsTrue(MakeCharacter("D", null, legacy: 7).Faction.IsNone, "warned only once");
		}

		[Test]
		public void OwnFactionAsset_WinsOverLegacy()
		{
			InstallWithLegacyOrder();
			Character c = MakeCharacter("A", factions.Townsfolk, legacy: 1);
			Assert.AreEqual(factions.Townsfolk.FactionId, c.Faction);
			Assert.IsFalse(c.UsesLegacyFaction);
		}

		[UnityTest]
		public IEnumerator SquadMember_IsPlayer()
		{
			Character c = MakeCharacter("A", factions.Townsfolk);
			Assert.AreEqual(factions.Townsfolk.FactionId, c.Faction);

			var squad = c.gameObject.AddComponent<PlayerControllable>();
			Assert.AreEqual(factions.Player.FactionId, c.Faction);
			Assert.AreEqual(FactionSource.Squad, c.FactionResolution.Source);

			Object.Destroy(squad);
			yield return null;
			Assert.AreEqual(factions.Townsfolk.FactionId, c.Faction);
			Assert.AreEqual(FactionSource.Own, c.FactionResolution.Source);
		}

		[Test]
		public void SetOwnFaction_ChangesFactionAndRaisesEvent()
		{
			Character c = MakeCharacter("A", factions.Townsfolk);
			Assert.AreEqual(factions.Townsfolk.FactionId, c.Faction);

			Character raised = null;
			Action<Character> handler = x => raised = x;
			CharacterFaction.OwnFactionChanged += handler;
			try
			{
				c.SetOwnFaction(factions.Traders);
			}
			finally
			{
				CharacterFaction.OwnFactionChanged -= handler;
			}

			Assert.AreEqual(factions.Traders.FactionId, c.Faction);
			Assert.AreSame(c, raised);
		}

		[Test]
		public void CommunitySource_UsedAndInvalidated()
		{
			Character c = MakeCharacter("A", factions.Townsfolk);
			var source = new StubCommunity { Answer = factions.Traders.FactionId };

			CharacterFaction.CommunitySource = source;
			Assert.AreEqual(factions.Traders.FactionId, c.Faction);
			Assert.AreEqual(FactionSource.Community, c.FactionResolution.Source);

			source.Answer = factions.Bandits.FactionId;
			Assert.AreEqual(factions.Traders.FactionId, c.Faction, "cached until invalidated");
			CharacterFaction.InvalidateAll();
			Assert.AreEqual(factions.Bandits.FactionId, c.Faction);

			CharacterFaction.CommunitySource = null;
			Assert.AreEqual(factions.Townsfolk.FactionId, c.Faction);
		}

		[Test]
		public void NewFactionAsset_WorksWithoutCatalog()
		{
			var raiders = Track(FactionDefinition.Create("Raiders", FactionRelation.Hostile));
			Character raider = MakeCharacter("R", raiders);
			Character townsperson = MakeCharacter("T", factions.Townsfolk);

			// Reading the faction registers the new definition and its stance.
			Assert.IsTrue(FactionRelations.AreHostile(raider.Faction, townsperson.Faction));
			Assert.AreSame(raiders, FactionRelations.Find(raiders.FactionId));
		}

		[UnityTest]
		public IEnumerator AttitudeProvider_OverridesDefence()
		{
			Character defender = MakeCharacter("Defender", factions.Player, position: Vector3.zero);
			Character attacker = MakeCharacter("Attacker", factions.Wildlife, position: Vector3.right);
			yield return null;

			Attitude.Provider = new StubProvider { Self = defender, Other = attacker };
			defender.Stats.NotifyAttacked(attacker.gameObject, false);
			yield return null;
			Assert.IsNull(defender.CurrentOrder, "allied by the provider: no fight back");

			Attitude.Provider = null;
			defender.Stats.NotifyAttacked(attacker.gameObject, false);
			yield return null;
			Assert.IsInstanceOf<AttackOrder>(defender.CurrentOrder);
		}

		// ── Helpers ─────────────────────────────────────────

		void InstallWithLegacyOrder()
		{
			var set = Track(ScriptableObject.CreateInstance<FactionRelationSet>());
			set.player = factions.Player;
			set.legacyEnumOrder.Add(factions.Player);
			set.legacyEnumOrder.Add(factions.Wildlife);
			// FromLegacy reads the asset; put this one where Resources would.
			typeof(FactionRelations).GetField("asset", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, set);
		}

		Character MakeCharacter(string name, FactionDefinition own, int legacy = 0, Vector3 position = default)
		{
			var go = Track(new GameObject(name));
			go.SetActive(false);
			go.transform.position = position;
			go.AddComponent<CapsuleCollider>();
			go.AddComponent<CharacterStats>();
			var character = go.AddComponent<Character>();
			character.characterName = name;
			SetField(character, "ownFaction", own);
			SetField(character, "legacyFaction", legacy);
			SetField(character, "combatFormulaConfig", config);
			go.SetActive(true);
			return character;
		}

		T Track<T>(T obj) where T : Object
		{
			made.Add(obj);
			return obj;
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
			Assert.Fail("No field " + field + " on " + target.GetType().Name);
		}

		class StubCommunity : ICommunityFactionSource
		{
			public FactionId Answer;

			public bool TryGetCommunityFaction(Character character, out FactionId faction, out string communityName)
			{
				faction = Answer;
				communityName = "Stub";
				return !Answer.IsNone;
			}
		}

		class StubProvider : IAttitudeProvider
		{
			public CombatActor Self, Other;

			public bool TryExplain(CombatActor self, CombatActor other, FactionRelation factionRelation, out AttitudeResult result)
			{
				result = new AttitudeResult(FactionRelation.Allied, AttitudeReason.Faction);
				return self == Self && other == Other;
			}

			public bool RefusesSquad(CombatActor self) => false;
		}
	}
}
