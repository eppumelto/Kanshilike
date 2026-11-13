using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SocialConfig in sections, plus a read-only preview: what each typical bond gives when the
/// victim is killed, at a chosen vengefulness and at the squad default.
/// </summary>
[CustomEditor(typeof(SocialConfig))]
public class SocialConfigEditor : Editor
{
	// The first field of each section in SocialTuning.
	private static readonly Dictionary<string, string> Sections = new Dictionary<string, string>
	{
		{ "hostileOpinion", "Opinion" },
		{ "neighbourBondOpinion", "Bonds: default opinions" },
		{ "weightFamily", "Bonds: closeness weights" },
		{ "grudgeClosenessThreshold", "Grudges: creation" },
		{ "minGrudgeIntensity", "Grudges: tiers" },
		{ "decayPerDay", "Grudges: decay and limits" },
		{ "attributionWindowHours", "Knowledge" },
		{ "rallyRadius", "Revenge: rally and hunt (4c)" },
		{ "bloodPriceWorthPerIntensity", "Blood price (4d)" },
	};

	private static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
	{
		{ "hostileOpinion", "At or below this opinion a person is Hostile, even toward their own faction." },
		{ "alliedOpinion", "At or above this a person is Allied: the first blow is taken as an accident." },
		{ "attackedOpinion", "Opinion change toward an attacker, per attack." },
		{ "grudgeClosenessThreshold", "Closeness to the victim needed for any grudge." },
		{ "kinGrudgeBonus", "Added when the victim was the holder's parent, child, sibling or spouse." },
		{ "minGrudgeIntensity", "Cold tier: refuses dealings, no attack." },
		{ "hostileAt", "Hostile tier: attacks on sight." },
		{ "rallyAt", "Rally tier: calls friends." },
		{ "huntAt", "Hunt tier: hunts the target." },
		{ "squadScopeAt", "At this intensity a grudge extends to the whole squad." },
		{ "inheritAt", "Killing grudges at least this strong pass to the holder's children when they die." },
		{ "inheritFactor", "A child takes over intensity × this." },
		{ "attributionWindowHours", "In-game hours a recent attacker is blamed for a death with no direct source." },
		{ "witnessRadius", "Metres; awake people this close see a death." },
	};

	private bool showPreview = true;
	private float vengefulness = 0.6f;

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		SerializedProperty tuning = serializedObject.FindProperty("tuning");
		SerializedProperty it = tuning.Copy();
		SerializedProperty end = tuning.GetEndProperty();
		bool enter = true;
		while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
		{
			enter = false;
			if (Sections.TryGetValue(it.name, out string header))
			{
				EditorGUILayout.Space(6);
				EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
			}
			Tooltips.TryGetValue(it.name, out string tip);
			EditorGUILayout.PropertyField(it, new GUIContent(it.displayName, tip), true);
		}
		serializedObject.ApplyModifiedProperties();

		EditorGUILayout.Space(8);
		showPreview = EditorGUILayout.Foldout(showPreview, "Preview: grudge when the victim is killed", true);
		if (showPreview)
			DrawPreview(((SocialConfig)target).ToTuning());
	}

	private void DrawPreview(SocialTuning t)
	{
		vengefulness = EditorGUILayout.Slider("Vengefulness", vengefulness, 0f, 1f);
		IReadOnlyList<GrudgePreviewRow> a = GrudgePreview.Rows(t, vengefulness);
		IReadOnlyList<GrudgePreviewRow> b = GrudgePreview.Rows(t, t.defaultVengefulness);

		using (new EditorGUILayout.HorizontalScope())
		{
			GUILayout.Label("Bond", EditorStyles.miniBoldLabel, GUILayout.Width(130));
			GUILayout.Label("Close", EditorStyles.miniBoldLabel, GUILayout.Width(40));
			GUILayout.Label($"At {vengefulness:0.##}", EditorStyles.miniBoldLabel, GUILayout.Width(100));
			GUILayout.Label($"At {t.defaultVengefulness:0.##} (squad)", EditorStyles.miniBoldLabel);
		}

		Color normal = GUI.color;
		for (int i = 0; i < a.Count; i++)
		{
			bool flagged = a[i].OnThreshold || b[i].OnThreshold;
			GUI.color = flagged ? new Color(1f, 0.75f, 0.3f) : normal;
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Label(a[i].Bond + (flagged ? " (on a threshold)" : ""), GUILayout.Width(130));
				GUILayout.Label(a[i].Closeness.ToString("0.##"), GUILayout.Width(40));
				GUILayout.Label(Cell(a[i]), GUILayout.Width(100));
				GUILayout.Label(Cell(b[i]));
			}
		}
		GUI.color = normal;
	}

	private static string Cell(GrudgePreviewRow r) => r.BelowCloseness ? "no grudge" : $"{r.Intensity:0.#} {r.Tier}";
}
