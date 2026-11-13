using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The thin game-side adapter over the Rules SocialSimulation: maps live Characters to person ids,
/// and turns attacks, damage and deaths into simulation calls. Records only while a SocialWorld is
/// active (Active); registering always works, so ids are known when one appears.
/// </summary>
public static class Social
{
	private static readonly Dictionary<string, Character> live = new Dictionary<string, Character>(StringComparer.Ordinal);
	private static readonly List<Witness> witnessScratch = new List<Witness>();

	public static bool Active { get; internal set; }
	/// <summary>Total in-game hours (DayClock.TotalHours), set by SocialWorld.</summary>
	public static double Now { get; internal set; }
	public static SocialSimulation Sim { get; private set; }
	public static SocialTuning Tuning => Sim.Tuning;

	/// <summary>A new grudge, with the live holder (null if not loaded).</summary>
	public static event Action<Grudge> GrudgeCreated;

	public static IEnumerable<Character> Live
	{
		get
		{
			foreach (Character c in live.Values)
				if (c != null)
					yield return c;
		}
	}

	public static Character Find(string personId) =>
		!string.IsNullOrEmpty(personId) && live.TryGetValue(personId, out Character c) && c != null ? c : null;

	/// <summary>Starts a fresh simulation with the current faction table. Called by SocialWorld and tests.</summary>
	public static void Reset(SocialTuning tuning)
	{
		live.Clear();
		Active = false;
		Now = 0;
		Sim = new SocialSimulation(tuning ?? SocialTuning.Default, FactionRelations.Table, FactionRelations.PlayerId);
		Sim.GrudgeCreated += g => GrudgeCreated?.Invoke(g);
		SocialDebugProvider.EnsureRegistered();
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		GrudgeCreated = null;
		Reset(SocialTuning.Default);
	}

	private static SocialSimulation EnsureSim()
	{
		if (Sim == null)
			Reset(SocialTuning.Default);
		return Sim;
	}

	// ── Registration ───────────────────────────────────────

	/// <summary>Adds the character as a person. Idempotent for the same object; a copied id gets a fresh one.</summary>
	public static void Register(Character c)
	{
		if (c == null)
			return;
		SocialSimulation sim = EnsureSim();

		if (string.IsNullOrEmpty(c.PersonId))
			c.AssignNewPersonId();
		if (live.TryGetValue(c.PersonId, out Character existing) && existing != null && existing != c)
		{
			string old = c.PersonId;
			c.AssignNewPersonId();
			Debug.LogWarning($"[Social] {c.name} had the same person id as {existing.name} ({old}); gave it a new one. Duplicate it with care.", c);
		}
		live[c.PersonId] = c;

		if (!sim.Registry.TryGet(c.PersonId, out _))
		{
			NpcBrain brain = c.GetComponent<NpcBrain>();
			PlayerControllable squad = c.GetComponent<PlayerControllable>();
			FactionDefinition own = c.OwnFaction;
			sim.Register(new PersonRecord
			{
				Id = c.PersonId,
				Name = string.IsNullOrEmpty(c.characterName) ? c.name : c.characterName,
				Sex = c.Sex,
				OwnFaction = own != null ? own.FactionId : FactionId.None,
				SquadId = squad != null ? SocialSimulation.PlayerSquadId : null,
				Vengefulness = brain != null && brain.Archetype != null ? brain.Archetype.vengefulness : sim.Tuning.defaultVengefulness,
				State = c.IsAlive ? PersonState.Alive : PersonState.Dead
			});
		}
		c.InvalidateFaction();
	}

	/// <summary>Destroyed: a live person becomes Gone.</summary>
	public static void Unregister(Character c)
	{
		if (c == null || string.IsNullOrEmpty(c.PersonId) || Sim == null)
			return;
		if (live.TryGetValue(c.PersonId, out Character existing) && existing == c)
			live.Remove(c.PersonId);
		Sim.MarkGone(c.PersonId);
	}

	public static void HandleJoined(PlayerControllable p)
	{
		if (p == null || p.Character == null || Sim == null)
			return;
		Sim.SetSquad(p.Character.PersonId, SocialSimulation.PlayerSquadId);
	}

	public static void HandleOwnFactionChanged(Character c)
	{
		if (c == null || Sim == null)
			return;
		FactionDefinition own = c.OwnFaction;
		Sim.SetOwnFaction(c.PersonId, own != null ? own.FactionId : FactionId.None);
	}

	/// <summary>True when this character is in the player's squad by the simulation's record.</summary>
	public static bool InSquad(Character c) =>
		c != null && Sim != null && Sim.Registry.TryGet(c.PersonId, out PersonRecord r) && r.SquadId != null;

	// ── Reports from Character hooks ───────────────────────

	public static void ReportAttack(Character victim, GameObject attacker)
	{
		if (!Active || victim == null)
			return;
		if (Resolve(attacker) is Character a && a != victim)
			Sim.RecordAttack(victim.PersonId, a.PersonId, Now);
	}

	public static void ReportDamaged(Character victim, GameObject source)
	{
		if (!Active || victim == null)
			return;
		CombatActor actor = Resolve(source);
		if (actor == null || actor == victim)
			return;
		Sim.RecordDamage(victim.PersonId, (actor as Character)?.PersonId, actor.Faction, Now);
	}

	public static DeathRecord ReportDeath(Character victim, GameObject source)
	{
		if (!Active || victim == null)
			return null;

		CombatActor actor = Resolve(source);
		if (actor == victim)
			actor = null;
		string killerId = (actor as Character)?.PersonId;

		witnessScratch.Clear();
		float radiusSq = Tuning.witnessRadius * Tuning.witnessRadius;
		Vector3 at = victim.transform.position;
		foreach (Character c in Live)
		{
			if (c == victim || !c.IsAlive || (c.Stats != null && c.Stats.IsSleeping))
				continue;
			float d = (c.transform.position - at).sqrMagnitude;
			if (d <= radiusSq)
				witnessScratch.Add(new Witness(c.PersonId, d));
		}

		DeathRecord record = Sim.RecordDeath(victim.PersonId, killerId, actor != null, actor != null ? actor.Faction : FactionId.None,
			new WorldPoint(at.x, at.y, at.z), witnessScratch, Now);

		PersonRecord killer = Sim.Registry.Get(record.KillerId);
		string by = killer != null ? killer.Name : record.KillerFactionKnown ? $"a {FactionRelations.DisplayName(record.KillerFaction)} creature" : "unknown";
		Debug.Log($"[Social] {victim.characterName} died, killed by {by} (faction {record.KillerFaction}); " +
			$"{(record.KillerWitnessed ? "witnessed" : "no witness")}, {witnessScratch.Count} saw it, {record.GrudgesCreated} grudge(s).", victim);
		return record;
	}

	/// <summary>Someone loots a body: they learn of the death, and the killer only if it was witnessed.</summary>
	public static void ReportCorpseInspected(Character looter, Character victim)
	{
		if (!Active || looter == null || victim == null)
			return;
		DeathRecord record = Sim.Deaths.LatestOf(victim.PersonId);
		if (record != null)
			Sim.Learn(looter.PersonId, record.Index, record.KillerWitnessed, Now);
	}

	private static CombatActor Resolve(GameObject source) =>
		source != null ? source.GetComponentInParent<CombatActor>() : null;
}
