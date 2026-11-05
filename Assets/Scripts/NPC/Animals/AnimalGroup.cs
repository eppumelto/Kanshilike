using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pack or herd created by AnimalSpawner. Members roam toward a shared waypoint and alert
/// each other to threats. A solitary animal is a group of one.
/// </summary>
public sealed class AnimalGroup
{
    private readonly List<AnimalAI> members = new List<AnimalAI>();
    private readonly Vector3 home;
    private readonly float memberSpread;
    private readonly float waypointInterval;

    private Vector3 waypoint;
    private float nextWaypointTime;

    /// <summary>The personality every member uses, so a group doesn't split into chasers and fleers. Null keeps each member's own.</summary>
    public AnimalPersonality Personality { get; }
    public int MemberCount => members.Count;

    public AnimalGroup(Vector3 home, AnimalPersonality personality, float memberSpread, float waypointInterval)
    {
        this.home = home;
        this.memberSpread = memberSpread;
        this.waypointInterval = waypointInterval;
        Personality = personality;
        waypoint = home;
    }

    public void Add(AnimalAI member)
    {
        if (!members.Contains(member))
            members.Add(member);
    }

    public void Remove(AnimalAI member)
    {
        members.Remove(member);
    }

    /// <summary>A point near the group's current waypoint. The waypoint moves around the group's home every so often.</summary>
    public bool TryGetRoamPoint(float roamRadius, out Vector3 destination)
    {
        if (Time.time >= nextWaypointTime
            && AnimalNavigationUtility.TryGetRoamPoint(home, roamRadius, out Vector3 newWaypoint))
        {
            waypoint = newWaypoint;
            nextWaypointTime = Time.time + waypointInterval;
        }

        return AnimalNavigationUtility.TryGetRoamPoint(waypoint, memberSpread, out destination);
    }

    /// <summary>Tells the other members about a character one member has seen or been hit by.</summary>
    public void Alert(AnimalAI sender, CombatActor character)
    {
        foreach (AnimalAI member in members)
        {
            if (member != sender)
                member.ReceiveGroupAlert(character);
        }
    }
}
