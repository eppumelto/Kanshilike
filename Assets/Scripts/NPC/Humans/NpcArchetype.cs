using UnityEngine;

/// <summary>
/// Tuning shared by a kind of NPC (townsperson, guard...). No faction: that comes from the
/// Character's own field, or later from its community.
/// </summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Archetype", fileName = "NewArchetype")]
public class NpcArchetype : ScriptableObject
{
	[Header("Routine")]
	[Tooltip("The daily routine.")]
	public NpcSchedule schedule;
	[Tooltip("Seconds between decisions.")]
	[Min(0.02f)] public float thinkIntervalSeconds = 0.5f;
	[Tooltip("How far to look for an anchor with the schedule's tag when none is assigned.")]
	[Min(0f)] public float anchorSearchRadius = 60f;
	[Tooltip("Wander radius around home, when no anchor gives one.")]
	[Min(0f)] public float wanderRadius = 8f;
	[Tooltip("Seconds to pause between wander walks (min, max).")]
	public Vector2 wanderPauseSeconds = new Vector2(2f, 6f);
	[Tooltip("Seconds to pause at each patrol waypoint.")]
	[Min(0f)] public float patrolPauseSeconds = 3f;

	[Header("Danger")]
	[Tooltip("Runs from an attacker below this share of health. 0 = never flees.")]
	[Range(0f, 1f)] public float fleeHealthFraction = 0.35f;
	[Tooltip("Seconds an attacker is remembered as a threat.")]
	[Min(0f)] public float threatMemorySeconds = 10f;
	[Tooltip("How far to run from a threat.")]
	[Min(1f)] public float fleeDistance = 15f;
	[Tooltip("Placeholder, unused until bandits.")]
	[Range(0f, 1f)] public float aggression;
	[Tooltip("How hard this person takes the death of someone close (0-1). Scales grudge intensity.")]
	[Range(0f, 1f)] public float vengefulness = 0.6f;

	[Header("Talk")]
	[Tooltip("Lines this NPC may say when talked to.")]
	[TextArea(1, 3)] public string[] barkLines = new string[0];
	[Tooltip("Seconds a line stays above the NPC.")]
	[Min(0.5f)] public float barkSeconds = 3f;
	[Tooltip("Seconds the NPC stands facing the speaker after talking.")]
	[Min(0f)] public float talkPauseSeconds = 4f;
}
