using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Kanshilike > Validate NPC Setup: runs every ISetupCheck on the active scene.</summary>
public static class SetupValidator
{
	public static List<SetupIssue> Validate(Scene scene) => Validate(new SetupContext(scene));

	public static List<SetupIssue> Validate(SetupContext ctx)
	{
		var issues = new List<SetupIssue>();
		foreach (Type type in TypeCache.GetTypesDerivedFrom<ISetupCheck>())
		{
			if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
				continue;
			var check = (ISetupCheck)Activator.CreateInstance(type);
			try
			{
				check.Run(ctx, issues);
			}
			catch (Exception e)
			{
				issues.Add(new SetupIssue(SetupSeverity.Error, $"{check.Name} failed: {e.Message}"));
			}
		}
		issues.Sort((a, b) => a.Severity.CompareTo(b.Severity));
		return issues;
	}

	[MenuItem("Kanshilike/Validate NPC Setup")]
	public static void ValidateActiveScene()
	{
		List<SetupIssue> issues = Validate(SceneManager.GetActiveScene());
		foreach (SetupIssue issue in issues)
		{
			if (issue.Severity == SetupSeverity.Error)
				Debug.LogError("[Setup] " + issue.Message, issue.Context);
			else
				Debug.LogWarning("[Setup] " + issue.Message, issue.Context);
		}
		Debug.Log($"[Setup] {issues.FindAll(i => i.Severity == SetupSeverity.Error).Count} error(s), " +
			$"{issues.FindAll(i => i.Severity == SetupSeverity.Warning).Count} warning(s).");
		SetupResultsWindow.Show(issues);
	}
}

/// <summary>Lists validator results; click one to ping its object.</summary>
public class SetupResultsWindow : EditorWindow
{
	private List<SetupIssue> issues = new List<SetupIssue>();
	private Vector2 scroll;

	public static void Show(List<SetupIssue> found)
	{
		var window = GetWindow<SetupResultsWindow>("NPC Setup");
		window.issues = found;
		window.Repaint();
	}

	private void OnGUI()
	{
		if (issues.Count == 0)
		{
			EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
			return;
		}

		scroll = EditorGUILayout.BeginScrollView(scroll);
		foreach (SetupIssue issue in issues)
		{
			MessageType type = issue.Severity == SetupSeverity.Error ? MessageType.Error : MessageType.Warning;
			EditorGUILayout.HelpBox(issue.Message, type);
			if (issue.Context != null && Event.current.type == EventType.MouseDown
				&& GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
			{
				EditorGUIUtility.PingObject(issue.Context);
				Selection.activeObject = issue.Context;
				Event.current.Use();
			}
		}
		EditorGUILayout.EndScrollView();
	}
}
