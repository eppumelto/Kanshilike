using UnityEngine;

public sealed class CombatActorProxy : ICombatActor
{
    public Transform ActorTransform { get; }
    public CharacterStats Stats { get; }
    public SkillSet Skills { get; }
    public EquipmentManager Equipment { get; }
    public bool CanUseEquipment { get; }
    public bool CanBlock { get; }
    public bool CanCounter { get; }

    public CombatActorProxy(
        Transform actorTransform,
        CharacterStats stats,
        SkillSet skills,
        EquipmentManager equipment,
        bool canUseEquipment,
        bool canBlock,
        bool canCounter)
    {
        ActorTransform = actorTransform;
        Stats = stats;
        Skills = skills;
        Equipment = equipment;
        CanUseEquipment = canUseEquipment;
        CanBlock = canBlock;
        CanCounter = canCounter;
    }
}
