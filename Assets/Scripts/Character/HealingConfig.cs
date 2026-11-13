using UnityEngine;

/// <summary>First Aid tuning. Health only comes back through treatment: there is no natural regeneration.</summary>
[CreateAssetMenu(fileName = "HealingConfig", menuName = "Survival/Healing Config")]
public class HealingConfig : ScriptableObject
{
	[Header("First Aid")]
	[Tooltip("How strongly the First Aid bonus raises the heal of a dressing. 0 turns the effect off.")]
	[Min(0f)] public float firstAidHealScale = 1f;
	[Tooltip("How strongly the First Aid bonus shortens the treatment. 0 turns the effect off.")]
	[Min(0f)] public float firstAidSpeedScale = 1f;
	[Tooltip("Treatment never takes less than this many real seconds.")]
	[Min(0f)] public float minTreatmentSeconds = 1.5f;
	[Tooltip("First Aid XP per health point actually restored.")]
	[Min(0f)] public float firstAidXPPerHealthPoint = 2f;

	[Header("Treatment Limits")]
	[Tooltip("A dressing may only be used when the patient is missing at least this share of what it would heal (0.5 = half). 0 allows any wound.")]
	[Range(0f, 1f)] public float minMissingShareOfHeal = 0.5f;
	[Tooltip("Metres between healer and patient while treating. Moving further apart stops the treatment.")]
	[Min(0.5f)] public float treatmentReach = 2f;
}
