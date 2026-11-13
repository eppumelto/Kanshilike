using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>The shipped faction assets, and that scenes and prefabs point at them.</summary>
	public class FactionContentTests
	{
		const string RelationsPath = "Assets/Resources/Factions/FactionRelations.asset";
		const string PlayerPath = "Assets/Factions/Player.asset";
		const string WildlifePath = "Assets/Factions/Wildlife.asset";

		[TearDown]
		public void TearDown() => FactionRelations.ResetToDefault();

		[Test]
		public void RelationsAsset_LoadsFromResources()
		{
			var set = Resources.Load<FactionRelationSet>("Factions/FactionRelations");
			Assert.IsNotNull(set, "Missing " + RelationsPath);
			Assert.IsNotNull(set.player);
			Assert.AreEqual("player", set.player.Id);
			Assert.AreEqual(2, set.legacyEnumOrder.Count, "legacy order: Player, Wildlife");
			Assert.AreEqual("player", set.legacyEnumOrder[0].Id);
			Assert.AreEqual("wildlife", set.legacyEnumOrder[1].Id);
		}

		[Test]
		public void ShippedTable_GivesTheDefaults()
		{
			var set = Resources.Load<FactionRelationSet>("Factions/FactionRelations");
			Assert.IsNotNull(set);
			FactionRelationTable t = set.BuildTable();
			FactionId P = new FactionId("player"), W = new FactionId("wildlife"), T = new FactionId("townsfolk"),
				Tr = new FactionId("traders"), B = new FactionId("bandits");

			foreach (FactionId f in new[] { P, W, T, Tr, B })
			{
				Assert.IsTrue(t.IsDefined(f), f + " is defined");
				Assert.AreEqual(FactionRelation.Allied, t.Get(f, f));
			}
			foreach (FactionId f in new[] { P, T, Tr, B })
				Assert.AreEqual(FactionRelation.Hostile, t.Get(W, f), "wildlife vs " + f);
			foreach (FactionId f in new[] { P, T, Tr })
				Assert.AreEqual(FactionRelation.Hostile, t.Get(B, f), "bandits vs " + f);
			Assert.AreEqual(FactionRelation.Neutral, t.Get(P, T));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(P, Tr));
			Assert.AreEqual(FactionRelation.Neutral, t.Get(Tr, T));
		}

		[Test]
		public void EveryFactionDefinition_HasUniqueId()
		{
			var seen = new Dictionary<string, string>();
			foreach (string guid in AssetDatabase.FindAssets("t:FactionDefinition"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var f = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
				Assert.IsNotNull(f, path);
				Assert.IsNotEmpty(f.Id, path);
				Assert.IsFalse(seen.ContainsKey(f.Id), $"Duplicate faction id '{f.Id}': {path} and {(seen.TryGetValue(f.Id, out string other) ? other : "")}");
				seen[f.Id] = path;
			}
			Assert.GreaterOrEqual(seen.Count, 5);
		}

		// ── Wiring: scenes and prefabs reference the assets (after the migration) ──

		[Test]
		public void Test1_Characters_HaveOwnFaction()
		{
			string text = File.ReadAllText("Assets/Scenes/test1.unity");
			string player = Guid(PlayerPath);
			AssertField(text, "ownFaction", "Character", expectAtLeast: 2);
			StringAssert.Contains($"ownFaction: {{fileID: 11400000, guid: {player}", text, "Jonttuli and Keppana are Player");
		}

		[Test]
		public void IidesMap_Characters_HaveOwnFaction()
		{
			AssertField(File.ReadAllText("Assets/Scenes/Iides_map.unity"), "ownFaction", "Character", expectAtLeast: 1);
		}

		[Test]
		public void AnimalPrefabs_HaveWildlifeFaction()
		{
			string wildlife = Guid(WildlifePath);
			foreach (string path in new[] { "Assets/Prefabs/Animals/Wolf.prefab", "Assets/Prefabs/Animals/Deer.prefab" })
			{
				string text = File.ReadAllText(path);
				StringAssert.Contains($"factionDefinition: {{fileID: 11400000, guid: {wildlife}", text, path);
			}
		}

		static void AssertField(string text, string field, string what, int expectAtLeast)
		{
			MatchCollection set = Regex.Matches(text, $@"^\s+{field}: \{{fileID: 11400000, guid: [0-9a-f]{{32}}", RegexOptions.Multiline);
			MatchCollection unset = Regex.Matches(text, $@"^\s+{field}: \{{fileID: 0\}}", RegexOptions.Multiline);
			Assert.AreEqual(0, unset.Count, $"{what} with no {field}: run Kanshilike > Factions > Migrate Legacy Faction Fields");
			Assert.GreaterOrEqual(set.Count, expectAtLeast, $"{what} {field} references");
		}

		static string Guid(string path)
		{
			string g = AssetDatabase.AssetPathToGUID(path);
			Assert.IsNotEmpty(g, "No GUID for " + path);
			return g;
		}
	}
}
