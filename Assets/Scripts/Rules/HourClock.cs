using System;
using System.Collections.Generic;

/// <summary>One whole in-game hour that just ended.</summary>
public readonly struct HourTick
{
	/// <summary>The hour that just started (0-23), e.g. 9 for the tick at 9:00.</summary>
	public readonly int Hour;
	/// <summary>Share of the hour that just ended spent asleep (0-1).</summary>
	public readonly float AsleepFraction;

	public HourTick(int hour, float asleepFraction)
	{
		Hour = hour;
		AsleepFraction = asleepFraction;
	}
}

/// <summary>
/// Follows the 0-24 day clock and reports every whole hour that passes, even several in one
/// frame, along with how much of each hour was slept. Also counts down a sleep in in-game hours.
/// No Unity dependency, so it can be tested on its own.
/// </summary>
public sealed class HourClock
{
	private bool started;
	private float lastTime;
	private float asleepThisHour;
	private float sleepHoursLeft;

	public bool IsSleeping => sleepHoursLeft > 0f;

	public void StartSleep(float hours)
	{
		sleepHoursLeft = Math.Max(0f, hours);
	}

	public void StopSleep()
	{
		sleepHoursLeft = 0f;
	}

	/// <summary>
	/// Moves the clock to <paramref name="time"/> (0-24) and adds a tick for each hour mark passed.
	/// The first call only sets the starting point. Returns true if a sleep ran out on the way.
	/// </summary>
	public bool Advance(float time, List<HourTick> ticks)
	{
		if (!started)
		{
			started = true;
			lastTime = time;
			return false;
		}

		// Going backwards means the clock passed midnight: count from the previous time minus a day.
		float position = time < lastTime ? lastTime - 24f : lastTime;
		float end = time;
		lastTime = time;

		bool sleepEnded = false;
		while (true)
		{
			float mark = (float)Math.Floor(position) + 1f;
			float segmentEnd = Math.Min(mark, end);

			if (sleepHoursLeft > 0f)
			{
				float slept = Math.Min(segmentEnd - position, sleepHoursLeft);
				asleepThisHour += slept;
				sleepHoursLeft -= slept;
				if (sleepHoursLeft <= 0f)
				{
					sleepHoursLeft = 0f;
					sleepEnded = true;
				}
			}

			if (segmentEnd < mark)
				break;

			int hour = (((int)mark % 24) + 24) % 24;
			ticks.Add(new HourTick(hour, Math.Min(1f, asleepThisHour)));
			asleepThisHour = 0f;
			position = mark;
		}

		return sleepEnded;
	}
}
