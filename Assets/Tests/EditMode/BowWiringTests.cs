using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>Checks the asset wiring of the bow feature without referencing game code.</summary>
	public class BowWiringTests
	{
		const string BowPath = "Assets/Items/Weapons/Bow.asset";
		const string ArrowPath = "Assets/Items/Weapons/Arrow.asset";
		const string BowPrefabPath = "Assets/Prefabs/Items/bow_1.prefab";
		const string ArrowsPrefabPath = "Assets/Prefabs/Items/arrows_1.prefab";
		const string ScenePath = "Assets/Scenes/test1.unity";
		const string PersonalityDir = "Assets/Scripts/NPC/Animals/Personalities/";

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

		static SerializedProperty FirstEntry(string prefabPath, out SerializedObject so)
		{
			var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			Assert.IsNotNull(go, "Missing prefab " + prefabPath);
			var gi = go.GetComponent("GenericInteractable");
			Assert.IsNotNull(gi, "Root has no GenericInteractable");
			so = new SerializedObject(gi);
			var list = Prop(so, "interactions");
			Assert.GreaterOrEqual(list.arraySize, 1);
			return list.GetArrayElementAtIndex(0);
		}

		[Test]
		public void Bow_Asset_HasExpectedWeaponSetup()
		{
			var so = Load(BowPath);
			Assert.AreEqual(10, Prop(so, "weaponSkill").intValue);
			Assert.IsTrue(Prop(so, "twoHanded").boolValue);
			Assert.IsNotNull(Prop(so, "icon").objectReferenceValue);
			Assert.IsNotNull(Prop(so, "weaponPrefab").objectReferenceValue);
		}

		[Test]
		public void Bow_Asset_HasTuningValuesSerialized()
		{
			var so = Load(BowPath);
			Assert.AreEqual(0.15f, Prop(so, "minHitChance").floatValue, 1e-4f);
			Assert.AreEqual(45f, Prop(so, "maxPursuitDistance").floatValue, 1e-4f);
		}

		[TestCase("Brave.asset")]
		[TestCase("Deer.asset")]
		[TestCase("Shy.asset")]
		public void Bow_AttackRange_IsBelowChaseGiveUpDistance(string personality)
		{
			float range = Prop(Load(BowPath), "attackRange").floatValue;
			float giveUp = Prop(Load(PersonalityDir + personality), "chaseGiveUpDistance").floatValue;
			Assert.Less(range, giveUp);
		}

		[Test]
		public void Arrow_Asset_HasProjectileAndStackOfTwentyOrMore()
		{
			var so = Load(ArrowPath);
			Assert.IsNotNull(Prop(so, "projectilePrefab").objectReferenceValue);
			Assert.GreaterOrEqual(Prop(so, "maxStack").intValue, 20);
		}

		[Test]
		public void BowPrefab_HasSolidColliderAndCollectEntryForBow()
		{
			var go = AssetDatabase.LoadAssetAtPath<GameObject>(BowPrefabPath);
			Assert.IsNotNull(go);
			var col = go.GetComponent<Collider>();
			Assert.IsNotNull(col, "Root needs a collider");
			Assert.IsFalse(col.isTrigger);

			var entry = FirstEntry(BowPrefabPath, out _);
			Assert.AreEqual("Collect", entry.FindPropertyRelative("actionType").enumNames[entry.FindPropertyRelative("actionType").enumValueIndex]);
			Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Object>(BowPath), entry.FindPropertyRelative("item").objectReferenceValue);
			Assert.AreEqual(1, entry.FindPropertyRelative("collectCount").intValue);
		}

		[Test]
		public void ArrowsPrefab_HasSolidColliderAndCollectEntryForTwentyArrows()
		{
			var go = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowsPrefabPath);
			Assert.IsNotNull(go);
			var col = go.GetComponent<Collider>();
			Assert.IsNotNull(col, "Root needs a collider");
			Assert.IsFalse(col.isTrigger);

			var entry = FirstEntry(ArrowsPrefabPath, out _);
			var action = entry.FindPropertyRelative("actionType");
			Assert.AreEqual("Collect", action.enumNames[action.enumValueIndex]);
			Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Object>(ArrowPath), entry.FindPropertyRelative("item").objectReferenceValue);
			Assert.AreEqual(20, entry.FindPropertyRelative("collectCount").intValue);
		}

		[Test]
		public void ArrowsPrefab_ContainsNoArrowProjectile()
		{
			var root = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowsPrefabPath);
			Assert.IsNotNull(root);
			foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				Assert.IsNotNull(mb, "Missing script on arrows prefab");
				Assert.AreNotEqual("ArrowProjectile", mb.GetType().Name);
			}
		}

		[Test]
		public void Scene_ReferencesBothPickupPrefabs()
		{
			string text = File.ReadAllText(ScenePath);
			Assert.IsTrue(text.Contains(AssetDatabase.AssetPathToGUID(BowPrefabPath)), "bow_1 not placed");
			Assert.IsTrue(text.Contains(AssetDatabase.AssetPathToGUID(ArrowsPrefabPath)), "arrows_1 not placed");
		}

		[Test]
		public void Scene_NoLongerStartsWithBowOrArrowsInInventory()
		{
			string text = File.ReadAllText(ScenePath);
			Assert.IsFalse(text.Contains(AssetDatabase.AssetPathToGUID(BowPath)), "Bow item still referenced in scene");
			Assert.IsFalse(text.Contains(AssetDatabase.AssetPathToGUID(ArrowPath)), "Arrow item still referenced in scene");
		}
	}
}
