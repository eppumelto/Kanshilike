using System;

/// <summary>
/// Every number the social rules use. SocialConfig (the asset) builds one with ToTuning(); tests
/// use Default and change fields.
/// </summary>
[Serializable]
public class SocialTuning
{
	// Opinion
	public int hostileOpinion = -60;
	public int alliedOpinion = 60;
	public int attackedOpinion = -25;
	public int foughtTogetherOpinion = 10;
	public int treatedWoundsOpinion = 15;

	// Bonds: default opinions
	public int neighbourBondOpinion = 20;
	public int campBondOpinion = 40;
	public int kinGroupBondOpinion = 30;
	public int householdBondOpinion = 60;
	public int elderBondOpinion = 40;

	// Bonds: closeness weights
	public float weightFamily = 0.4f;
	public float weightLover = 0.5f;
	public float weightFriend = 0.2f;
	public float weightComrade = 0.1f;
	public float weightRival = -0.3f;
	public float weightNeighbour = 0.2f;
	public float weightElder = 0.3f;
	public float weightChild = 0.7f;
	public float weightParent = 0.6f;
	public float weightSpouse = 0.6f;
	public float weightSibling = 0.5f;

	// Grudges: creation
	public float grudgeClosenessThreshold = 0.35f;
	public float defaultVengefulness = 0.5f;
	public float kinGrudgeBonus = 20f;

	// Grudges: tiers
	public float minGrudgeIntensity = 15f;
	public float hostileAt = 36f;
	public float rallyAt = 48f;
	public float huntAt = 68f;
	public float squadScopeAt = 85f;

	// Grudges: decay and limits
	public float decayPerDay = 5f;
	public float forgetBelow = 10f;
	public float stackBonus = 10f;
	public int maxGrudgesPerHolder = 3;
	public int maxGrudgesPerDeath = 4;
	public int elderDeathGrudgeCap = 20;
	public int maxFeudGeneration = 2;
	public float generationFalloff = 0.6f;
	public float inheritAt = 60f;
	public float inheritFactor = 0.75f;

	// Knowledge
	public float attributionWindowHours = 0.25f;
	public float witnessRadius = 20f;
	public float newsDelayHours = 3f;
	public float kinNewsDelayHours = 24f;

	// Revenge: rally and hunt (4c)
	public float rallyRadius = 15f;
	public float rallyCloseness = 0.35f;
	public float rallyIntensityFactor = 0.5f;
	public float calloutCooldownSeconds = 20f;
	public float huntGiveUpHours = 6f;
	public float huntSearchRadius = 8f;
	public float gatherRadius = 30f;
	public int maxHuntParty = 3;
	public float gatherBelowRatio = 1f;
	public float huntMinStrengthRatio = 0.6f;

	// Blood price (4d)
	public float bloodPriceWorthPerIntensity = 1f;
	public int bloodPriceMinWorth = 10;
	public int bloodPriceOpinionFloor = -20;

	public static SocialTuning Default => new SocialTuning();

	public SocialTuning Clone() => (SocialTuning)MemberwiseClone();

	/// <summary>The closeness weight of all these flags together.</summary>
	public float WeightOf(BondFlags bonds)
	{
		float w = 0f;
		if ((bonds & BondFlags.Family) != 0) w += weightFamily;
		if ((bonds & BondFlags.Lover) != 0) w += weightLover;
		if ((bonds & BondFlags.Friend) != 0) w += weightFriend;
		if ((bonds & BondFlags.Comrade) != 0) w += weightComrade;
		if ((bonds & BondFlags.Rival) != 0) w += weightRival;
		if ((bonds & BondFlags.Neighbour) != 0) w += weightNeighbour;
		if ((bonds & BondFlags.Elder) != 0) w += weightElder;
		if ((bonds & BondFlags.Child) != 0) w += weightChild;
		if ((bonds & BondFlags.Parent) != 0) w += weightParent;
		if ((bonds & BondFlags.Spouse) != 0) w += weightSpouse;
		if ((bonds & BondFlags.Sibling) != 0) w += weightSibling;
		return w;
	}
}
