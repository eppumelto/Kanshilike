using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-off tool: fills Character.ownFaction and AnimalAI.factionDefinition from the old enum value
/// kept in their hidden legacyFaction field. Prefabs first, then every scene under Assets/Scenes.
/// Objects that already have an asset are skipped, so running it twice changes nothing.
/// </summary>
public static class FactionMigration
{
	private const string MenuPath = "Kanshilike/Factions/Migrate Legacy Faction Fields";

	[MenuItem(MenuPath)]
	public static void MigrateAll()
	{
		FactionRelationSet set = Resources.Load<FactionRelationSet>("Factions/FactionRelations");
		if (set == null)
		{
			Debug.LogError("[FactionMigration] Resources/Factions/FactionRelations.asset is missing. Create it first.");
			return;
		}

		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			return;

		int objects = 0;
		int files = 0;

		foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (!path.StartsWith("Assets/"))
				continue;
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			int changed = MigrateUnder(root, set.legacyEnumOrder);
			if (changed > 0)
			{
				PrefabUtility.SaveAsPrefabAsset(root, path);
				objects += changed;
				files++;
				Debug.Log($"[FactionMigration] {path}: {changed} object(s).");
			}
			PrefabUtility.UnloadPrefabContents(root);
		}

		foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			Scene scene = SceneManager.GetSceneByPath(path);
			bool wasOpen = scene.IsValid() && scene.isLoaded;
			if (!wasOpen)
				scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

			int changed = 0;
			foreach (GameObject root in scene.GetRootGameObjects())
				changed += MigrateUnder(root, set.legacyEnumOrder);

			if (changed > 0)
			{
				EditorSceneManager.MarkSceneDirty(scene);
				EditorSceneManager.SaveScene(scene);
				objects += changed;
				files++;
				Debug.Log($"[FactionMigration] {path}: {changed} object(s).");
			}

			if (!wasOpen)
				EditorSceneManager.CloseScene(scene, true);
		}

		Debug.Log($"[FactionMigration] Migrated {objects} object(s) in {files} file(s).");
	}

	// Prefabs are migrated first, so scene instances of them already inherit the asset and are skipped.
	private static int MigrateUnder(GameObject root, List<FactionDefinition> legacyOrder)
	{
		int changed = 0;
		foreach (Character c in root.GetComponentsInChildren<Character>(true))
			if (Migrate(c, "ownFaction", legacyOrder))
				changed++;
		foreach (AnimalAI a in root.GetComponentsInChildren<AnimalAI>(true))
			if (Migrate(a, "factionDefinition", legacyOrder))
				changed++;
		return changed;
	}

	private static bool Migrate(Component component, string assetField, List<FactionDefinition> legacyOrder)
	{
		var so = new SerializedObject(component);
		SerializedProperty asset = so.FindProperty(assetField);
		SerializedProperty legacy = so.FindProperty("legacyFaction");
		if (asset == null || legacy == null || asset.objectReferenceValue != null)
			return false;

		int index = legacy.intValue;
		if (index < 0 || index >= legacyOrder.Count || legacyOrder[index] == null)
		{
			Debug.LogWarning($"[FactionMigration] {component.name}: legacy value {index} has no faction asset; left unset.", component);
			return false;
		}

		asset.objectReferenceValue = legacyOrder[index];
		so.ApplyModifiedPropertiesWithoutUndo();
		return true;
	}
}
