using UnityEngine;

/// <summary>
/// Base for data assets that are looked up by a stable id (factions, anchor tags, later tribes,
/// kin groups and households). The id falls back to the asset name so a fresh asset never keeps
/// a "new-..." id frozen at creation time.
/// </summary>
public abstract class DefinitionAsset : ScriptableObject
{
	[Tooltip("Stable id used for lookups and saves. Leave empty to use the asset name. Never change it after content ships.")]
	[SerializeField] private string id;
	[Tooltip("Name shown in the game and the debug views. Leave empty to use the asset name.")]
	[SerializeField] private string displayName;

	public string Id => string.IsNullOrWhiteSpace(id) ? IdFromName(name) : id.Trim();
	public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

	public static string IdFromName(string assetName) =>
		string.IsNullOrEmpty(assetName) ? string.Empty : assetName.Trim().ToLowerInvariant().Replace(' ', '-');
}
