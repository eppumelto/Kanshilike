using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Person ids: unique in the scene, set on scene characters, empty on prefab assets.</summary>
public class PersonIdCheck : ISetupCheck
{
	public string Name => "Person ids";

	/// <summary>Where NPC and character prefabs live; their own person id must stay empty.</summary>
	public static readonly string[] PrefabFolders = { "Assets/Prefabs" };

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		var seen = new Dictionary<string, Character>();
		foreach (Character c in ctx.FindInScene<Character>())
		{
			string id = c.PersonId;
			if (string.IsNullOrEmpty(id))
			{
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{c.name}: no person id yet; select it once to assign one, then save the scene.", c));
				continue;
			}
			if (seen.TryGetValue(id, out Character first))
				issues.Add(new SetupIssue(SetupSeverity.Error, $"{c.name}: same person id as {first.name} (copied with Ctrl+D?). Select it to get a new id, or delete it.", c));
			else
				seen.Add(id, c);
		}

		if (!ctx.CheckProjectPrefabs)
			return;
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
			Character c = prefab != null ? prefab.GetComponent<Character>() : null;
			if (c != null && !string.IsNullOrEmpty(c.PersonId))
				issues.Add(new SetupIssue(SetupSeverity.Error, $"Prefab {prefab.name} has a person id; every instance would share it. Open the prefab once to clear it.", prefab));
		}
	}
}

/// <summary>One SocialWorld per scene, with a config.</summary>
public class SocialWorldCheck : ISetupCheck
{
	public string Name => "Social world";

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		List<SocialWorld> worlds = ctx.FindInScene<SocialWorld>();
		if (worlds.Count > 1)
			issues.Add(new SetupIssue(SetupSeverity.Error, $"{worlds.Count} SocialWorlds in the scene; keep one.", worlds[1]));
		foreach (SocialWorld w in worlds)
			if (w.Config == null)
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{w.name}: SocialWorld has no SocialConfig; defaults are used.", w));
	}
}
