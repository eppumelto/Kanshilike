using System;
using UnityEngine;

/// <summary>Gives a character its community's faction. Installed by the social system; null otherwise.</summary>
public interface ICommunityFactionSource
{
	bool TryGetCommunityFaction(Character character, out FactionId faction, out string communityName);
}

/// <summary>
/// Shared state for how characters resolve their faction (see FactionRules.Resolve). Characters
/// cache their result and compare against Version, so InvalidateAll is one increment.
/// </summary>
public static class CharacterFaction
{
	private static ICommunityFactionSource communitySource;

	/// <summary>Bumped whenever every character must work out its faction again.</summary>
	public static int Version { get; private set; }

	/// <summary>Fires when a character's own faction is changed at runtime.</summary>
	public static event Action<Character> OwnFactionChanged;

	public static ICommunityFactionSource CommunitySource
	{
		get => communitySource;
		set
		{
			communitySource = value;
			InvalidateAll();
		}
	}

	public static void InvalidateAll() => Version++;

	internal static void RaiseOwnFactionChanged(Character character) => OwnFactionChanged?.Invoke(character);

	/// <summary>The character's faction and a readable reason, for debug views.</summary>
	public static string Explain(Character character)
	{
		if (character == null)
			return "none";

		FactionResolution r = character.FactionResolution;
		string name = FactionRelations.DisplayName(r.Id);
		switch (r.Source)
		{
			case FactionSource.Squad:
				return $"{name} (squad member)";
			case FactionSource.Community:
				string community = null;
				if (communitySource != null)
					communitySource.TryGetCommunityFaction(character, out _, out community);
				return string.IsNullOrEmpty(community) ? $"{name} (community)" : $"{name} (community {community})";
			default:
				return character.UsesLegacyFaction ? $"{name} (own faction, legacy field)" : $"{name} (own faction)";
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		communitySource = null;
		OwnFactionChanged = null;
		Version++;
	}
}
