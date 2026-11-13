using System.Collections;
using UnityEngine;

/// <summary>
/// Bind a patient's wounds (the healer's own, or a squadmate's) with a healing item. The healer walks
/// to the patient first. The heal and the item cost come at the end, so an interruption (any attack
/// on the healer, the patient walking off or dying) costs only time.
/// </summary>
public sealed class TreatWoundsOrder : CharacterOrder
{
	private const float RepathSeconds = 0.25f;

	private readonly Character patient;
	private readonly Inventory.Entry entry;
	private readonly HealingItem item;

	public TreatWoundsOrder(Character patient, Inventory.Entry entry, HealingItem item)
	{
		this.patient = patient;
		this.entry = entry;
		this.item = item;
	}

	public Character Patient => patient;

	public override bool StopsWhenAttacked => true;

	public override IEnumerator Run(Character character)
	{
		CharacterStats stats = character.Stats;
		Inventory inventory = character.CarriedInventory;
		HealingConfig config = stats.HealingConfig;
		bool self = patient == character;
		float reach = config.treatmentReach;

		// Walk over to a squadmate, following them if they move.
		if (!self)
		{
			float nextRepath = 0f;
			while (Distance(character) > reach)
			{
				if (!PatientAlive())
					yield break;
				if (Time.time >= nextRepath)
				{
					character.MoveTo(patient.transform.position);
					nextRepath = Time.time + RepathSeconds;
				}
				yield return null;
			}
		}

		character.StopMoving();
		character.ShowActionLabel(item.progressLabel);

		float bonus = character.FirstAidBonus;
		float seconds = HealingRules.TreatmentSeconds(item.useSeconds, bonus, config.firstAidSpeedScale, config.minTreatmentSeconds);
		for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
		{
			if (!PatientAlive())
				yield break;
			if (!self && Distance(character) > reach * 1.5f)
			{
				character.ShowFloatingText("Patient moved away", 2f);
				yield break;
			}
			yield return null;
		}

		character.ClearActionLabel();

		// The stack may have been dragged away or looted while we worked, or someone else healed the
		// patient fully: then nothing is healed or spent.
		CharacterStats patientStats = patient.Stats;
		if (!inventory.Contains(entry) || !stats.IsAlive || !PatientAlive()
			|| !HealingRules.CanTreat(patientStats.currentHealth, patientStats.maxHealth))
			yield break;

		float gained = patientStats.Heal(HealingRules.TreatmentHeal(item.healAmount, bonus, config.firstAidHealScale));
		inventory.RemoveOne(entry);
		character.Skills?.GainXP(SkillType.FirstAid, HealingRules.TreatmentXP(gained, config.firstAidXPPerHealthPoint));
		patient.ShowFloatingText($"+{gained:F0} HP", 2f);
		Debug.Log(self
			? $"{character.characterName} used {item.itemName}: +{gained:F0} HP"
			: $"{character.characterName} treated {patient.characterName} with {item.itemName}: +{gained:F0} HP");
	}

	public override void End(Character character)
	{
		character.ClearActionLabel();
	}

	private bool PatientAlive() => patient != null && patient.IsAlive;

	private float Distance(Character character) =>
		Vector3.Distance(character.transform.position, patient.transform.position);
}
