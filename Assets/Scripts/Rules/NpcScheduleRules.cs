using System.Collections.Generic;

/// <summary>Which schedule entry runs at a given hour, and where. Pure, so it can be tested on its own.</summary>
public static class NpcScheduleRules
{
	/// <summary>Wraps an hour into [0, 24): 24 becomes 0, -1 becomes 23.</summary>
	public static float NormalizeHour(float hour)
	{
		float h = hour % 24f;
		if (h < 0f)
			h += 24f;
		return h >= 24f ? 0f : h;
	}

	/// <summary>
	/// The entry running at this hour: the one with the latest start at or before it, wrapping past
	/// midnight to the latest start of the day. Ties go to the lowest index. -1 for an empty list.
	/// </summary>
	public static int ResolveIndex(IReadOnlyList<float> fromHours, float hour)
	{
		if (fromHours == null || fromHours.Count == 0)
			return -1;

		float h = NormalizeHour(hour);
		int best = -1;
		int latest = 0;
		for (int i = 0; i < fromHours.Count; i++)
		{
			float start = NormalizeHour(fromHours[i]);
			if (start <= h && (best < 0 || start > NormalizeHour(fromHours[best])))
				best = i;
			if (start > NormalizeHour(fromHours[latest]))
				latest = i;
		}
		return best >= 0 ? best : latest;
	}

	/// <summary>Hours until the next different start time after this hour, wrapping. 24 for one entry, 0 for none.</summary>
	public static float HoursUntilNext(IReadOnlyList<float> fromHours, float hour)
	{
		if (fromHours == null || fromHours.Count == 0)
			return 0f;

		float h = NormalizeHour(hour);
		float best = 24f;
		for (int i = 0; i < fromHours.Count; i++)
		{
			float wait = NormalizeHour(fromHours[i]) - h;
			if (wait <= 0f)
				wait += 24f;
			if (wait < best)
				best = wait;
		}
		return best;
	}

	/// <summary>
	/// Where an activity runs: wandering at home when its prerequisites fail (Follow with no leader),
	/// its own fallback when it needs an anchor that is missing or has too few waypoints, else normally.
	/// </summary>
	public static ActivityPlacement ResolvePlacement(bool usesAnchor, bool anchorFound, int waypointCount, int minWaypoints,
		bool prerequisitesMet, MissingAnchorFallback fallback)
	{
		if (!prerequisitesMet)
			return ActivityPlacement.WanderAtHome;

		if (usesAnchor && (!anchorFound || waypointCount < minWaypoints))
			return fallback == MissingAnchorFallback.WanderAtHome ? ActivityPlacement.WanderAtHome : ActivityPlacement.AtHome;

		return ActivityPlacement.Normal;
	}
}
