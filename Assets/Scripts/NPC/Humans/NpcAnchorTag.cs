using UnityEngine;

/// <summary>
/// A kind of place an NPC goes: village square, stall, campfire, bed. Schedules and anchors pick
/// the same asset, so a typo can't create a new tag.
/// </summary>
[CreateAssetMenu(menuName = "Kanshilike/NPC/Anchor Tag", fileName = "NewAnchorTag")]
public class NpcAnchorTag : DefinitionAsset
{
}
