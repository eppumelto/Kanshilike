using System.Collections;
using UnityEngine;

/// <summary>
/// Fight one actor: walk into range, swing or shoot, wait out the attack interval, and repeat until
/// the target dies or disappears, gets too far away, or another order arrives.
/// </summary>
public sealed class AttackOrder : CharacterOrder
{
	private readonly CombatActor target;
	private readonly float maxChaseDistance;

	/// <param name="maxChaseDistance">Give up once the target is farther than this. Player orders chase without limit.</param>
	public AttackOrder(CombatActor target, float maxChaseDistance = float.PositiveInfinity)
	{
		this.target = target;
		this.maxChaseDistance = maxChaseDistance;
	}

	public CombatActor Target => target;

	public override bool YieldsToSelfDefence => false;

	public override IEnumerator Run(Character character)
	{
		CombatFormulaConfig config = character.CombatConfig;
		if (config == null)
		{
			Debug.LogWarning($"[Combat] {character.characterName} has no CombatFormulaConfig assigned.");
			yield break;
		}

		float nextShotTime = 0f;

		while (target != null && target.IsAlive && character.IsAlive)
		{
			character.CombatOpponent = target.transform;

			BowItem bow = CombatFormulaResolver.GetBow(character);
			if (bow != null && (character.CarriedInventory == null || character.CarriedInventory.FindFirst<ArrowItem>() == null))
			{
				character.ShowFloatingText("Out of arrows", 2f);
				yield break;
			}

			AttackProfile profile = CombatFormulaResolver.BuildAttackProfile(character, config);
			float distance = Vector3.Distance(character.transform.position, target.transform.position);
			if (distance > maxChaseDistance)
				yield break;

			if (bow != null && distance > bow.maxPursuitDistance)
			{
				character.ShowFloatingText("Target got away", 2f);
				yield break;
			}

			bool inRange = distance <= profile.Range;
			if (!inRange || (bow != null && !CharacterArchery.HasGoodShot(character, target, bow, distance)))
			{
				character.MoveTo(target.transform.position);
				yield return null;
				continue;
			}

			character.StopMoving();

			// Character.FaceOpponent is turning us; an archer waits until the target is in front.
			if (bow != null && (Time.time < nextShotTime || !CharacterArchery.IsFacing(character.transform, target.transform.position)))
			{
				yield return null;
				continue;
			}

			character.StrikeAt(target, bow, profile);

			// Between shots an archer keeps following: the checks above run again while reloading.
			if (bow != null)
			{
				nextShotTime = Time.time + Mathf.Max(0.1f, profile.Interval);
				yield return null;
				continue;
			}

			yield return new WaitForSeconds(Mathf.Max(0.1f, profile.Interval));
		}
	}

	public override void End(Character character)
	{
		// The fight is over: don't keep walking to where the target last was.
		character.StopMoving();
		character.CombatOpponent = null;
		character.ReleaseAttackSlot();
	}
}
