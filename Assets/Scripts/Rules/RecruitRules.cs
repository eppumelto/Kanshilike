/// <summary>Why a recruit attempt failed. Append only: the number is the check order.</summary>
public enum RecruitBlock
{
	None = 0,
	Dead = 1,
	Hostile = 2,
	InCombat = 3,
	TooFar = 4,
	CantAfford = 5,
	Unwilling = 6
}

/// <summary>Whether someone can be recruited right now. Pure; checks run in the enum's order.</summary>
public static class RecruitRules
{
	public static RecruitBlock Check(bool alive, FactionRelation relation, bool inCombat, bool inReach, int carried, int cost, bool unwilling)
	{
		if (!alive)
			return RecruitBlock.Dead;
		if (relation == FactionRelation.Hostile)
			return RecruitBlock.Hostile;
		if (inCombat)
			return RecruitBlock.InCombat;
		if (!inReach)
			return RecruitBlock.TooFar;
		if (cost > 0 && carried < cost)
			return RecruitBlock.CantAfford;
		if (unwilling)
			return RecruitBlock.Unwilling;
		return RecruitBlock.None;
	}

	public static string Reason(RecruitBlock block)
	{
		switch (block)
		{
			case RecruitBlock.Dead: return "Dead";
			case RecruitBlock.Hostile: return "Won't listen to you";
			case RecruitBlock.InCombat: return "Too busy fighting";
			case RecruitBlock.TooFar: return "Too far away";
			case RecruitBlock.CantAfford: return "Can't pay";
			case RecruitBlock.Unwilling: return "Won't share a fire with your people";
			default: return "Willing";
		}
	}
}
