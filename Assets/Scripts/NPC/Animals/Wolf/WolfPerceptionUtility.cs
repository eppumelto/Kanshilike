using UnityEngine;

public readonly struct WolfDetectionResult
{
    public Transform Target { get; }
    public SelectableCharacter Character { get; }

    public WolfDetectionResult(Transform target, SelectableCharacter character)
    {
        Target = target;
        Character = character;
    }
}

public static class WolfPerceptionUtility
{
    public static WolfDetectionResult DetectNearestTarget(
        Transform self,
        AnimalPersonality personality,
        Transform forcedAggroTarget,
        SelectableCharacter forcedAggroCharacter,
        float forcedAggroUntil,
        float now)
    {
        Transform nearest = null;
        float nearestSqDist = float.MaxValue;

        Collider[] nearby = Physics.OverlapSphere(self.position, personality.detectionRange, personality.detectionMask);
        foreach (Collider col in nearby)
        {
            if (!col.TryGetComponent<SelectableCharacter>(out _))
                continue;

            Vector3 dirToTarget = col.transform.position - self.position;
            float sqDist = dirToTarget.sqrMagnitude;

            float angle = Vector3.Angle(self.forward, dirToTarget);
            if (angle > personality.fieldOfViewAngle)
                continue;

            Vector3 eyePos = self.position + Vector3.up * personality.eyeHeight;
            Vector3 targetCenter = col.bounds.center;

            if (!Physics.Raycast(eyePos, targetCenter - eyePos, out RaycastHit hit, personality.detectionRange, personality.detectionMask))
                continue;

            if (hit.collider != col)
                continue;

            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest = col.transform;
            }
        }

        bool hasForcedAggro = forcedAggroTarget != null
            && now < forcedAggroUntil
            && forcedAggroCharacter != null
            && forcedAggroCharacter.Stats != null
            && forcedAggroCharacter.Stats.IsAlive;

        if (hasForcedAggro)
            nearest = forcedAggroTarget;

        SelectableCharacter character = nearest != null ? nearest.GetComponent<SelectableCharacter>() : null;
        return new WolfDetectionResult(nearest, character);
    }
}
