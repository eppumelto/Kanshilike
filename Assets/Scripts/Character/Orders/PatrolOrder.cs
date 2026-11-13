using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Walk a loop of waypoints, pausing at each, starting from the nearest. Needs at least two.</summary>
public sealed class PatrolOrder : CharacterOrder
{
	private const float ArriveSlack = 0.5f;

	private readonly List<Vector3> waypoints;
	private readonly float pauseSeconds;

	public PatrolOrder(IReadOnlyList<Vector3> waypoints, float pauseSeconds)
	{
		this.waypoints = waypoints != null ? new List<Vector3>(waypoints) : new List<Vector3>();
		this.pauseSeconds = Mathf.Max(0f, pauseSeconds);
	}

	public IReadOnlyList<Vector3> Waypoints => waypoints;

	public override IEnumerator Run(Character character)
	{
		if (waypoints.Count < 2)
			yield break;

		int index = 0;
		float nearest = float.MaxValue;
		for (int i = 0; i < waypoints.Count; i++)
		{
			float d = (waypoints[i] - character.transform.position).sqrMagnitude;
			if (d < nearest)
			{
				nearest = d;
				index = i;
			}
		}

		while (true)
		{
			yield return OrderMovement.WalkTo(character, waypoints[index], ArriveSlack);
			character.StopMoving();
			yield return new WaitForSeconds(Mathf.Max(0.1f, pauseSeconds));
			index = (index + 1) % waypoints.Count;
		}
	}

	public override void End(Character character) => character.StopMoving();
}
