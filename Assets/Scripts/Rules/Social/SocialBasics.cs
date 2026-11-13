using System;

/// <summary>A position in the world. Rules code cannot use Vector3.</summary>
[Serializable]
public struct WorldPoint
{
	public float X, Y, Z;

	public WorldPoint(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public static float DistanceSq(WorldPoint a, WorldPoint b)
	{
		float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
		return dx * dx + dy * dy + dz * dz;
	}

	public override string ToString() => $"({X:0.#}, {Y:0.#}, {Z:0.#})";
}

/// <summary>
/// Counts days from the 0-24 clock. A backwards hour means midnight passed (the same rule as
/// HourClock). The first Advance only seeds.
/// </summary>
public sealed class DayClock
{
	private bool started;
	private float hour;

	public int Day { get; private set; }
	public double TotalHours => Day * 24.0 + hour;

	public void Advance(float newHour)
	{
		if (started && newHour < hour)
			Day++;
		started = true;
		hour = newHour;
	}

	public static int DayOf(double totalHours) => (int)Math.Floor(totalHours / 24.0);
}
