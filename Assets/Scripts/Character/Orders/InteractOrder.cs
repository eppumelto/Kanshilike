using System.Collections;
using UnityEngine;

/// <summary>Walk to an interactable, spend any time the action takes, then perform it once.</summary>
public sealed class InteractOrder : CharacterOrder
{
	private const float ReachDistance = 2f;
	private const float RecheckSeconds = 0.25f;
	private const float RetargetDistance = 1f;

	private readonly IInteractable target;
	private readonly string actionId;

	public InteractOrder(IInteractable target, string actionId)
	{
		this.target = target;
		this.actionId = actionId;
	}

	public IInteractable Target => target;
	public string ActionId => actionId;

	public override IEnumerator Run(Character character)
	{
		Vector3 destination = target.GetInteractionPoint();
		character.MoveTo(destination);
		float nextRecheck = Time.time + RecheckSeconds;

		while (Vector3.Distance(character.transform.position, destination) > ReachDistance)
		{
			if (!CanStillPerform(character))
				yield break;

			// A moving target (a wandering NPC): head for where it is now.
			if (Time.time >= nextRecheck)
			{
				nextRecheck = Time.time + RecheckSeconds;
				Vector3 now = target.GetInteractionPoint();
				if (Vector3.Distance(now, destination) > RetargetDistance)
				{
					destination = now;
					character.MoveTo(destination);
				}
			}
			yield return null;
		}

		// Some actions (e.g. Harvest) take time. Scale duration by the character's harvest speed.
		float duration = target.GetInteractionDuration(actionId);
		if (duration > 0f)
		{
			float speed = character.Stats != null ? character.Stats.HarvestSpeedMultiplier : 1f;
			character.ShowActionLabel(target.GetInteractionLabel(actionId));
			yield return new WaitForSeconds(duration / speed);
			character.ClearActionLabel();
		}

		// Another character may have picked up or used up the target while we walked or worked.
		if (CanStillPerform(character))
			target.ExecuteInteraction(character, actionId);
	}

	public override void End(Character character)
	{
		character.ClearActionLabel();
	}

	private bool CanStillPerform(Character character)
	{
		if (!IsTargetValid())
			return false;

		foreach (var option in target.GetAvailableInteractions(character))
			if (option.id == actionId)
				return true;
		return false;
	}

	private bool IsTargetValid()
	{
		// Destroyed Unity objects only compare equal to null through UnityEngine.Object's
		// operator, which a plain interface null check bypasses.
		return target is Object unityObject ? unityObject != null : target != null;
	}
}
