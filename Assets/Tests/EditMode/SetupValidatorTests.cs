using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>Each setup check finds its problem, and a correct NPC gives none.</summary>
	public class SetupValidatorTests
	{
		Scene scene;
		readonly List<Object> assets = new List<Object>();
		readonly List<GameObject> objects = new List<GameObject>();
		FactionDefinition townsfolk;
		NpcAnchorTag stall;
		NpcAnchorTag market;
		IdleAtActivity idleAt;
		PatrolActivity patrol;

		[SetUp]
		public void SetUp()
		{
			// The test runner's own (untitled) scene; everything made here is destroyed in TearDown.
			scene = SceneManager.GetActiveScene();
			townsfolk = Make(FactionDefinition.Create("Townsfolk", FactionRelation.Neutral));
			stall = Make(ScriptableObject.CreateInstance<NpcAnchorTag>(), "Stall");
			market = Make(ScriptableObject.CreateInstance<NpcAnchorTag>(), "Market");
			idleAt = Make(ScriptableObject.CreateInstance<IdleAtActivity>(), "IdleAt");
			patrol = Make(ScriptableObject.CreateInstance<PatrolActivity>(), "Patrol");
		}

		[TearDown]
		public void TearDown()
		{
			foreach (GameObject go in objects)
				if (go != null)
					Object.DestroyImmediate(go);
			objects.Clear();
			foreach (Object o in assets)
				if (o != null)
					Object.DestroyImmediate(o);
			assets.Clear();
		}

		[Test]
		public void CorrectNpc_NoIssues()
		{
			Anchor("StallAnchor", stall);
			Npc("Aino", Archetype(Entry(9f, idleAt, stall)));
			Assert.IsEmpty(SceneIssues(), string.Join("\n", SceneIssues()));
		}

		[Test]
		public void BrainWithNoArchetype_Error()
		{
			NpcBrain brain = Npc("Aino", null);
			AssertSingle(SetupSeverity.Error, "no archetype", brain);
		}

		[Test]
		public void ArchetypeWithNoSchedule_Warning()
		{
			var archetype = Make(ScriptableObject.CreateInstance<NpcArchetype>(), "Plain");
			Npc("Aino", archetype);
			AssertSingle(SetupSeverity.Warning, "no schedule", archetype);
		}

		[Test]
		public void EntryWithNoActivity_Error()
		{
			NpcArchetype archetype = Archetype(Entry(9f, null, stall));
			Npc("Aino", archetype);
			AssertSingle(SetupSeverity.Error, "no activity", archetype.schedule);
		}

		[Test]
		public void TagWithNoAnchor_Warning()
		{
			NpcBrain brain = Npc("Aino", Archetype(Entry(9f, idleAt, market)));
			AssertSingle(SetupSeverity.Warning, "Market", brain);
		}

		[Test]
		public void AnchorWithNoTag_Error()
		{
			NpcAnchor anchor = Anchor("Nameless", null);
			AssertSingle(SetupSeverity.Error, "no tag", anchor);
		}

		[Test]
		public void PatrolAnchorWithOneWaypoint_Warning()
		{
			NpcAnchor anchor = Anchor("Route", stall);
			Track(new GameObject("Point")).transform.SetParent(anchor.transform);
			Npc("Guard", Archetype(Entry(9f, patrol, stall)));
			AssertSingle(SetupSeverity.Warning, "waypoint", anchor);
		}

		[Test]
		public void CharacterWithNoFactionAsset_Warning()
		{
			var go = Track(new GameObject("Stranger"));
			Character c = go.AddComponent<Character>();
			AssertSingle(SetupSeverity.Warning, "legacy", c);
		}

		[Test]
		public void DuplicateFactionIds_Error()
		{
			var a = Make(FactionDefinition.Create("Kin", FactionRelation.Neutral));
			var b = Make(FactionDefinition.Create("kin", FactionRelation.Hostile));
			var ctx = new SetupContext(scene).UseAssets(new List<FactionDefinition> { a, b });
			List<SetupIssue> issues = new List<SetupIssue>();
			new FactionSetupCheck().Run(ctx, issues);
			SetupIssue issue = issues.Single(i => i.Message.Contains("share the id"));
			Assert.AreEqual(SetupSeverity.Error, issue.Severity);
			Assert.AreSame(b, issue.Context);
		}

		[Test]
		public void Test1_HasNoErrors()
		{
			const string path = "Assets/Scenes/test1.unity";
			Scene test1 = SceneManager.GetSceneByPath(path);
			bool alreadyOpen = test1.IsValid() && test1.isLoaded;
			if (!alreadyOpen)
				test1 = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
			try
			{
				List<SetupIssue> errors = SetupValidator.Validate(test1).Where(i => i.Severity == SetupSeverity.Error).ToList();
				Assert.IsEmpty(errors, string.Join("\n", errors));
			}
			finally
			{
				if (!alreadyOpen)
					EditorSceneManager.CloseScene(test1, true);
			}
		}

		// ── Helpers ─────────────────────────────────────────

		// Scene-only checks, with the project's assets out of the way.
		List<SetupIssue> SceneIssues()
		{
			var ctx = new SetupContext(scene).UseAssets(new List<FactionDefinition>()).UseAssets(new List<FactionRelationSet>());
			var issues = new List<SetupIssue>();
			foreach (ISetupCheck check in new ISetupCheck[] { new ActorFactionCheck(), new NpcBrainCheck(), new NpcAnchorCheck(), new NpcComponentCheck() })
				check.Run(ctx, issues);
			return issues;
		}

		void AssertSingle(SetupSeverity severity, string text, Object context)
		{
			List<SetupIssue> issues = SceneIssues();
			Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
			Assert.AreEqual(severity, issues[0].Severity);
			StringAssert.Contains(text, issues[0].Message);
			Assert.AreSame(context, issues[0].Context);
		}

		NpcBrain Npc(string name, NpcArchetype archetype)
		{
			var go = Track(new GameObject(name));
			go.SetActive(false);
			Character c = go.AddComponent<Character>();
			SetField(c, "ownFaction", townsfolk);
			NpcBrain brain = go.AddComponent<NpcBrain>();
			SetField(brain, "archetype", archetype);
			return brain;
		}

		NpcAnchor Anchor(string name, NpcAnchorTag tag)
		{
			var go = Track(new GameObject(name));
			NpcAnchor anchor = go.AddComponent<NpcAnchor>();
			anchor.anchorTag = tag;
			return anchor;
		}

		NpcArchetype Archetype(params NpcSchedule.Entry[] entries)
		{
			var schedule = Make(ScriptableObject.CreateInstance<NpcSchedule>(), "Day");
			schedule.entries.AddRange(entries);
			var archetype = Make(ScriptableObject.CreateInstance<NpcArchetype>(), "Townsperson");
			archetype.schedule = schedule;
			return archetype;
		}

		static NpcSchedule.Entry Entry(float hour, NpcActivity activity, NpcAnchorTag tag) =>
			new NpcSchedule.Entry { fromHour = hour, activity = activity, anchorTag = tag };

		GameObject Track(GameObject go)
		{
			objects.Add(go);
			return go;
		}

		T Make<T>(T obj, string name = null) where T : Object
		{
			if (name != null)
				obj.name = name;
			assets.Add(obj);
			return obj;
		}

		static void SetField(object target, string field, object value)
		{
			for (System.Type type = target.GetType(); type != null; type = type.BaseType)
			{
				var info = type.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
				if (info != null)
				{
					info.SetValue(target, value);
					return;
				}
			}
			Assert.Fail("No field " + field);
		}
	}
}
