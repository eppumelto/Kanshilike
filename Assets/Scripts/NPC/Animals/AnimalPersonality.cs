using UnityEngine;

/// <summary>
/// Reusable personality preset for animals. Create via
/// Assets > Create > Animals > Personality.
/// Assign the same asset to multiple animal prefabs to share a personality,
/// or create unique assets per individual.
/// </summary>
[CreateAssetMenu(menuName = "Animals/Personality", fileName = "NewAnimalPersonality")]
public class AnimalPersonality : ScriptableObject
{
    [Header("Disposition")]
    [Tooltip("Brave animals attack when they spot the player. Shy animals flee.")]
    public bool isBrave = false;

    [Header("Detection")]
    [Tooltip("Sphere radius used as a fast pre-filter before the line-of-sight check.")]
    [Min(0f)] public float detectionRange = 15f;

    [Tooltip("Half-angle of the forward vision cone in degrees (e.g. 60 = 120° total FOV).")]
    [Range(0f, 180f)] public float fieldOfViewAngle = 60f;

    [Tooltip("Height offset from the transform origin used as the eye position for raycasts.")]
    [Min(0f)] public float eyeHeight = 1.0f;

    [Tooltip("Layer mask the detection raycast should hit. Typically 'Default' obstacles plus the player layer.")]
    public LayerMask detectionMask = ~0; // everything by default

    [Header("Movement Speeds")]
    [Tooltip("Speed while roaming casually.")]
    [Min(0f)] public float roamSpeed = 2f;

    [Tooltip("Speed when fleeing or chasing.")]
    [Min(0f)] public float reactionSpeed = 5f;

    [Tooltip("NavMeshAgent acceleration used for all states.")]
    [Min(0f)] public float acceleration = 8f;

    [Header("Roaming")]
    [Tooltip("Maximum distance from spawn origin a random roam point can be chosen.")]
    [Min(0f)] public float roamRadius = 20f;

    [Tooltip("Minimum seconds the animal idles before picking a new roam destination.")]
    [Min(0f)] public float idleTimeMin = 3f;

    [Tooltip("Maximum seconds the animal idles before picking a new roam destination.")]
    [Min(0f)] public float idleTimeMax = 8f;

    [Header("Fleeing")]
    [Tooltip("How far the animal tries to run in the opposite direction of the threat per flee destination update.")]
    [Min(0f)] public float fleeDistance = 25f;

    [Tooltip("Minimum distance to threat before the animal considers itself safe while fleeing.")]
    [Min(0f)] public float fleeSafeDistance = 30f;

    [Tooltip("Seconds the animal can flee at full speed before tiring. 0 = never tires.")]
    [Min(0f)] public float fleeSprintSeconds = 0f;

    [Tooltip("Flee speed once tired. Set it below a character's walking speed to let them catch up.")]
    [Min(0f)] public float tiredFleeSpeed = 2.5f;

    [Tooltip("Seconds of not fleeing it takes to fully recover from tiring.")]
    [Min(0f)] public float staminaRecoverySeconds = 30f;

    [Header("Chasing")]
    [Tooltip("The animal gives up the chase when the player exceeds this distance.")]
    [Min(0f)] public float chaseGiveUpDistance = 30f;

    [Header("Combat Profile")]
    [Tooltip("Animals generally cannot block. Keep false unless you intentionally want this behavior.")]
    public bool canBlock = false;

    [Tooltip("Animals generally cannot dodge. Keep false unless you intentionally want this behavior.")]
    public bool canDodge = false;

    [Tooltip("Whether this animal can counterattack if skill checks allow it.")]
    public bool canCounter = true;
}
