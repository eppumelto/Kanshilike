using System.Collections;
using UnityEngine;

/// <summary>
/// Sleep, optionally after walking to a bed. Ends when CharacterStats wakes it: time is up, it was
/// attacked, or it died.
/// </summary>
public sealed class SleepOrder : CharacterOrder
{
	private const float ArriveSlack = 0.5f;

	private readonly float hours;
	private readonly Vector3? bedPosition;

	public SleepOrder(float hours, Vector3? bedPosition = null)
	{
		this.hours = hours;
		this.bedPosition = bedPosition;
	}

	public override IEnumerator Run(Character character)
	{
		CharacterStats stats = character.Stats;
		if (stats == null)
			yield break;

		if (bedPosition.HasValue)
			yield return OrderMovement.WalkTo(character, bedPosition.Value, ArriveSlack);

		character.StopMoving();
		stats.Sleep(hours);
		character.ShowActionLabel("Sleeping...");

		while (stats.IsSleeping)
			yield return null;
	}

	public override void End(Character character)
	{
		if (character.Stats != null)
			character.Stats.WakeUp();
		character.ClearActionLabel();
	}
}
