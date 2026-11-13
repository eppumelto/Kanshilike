using System.Collections;
using UnityEngine;

/// <summary>Stroll between random points in a circle, pausing at each, until replaced.</summary>
public sealed class WanderOrder : CharacterOrder
{
	private const float ArriveSlack = 0.5f;
	private const float SampleDistance = 3f;

	private readonly Vector3 center;
	private readonly float radius;
	private readonly Vector2 pauseSeconds;

	public WanderOrder(Vector3 center, float radius, Vector2 pauseSeconds)
	{
		this.center = center;
		this.radius = Mathf.Max(0f, radius);
		this.pauseSeconds = pauseSeconds;
	}

	public Vector3 Center => center;
	public float Radius => radius;

	public override IEnumerator Run(Character character)
	{
		while (true)
		{
			Vector2 offset = Random.insideUnitCircle * radius;
			Vector3 target = center + new Vector3(offset.x, 0f, offset.y);

			if (character.Agent != null && !OrderMovement.TrySampleNavMesh(target, SampleDistance, out target))
			{
				yield return new WaitForSeconds(1f);
				continue;
			}

			yield return OrderMovement.WalkTo(character, target, ArriveSlack);
			character.StopMoving();

			float pause = Random.Range(Mathf.Min(pauseSeconds.x, pauseSeconds.y), Mathf.Max(pauseSeconds.x, pauseSeconds.y));
			yield return new WaitForSeconds(Mathf.Max(0.1f, pause));
		}
	}

	public override void End(Character character) => character.StopMoving();
}
