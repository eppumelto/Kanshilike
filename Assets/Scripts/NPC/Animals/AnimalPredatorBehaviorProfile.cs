using UnityEngine;

public enum PredatorThreatResponseMode
{
    UsePersonality,
    AlwaysChase,
    AlwaysFlee
}

[CreateAssetMenu(menuName = "Animals/Predator Behavior Profile", fileName = "NewPredatorBehaviorProfile")]
public class AnimalPredatorBehaviorProfile : ScriptableObject
{
    [Header("State Timings")]
    [Min(0f)] public float alertDuration = 0.4f;

    [Header("Retaliation")]
    public bool interruptToChaseOnRetaliation = true;

    [Header("Threat Response")]
    public PredatorThreatResponseMode threatResponseMode = PredatorThreatResponseMode.UsePersonality;

    public bool ShouldChase(AnimalPersonality personality)
    {
        switch (threatResponseMode)
        {
            case PredatorThreatResponseMode.AlwaysChase:
                return true;
            case PredatorThreatResponseMode.AlwaysFlee:
                return false;
            default:
                return personality != null && personality.isBrave;
        }
    }
}
