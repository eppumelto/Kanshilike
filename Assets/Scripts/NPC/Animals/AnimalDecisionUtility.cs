using UnityEngine;

public static class AnimalDecisionUtility
{
    public static bool ShouldReactToDetection(Transform detectedTarget)
    {
        return detectedTarget != null;
    }

    public static bool ShouldGiveUpChase(float distanceToTarget, float chaseGiveUpDistance)
    {
        return distanceToTarget > chaseGiveUpDistance;
    }

    public static bool ShouldEnterChasing(AnimalPersonality personality)
    {
        return personality != null && personality.isBrave;
    }
}
