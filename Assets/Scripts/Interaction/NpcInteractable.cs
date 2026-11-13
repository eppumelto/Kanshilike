using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Right-click options on a living person: Talk, then whatever INpcInteractionSource components add
/// (Recruit...), then Attack. Someone hostile by faction only offers Attack. Animals keep
/// CombatEntityInteractable.
/// </summary>
[RequireComponent(typeof(Character))]
public class NpcInteractable : MonoBehaviour, IInteractable
{
	public const string TalkId = "talk";
	public const string AttackId = "attack";

	private const float DefaultBarkSeconds = 3f;
	private static readonly List<NpcTalkLine> lineBuffer = new List<NpcTalkLine>();
	private static readonly List<NpcTalkLine> bestBuffer = new List<NpcTalkLine>();

	private Character character;
	private NpcBrain brain;

	private void Awake()
	{
		character = GetComponent<Character>();
		brain = GetComponent<NpcBrain>();
	}

	public string GetName() =>
		character != null && !string.IsNullOrEmpty(character.characterName) ? character.characterName : gameObject.name;

	public List<InteractionOption> GetAvailableInteractions(Character interactor)
	{
		var options = new List<InteractionOption>();
		if (character == null || !character.IsAlive)
			return options;

		AttitudeResult attitude = Attitude.Explain(character, interactor);
		bool enemyByFaction = attitude.Relation == FactionRelation.Hostile && attitude.Reason == AttitudeReason.Faction;
		if (!enemyByFaction)
		{
			options.Add(new InteractionOption(TalkId, "Talk", InteractionKind.Task, primaryOnly: true));
			foreach (INpcInteractionSource source in GetComponents<INpcInteractionSource>())
				source.AddOptions(interactor, attitude, options);
		}
		options.Add(new InteractionOption(AttackId, "Attack", InteractionKind.Attack));
		return options;
	}

	public void ExecuteInteraction(Character interactor, string actionId)
	{
		if (interactor == null || character == null)
			return;

		switch (actionId)
		{
			case TalkId:
				Talk(interactor);
				return;
			case AttackId:
				interactor.AttackTarget(character);
				return;
		}

		foreach (INpcInteractionSource source in GetComponents<INpcInteractionSource>())
			if (source.TryExecute(interactor, actionId))
				return;
	}

	public Vector3 GetInteractionPoint() => transform.position;

	private void Talk(Character interactor)
	{
		NpcArchetype archetype = brain != null ? brain.Archetype : null;

		lineBuffer.Clear();
		if (archetype != null && archetype.barkLines != null)
			foreach (string line in archetype.barkLines)
				if (!string.IsNullOrWhiteSpace(line))
					lineBuffer.Add(new NpcTalkLine(line, 0, true));
		foreach (INpcTalkSource source in GetComponents<INpcTalkSource>())
			source.CollectLines(interactor, lineBuffer);

		NpcTalkLine chosen = Pick(lineBuffer);
		character.ShowFloatingText(chosen.Text, archetype != null ? archetype.barkSeconds : DefaultBarkSeconds);

		AttitudeResult attitude = Attitude.Explain(character, interactor);
		if (chosen.Hold && brain != null && archetype != null
			&& attitude.Relation != FactionRelation.Hostile && !attitude.RefusesDealings)
			brain.HoldFor(archetype.talkPauseSeconds, interactor.transform);
	}

	/// <summary>A random line among the highest priority; "..." (holding) when there are none.</summary>
	public static NpcTalkLine Pick(List<NpcTalkLine> lines)
	{
		if (lines == null || lines.Count == 0)
			return new NpcTalkLine("...", 0, true);

		int best = int.MinValue;
		foreach (NpcTalkLine l in lines)
			if (l.Priority > best)
				best = l.Priority;

		bestBuffer.Clear();
		foreach (NpcTalkLine l in lines)
			if (l.Priority == best)
				bestBuffer.Add(l);
		return bestBuffer[Random.Range(0, bestBuffer.Count)];
	}
}
