using UnityEngine;
using UnityEngine.AI;

public enum WolfStateTransition
{
    None,
    EnterIdle,
    EnterRoaming,
    EnterAlert,
    EnterFleeing,
    EnterChasing
}

public static class WolfPassiveStateExecutor
{
    public static WolfStateTransition TickIdle(Transform detectedTarget, ref float idleTimer)
    {
        if (WolfDecisionUtility.ShouldReactToDetection(detectedTarget))
            return WolfStateTransition.EnterAlert;

        idleTimer -= Time.deltaTime;
        return idleTimer <= 0f ? WolfStateTransition.EnterRoaming : WolfStateTransition.None;
    }

    public static WolfStateTransition TickRoaming(Transform detectedTarget, NavMeshAgent agent)
    {
        if (WolfDecisionUtility.ShouldReactToDetection(detectedTarget))
            return WolfStateTransition.EnterAlert;

        bool reachedDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
        return reachedDestination ? WolfStateTransition.EnterIdle : WolfStateTransition.None;
    }

    public static WolfStateTransition TickAlert(ref float alertTimer, bool shouldChaseOnThreat)
    {
        alertTimer -= Time.deltaTime;
        if (alertTimer > 0f)
            return WolfStateTransition.None;

        return shouldChaseOnThreat ? WolfStateTransition.EnterChasing : WolfStateTransition.EnterFleeing;
    }

    public static WolfStateTransition TickFleeing(
        Transform self,
        Transform threat,
        NavMeshAgent agent,
        AnimalPersonality personality)
    {
        bool reachedDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

        // Keep running until the threat is at least fleeSafeDistance away, whether or not it is
        // still in view (a fleeing animal faces away from what it is running from).
        bool stillInDanger = threat != null
            && Vector3.Distance(self.position, threat.position) < personality.fleeSafeDistance;

        if (stillInDanger)
        {
            // Only pick a new flee point once the current one is reached, rather than every frame.
            if ((reachedDestination || !agent.hasPath)
                && WolfNavigationUtility.TryGetFleePoint(self.position, threat.position, personality.fleeDistance, out Vector3 fleeDestination))
                agent.SetDestination(fleeDestination);

            return WolfStateTransition.None;
        }

        return reachedDestination ? WolfStateTransition.EnterIdle : WolfStateTransition.None;
    }
}
