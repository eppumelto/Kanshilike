using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's shared faction relations, built from Resources/Factions/FactionRelations on first
/// use. Every relation query goes through Get, so a later reputation system can change the table
/// at runtime (Table.Set) without touching any caller.
/// </summary>
public static class FactionRelations
{
	private const string ResourcePath = "Factions/FactionRelations";

	private static bool loaded;
	private static FactionRelationTable table;
	private static FactionRelationSet asset;
	private static FactionDefinition player;
	private static readonly Dictionary<FactionId, FactionDefinition> known = new Dictionary<FactionId, FactionDefinition>();
	private static readonly HashSet<int> warnedLegacy = new HashSet<int>();

	public static FactionRelationTable Table { get { EnsureLoaded(); return table; } }
	public static FactionRelationSet Asset { get { EnsureLoaded(); return asset; } }
	public static FactionDefinition Player { get { EnsureLoaded(); return player; } }
	public static FactionId PlayerId => Player != null ? Player.FactionId : FactionId.None;

	public static FactionRelation Get(FactionId a, FactionId b) => Table.Get(a, b);
	public static bool AreHostile(FactionId a, FactionId b) => Get(a, b) == FactionRelation.Hostile;
	public static bool AreAllied(FactionId a, FactionId b) => Get(a, b) == FactionRelation.Allied;
	/// <summary>Anyone not allied fights back when attacked.</summary>
	public static bool FightsBack(FactionId a, FactionId b) => Get(a, b) != FactionRelation.Allied;

	/// <summary>Old Faction enum value (0 Player, 1 Wildlife) to its asset, for fields saved before factions were assets.</summary>
	public static FactionDefinition FromLegacy(int legacyValue)
	{
		EnsureLoaded();
		List<FactionDefinition> order = asset != null ? asset.legacyEnumOrder : null;
		if (order != null && legacyValue >= 0 && legacyValue < order.Count && order[legacyValue] != null)
			return order[legacyValue];

		if (warnedLegacy.Add(legacyValue))
			Debug.LogWarning($"[FactionRelations] No faction for legacy value {legacyValue}; the actor has no faction. Set its faction asset or run Kanshilike > Factions > Migrate Legacy Faction Fields.");
		return null;
	}

	/// <summary>
	/// Makes a faction known: defines its stance if the table has none yet. Actors call this when
	/// they resolve their faction, so a new faction asset works without being added to any list.
	/// </summary>
	public static void Register(FactionDefinition definition)
	{
		if (definition == null)
			return;
		EnsureLoaded();
		FactionId id = definition.FactionId;
		if (id.IsNone)
			return;
		if (!table.IsDefined(id))
			table.Define(id, definition.DefaultStance);
		if (!known.ContainsKey(id))
			known[id] = definition;
	}

	public static FactionDefinition Find(FactionId id)
	{
		EnsureLoaded();
		return !id.IsNone && known.TryGetValue(id, out FactionDefinition definition) ? definition : null;
	}

	public static string DisplayName(FactionId id)
	{
		FactionDefinition definition = Find(id);
		return definition != null ? definition.DisplayName : id.ToString();
	}

	/// <summary>Replaces the shared setup. For tests; call ResetToDefault afterwards.</summary>
	public static void Configure(FactionRelationTable newTable, FactionDefinition newPlayer, IEnumerable<FactionDefinition> catalog)
	{
		Clear();
		loaded = true;
		table = newTable ?? new FactionRelationTable();
		player = newPlayer;
		Remember(newPlayer);
		if (catalog != null)
			foreach (FactionDefinition f in catalog)
				Remember(f);
		CharacterFaction.InvalidateAll();
	}

	/// <summary>Forgets everything; the next use loads the Resources asset again.</summary>
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	public static void ResetToDefault()
	{
		Clear();
		CharacterFaction.InvalidateAll();
	}

	private static void Clear()
	{
		loaded = false;
		table = null;
		asset = null;
		player = null;
		known.Clear();
		warnedLegacy.Clear();
	}

	private static void EnsureLoaded()
	{
		if (loaded)
			return;
		loaded = true;

		asset = Resources.Load<FactionRelationSet>(ResourcePath);
		if (asset == null)
		{
			Debug.LogError($"[FactionRelations] Missing Resources/{ResourcePath}.asset. Every faction is now only allied with itself and neutral to the rest.");
			table = new FactionRelationTable();
			return;
		}

		table = asset.BuildTable();
		player = asset.player;
		foreach (FactionDefinition f in asset.AllDefinitions())
			Remember(f);
	}

	private static void Remember(FactionDefinition definition)
	{
		if (definition != null && !definition.FactionId.IsNone && !known.ContainsKey(definition.FactionId))
			known[definition.FactionId] = definition;
	}
}
