using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SetupSeverity
{
	Error = 0,
	Warning = 1
}

public class SetupIssue
{
	public SetupSeverity Severity;
	public string Message;
	public Object Context;

	public SetupIssue(SetupSeverity severity, string message, Object context = null)
	{
		Severity = severity;
		Message = message;
		Context = context;
	}

	public override string ToString() => $"{Severity}: {Message}";
}

/// <summary>What a check looks at: one scene, plus project assets.</summary>
public class SetupContext
{
	public readonly Scene Scene;
	private readonly Dictionary<System.Type, object> assetOverrides = new Dictionary<System.Type, object>();

	/// <summary>Checks may also look at prefab assets in the project. Tests turn it off.</summary>
	public bool CheckProjectPrefabs = true;

	public SetupContext(Scene scene)
	{
		Scene = scene;
	}

	public List<T> FindInScene<T>(bool includeInactive = true) where T : Component
	{
		var found = new List<T>();
		if (!Scene.IsValid() || !Scene.isLoaded)
			return found;
		foreach (GameObject root in Scene.GetRootGameObjects())
			found.AddRange(root.GetComponentsInChildren<T>(includeInactive));
		return found;
	}

	/// <summary>Project assets of a type, or the list a test put in with UseAssets.</summary>
	public List<T> FindAssets<T>() where T : Object
	{
		if (assetOverrides.TryGetValue(typeof(T), out object list))
			return new List<T>((List<T>)list);

		var found = new List<T>();
		foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
			if (asset != null)
				found.Add(asset);
		}
		return found;
	}

	/// <summary>For tests: check these instead of the project's assets of this type.</summary>
	public SetupContext UseAssets<T>(List<T> assets) where T : Object
	{
		assetOverrides[typeof(T)] = assets;
		return this;
	}
}

/// <summary>One kind of setup problem to look for. Found automatically by SetupValidator.</summary>
public interface ISetupCheck
{
	string Name { get; }
	void Run(SetupContext ctx, List<SetupIssue> issues);
}
