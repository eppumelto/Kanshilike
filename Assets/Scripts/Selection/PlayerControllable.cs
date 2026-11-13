using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a Character part of the player's squad: it can be selected and given orders. A Character
/// without this component (a bandit, a trader) still fights and interacts, but the player can't command it.
/// </summary>
[RequireComponent(typeof(Character))]
public class PlayerControllable : MonoBehaviour
{
	private static readonly List<PlayerControllable> all = new List<PlayerControllable>();

	/// <summary>The whole squad, dead members included.</summary>
	public static IReadOnlyList<PlayerControllable> All => all;

	/// <summary>Fires when someone joins the squad, including members spawned or recruited during play.</summary>
	public static event System.Action<PlayerControllable> Joined;

	private SelectionRing ring;
	private CharacterStats stats;

	public Character Character { get; private set; }
	public bool IsSelected { get; private set; }
	public bool IsAlive => Character.IsAlive;

	/// <summary>Ground ring shown when selected (and while a drag-box is over the character).</summary>
	public SelectionRing Ring
	{
		get
		{
			if (ring == null)
			{
				ring = GetComponent<SelectionRing>();
				if (ring == null)
					ring = gameObject.AddComponent<SelectionRing>();
			}
			return ring;
		}
	}

	private void Awake()
	{
		Character = GetComponent<Character>();
		stats = GetComponent<CharacterStats>();
	}

	private void OnEnable()
	{
		if (stats != null)
			stats.OnDied += HandleDied;

		all.Add(this);
		if (Character != null)
			Character.InvalidateFaction(); // squad members count as the player's faction
		Joined?.Invoke(this);
	}

	private void OnDisable()
	{
		if (stats != null)
			stats.OnDied -= HandleDied;

		all.Remove(this);
		if (Character != null)
			Character.InvalidateFaction();
	}

	public void SetSelected(bool selected)
	{
		IsSelected = selected;
		Ring.SetSelected(selected);
	}

	// SelectionManager drops the dead from its list on its next update.
	private void HandleDied(GameObject killer)
	{
		if (IsSelected)
			SetSelected(false);
		Ring.SetPreview(false);
	}
}
