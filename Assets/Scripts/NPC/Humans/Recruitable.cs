using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets the squad recruit this person (adds Recruit to their right-click menu). Works on anyone,
/// whatever their faction. Joining makes them a squad member: Player faction, no brain, needs on.
/// </summary>
[RequireComponent(typeof(Character))]
public class Recruitable : MonoBehaviour, INpcInteractionSource
{
	public const string RecruitId = "recruit";

	[Tooltip("What the recruiter must hand over. Empty: free. There is no money: costs are goods.")]
	[SerializeField] private Item costItem;
	[SerializeField, Min(0)] private int costCount;
	[Tooltip("Hunger and thirst start once they join (townsfolk have them off).")]
	[SerializeField] private bool enableNeedsOnJoin = true;
	[Tooltip("Said on joining. Empty: a plain line.")]
	[TextArea(1, 2)] [SerializeField] private string[] acceptLines = new string[0];
	[Tooltip("Said when refusing. Empty: the reason itself.")]
	[TextArea(1, 2)] [SerializeField] private string[] refuseLines = new string[0];

	private Character character;
	private NpcBrain brain;

	public Item CostItem => costItem;
	public int CostCount => costCount;

	private void Awake()
	{
		character = GetComponent<Character>();
		brain = GetComponent<NpcBrain>();
	}

	public void AddOptions(Character interactor, AttitudeResult attitude, List<InteractionOption> into)
	{
		into.Add(new InteractionOption(RecruitId, "Recruit", InteractionKind.Task, primaryOnly: true));
	}

	public bool TryExecute(Character interactor, string actionId)
	{
		if (actionId != RecruitId)
			return false;
		TryRecruit(interactor, out _);
		return true;
	}

	/// <summary>Recruits them if every check passes; otherwise they say why not.</summary>
	public bool TryRecruit(Character recruiter, out string reason)
	{
		RecruitBlock block = Check(recruiter);
		if (block != RecruitBlock.None)
		{
			reason = RecruitRules.Reason(block);
			character.ShowFloatingText(Line(refuseLines, reason), 2.5f);
			return false;
		}

		reason = null;
		if (costItem != null)
			for (int i = 0; i < costCount; i++)
				recruiter.CarriedInventory.RemoveOneOf(costItem);
		Join();
		return true;
	}

	public RecruitBlock Check(Character recruiter)
	{
		if (recruiter == null)
			return RecruitBlock.TooFar;

		bool inCombat = brain != null ? brain.InCombat : character.CurrentOrder is AttackOrder;
		bool inReach = InventoryActions.InReach(recruiter, character);
		bool unwilling = Attitude.RefusesDealings(character, recruiter) || Attitude.RefusesSquad(character);
		return RecruitRules.Check(character.IsAlive, Attitude.Of(character, recruiter), inCombat, inReach,
			Carried(recruiter), costItem != null ? costCount : 0, unwilling);
	}

	private int Carried(Character recruiter)
	{
		Inventory inventory = recruiter.CarriedInventory;
		if (costItem == null || inventory == null)
			return 0;
		int total = 0;
		foreach (Inventory.Entry e in inventory.Entries)
			if (e.item == costItem)
				total += e.count;
		return total;
	}

	private void Join()
	{
		character.SetOwnFaction(FactionRelations.Player);
		if (brain != null)
			brain.enabled = false; // its OnDisable ends its own order
		if (enableNeedsOnJoin && character.Stats != null)
			character.Stats.hasNeeds = true;
		if (GetComponent<PlayerControllable>() == null)
			gameObject.AddComponent<PlayerControllable>();

		character.ShowFloatingText(Line(acceptLines, "I'll walk with you."), 3f);

		// Disabled components are still found by GetComponentInParent<IInteractable>, so remove them.
		foreach (INpcInteractionSource source in GetComponents<INpcInteractionSource>())
			if (!ReferenceEquals(source, this) && source is Component c)
				Destroy(c);
		NpcInteractable interactable = GetComponent<NpcInteractable>();
		if (interactable != null)
			Destroy(interactable);
		Destroy(this);
	}

	private static string Line(string[] lines, string fallback)
	{
		if (lines == null || lines.Length == 0)
			return fallback;
		string line = lines[Random.Range(0, lines.Length)];
		return string.IsNullOrWhiteSpace(line) ? fallback : line;
	}
}
