using UnityEngine;
using UnityEngine.AI;

public enum AnimalStateTransition
{
    None,
    EnterIdle,
    EnterRoaming,
    EnterAlert,
    EnterFleeing,
    EnterChasing
}

public static class AnimalPassiveStateExecutor
{
    public static AnimalStateTransition TickIdle(Transform detectedTarget, ref float idleTimer)
    {
        if (AnimalDecisionUtility.ShouldReactToDetection(detectedTarget))
            return AnimalStateTransition.EnterAlert;

        idleTimer -= Time.deltaTime;
        return idleTimer <= 0f ? AnimalStateTransition.EnterRoaming : AnimalStateTransition.None;
    }

    public static AnimalStateTransition TickRoaming(Transform detectedTarget, NavMeshAgent agent)
    {
        if (AnimalDecisionUtility.ShouldReactToDetection(detectedTarget))
            return AnimalStateTransition.EnterAlert;

        bool reachedDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
        return reachedDestination ? AnimalStateTransition.EnterIdle : AnimalStateTransition.None;
    }

    public static AnimalStateTransition TickAlert(ref float alertTimer, bool shouldChaseOnThreat)
    {
        alertTimer -= Time.deltaTime;
        if (alertTimer > 0f)
            return AnimalStateTransition.None;

        return shouldChaseOnThreat ? AnimalStateTransition.EnterChasing : AnimalStateTransition.EnterFleeing;
    }

    public static AnimalStateTransition TickFleeing(
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
                && AnimalNavigationUtility.TryGetFleePoint(self.position, threat.position, personality.fleeDistance, out Vector3 fleeDestination))
                agent.SetDestination(fleeDestination);

            return AnimalStateTransition.None;
        }

        return reachedDestination ? AnimalStateTransition.EnterIdle : AnimalStateTransition.None;
    }
}
