using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>Checks the asset wiring of the healing feature without referencing game code.</summary>
	public class HealingWiringTests
	{
		const string FirstAidPath = "Assets/Skills/FirstAid.asset";
		const string ConfigPath = "Assets/Items/HealingConfig.asset";
		const string DressingPath = "Assets/Items/Medicine/MossDressing.asset";
		const string PatchPath = "Assets/Prefabs/Items/moss_patch_1.prefab";
		const string ScenePath = "Assets/Scenes/test1.unity";
		const string SkillTypePath = "Assets/Scripts/Skills/SkillType.cs";
		const string WolfPath = "Assets/Prefabs/Animals/Wolf.prefab";
		const string DeerPath = "Assets/Prefabs/Animals/Deer.prefab";

		static SerializedObject Load(string path)
		{
			var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
			Assert.IsNotNull(obj, "Missing asset " + path);
			return new SerializedObject(obj);
		}

		static SerializedProperty Prop(SerializedObject so, string name)
		{
			var p = so.FindProperty(name);
			Assert.IsNotNull(p, "Missing field " + name + " on " + so.targetObject.name);
			return p;
		}

		static string Guid(string path)
		{
			string g = AssetDatabase.AssetPathToGUID(path);
			Assert.IsNotEmpty(g, "No GUID for " + path);
			return g;
		}

		static int Count(string text, string needle)
		{
			return Regex.Matches(text, Regex.Escape(needle)).Count;
		}

		[Test]
		public void FirstAid_Asset_HasSkillTypeElevenAndBonusOneAtLevelZero()
		{
			var so = Load(FirstAidPath);
			Assert.AreEqual(11, Prop(so, "skillType").intValue);
			Assert.AreEqual(1f, Prop(so, "bonusCurve").animationCurveValue.Evaluate(0f), 1e-4f);
		}

		[Test]
		public void SkillType_Source_KeepsAllExistingNumbersAndAddsFirstAid()
		{
			string text = File.ReadAllText(SkillTypePath);
			string[] expected =
			{
				"Unarmed", "Axes", "Strength", "Athletics", "Harvesting", "Dexterity",
				"Block", "Spears", "Swords", "Blunt", "Bows", "FirstAid"
			};
			for (int i = 0; i < expected.Length; i++)
				Assert.IsTrue(Regex.IsMatch(text, @"\b" + expected[i] + @"\s*=\s*" + i + @"\b"),
					expected[i] + " should equal " + i);
		}

		[Test]
		public void HealingConfig_Asset_HasNoRegeneration()
		{
			var so = Load(ConfigPath);
			Assert.IsNull(so.FindProperty("naturalRegenPerHour"), "regeneration was removed: health only comes back through First Aid");
			Assert.IsNull(so.FindProperty("sleepRegenPerHour"));
		}

		[Test]
		public void MossDressing_Asset_HasIconPositiveValuesAndStackable()
		{
			var so = Load(DressingPath);
			Assert.IsNotNull(Prop(so, "icon").objectReferenceValue, "A null icon is invisible in the grid");
			Assert.Greater(Prop(so, "healAmount").floatValue, 0f);
			Assert.Greater(Prop(so, "useSeconds").floatValue, 0f);
			Assert.GreaterOrEqual(Prop(so, "maxStack").intValue, 2);
		}

		[Test]
		public void MossPatchPrefab_HasSolidRootColliderAndHarvestEntryForDressing()
		{
			var go = AssetDatabase.LoadAssetAtPath<GameObject>(PatchPath);
			Assert.IsNotNull(go);
			var col = go.GetComponent<Collider>();
			Assert.IsNotNull(col, "Root needs a collider");
			Assert.IsFalse(col.isTrigger);

			var gi = go.GetComponent("GenericInteractable");
			Assert.IsNotNull(gi, "Root has no GenericInteractable");
			var list = Prop(new SerializedObject(gi), "interactions");
			bool found = false;
			for (int i = 0; i < list.arraySize; i++)
			{
				var e = list.GetArrayElementAtIndex(i);
				var action = e.FindPropertyRelative("actionType");
				if (action.enumNames[action.enumValueIndex] == "Harvest"
					&& e.FindPropertyRelative("item").objectReferenceValue == AssetDatabase.LoadAssetAtPath<Object>(DressingPath))
					found = true;
			}
			Assert.IsTrue(found, "No Harvest entry giving MossDressing");
		}

		[Test]
		public void Scene_ReferencesFirstAidAndHealingConfigForTheSquad()
		{
			string text = File.ReadAllText(ScenePath);
			Assert.GreaterOrEqual(Count(text, Guid(FirstAidPath)), 2, "FirstAid.asset references");
			Assert.GreaterOrEqual(Count(text, Guid(ConfigPath)), 2, "HealingConfig.asset references");
		}

		[Test]
		public void Scene_PlacesMossPatchAtLeastOnce()
		{
			string text = File.ReadAllText(ScenePath);
			Assert.GreaterOrEqual(Count(text, Guid(PatchPath)), 1);
		}

		[TestCase(WolfPath)]
		[TestCase(DeerPath)]
		public void AnimalPrefab_DoesNotReferenceHealingConfig(string prefabPath)
		{
			Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), "Missing " + prefabPath);
			string text = File.ReadAllText(prefabPath);
			Assert.IsFalse(text.Contains(Guid(ConfigPath)));
		}
	}
}
