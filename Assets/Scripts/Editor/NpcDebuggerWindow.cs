using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kanshilike > NPC Debugger: shows NpcDebugReport for the character selected in the Hierarchy, or
/// for the primary selected squad member when nothing is selected. Works while paused.
/// </summary>
public class NpcDebuggerWindow : EditorWindow
{
	private const double RepaintSeconds = 0.25;

	private Character pinned;
	private bool pin;
	private Vector2 scroll;
	private double nextRepaint;
	private readonly Dictionary<string, bool> folded = new Dictionary<string, bool>();

	[MenuItem("Kanshilike/NPC Debugger")]
	public static void Open() => GetWindow<NpcDebuggerWindow>("NPC Debugger");

	private void OnEnable() => EditorApplication.update += Tick;
	private void OnDisable() => EditorApplication.update -= Tick;
	private void OnSelectionChange() => Repaint();

	private void Tick()
	{
		if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextRepaint)
			return;
		nextRepaint = EditorApplication.timeSinceStartup + RepaintSeconds;
		Repaint();
	}

	private void OnGUI()
	{
		if (!EditorApplication.isPlaying)
		{
			EditorGUILayout.HelpBox("Enter Play mode to inspect NPCs.", MessageType.Info);
			return;
		}

		Character target = pin && pinned != null ? pinned : FindTarget();
		if (!pin)
			pinned = target;

		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.LabelField(target != null ? Title(target) : "Nothing selected", EditorStyles.boldLabel);
			pin = GUILayout.Toggle(pin, "Pin", "Button", GUILayout.Width(50));
		}

		if (target == null)
		{
			EditorGUILayout.HelpBox("Select a character in the Hierarchy, or select a squad member in the game.", MessageType.None);
			return;
		}

		NpcDebugReport report = NpcDebugReport.Build(target);
		scroll = EditorGUILayout.BeginScrollView(scroll);
		foreach (NpcDebugReport.Section section in report.Sections)
		{
			folded.TryGetValue(section.Title, out bool closed);
			bool open = EditorGUILayout.Foldout(!closed, section.Title, true);
			folded[section.Title] = !open;
			if (!open)
				continue;
			EditorGUI.indentLevel++;
			foreach (string line in section.Lines)
				EditorGUILayout.SelectableLabel(line, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndScrollView();
	}

	private static Character FindTarget()
	{
		GameObject selected = Selection.activeGameObject;
		if (selected != null)
		{
			Character c = selected.GetComponentInParent<Character>();
			if (c != null)
				return c;
		}
		foreach (PlayerControllable member in PlayerControllable.All)
			if (member != null && member.IsSelected)
				return member.Character;
		return null;
	}

	private static string Title(Character c) =>
		string.IsNullOrEmpty(c.characterName) ? c.name : $"{c.characterName} ({c.name})";
}
