using System.Collections;

/// <summary>Sleep where the character stands. Ends when CharacterStats wakes it: time is up, it was attacked, or it died.</summary>
public sealed class SleepOrder : CharacterOrder
{
	private readonly float hours;

	public SleepOrder(float hours)
	{
		this.hours = hours;
	}

	public override IEnumerator Run(Character character)
	{
		CharacterStats stats = character.Stats;
		if (stats == null)
			yield break;

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
