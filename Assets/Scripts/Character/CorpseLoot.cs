using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a creature leaves behind to loot when it dies (e.g. a wolf's meat and pelt).
/// Creates a small Inventory on the body at death if it doesn't have one, and fills it.
/// </summary>
[RequireComponent(typeof(CharacterStats))]
public class CorpseLoot : MonoBehaviour
{
	[System.Serializable]
	public class Drop
	{
		public Item item;
		[Min(0)] public int min = 1;
		[Min(0)] public int max = 1;
	}

	[SerializeField] private List<Drop> drops = new List<Drop>();
	[Tooltip("Grid size of the corpse's inventory, if it needs one made.")]
	[SerializeField] private Vector2Int lootGridSize = new Vector2Int(4, 3);

	private CharacterStats stats;

	private void Awake()
	{
		stats = GetComponent<CharacterStats>();
		stats.OnDied += HandleDied;
	}

	private void OnDestroy()
	{
		if (stats != null)
			stats.OnDied -= HandleDied;
	}

	private void HandleDied(GameObject killer)
	{
		var inventory = GetComponent<Inventory>();
		if (inventory == null)
		{
			inventory = gameObject.AddComponent<Inventory>();
			inventory.Resize(lootGridSize.x, lootGridSize.y);
		}

		foreach (var drop in drops)
		{
			if (drop.item == null)
				continue;
			int count = Random.Range(drop.min, Mathf.Max(drop.min, drop.max) + 1);
			if (count > 0)
				inventory.Add(drop.item, count);
		}
	}
}
