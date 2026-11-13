using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Walking helpers shared by orders. All cope with a character that has no NavMeshAgent (tests).</summary>
internal static class OrderMovement
{
	/// <summary>
	/// Done walking: no agent (treated as arrived), or no path left to walk, or close enough.
	/// The path test is the same one MoveOrder has always used.
	/// </summary>
	public static bool HasArrived(Character c, Vector3 point, float slack)
	{
		NavMeshAgent agent = c.Agent;
		if (agent == null || !agent.enabled)
			return true;

		Vector3 flat = c.transform.position - point;
		flat.y = 0f;
		if (flat.magnitude <= agent.stoppingDistance + slack)
			return true;

		return !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + slack);
	}

	/// <summary>The path test alone, for MoveOrder (which walks as far as the NavMesh allows).</summary>
	public static bool PathDone(Character c, float slack)
	{
		NavMeshAgent agent = c.Agent;
		if (agent == null || !agent.enabled)
			return true;
		return !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + slack);
	}

	public static IEnumerator WalkTo(Character c, Vector3 point, float slack)
	{
		c.MoveTo(point);
		yield return null; // let the agent pick up the path
		while (!HasArrived(c, point, slack))
			yield return null;
	}

	public static bool TrySampleNavMesh(Vector3 point, float maxDistance, out Vector3 result)
	{
		if (NavMesh.SamplePosition(point, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
		{
			result = hit.position;
			return true;
		}
		result = point;
		return false;
	}
}
