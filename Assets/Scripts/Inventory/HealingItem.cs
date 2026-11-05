using UnityEngine;

[CreateAssetMenu(fileName = "NewHealingItem", menuName = "Inventory/Healing")]
public class HealingItem : Item
{
	[Header("Healing")]
	[Tooltip("Health restored at the end of the treatment, before the First Aid bonus.")]
	[Min(0f)] public float healAmount = 25f;
	[Tooltip("Real seconds the treatment takes, before the First Aid bonus.")]
	[Min(0f)] public float useSeconds = 4f;
	[Tooltip("Text shown above the character while treating.")]
	public string progressLabel = "Binding wounds...";

	public override bool TryUse(Character user, Inventory.Entry entry, out string reason)
	{
		return user.TryTreatWounds(entry, out reason);
	}

	// Called when the asset is created or reset in the editor.
	private void Reset() => maxStack = 10;
}
