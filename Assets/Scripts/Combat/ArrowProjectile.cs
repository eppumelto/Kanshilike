using UnityEngine;

/// <summary>
/// A flying arrow. It hits the first thing in its path, so a stray shot can strike someone other
/// than the target, allies included. Sweeps a small sphere each frame instead of using physics
/// bodies, which keeps big fights cheap and stops fast arrows passing through targets.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
	private const float HitRadius = 0.05f;
	private const float MaxFlightSeconds = 6f;
	private const float StuckSeconds = 10f;

	// Shared by all arrows: only used within a single sweep.
	private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

	private AttackProfile attack;
	private CombatFormulaConfig config;
	private GameObject shooter;
	private System.Action<ICombatActor, CombatExchangeResult> onHit;
	private Vector3 velocity;
	private float flightEndTime;
	private bool stuck;

	/// <summary>Sends the arrow off. onHit runs when it strikes a living character.</summary>
	public void Launch(
		Vector3 startVelocity,
		AttackProfile attackProfile,
		CombatFormulaConfig formulaConfig,
		GameObject shooterObject,
		System.Action<ICombatActor, CombatExchangeResult> onHitResolved)
	{
		velocity = startVelocity;
		attack = attackProfile;
		config = formulaConfig;
		shooter = shooterObject;
		onHit = onHitResolved;
		flightEndTime = Time.time + MaxFlightSeconds;
		PointAlongFlight();
	}

	private void Update()
	{
		if (stuck)
			return;

		if (Time.time >= flightEndTime)
		{
			Destroy(gameObject);
			return;
		}

		float deltaTime = Time.deltaTime;
		Vector3 start = transform.position;
		// The exact arc for this frame, so the frame rate doesn't change where the arrow lands.
		Vector3 step = velocity * deltaTime + 0.5f * deltaTime * deltaTime * Physics.gravity;
		velocity += Physics.gravity * deltaTime;

		if (TryFindHit(start, step, out RaycastHit hit))
		{
			Land(hit);
			return;
		}

		transform.position = start + step;
		PointAlongFlight();
	}

	private bool TryFindHit(Vector3 start, Vector3 step, out RaycastHit nearest)
	{
		nearest = default;
		float length = step.magnitude;
		if (length <= 0f)
			return false;

		int count = Physics.SphereCastNonAlloc(start, HitRadius, step / length, HitBuffer, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

		bool found = false;
		for (int i = 0; i < count; i++)
		{
			RaycastHit candidate = HitBuffer[i];
			if (found && candidate.distance >= nearest.distance)
				continue;
			if (!CanHit(candidate.collider))
				continue;

			nearest = candidate;
			found = true;
		}

		return found;
	}

	private bool CanHit(Collider other)
	{
		if (shooter != null && other.transform.IsChildOf(shooter.transform))
			return false;

		// Arrows pass over the dead.
		CharacterStats stats = other.GetComponentInParent<CharacterStats>();
		return stats == null || stats.IsAlive;
	}

	private void Land(RaycastHit hit)
	{
		CharacterStats stats = hit.collider.GetComponentInParent<CharacterStats>();
		if (stats == null)
		{
			StickInto(hit);
			return;
		}

		ICombatActor defender = CombatExchangeResolver.ResolveDefender(stats, stats);
		Vector3 cameFrom = transform.position - velocity;
		CombatExchangeResult result = CombatExchangeResolver.ResolveProjectileHit(attack, defender, stats, config, shooter, cameFrom);
		onHit?.Invoke(defender, result);
		Destroy(gameObject);
	}

	// A sphere sweep that starts overlapping something reports a zero point: stay where we are.
	private void StickInto(RaycastHit hit)
	{
		stuck = true;
		if (hit.distance > 0f)
			transform.position = hit.point;
		Destroy(gameObject, StuckSeconds);
	}

	private void PointAlongFlight()
	{
		if (velocity.sqrMagnitude > 0.0001f)
			transform.rotation = Quaternion.LookRotation(velocity);
	}
}
