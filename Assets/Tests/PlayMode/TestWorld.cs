using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kanshilike.Tests.PlayMode
{
	/// <summary>
	/// Code-built characters, NPCs and clocks for PlayMode tests: no scene, NavMesh or UI. Installs
	/// the test factions; Dispose() destroys everything it made.
	/// </summary>
	public sealed class TestWorld
	{
		readonly List<Object> made = new List<Object>();
		public readonly TestFactions Factions = new TestFactions();
		public readonly CombatFormulaConfig Config;

		public TestWorld()
		{
			Factions.Install();
			Config = Track(ScriptableObject.CreateInstance<CombatFormulaConfig>());
		}

		public void Dispose()
		{
			foreach (Object obj in made)
				if (obj != null)
					Object.Destroy(obj);
			made.Clear();
			Factions.Uninstall();
		}

		public Character MakeCharacter(string name, Vector3 position, FactionDefinition faction, bool withInventory = false,
			Action<GameObject> beforeEnable = null)
		{
			var go = Track(new GameObject(name));
			go.SetActive(false);
			go.transform.position = position;
			go.AddComponent<CapsuleCollider>();
			go.AddComponent<CharacterStats>();
			if (withInventory)
				go.AddComponent<Inventory>();
			var character = go.AddComponent<Character>();
			character.characterName = name;
			SetField(character, "ownFaction", faction);
			SetField(character, "combatFormulaConfig", Config);
			beforeEnable?.Invoke(go);
			go.SetActive(true);
			return character;
		}

		/// <summary>A Townsfolk NPC with a brain running this archetype.</summary>
		public NpcBrain MakeNpc(string name, Vector3 position, NpcArchetype archetype, params NpcAnchor[] assigned)
		{
			NpcBrain brain = null;
			MakeCharacter(name, position, Factions.Townsfolk, beforeEnable: go =>
			{
				brain = go.AddComponent<NpcBrain>();
				SetField(brain, "archetype", archetype);
				SetField(brain, "assignedAnchors", assigned);
			});
			return brain;
		}

		public NpcArchetype MakeArchetype(params NpcSchedule.Entry[] entries)
		{
			var schedule = Track(ScriptableObject.CreateInstance<NpcSchedule>());
			schedule.name = "TestSchedule";
			schedule.entries.AddRange(entries);
			var archetype = Track(ScriptableObject.CreateInstance<NpcArchetype>());
			archetype.name = "TestArchetype";
			archetype.schedule = schedule;
			archetype.thinkIntervalSeconds = 0.05f;
			archetype.wanderPauseSeconds = new Vector2(5f, 5f);
			return archetype;
		}

		public static NpcSchedule.Entry Entry(float hour, NpcActivity activity, NpcAnchorTag tag = null) =>
			new NpcSchedule.Entry { fromHour = hour, activity = activity, anchorTag = tag };

		public T MakeAsset<T>(string name) where T : ScriptableObject
		{
			var asset = Track(ScriptableObject.CreateInstance<T>());
			asset.name = name;
			return asset;
		}

		public NpcAnchor MakeAnchor(string name, Vector3 position, NpcAnchorTag tag)
		{
			var go = Track(new GameObject(name));
			go.transform.position = position;
			var anchor = go.AddComponent<NpcAnchor>();
			anchor.anchorTag = tag;
			return anchor;
		}

		/// <summary>A clock frozen at this hour (timeMultiplier 0) unless told otherwise.</summary>
		public TimeManager MakeTimeManager(float startHour, float timeMultiplier = 0f)
		{
			var go = Track(new GameObject("Time"));
			go.SetActive(false);
			var time = go.AddComponent<TimeManager>();
			time.sun = go.AddComponent<Light>();
			time.ambientColor = new Gradient();
			time.sunIntensity = AnimationCurve.Constant(0f, 1f, 1f);
			time.startTime = startHour;
			time.currentTime = startHour;
			time.timeMultiplier = timeMultiplier;
			go.SetActive(true);
			return time;
		}

		/// <summary>A SocialWorld with this tuning (default when null). Registers the characters already made.</summary>
		public SocialWorld MakeSocialWorld(SocialTuning tuning = null)
		{
			var config = Track(ScriptableObject.CreateInstance<SocialConfig>());
			config.tuning = tuning ?? SocialTuning.Default;
			var go = Track(new GameObject("SocialWorld"));
			go.SetActive(false);
			var social = go.AddComponent<SocialWorld>();
			SetField(social, "config", config);
			go.SetActive(true);
			return social;
		}

		public T MakeItem<T>(string name, int maxStack) where T : Item
		{
			var item = Track(ScriptableObject.CreateInstance<T>());
			item.name = name;
			item.itemName = name;
			item.maxStack = maxStack;
			return item;
		}

		public T Track<T>(T obj) where T : Object
		{
			made.Add(obj);
			return obj;
		}

		public static IEnumerator WaitFor(Func<bool> condition, float seconds)
		{
			float until = Time.time + seconds;
			while (!condition() && Time.time < until)
				yield return null;
		}

		public static void SetField(object target, string field, object value)
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
			Assert.Fail($"No field {field} on {target.GetType().Name}");
		}
	}
}
