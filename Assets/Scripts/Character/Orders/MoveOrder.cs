using System.Collections;
using UnityEngine;

/// <summary>Walk to a point. The player's way out of a fight, so self-defence never overrides it.</summary>
public sealed class MoveOrder : CharacterOrder
{
	private const float ArriveSlack = 0.1f;

	private readonly Vector3 destination;

	public MoveOrder(Vector3 destination)
	{
		this.destination = destination;
	}

	public override bool YieldsToSelfDefence => false;

	public override IEnumerator Run(Character character)
	{
		character.MoveTo(destination);

		if (character.Agent == null)
			yield break;

		// Done once there is no path left to walk (arrived, or as close as the NavMesh allows).
		while (!OrderMovement.PathDone(character, ArriveSlack))
			yield return null;
	}
}
