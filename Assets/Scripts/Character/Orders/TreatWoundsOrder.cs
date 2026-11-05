using System.Collections;
using UnityEngine;

/// <summary>
/// Bind a wound with a healing item. The heal and the item cost come at the end, so an
/// interruption (any attack) costs only time.
/// </summary>
public sealed class TreatWoundsOrder : CharacterOrder
{
	private readonly Inventory.Entry entry;
	private readonly HealingItem item;

	public TreatWoundsOrder(Inventory.Entry entry, HealingItem item)
	{
		this.entry = entry;
		this.item = item;
	}

	public override bool StopsWhenAttacked => true;

	public override IEnumerator Run(Character character)
	{
		CharacterStats stats = character.Stats;
		Inventory inventory = character.CarriedInventory;
		HealingConfig config = stats.HealingConfig;
		SkillSet skills = character.Skills;
		float bonus = skills != null ? skills.GetBonus(SkillType.FirstAid) : 1f;

		character.StopMoving();
		character.ShowActionLabel(item.progressLabel);
		yield return new WaitForSeconds(HealingRules.TreatmentSeconds(item.useSeconds, bonus, config.firstAidSpeedScale, config.minTreatmentSeconds));
		character.ClearActionLabel();

		// The stack may have been dragged away or looted while we worked: then nothing is healed or spent.
		if (inventory.Contains(entry) && stats.IsAlive)
		{
			float gained = stats.Heal(HealingRules.TreatmentHeal(item.healAmount, bonus, config.firstAidHealScale));
			inventory.RemoveOne(entry);
			skills?.GainXP(SkillType.FirstAid, HealingRules.TreatmentXP(gained, config.firstAidXPPerHealthPoint));
			character.ShowFloatingText($"+{gained:F0} HP", 2f);
			Debug.Log($"{character.characterName} used {item.itemName}: +{gained:F0} HP");
		}
	}

	public override void End(Character character)
	{
		character.ClearActionLabel();
	}
}
