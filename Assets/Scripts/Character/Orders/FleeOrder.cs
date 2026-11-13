using System.Collections;
using UnityEngine;

/// <summary>
/// Run away from a threat. Self-defence never replaces it. Ends on arrival, when the threat is gone
/// or far enough, or at once when there is nowhere to run (cornered), letting self-defence take over.
/// </summary>
public sealed class FleeOrder : CharacterOrder
{
	private const float ArriveSlack = 0.75f;
	private const float SampleDistance = 4f;
	private static readonly float[] Angles = { 0f, 45f, -45f, 90f, -90f };

	private readonly Transform threat;
	private readonly float fleeDistance;

	public FleeOrder(Transform threat, float fleeDistance)
	{
		this.threat = threat;
		this.fleeDistance = Mathf.Max(1f, fleeDistance);
	}

	public Transform Threat => threat;

	public override bool YieldsToSelfDefence => false;

	public override IEnumerator Run(Character character)
	{
		if (!ThreatActive(character))
			yield break;

		if (!TryFindFleePoint(character, out Vector3 point))
			yield break; // cornered

		character.MoveTo(point);
		yield return null;
		while (ThreatActive(character) && !OrderMovement.HasArrived(character, point, ArriveSlack))
			yield return null;
	}

	public override void End(Character character) => character.StopMoving();

	private bool ThreatActive(Character character)
	{
		if (threat == null)
			return false;
		CombatActor actor = threat.GetComponentInParent<CombatActor>();
		if (actor != null && !actor.IsAlive)
			return false;
		return Vector3.Distance(character.transform.position, threat.position) < fleeDistance;
	}

	private bool TryFindFleePoint(Character character, out Vector3 point)
	{
		Vector3 away = character.transform.position - threat.position;
		away.y = 0f;
		if (away.sqrMagnitude < 0.0001f)
			away = -character.transform.forward;
		away.Normalize();

		foreach (float angle in Angles)
		{
			Vector3 candidate = character.transform.position + Quaternion.Euler(0f, angle, 0f) * away * fleeDistance;
			if (character.Agent == null)
			{
				point = candidate;
				return true;
			}
			if (OrderMovement.TrySampleNavMesh(candidate, SampleDistance, out point))
				return true;
		}

		point = character.transform.position;
		return false;
	}
}
