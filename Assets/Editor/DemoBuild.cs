using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the game for play-testers without the Unity MCP plugin. The plugin is an editor tool for
/// AI agents; its runtime part would otherwise compile into the player with its networking DLLs.
/// The package's own UNITY_MCP_EXCLUDE_FROM_COMPILATION switch is set for this build only, so the
/// editor keeps the plugin.
/// </summary>
public static class DemoBuild
{
	public const string ExcludeMcpDefine = "UNITY_MCP_EXCLUDE_FROM_COMPILATION";

	/// <summary>DLLs that belong to the MCP plugin and must not reach a player build.</summary>
	public static readonly string[] McpAssemblies =
	{
		"McpPlugin.dll", "McpPlugin.Common.dll", "ReflectorNet.dll",
		"Microsoft.AspNetCore.SignalR.Client.dll", "com.IvanMurzak.Unity.MCP.Runtime.dll"
	};

	[MenuItem("Kanshilike/Build Windows Demo")]
	public static void BuildWindowsDemo()
	{
		string path = EditorUtility.SaveFolderPanel("Build Windows demo into", "Builds", "Demo");
		if (!string.IsNullOrEmpty(path))
			Build(Path.Combine(path, "Kanshilike.exe"));
	}

	/// <summary>Builds the scenes in Build Settings for Windows. Returns true when it succeeded and no MCP DLL shipped.</summary>
	public static bool Build(string exePath)
	{
		var options = new BuildPlayerOptions
		{
			scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
			locationPathName = exePath,
			target = BuildTarget.StandaloneWindows64,
			options = BuildOptions.None,
			extraScriptingDefines = new[] { ExcludeMcpDefine }
		};

		BuildReport report = BuildPipeline.BuildPlayer(options);
		if (report.summary.result != BuildResult.Succeeded)
		{
			Debug.LogError($"[DemoBuild] Build {report.summary.result}: {report.summary.totalErrors} errors.");
			return false;
		}

		if (!RemovePluginLibraries(exePath, out string problem))
		{
			Debug.LogError($"[DemoBuild] {problem}");
			return false;
		}

		string[] shipped = FindMcpAssemblies(exePath);
		if (shipped.Length > 0)
		{
			Debug.LogError($"[DemoBuild] MCP plugin files reached the build: {string.Join(", ", shipped)}");
			return false;
		}

		Debug.Log($"[DemoBuild] Built {exePath} ({report.summary.totalSize / (1024 * 1024)} MB) without the MCP plugin.");
		return true;
	}

	/// <summary>
	/// The MCP package's NuGet libraries (Assets/Plugins/NuGet) are imported for every platform, so
	/// Unity copies them into the player even with the plugin compiled out. They are removed from the
	/// build folder, but only after checking that nothing left in the build refers to them.
	/// </summary>
	public static bool RemovePluginLibraries(string exePath, out string problem)
	{
		problem = null;
		string managed = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath) + "_Data", "Managed");
		if (!Directory.Exists(managed))
			return true;

		var pluginNames = Directory.GetFiles("Assets/Plugins/NuGet", "*.dll")
			.Select(Path.GetFileNameWithoutExtension)
			.ToHashSet();

		string[] toRemove = Directory.GetFiles(managed, "*.dll")
			.Where(file => pluginNames.Contains(Path.GetFileNameWithoutExtension(file)))
			.ToArray();
		var removedNames = toRemove.Select(Path.GetFileNameWithoutExtension).ToArray();

		// Unity's and .NET's own assemblies are built before any project and can't refer to its plugins,
		// so only the game's and the packages' assemblies need checking.
		foreach (string kept in Directory.GetFiles(managed, "*.dll").Except(toRemove).Where(file => !IsPlatformAssembly(file)))
		{
			string text = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(kept));
			// Assembly references are stored as null-terminated names.
			string used = removedNames.FirstOrDefault(name => text.Contains("\0" + name + "\0"));
			if (used != null)
			{
				problem = $"{Path.GetFileName(kept)} refers to {used}, so the plugin libraries were left in place.";
				return false;
			}
		}

		foreach (string file in toRemove)
			File.Delete(file);

		Debug.Log($"[DemoBuild] Removed {toRemove.Length} unused plugin libraries from the build.");
		return true;
	}

	private static bool IsPlatformAssembly(string file)
	{
		string name = Path.GetFileName(file);
		return name == "mscorlib.dll" || name == "netstandard.dll"
			|| name.StartsWith("System.") || name == "System.dll"
			|| name.StartsWith("Mono.") || name.StartsWith("UnityEngine.") || name == "UnityEngine.dll";
	}

	/// <summary>MCP plugin DLLs found anywhere in the build folder.</summary>
	public static string[] FindMcpAssemblies(string exePath)
	{
		string folder = Path.GetDirectoryName(exePath);
		return Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories)
			.Select(Path.GetFileName)
			.Where(file => McpAssemblies.Contains(file))
			.Distinct()
			.ToArray();
	}
}
