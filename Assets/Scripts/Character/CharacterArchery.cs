using UnityEngine;
using UnityEngine.AI;

/// <summary>Aiming and shooting for characters with a bow. Used by AttackOrder and Character.StrikeAt.</summary>
public static class CharacterArchery
{
	private const float MaxShootingAngle = 15f;
	private const float ArrowSpawnForward = 0.6f;
	private const float ArrowSpawnUp = 0.3f;
	private const float DefaultBodyRadius = 0.5f;

	// Shared by all archers: only used within a single check.
	private static readonly RaycastHit[] ClearShotHits = new RaycastHit[16];

	/// <summary>Worth an arrow? Otherwise the archer holds fire and moves closer.</summary>
	public static bool HasGoodShot(Character archer, Component target, BowItem bow, float distance)
	{
		if (!HasClearShot(archer, target))
			return false;

		float chance = CombatFormulaResolver.GetBowHitChance(archer, bow, archer.CombatConfig, distance, VelocityOf(target).magnitude, BodyRadius(target));
		return chance >= bow.minHitChance;
	}

	public static bool IsFacing(Transform self, Vector3 point)
	{
		Vector3 toPoint = point - self.position;
		toPoint.y = 0f;
		return Vector3.Angle(self.forward, toPoint) <= MaxShootingAngle;
	}

	/// <summary>Spends one arrow and sends it toward where the target will be, strayed by the archer's spread.</summary>
	public static void Shoot(Character archer, Component target, BowItem bow, AttackProfile attackProfile)
	{
		Inventory inventory = archer.CarriedInventory;
		Inventory.Entry arrows = inventory != null ? inventory.FindFirst<ArrowItem>() : null;
		if (arrows == null)
			return;

		ArrowProjectile prefab = ((ArrowItem)arrows.item).projectilePrefab;
		if (prefab == null)
		{
			Debug.LogWarning($"[Combat] {arrows.item.itemName} has no projectile prefab assigned.");
			return;
		}

		inventory.RemoveOne(arrows);

		Transform self = archer.transform;
		Vector3 targetVelocity = VelocityOf(target);
		Vector3 origin = BodyCenter(archer) + self.forward * ArrowSpawnForward + Vector3.up * ArrowSpawnUp;
		Vector3 aimPoint = BodyCenter(target);
		float flightSeconds = Vector3.Distance(origin, aimPoint) / bow.arrowSpeed;
		aimPoint += targetVelocity * flightSeconds;

		// Lobbed just enough that gravity brings it down on the aim point.
		Vector3 velocity = (aimPoint - origin) / flightSeconds - 0.5f * flightSeconds * Physics.gravity;

		float spread = CombatFormulaResolver.GetBowSpread(archer, bow, archer.CombatConfig, targetVelocity.magnitude);
		Vector2 stray = Random.insideUnitCircle * spread;
		velocity = Quaternion.AngleAxis(stray.x, Vector3.up) * Quaternion.AngleAxis(stray.y, self.right) * velocity;

		ArrowProjectile projectile = Object.Instantiate(prefab, origin, Quaternion.LookRotation(velocity));
		projectile.Launch(velocity, attackProfile, archer.CombatConfig, archer.gameObject,
			(defender, result) => archer.HandleArrowHit(defender, result, attackProfile.UsedWeaponSkill));

		archer.Skills?.GainXP(attackProfile.UsedWeaponSkill, archer.CombatConfig.arrowShotXP);
	}

	private static Vector3 VelocityOf(Component target)
	{
		return target.TryGetComponent<NavMeshAgent>(out var targetAgent) ? targetAgent.velocity : Vector3.zero;
	}

	private static float BodyRadius(Component body)
	{
		Collider bodyCollider = body.GetComponent<Collider>();
		if (bodyCollider == null)
			return DefaultBodyRadius;

		Vector3 extents = bodyCollider.bounds.extents;
		return Mathf.Max(extents.x, extents.z);
	}

	// Only the world blocks a shot. Characters in the way don't stop the archer firing, and may catch the arrow.
	private static bool HasClearShot(Component archer, Component target)
	{
		Vector3 from = BodyCenter(archer);
		Vector3 toTarget = BodyCenter(target) - from;
		int count = Physics.RaycastNonAlloc(from, toTarget.normalized, ClearShotHits, toTarget.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < count; i++)
		{
			if (ClearShotHits[i].collider.GetComponentInParent<CharacterStats>() == null)
				return false;
		}

		return true;
	}

	private static Vector3 BodyCenter(Component body)
	{
		Collider bodyCollider = body.GetComponent<Collider>();
		return bodyCollider != null ? bodyCollider.bounds.center : body.transform.position;
	}
}
