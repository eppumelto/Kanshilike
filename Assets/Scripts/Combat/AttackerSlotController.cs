using System.Collections.Generic;
using UnityEngine;

public class AttackerSlotController : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxActiveAttackers = 2;

    private readonly HashSet<GameObject> activeAttackers = new HashSet<GameObject>();
    private readonly Queue<GameObject> queuedAttackers = new Queue<GameObject>();

    public int ActiveCount => activeAttackers.Count;
    public int QueueCount => queuedAttackers.Count;

    public bool IsActiveAttacker(GameObject attacker)
    {
        return attacker != null && activeAttackers.Contains(attacker);
    }

    public bool TryEnterCombat(GameObject attacker)
    {
        if (attacker == null)
            return false;

        if (activeAttackers.Contains(attacker))
            return true;

        if (activeAttackers.Count < maxActiveAttackers)
        {
            activeAttackers.Add(attacker);
            return true;
        }

        if (!queuedAttackers.Contains(attacker))
            queuedAttackers.Enqueue(attacker);

        return false;
    }

    public GameObject ExitCombat(GameObject attacker)
    {
        if (attacker != null)
            activeAttackers.Remove(attacker);

        while (queuedAttackers.Count > 0)
        {
            GameObject next = queuedAttackers.Dequeue();
            if (next == null)
                continue;

            activeAttackers.Add(next);
            return next;
        }

        return null;
    }

    public void RemoveQueued(GameObject attacker)
    {
        if (attacker == null || queuedAttackers.Count == 0)
            return;

        Queue<GameObject> rebuilt = new Queue<GameObject>(queuedAttackers.Count);
        while (queuedAttackers.Count > 0)
        {
            GameObject current = queuedAttackers.Dequeue();
            if (current != attacker)
                rebuilt.Enqueue(current);
        }

        while (rebuilt.Count > 0)
            queuedAttackers.Enqueue(rebuilt.Dequeue());
    }
}
