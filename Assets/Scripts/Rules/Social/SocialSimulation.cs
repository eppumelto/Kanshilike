using System;
using System.Collections.Generic;

/// <summary>Someone who saw a death. The adapter passes only live, awake people.</summary>
public struct Witness
{
	public string Id;
	public float DistanceSq;

	public Witness(string id, float distanceSq)
	{
		Id = id;
		DistanceSq = distanceSq;
	}
}

/// <summary>What one blood price payment would settle.</summary>
public class BloodPriceQuote
{
	public Grudge Primary;
	public readonly List<Grudge> Settles = new List<Grudge>();
	/// <summary>The strongest settled grudge; the price is sized from this.</summary>
	public float Intensity;
}

/// <summary>
/// People, communities, relationships, deaths, knowledge and grudges, all keyed by person id
/// strings with no Unity objects, so every rule is EditMode-testable and a later save can write
/// it as it is. Time is total in-game hours (DayClock.TotalHours).
/// </summary>
public sealed class SocialSimulation
{
	public const string PlayerSquadId = "player";

	public readonly SocialTuning Tuning;
	public readonly PersonRegistry Registry = new PersonRegistry();
	public readonly CommunityRegistry Communities = new CommunityRegistry();
	public readonly TribeOpinionTable Tribes = new TribeOpinionTable();
	public readonly RelationshipBook Relations = new RelationshipBook();
	public readonly DamageLog Damage = new DamageLog();
	public readonly DeathLedger Deaths = new DeathLedger();
	public readonly KnowledgeBook Knowledge = new KnowledgeBook();
	public readonly GrudgeBook Grudges = new GrudgeBook();

	private readonly FactionRelationTable factions;
	private readonly FactionId player;
	private readonly List<PendingNews> dueScratch = new List<PendingNews>();

	/// <summary>A new grudge (not a merge), including inherited ones.</summary>
	public event Action<Grudge> GrudgeCreated;

	public SocialSimulation(SocialTuning tuning, FactionRelationTable factions, FactionId player)
	{
		Tuning = tuning ?? SocialTuning.Default;
		this.factions = factions ?? new FactionRelationTable();
		this.player = player;
	}

	// ── People and communities ─────────────────────────────

	/// <summary>Adds the record. No bonds are written: default bonds are derived on read.</summary>
	public bool Register(PersonRecord record) => Registry.Add(record);

	public bool RegisterCommunity(CommunityRecord record) => Communities.Add(record);

	public FactionResolution EffectiveFaction(string personId)
	{
		if (!Registry.TryGet(personId, out PersonRecord r))
			return new FactionResolution(FactionId.None, FactionSource.Own);
		FactionId community = Communities.TryGet(r.CommunityId, out CommunityRecord c) ? c.Faction : FactionId.None;
		return FactionRules.Resolve(r.SquadId != null, community, r.OwnFaction, player);
	}

	/// <summary>Sets the community's faction and lists the alive members whose effective faction changed.</summary>
	public bool SetCommunityFaction(string communityId, FactionId faction, List<string> changedMembers)
	{
		if (!Communities.TryGet(communityId, out _))
			return false;

		List<PersonRecord> members = Registry.MembersOf(communityId);
		var before = new List<FactionId>(members.Count);
		foreach (PersonRecord m in members)
			before.Add(EffectiveFaction(m.Id).Id);

		Communities.SetFaction(communityId, faction);

		for (int i = 0; i < members.Count; i++)
			if (EffectiveFaction(members[i].Id).Id != before[i])
				changedMembers?.Add(members[i].Id);
		return true;
	}

	/// <summary>Only sets ElderId: every member's Elder bond follows at once. Null clears.</summary>
	public void SetElder(string communityId, string personId) => Communities.SetElder(communityId, personId);

	/// <summary>Changes community (null leaves). An elder who leaves stops being elder.</summary>
	public void MoveTo(string personId, string communityId)
	{
		if (!Registry.TryGet(personId, out PersonRecord r) || PersonRegistry.SameId(r.CommunityId, communityId))
			return;
		if (Communities.TryGet(r.CommunityId, out CommunityRecord old) && PersonRegistry.SameId(old.ElderId, personId))
			old.ElderId = null;
		Registry.SetCommunity(personId, communityId);
	}

	public void SetSquad(string personId, string squadId) => Registry.SetSquad(personId, squadId);
	public void SetOwnFaction(string personId, FactionId faction) => Registry.SetOwnFaction(personId, faction);

	/// <summary>Destroyed without dying. Grudges stay and decay; an elder role ends.</summary>
	public void MarkGone(string personId)
	{
		if (!Registry.TryGet(personId, out PersonRecord r) || r.State != PersonState.Alive)
			return;
		r.State = PersonState.Gone;
		Communities.ClearElder(personId);
	}

	// ── Relationships ──────────────────────────────────────

	/// <summary>The effective relationship: derived from shared memberships, plus what is stored.</summary>
	public Relationship Relationship(string from, string to) => Relationship(from, to, null);

	private Relationship Relationship(string from, string to, CommunityRecord fromCommunityOverride)
	{
		StoredRelationship stored = Relations.GetStored(from, to);
		bool hasDerived = Derive(from, to, fromCommunityOverride, out Relationship derived);
		return RelationshipRules.Effective(hasDerived, derived, stored);
	}

	public float Closeness(string from, string to) => GrudgeRules.Closeness(Relationship(from, to), Tuning);

	public void AddOpinion(string from, string to, int change)
	{
		if (!Valid(from, to))
			return;
		int baseOpinion = BaseOpinion(from, to, out StoredRelationship stored);
		Relations.SetDelta(from, to, RelationshipRules.DeltaAfterChange(baseOpinion, stored.Delta, change));
	}

	/// <summary>Raises the effective opinion to at least floor; never lowers it.</summary>
	public void RaiseOpinionTo(string from, string to, int floor)
	{
		if (!Valid(from, to))
			return;
		int baseOpinion = BaseOpinion(from, to, out StoredRelationship stored);
		Relations.SetDelta(from, to, RelationshipRules.DeltaAfterRaise(baseOpinion, stored.Delta, floor));
	}

	/// <summary>An authored bond: adds non-kin flags and replaces the opinion base.</summary>
	public void SetAuthoredBond(string from, string to, BondFlags nonKinFlags, int opinion)
	{
		if (!Valid(from, to))
			return;
		Relations.AddBonds(from, to, nonKinFlags);
		Relations.SetBase(from, to, opinion);
	}

	private int BaseOpinion(string from, string to, out StoredRelationship stored)
	{
		stored = Relations.GetStored(from, to);
		bool hasDerived = Derive(from, to, null, out Relationship derived);
		return RelationshipRules.BaseOf(hasDerived, derived, stored);
	}

	private bool Derive(string from, string to, CommunityRecord fromCommunityOverride, out Relationship derived)
	{
		derived = default;
		if (!Registry.TryGet(from, out PersonRecord a) || !Registry.TryGet(to, out PersonRecord b))
			return false;
		CommunityRecord community = fromCommunityOverride ?? Communities.Get(a.CommunityId);
		return DefaultBondRules.Between(a, b, community, Tuning, out derived);
	}

	private static bool Valid(string from, string to) =>
		!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to) && !string.Equals(from, to, StringComparison.Ordinal);

	// ── Fighting and dying ─────────────────────────────────

	public void RecordAttack(string victimId, string attackerId, double now)
	{
		if (!Valid(victimId, attackerId))
			return;
		AddOpinion(victimId, attackerId, Tuning.attackedOpinion);
	}

	public void RecordDamage(string victimId, string attackerId, FactionId attackerFaction, double now)
	{
		if (string.IsNullOrEmpty(victimId) || (attackerId == null && attackerFaction.IsNone))
			return;
		if (string.Equals(victimId, attackerId, StringComparison.Ordinal))
			return;
		Damage.Record(victimId, attackerId, attackerFaction, now);
	}

	/// <summary>
	/// Records a death: blames the killer, ends grudges against and held by the victim (keeping the
	/// hardest for the victim's children), lets witnesses learn and schedules news for everyone else.
	/// </summary>
	public DeathRecord RecordDeath(string victimId, string directKillerId, bool hasDirectFaction, FactionId directFaction,
		WorldPoint at, IReadOnlyList<Witness> witnesses, double now)
	{
		Registry.TryGet(victimId, out PersonRecord victim);

		// 1. Attribute the killer: the direct source, else the most recent damager in the window.
		string killerId = string.Equals(directKillerId, victimId, StringComparison.Ordinal) ? null : directKillerId;
		bool factionKnown = hasDirectFaction;
		FactionId killerFaction = directFaction;
		if (killerId == null && !hasDirectFaction
			&& Damage.TryLastAttacker(victimId, now, Tuning.attributionWindowHours, out DamageEntry last))
		{
			killerId = last.AttackerId;
			killerFaction = last.Faction;
			factionKnown = !last.Faction.IsNone;
		}
		if (killerId != null && !factionKnown && Registry.TryGet(killerId, out _))
		{
			killerFaction = EffectiveFaction(killerId).Id;
			factionKnown = true;
		}

		var record = new DeathRecord
		{
			VictimId = victimId,
			KillerId = killerId,
			KillerFactionKnown = factionKnown,
			KillerFaction = killerFaction,
			Day = DayClock.DayOf(now),
			Hour = (float)(now - DayClock.DayOf(now) * 24.0),
			Position = at
		};

		// 2. Feud generation: revenge, or an avenger killed by their target.
		if (killerId != null)
		{
			int generation = 0;
			Grudge revenge = Grudges.Get(killerId, victimId);
			if (revenge != null)
				generation = Math.Max(generation, revenge.Generation + 1);
			Grudge avenger = Grudges.Get(victimId, killerId);
			if (avenger != null)
				generation = Math.Max(generation, avenger.Generation + 1);
			record.FeudGeneration = generation;
		}

		// 3. Witnessed by someone outside the killer's side.
		if (witnesses != null && (killerId != null || factionKnown))
			foreach (Witness w in witnesses)
			{
				if (string.Equals(w.Id, killerId, StringComparison.Ordinal) || string.Equals(w.Id, victimId, StringComparison.Ordinal))
					continue;
				if (factionKnown && factions.Get(EffectiveFaction(w.Id).Id, killerFaction) == FactionRelation.Allied)
					continue;
				record.KillerWitnessed = true;
				break;
			}

		// 4. Elder, then the record itself.
		foreach (CommunityRecord c in Communities.All)
			if (PersonRegistry.SameId(c.ElderId, victimId))
			{
				record.VictimWasElder = true;
				record.ElderOfCommunityId = c.Id;
				break;
			}
		Deaths.Add(record);
		Registry.SetState(victimId, PersonState.Dead);
		Communities.ClearElder(victimId);

		// 5. Grudges against the victim end; the victim's hardest grudges wait for their children.
		Grudges.RemoveAgainst(victimId);
		foreach (Grudge g in Grudges.HeldBy(victimId))
			if (GrudgeRules.Inheritable(g, Tuning))
				record.InheritableGrudges.Add(new InheritedGrudge
				{
					TargetId = g.TargetId,
					VictimId = g.VictimId,
					DeathIndex = g.DeathIndex,
					Intensity = g.Intensity,
					Generation = g.Generation
				});
		Grudges.RemoveHeldBy(victimId);

		// 6. People who fought on the same side warm to each other.
		List<string> fighters = Damage.RecentPersonAttackers(victimId, now, Tuning.attributionWindowHours);
		for (int i = 0; i < fighters.Count; i++)
			for (int j = i + 1; j < fighters.Count; j++)
			{
				string a = fighters[i], b = fighters[j];
				if (!Registry.IsAlive(a) || !Registry.IsAlive(b))
					continue;
				if (AttitudeOf(a, b, FactionBetween(a, b)).Relation == FactionRelation.Hostile
					|| AttitudeOf(b, a, FactionBetween(b, a)).Relation == FactionRelation.Hostile)
					continue;
				AddOpinion(a, b, Tuning.foughtTogetherOpinion);
				AddOpinion(b, a, Tuning.foughtTogetherOpinion);
			}

		// 7. Witnesses learn at once, closest first (the per-death cap favours them).
		var seen = new HashSet<string>(StringComparer.Ordinal);
		if (witnesses != null)
		{
			var sorted = new List<Witness>(witnesses);
			sorted.Sort((x, y) => x.DistanceSq.CompareTo(y.DistanceSq));
			foreach (Witness w in sorted)
			{
				if (string.IsNullOrEmpty(w.Id) || string.Equals(w.Id, victimId, StringComparison.Ordinal) || !seen.Add(w.Id))
					continue;
				Learn(w.Id, record.Index, revealKiller: true, now);
			}
		}

		// 8. News for everyone else who shares a household, community or kin group.
		if (victim != null)
			foreach (PersonRecord p in Registry.All)
			{
				if (!p.IsAlive || seen.Contains(p.Id))
					continue;
				if (NewsRules.Delay(victim, p, Tuning, out double hours))
					Knowledge.Schedule(new PendingNews { PersonId = p.Id, DeathIndex = record.Index, DueHours = now + hours });
			}

		// 9.
		Damage.Clear(victimId);
		return record;
	}

	/// <summary>
	/// Someone learns of a death. A child takes over the dead parent's hardest grudges; whoever
	/// newly learns the killer may take a grudge.
	/// </summary>
	public void Learn(string personId, int deathIndex, bool revealKiller, double now)
	{
		DeathRecord record = Deaths.Get(deathIndex);
		if (record == null || !Registry.TryGet(personId, out PersonRecord person) || !person.IsAlive)
			return;

		bool knowsKiller = revealKiller && record.KillerId != null;
		bool newDeath = Knowledge.Learn(personId, deathIndex, knowsKiller, out bool killerNewlyKnown);

		if (newDeath && (Relations.KinOf(personId, record.VictimId) & BondFlags.Parent) != 0)
			InheritFrom(person, record, now);

		if (killerNewlyKnown)
			EvaluateGrudge(person, record, now);
	}

	private void InheritFrom(PersonRecord child, DeathRecord record, double now)
	{
		int day = DayClock.DayOf(now);
		foreach (InheritedGrudge inherited in record.InheritableGrudges)
		{
			if (string.Equals(inherited.TargetId, child.Id, StringComparison.Ordinal))
				continue;
			if (!Registry.TryGet(inherited.TargetId, out PersonRecord target) || target.State == PersonState.Dead)
				continue;

			float intensity = GrudgeRules.Inherit(inherited.Intensity, Tuning);
			if (!GrudgeRules.AtLeast(intensity, Tuning.minGrudgeIntensity))
				continue;

			Add(new Grudge
			{
				HolderId = child.Id,
				TargetId = inherited.TargetId,
				VictimId = inherited.VictimId,
				DeathIndex = inherited.DeathIndex,
				Cause = GrudgeCause.Killing,
				Intensity = intensity,
				CreatedDay = day,
				LastDecayDay = day,
				Generation = inherited.Generation,
				Inherited = true
			});
		}
	}

	private void EvaluateGrudge(PersonRecord holder, DeathRecord record, double now)
	{
		if (record.KillerId == null || !Registry.TryGet(record.KillerId, out PersonRecord killer) || killer.State == PersonState.Dead)
			return;
		if (!holder.IsAlive || holder.Id == killer.Id || holder.Id == record.VictimId)
			return;
		if (record.FeudGeneration > Tuning.maxFeudGeneration)
			return;
		if (record.GrudgesCreated >= GrudgeRules.GrudgeCapFor(record.VictimWasElder, Tuning))
			return;

		float closeness = GrudgeRules.Closeness(RelationshipForDeath(holder, record), Tuning);
		if (closeness + GrudgeRules.Epsilon < Tuning.grudgeClosenessThreshold)
			return;

		BondFlags kin = Relations.KinOf(holder.Id, record.VictimId);
		float intensity = GrudgeRules.Intensity(closeness, holder.Vengefulness, kin, record.FeudGeneration, Tuning);
		if (!GrudgeRules.AtLeast(intensity, Tuning.minGrudgeIntensity))
			return;

		int day = DayClock.DayOf(now);
		Grudge stored = Add(new Grudge
		{
			HolderId = holder.Id,
			TargetId = killer.Id,
			VictimId = record.VictimId,
			DeathIndex = record.Index,
			Cause = GrudgeCause.Killing,
			Intensity = intensity,
			CreatedDay = day,
			LastDecayDay = day,
			Generation = record.FeudGeneration,
			HasLastKnown = true,
			LastKnown = record.Position
		});
		if (stored != null)
			record.GrudgesCreated++;
	}

	// The holder's relationship to the victim; for an elder's death, with the elder layer the
	// victim had, since ElderId was cleared when they died.
	private Relationship RelationshipForDeath(PersonRecord holder, DeathRecord record)
	{
		if (record.VictimWasElder && PersonRegistry.SameId(record.ElderOfCommunityId, holder.CommunityId)
			&& Communities.TryGet(holder.CommunityId, out CommunityRecord community))
		{
			CommunityRecord withElder = community.Clone();
			withElder.ElderId = record.VictimId;
			return Relationship(holder.Id, record.VictimId, withElder);
		}
		return Relationship(holder.Id, record.VictimId);
	}

	private Grudge Add(Grudge grudge)
	{
		Grudge stored = Grudges.AddOrMerge(grudge, Tuning, out bool created);
		if (created)
			GrudgeCreated?.Invoke(stored);
		return stored;
	}

	/// <summary>Delivers due news, then decays every grudge by whole days and forgets weak ones.</summary>
	public void Advance(double now)
	{
		dueScratch.Clear();
		Knowledge.TakeDue(now, dueScratch);
		foreach (PendingNews news in dueScratch)
		{
			DeathRecord record = Deaths.Get(news.DeathIndex);
			if (record != null)
				Learn(news.PersonId, news.DeathIndex, record.KillerWitnessed, news.DueHours);
		}

		int day = DayClock.DayOf(now);
		foreach (Grudge g in new List<Grudge>(Grudges.All))
		{
			int days = day - g.LastDecayDay;
			if (days <= 0)
				continue;
			g.Intensity = GrudgeRules.Decayed(g.Intensity, days, Tuning);
			g.LastDecayDay = day;
			if (GrudgeRules.Forgotten(g.Intensity, Tuning))
				Grudges.Remove(g);
		}
	}

	// ── Attitude ───────────────────────────────────────────

	/// <summary>The direct grudge, or the strongest squad-wide one whose target shares other's squad.</summary>
	public float EffectiveGrudge(string holderId, string otherId, out Grudge source)
	{
		source = Grudges.Get(holderId, otherId);
		float best = source != null ? source.Intensity : 0f;

		if (Registry.TryGet(otherId, out PersonRecord other) && other.SquadId != null)
			foreach (Grudge g in Grudges.HeldBy(holderId))
			{
				if (g.Intensity <= best || !GrudgeRules.AtLeast(g.Intensity, Tuning.squadScopeAt))
					continue;
				if (Registry.TryGet(g.TargetId, out PersonRecord target) && target.IsAlive
					&& string.Equals(target.SquadId, other.SquadId, StringComparison.Ordinal))
				{
					best = g.Intensity;
					source = g;
				}
			}
		return best;
	}

	public float EffectiveGrudge(string holderId, string otherId) => EffectiveGrudge(holderId, otherId, out _);

	/// <summary>How self regards other. Squad members (and unknown people) use the faction relation only.</summary>
	public AttitudeResult AttitudeOf(string selfId, string otherId, FactionRelation factionRelation)
	{
		if (!Registry.TryGet(selfId, out PersonRecord self) || !Registry.TryGet(otherId, out PersonRecord other) || self.SquadId != null)
			return new AttitudeResult(factionRelation, AttitudeReason.Faction);

		Relationship rel = Relationship(selfId, otherId);
		int opinion = rel.Opinion;
		if (rel.Bonds == BondFlags.None && Relationship(otherId, selfId).Bonds == BondFlags.None)
			opinion = RelationshipRules.Clamp(opinion + Tribes.Get(self.TribeId, other.TribeId));

		bool closeKin = Relations.IsCloseKin(selfId, otherId);
		return AttitudeRules.Resolve(factionRelation, closeKin, opinion, EffectiveGrudge(selfId, otherId), Tuning);
	}

	/// <summary>The faction relation between two people by their effective factions in this simulation.</summary>
	public FactionRelation FactionBetween(string a, string b) =>
		factions.Get(EffectiveFaction(a).Id, EffectiveFaction(b).Id);

	/// <summary>Holds a grudge of at least minIntensity against a live member of this squad.</summary>
	public bool HoldsGrudgeAgainstSquad(string holderId, string squadId, float minIntensity)
	{
		if (string.IsNullOrEmpty(squadId))
			return false;
		foreach (Grudge g in Grudges.HeldBy(holderId))
			if (GrudgeRules.AtLeast(g.Intensity, minIntensity) && Registry.TryGet(g.TargetId, out PersonRecord t)
				&& t.IsAlive && string.Equals(t.SquadId, squadId, StringComparison.Ordinal))
				return true;
		return false;
	}

	// ── Rally and blood price (used from 4c and 4d) ────────

	/// <summary>A friend pulled into a fight takes the caller's grudge: at least Hostile, same generation.</summary>
	public Grudge JoinFeud(string friendId, Grudge callerGrudge, double now)
	{
		if (callerGrudge == null || !Registry.IsAlive(friendId) || string.Equals(friendId, callerGrudge.TargetId, StringComparison.Ordinal))
			return null;
		int day = DayClock.DayOf(now);
		return Add(new Grudge
		{
			HolderId = friendId,
			TargetId = callerGrudge.TargetId,
			VictimId = callerGrudge.VictimId,
			DeathIndex = callerGrudge.DeathIndex,
			Cause = callerGrudge.Cause,
			Intensity = Math.Max(Tuning.hostileAt, callerGrudge.Intensity * Tuning.rallyIntensityFactor),
			CreatedDay = day,
			LastDecayDay = day,
			Generation = callerGrudge.Generation,
			HasLastKnown = callerGrudge.HasLastKnown,
			LastKnown = callerGrudge.LastKnown
		});
	}

	/// <summary>
	/// The payee's strongest grudge against a squad member, plus their housemates' grudges against
	/// the same target over the same victim. Null when there is nothing to pay for.
	/// </summary>
	public BloodPriceQuote QuoteBloodPrice(string payeeId)
	{
		if (!Registry.TryGet(payeeId, out PersonRecord payee) || !payee.IsAlive)
			return null;

		Grudge primary = null;
		foreach (Grudge g in Grudges.HeldBy(payeeId))
			if (Registry.TryGet(g.TargetId, out PersonRecord t) && t.IsAlive && t.SquadId != null
				&& (primary == null || g.Intensity > primary.Intensity))
				primary = g;
		if (primary == null)
			return null;

		var quote = new BloodPriceQuote { Primary = primary, Intensity = primary.Intensity };
		quote.Settles.Add(primary);
		if (!string.IsNullOrEmpty(payee.HouseholdId))
			foreach (PersonRecord mate in Registry.HouseholdOf(payee.HouseholdId))
			{
				if (mate.Id == payeeId)
					continue;
				Grudge g = Grudges.Get(mate.Id, primary.TargetId);
				if (g != null && string.Equals(g.VictimId, primary.VictimId, StringComparison.Ordinal))
				{
					quote.Settles.Add(g);
					quote.Intensity = Math.Max(quote.Intensity, g.Intensity);
				}
			}
		return quote;
	}

	/// <summary>Removes every settled grudge and raises each holder's opinion of the target to the floor.</summary>
	public int PayBloodPrice(BloodPriceQuote quote)
	{
		if (quote == null)
			return 0;
		int count = 0;
		foreach (Grudge g in quote.Settles)
			if (Grudges.Remove(g))
			{
				RaiseOpinionTo(g.HolderId, g.TargetId, Tuning.bloodPriceOpinionFloor);
				count++;
			}
		return count;
	}
}
