using UnityEngine;

public enum WolfThreatResponseMode
{
    UsePersonality,
    AlwaysChase,
    AlwaysFlee
}

/// <summary>
/// Backward compatibility wrapper for AnimalPredatorBehaviorProfile.
/// Existing Wolf prefabs and serialized assets reference WolfBehaviorProfile,
/// so this wrapper maintains that interface while delegating to the generic base.
/// </summary>
[CreateAssetMenu(menuName = "Animals/Wolf Behavior Profile", fileName = "NewWolfBehaviorProfile")]
public class WolfBehaviorProfile : AnimalPredatorBehaviorProfile
{
    // All functionality inherited from AnimalPredatorBehaviorProfile.
    // This class exists purely for backward compatibility with existing serialized assets.
}
