using UnityEngine;

[CreateAssetMenu(fileName = "HealingConfig", menuName = "Survival/Healing Config")]
public class HealingConfig : ScriptableObject
{
	[Header("Regeneration (HP per in-game hour)")]
	[Tooltip("Health regained per hour while awake and not starving or dehydrated.")]
	[Min(0f)] public float naturalRegenPerHour = 3f;
	[Tooltip("Health regained per hour while sleeping. Replaces the natural rate.")]
	[Min(0f)] public float sleepRegenPerHour = 8f;

	[Header("First Aid")]
	[Tooltip("How strongly the First Aid bonus raises the heal of a dressing. 0 turns the effect off.")]
	[Min(0f)] public float firstAidHealScale = 1f;
	[Tooltip("How strongly the First Aid bonus shortens the treatment. 0 turns the effect off.")]
	[Min(0f)] public float firstAidSpeedScale = 1f;
	[Tooltip("Treatment never takes less than this many real seconds.")]
	[Min(0f)] public float minTreatmentSeconds = 1.5f;
	[Tooltip("First Aid XP per health point actually restored.")]
	[Min(0f)] public float firstAidXPPerHealthPoint = 2f;
}
