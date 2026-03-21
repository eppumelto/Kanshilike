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
        Transform detectedTarget,
        NavMeshAgent agent,
        AnimalPersonality personality)
    {
        if (detectedTarget != null)
        {
            if (WolfNavigationUtility.TryGetFleePoint(self.position, detectedTarget.position, personality.fleeDistance, out Vector3 fleeDestination))
                agent.SetDestination(fleeDestination);

            return WolfStateTransition.None;
        }

        bool reachedDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
        return reachedDestination ? WolfStateTransition.EnterIdle : WolfStateTransition.None;
    }
}
