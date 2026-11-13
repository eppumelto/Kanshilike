using UnityEngine;

/// <summary>
/// Turns the social system on for a scene: people remember attacks and deaths, hold grudges, and
/// those change how they regard others. One per scene. Without it, everyone uses plain faction relations.
/// </summary>
[DefaultExecutionOrder(-50)]
public class SocialWorld : MonoBehaviour
{
	[SerializeField] private SocialConfig config;

	private readonly DayClock clock = new DayClock();
	private readonly SocialAttitudeProvider provider = new SocialAttitudeProvider();
	private readonly SocialCommunityFactionSource communitySource = new SocialCommunityFactionSource();
	private TimeManager time;
	private long lastWholeHour = long.MinValue;

	public SocialConfig Config => config;

	/// <summary>The enabled SocialWorld, or null.</summary>
	public static SocialWorld Current { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics() => Current = null;

	private void Awake()
	{
		if (config == null)
			Debug.LogWarning($"[Social] {name}: SocialWorld has no SocialConfig; using defaults.", this);
		Social.Reset(config != null ? config.ToTuning() : SocialTuning.Default);

		// Communities first (4b), then everyone already in the scene.
		foreach (Character c in FindObjectsByType<Character>(FindObjectsSortMode.None))
			if (c.isActiveAndEnabled)
				Social.Register(c);
	}

	private void OnEnable()
	{
		Current = this;
		Social.Active = true;
		Attitude.Provider = provider;
		CharacterFaction.CommunitySource = communitySource;
		PlayerControllable.Joined += Social.HandleJoined;
		CharacterFaction.OwnFactionChanged += Social.HandleOwnFactionChanged;
		Social.GrudgeCreated += HandleGrudgeCreated;
		HookTime();
	}

	private void OnDisable()
	{
		if (ReferenceEquals(Attitude.Provider, provider))
			Attitude.Provider = null;
		if (ReferenceEquals(CharacterFaction.CommunitySource, communitySource))
			CharacterFaction.CommunitySource = null;
		PlayerControllable.Joined -= Social.HandleJoined;
		CharacterFaction.OwnFactionChanged -= Social.HandleOwnFactionChanged;
		Social.GrudgeCreated -= HandleGrudgeCreated;
		if (time != null)
			time.OnTimeChanged -= HandleTime;
		time = null;
		// A world destroyed late (end of frame) must not switch off a newer one.
		if (Current == this)
		{
			Current = null;
			Social.Active = false;
		}
	}

	private void Update()
	{
		// A TimeManager made after this component (tests, additive scenes) is picked up here.
		if (time == null)
			HookTime();
	}

	private void HookTime()
	{
		if (time != null)
			return;
		time = FindFirstObjectByType<TimeManager>();
		if (time != null)
			time.OnTimeChanged += HandleTime;
	}

	private void HandleTime(float hour)
	{
		clock.Advance(hour);
		Social.Now = clock.TotalHours;
		long whole = (long)System.Math.Floor(Social.Now);
		if (whole != lastWholeHour)
		{
			lastWholeHour = whole;
			Social.Sim.Advance(Social.Now);
		}
	}

	// Logged for everyone; squad members never act on grudges, so the player sees theirs form.
	private void HandleGrudgeCreated(Grudge g)
	{
		PersonRecord holderRecord = Social.Sim.Registry.Get(g.HolderId);
		PersonRecord target = Social.Sim.Registry.Get(g.TargetId);
		string holderName = holderRecord != null ? holderRecord.Name : g.HolderId;
		string targetName = target != null ? target.Name : "someone";
		GrudgeTier tier = GrudgeRules.Tier(g.Intensity, Social.Tuning);
		Character holder = Social.Find(g.HolderId);
		Debug.Log($"[Social] {holderName} holds a {tier} grudge ({g.Intensity:0}) against {targetName}{(g.Inherited ? ", inherited" : "")}.", holder);

		if (holder != null && Social.InSquad(holder))
			holder.ShowFloatingText($"{holderName} swears vengeance on {targetName}", 4f);
	}
}
