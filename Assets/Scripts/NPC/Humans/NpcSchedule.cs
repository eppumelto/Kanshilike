using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A daily routine. Each entry runs from its hour until the next entry's hour, wrapping past
/// midnight. The order of entries in the list does not matter.
/// </summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Schedule", fileName = "NewSchedule")]
public class NpcSchedule : ScriptableObject
{
	[Serializable]
	public class Entry
	{
		[Tooltip("In-game hour this entry starts (0-24).")]
		[Range(0f, 24f)] public float fromHour;
		[Tooltip("What the NPC does. Its asset decides whether it needs an anchor.")]
		public NpcActivity activity;
		[Tooltip("Where: the NPC's assigned anchor with this tag, else the nearest one in the scene.")]
		public NpcAnchorTag anchorTag;
	}

	public List<Entry> entries = new List<Entry>();

	private float[] hours;

	public int ResolveIndex(float hour) => NpcScheduleRules.ResolveIndex(Hours(), hour);

	public float HoursUntilNext(float hour) => NpcScheduleRules.HoursUntilNext(Hours(), hour);

	public Entry Get(int index) => index >= 0 && index < entries.Count ? entries[index] : null;

	private float[] Hours()
	{
		if (hours == null || hours.Length != entries.Count)
			RebuildCache();
		return hours;
	}

	private void RebuildCache()
	{
		hours = new float[entries.Count];
		for (int i = 0; i < entries.Count; i++)
			hours[i] = entries[i] != null ? entries[i].fromHour : 0f;
	}

	private void OnValidate() => RebuildCache();
}
