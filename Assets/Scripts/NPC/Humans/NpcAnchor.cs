using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place NPCs go for a schedule activity: a stall, a bed, the village square. Its tag says what
/// kind of place it is; patrols use its waypoints (or its children when none are set).
/// </summary>
public class NpcAnchor : MonoBehaviour
{
	private static readonly List<NpcAnchor> all = new List<NpcAnchor>();
	public static IReadOnlyList<NpcAnchor> All => all;

	[Tooltip("What kind of place this is. Schedules name the same tag asset.")]
	public NpcAnchorTag anchorTag;
	[Tooltip("Wander area around this anchor.")]
	[Min(0f)] public float radius = 5f;
	[Tooltip("Patrol route, in order. Empty: this object's children, in hierarchy order.")]
	public Transform[] waypoints = new Transform[0];

	private readonly List<Vector3> waypointBuffer = new List<Vector3>();

	public Vector3 Position => transform.position;
	public Quaternion Facing => transform.rotation;

	private void OnEnable() => all.Add(this);
	private void OnDisable() => all.Remove(this);

	public IReadOnlyList<Vector3> GetWaypoints()
	{
		waypointBuffer.Clear();
		if (waypoints != null && waypoints.Length > 0)
		{
			foreach (Transform t in waypoints)
				if (t != null)
					waypointBuffer.Add(t.position);
		}
		else
		{
			foreach (Transform child in transform)
				waypointBuffer.Add(child.position);
		}
		return new List<Vector3>(waypointBuffer);
	}

	/// <summary>The nearest enabled anchor with exactly this tag asset within maxDistance, or null.</summary>
	public static NpcAnchor FindNearest(NpcAnchorTag tag, Vector3 from, float maxDistance)
	{
		if (tag == null)
			return null;

		NpcAnchor best = null;
		float bestSq = maxDistance * maxDistance;
		foreach (NpcAnchor a in all)
		{
			if (a == null || a.anchorTag != tag)
				continue;
			float sq = (a.transform.position - from).sqrMagnitude;
			if (sq <= bestSq)
			{
				bestSq = sq;
				best = a;
			}
		}
		return best;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics() => all.Clear();

	private void OnDrawGizmos()
	{
		Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
		Gizmos.DrawWireSphere(transform.position, radius);
		IReadOnlyList<Vector3> points = GetWaypoints();
		for (int i = 0; i < points.Count; i++)
			Gizmos.DrawLine(points[i], points[(i + 1) % points.Count]);
#if UNITY_EDITOR
		if (anchorTag != null)
			UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, anchorTag.DisplayName);
#endif
	}
}
