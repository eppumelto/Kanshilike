using UnityEngine;

/// <summary>
/// Every social number in one asset, grouped into sections by SocialConfigEditor, with a preview
/// of what each typical bond gives when someone is killed.
/// </summary>
[CreateAssetMenu(menuName = "Kanshilike/Social Config")]
public class SocialConfig : ScriptableObject
{
	[Tooltip("Opinion, bonds, grudges, knowledge, revenge and blood price numbers. See docs/plans/feuds-and-kinship.md.")]
	public SocialTuning tuning = new SocialTuning();

	/// <summary>A copy for the Rules simulation, so play never edits the asset.</summary>
	public SocialTuning ToTuning() => (tuning ?? new SocialTuning()).Clone();

	private void OnValidate()
	{
		if (tuning == null)
			return;
		SocialTuning t = tuning;
		if (!(t.minGrudgeIntensity <= t.hostileAt && t.hostileAt <= t.rallyAt && t.rallyAt <= t.huntAt))
			Debug.LogWarning($"[Social] {name}: grudge tiers out of order (Cold {t.minGrudgeIntensity} <= Hostile {t.hostileAt} <= Rally {t.rallyAt} <= Hunt {t.huntAt}).", this);
	}
}
