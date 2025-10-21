using UnityEngine;

public class InventoryUI : MonoBehaviour
{
	public SelectableCharacter character;
	public GameObject slotPrefab;
	public Transform slotParent;

	private InventorySlotUI[,] slotUIs;

	public void Initialize()
	{
		int w = character.inventory.width;
		int h = character.inventory.height;
		slotUIs = new InventorySlotUI[w, h];

		for (int x = 0; x < w; x++)
		{
			for (int y = 0; y < h; y++)
			{
				GameObject slotGO = Instantiate(slotPrefab, slotParent);
				slotUIs[x, y] = slotGO.GetComponent<InventorySlotUI>();
			}
		}

		RefreshUI();
	}

	public void RefreshUI()
	{
		for (int x = 0; x < character.inventory.width; x++)
		{
			for (int y = 0; y < character.inventory.height; y++)
			{
				slotUIs[x, y].SetItem(character.inventory.slots[x, y]);
			}
		}
	}
}
