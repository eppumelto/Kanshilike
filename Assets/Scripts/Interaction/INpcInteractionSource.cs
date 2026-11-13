using System.Collections.Generic;

/// <summary>
/// Adds options to an NPC's right-click menu (Recruit, later blood price and trade). Put it on the
/// NPC next to NpcInteractable; options appear between Talk and Attack, in component order.
/// </summary>
public interface INpcInteractionSource
{
	void AddOptions(Character interactor, AttitudeResult attitude, List<InteractionOption> into);

	/// <summary>Performs the action if it is one of this source's. Returns false otherwise.</summary>
	bool TryExecute(Character interactor, string actionId);
}
