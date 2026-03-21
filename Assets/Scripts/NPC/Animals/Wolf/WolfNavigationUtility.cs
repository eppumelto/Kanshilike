using UnityEngine;
using UnityEngine.AI;

public static class WolfNavigationUtility
{
    public static bool TryGetRoamPoint(Vector3 spawnOrigin, float roamRadius, out Vector3 destination)
    {
        Vector3 randomPoint = spawnOrigin + Random.insideUnitSphere * roamRadius;
        randomPoint.y = spawnOrigin.y;

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
        {
            destination = hit.position;
            return true;
        }

        destination = default;
        return false;
    }

    public static bool TryGetFleePoint(Vector3 selfPosition, Vector3 threatPosition, float fleeDistance, out Vector3 destination)
    {
        Vector3 fleeDir = (selfPosition - threatPosition).normalized;
        Vector3 fleeTarget = selfPosition + fleeDir * fleeDistance;

        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            destination = hit.position;
            return true;
        }

        destination = default;
        return false;
    }
}
