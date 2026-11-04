using UnityEngine;

public static class CombatFacing
{
	private const float TurnDegreesPerSecond = 360f;

	/// <summary>Turns a fighter toward a point at a fixed speed, staying upright.</summary>
	public static void TurnToward(Transform self, Vector3 target)
	{
		Vector3 direction = target - self.position;
		direction.y = 0f;
		if (direction.sqrMagnitude < 0.0001f)
			return;

		self.rotation = Quaternion.RotateTowards(self.rotation, Quaternion.LookRotation(direction), TurnDegreesPerSecond * Time.deltaTime);
	}
}
