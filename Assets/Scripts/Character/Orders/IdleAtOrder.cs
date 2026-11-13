using System.Collections;
using UnityEngine;

/// <summary>Walk to a point and stay there, facing a direction or someone, for a while or until replaced.</summary>
public sealed class IdleAtOrder : CharacterOrder
{
	private const float ArriveSlack = 0.5f;

	private readonly Vector3 point;
	private readonly Quaternion? facing;
	private readonly float seconds;
	private readonly Transform lookAt;

	public IdleAtOrder(Vector3 point, Quaternion? facing = null, float seconds = float.PositiveInfinity, Transform lookAt = null)
	{
		this.point = point;
		this.facing = facing;
		this.seconds = seconds;
		this.lookAt = lookAt;
	}

	public Vector3 Point => point;
	public Transform LookAt => lookAt;

	public override IEnumerator Run(Character character)
	{
		yield return OrderMovement.WalkTo(character, point, ArriveSlack);
		character.StopMoving();

		float until = Time.time + seconds;
		while (Time.time < until)
		{
			if (lookAt != null)
				CombatFacing.TurnToward(character.transform, lookAt.position);
			else if (facing.HasValue)
				CombatFacing.TurnToward(character.transform, character.transform.position + facing.Value * Vector3.forward);
			yield return null;
		}
	}

	public override void End(Character character) => character.StopMoving();
}
