using System.Collections;
using UnityEngine;

/// <summary>Stay near a leader until they die or are gone.</summary>
public sealed class FollowOrder : CharacterOrder
{
	private const float RepathSeconds = 0.25f;

	private readonly Character leader;
	private readonly float keepDistance;

	public FollowOrder(Character leader, float keepDistance = 3f)
	{
		this.leader = leader;
		this.keepDistance = Mathf.Max(0.5f, keepDistance);
	}

	public Character Leader => leader;

	public override IEnumerator Run(Character character)
	{
		bool walking = false;
		while (leader != null && leader.IsAlive)
		{
			float distance = Vector3.Distance(character.transform.position, leader.transform.position);
			if (distance > keepDistance + 1f)
			{
				character.MoveTo(leader.transform.position);
				walking = true;
			}
			else if (walking && distance <= keepDistance)
			{
				character.StopMoving();
				walking = false;
			}
			yield return new WaitForSeconds(RepathSeconds);
		}
	}

	public override void End(Character character) => character.StopMoving();
}
