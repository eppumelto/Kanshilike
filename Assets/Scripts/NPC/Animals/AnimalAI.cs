using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// FSM-based predator behaviour.
///
///  Idle ↔ Roaming  (default roaming loop)
///  Any  → Alert    (player spotted)
///  Alert → Fleeing (shy predator) | Chasing (brave predator)
///  Fleeing / Chasing → Idle (player lost)
///
/// Requires: NavMeshAgent on the same GameObject.
/// Assign an AnimalPersonality asset in the Inspector.
/// Player characters need a SelectableCharacter component.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AnimalPredatorAI : MonoBehaviour, ICombatActor, INaturalAttacker
{
    // ------------------------------------------------------------------ //
    //  Inspector
    // ------------------------------------------------------------------ //

    [SerializeField] private AnimalPersonality personality;
    [SerializeField] private AnimalPredatorBehaviorProfile behaviorProfile;
    [SerializeField] private CombatFormulaConfig combatFormulaConfig;
    [SerializeField] private bool retaliateWhenAttacked = true;
    [SerializeField, Min(0f)] private float retaliationMemorySeconds = 8f;
    [Tooltip("Bite/claw attack used instead of the human unarmed values.")]
    [SerializeField] private NaturalAttack naturalAttack = NaturalAttack.Wolf;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // ------------------------------------------------------------------ //
    //  State
    // ------------------------------------------------------------------ //

    private enum PredatorState { Idle, Roaming, Alert, Fleeing, Chasing }

    private PredatorState currentState = PredatorState.Idle;
    private NavMeshAgent agent;

    /// <summary>World position where this predator spawned – used as roam centre.</summary>
    private Vector3 spawnOrigin;

    /// <summary>Currently detected threat (nearest player character in LoS).</summary>
    private Transform detectedTarget;
    private SelectableCharacter detectedCharacter;

    /// <summary>
    /// The character this predator is reacting to. Locked in when first seen and kept while
    /// chasing/fleeing even if it leaves the vision cone; distance thresholds end the reaction.
    /// </summary>
    private Transform threat;
    private SelectableCharacter threatCharacter;
    private readonly AnimalAggroMemory aggroMemory = new AnimalAggroMemory();

    /// <summary>The group this predator belongs to. Null for one placed by hand in a scene.</summary>
    private AnimalGroup group;

    private CharacterStats stats;
    private SkillSet skills;
    private EquipmentManager equipment;
    private readonly CombatSlotReservation reservedTargetSlot = new CombatSlotReservation();
    private float nextAttackTime;

    public Transform ActorTransform => transform;
    public CharacterStats Stats => stats;
    public SkillSet Skills => skills;
    public EquipmentManager Equipment => equipment;
    public bool CanUseEquipment => false;
    public bool CanBlock => personality != null && personality.canBlock;
    public bool CanDodge => personality != null && personality.canDodge;
    public bool CanCounter => personality == null || personality.canCounter;
    public NaturalAttack NaturalAttack => naturalAttack;

    /// <summary>Countdown timer used in the Idle state.</summary>
    private float idleTimer;

    /// <summary>Brief pause before entering Fleeing/Chasing after Alert.</summary>
    private float alertTimer;

    /// <summary>Seconds of full-speed fleeing left. Only used when the personality has a sprint limit.</summary>
    private float sprintSecondsLeft;

    /// <summary>Speed the current state asks for, before wounds slow it down.</summary>
    private float baseSpeed;

    private bool CanTire => personality.fleeSprintSeconds > 0f;
    private float FleeSpeed => CanTire && sprintSecondsLeft <= 0f ? personality.tiredFleeSpeed : personality.reactionSpeed;

    private float AlertDuration => behaviorProfile != null ? behaviorProfile.alertDuration : 0.4f;

    private bool ShouldChaseOnThreat
    {
        get
        {
            if (behaviorProfile != null)
                return behaviorProfile.ShouldChase(personality);

            return AnimalDecisionUtility.ShouldEnterChasing(personality);
        }
    }

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        spawnOrigin = transform.position;
        stats = GetComponent<CharacterStats>();
        skills = GetComponent<SkillSet>();
        equipment = GetComponent<EquipmentManager>();
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.OnDamaged += HandleDamaged;
            stats.OnDied += HandleDied;
        }
    }

    private void Start()
    {
        if (personality == null)
        {
            Debug.LogWarning($"[AnimalPredatorAI] '{name}' has no AnimalPersonality assigned. Disabling.", this);
            enabled = false;
            return;
        }

        if (combatFormulaConfig == null)
        {
            Debug.LogWarning($"[AnimalPredatorAI] '{name}' has no CombatFormulaConfig assigned. Predator will only chase/flee.", this);
        }

        sprintSecondsLeft = personality.fleeSprintSeconds;
        ApplyBaseAgentSettings();
        EnterIdle();
    }

    private void Update()
    {
        if (stats != null && !stats.IsAlive)
            return;

        DetectPlayer();
        TickStamina();
        TickState();

        agent.speed = baseSpeed * (stats != null ? stats.WoundSpeedMultiplier : 1f);
    }

    private void TickStamina()
    {
        if (!CanTire)
            return;

        if (currentState == PredatorState.Fleeing)
        {
            sprintSecondsLeft = Mathf.Max(0f, sprintSecondsLeft - Time.deltaTime);
            baseSpeed =FleeSpeed;
            return;
        }

        float recoveryPerSecond = personality.fleeSprintSeconds / Mathf.Max(0.01f, personality.staminaRecoverySeconds);
        sprintSecondsLeft = Mathf.Min(personality.fleeSprintSeconds, sprintSecondsLeft + recoveryPerSecond * Time.deltaTime);
    }

    // ------------------------------------------------------------------ //
    //  Detection
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Two-phase detection: sphere pre-filter → FOV cone → raycast LoS.
    /// Sets <see cref="detectedTarget"/> to the nearest visible SelectableCharacter,
    /// or null if none are visible.
    /// </summary>
    private void DetectPlayer()
    {
        bool hasAggroMemory = aggroMemory.TryGetLiveTarget(Time.time, out _, out _);

        AnimalDetectionResult detection = AnimalPerceptionUtility.DetectNearestTarget(
            transform,
            personality,
            aggroMemory.Target,
            aggroMemory.Character,
            aggroMemory.Until,
            Time.time);

        detectedTarget = detection.Target;
        detectedCharacter = detection.Character;

        if (detectedTarget == null)
            return;

        SetThreat(detectedTarget, detectedCharacter);

        // Only a real sighting alerts the group. Passing on a remembered alert would let
        // members keep each other's aggro alive forever.
        if (!hasAggroMemory)
            group?.Alert(this, detectedCharacter);
    }

    private void SetThreat(Transform target, SelectableCharacter character)
    {
        threat = target;
        threatCharacter = character;
    }

    // ------------------------------------------------------------------ //
    //  State machine tick
    // ------------------------------------------------------------------ //

    private void TickState()
    {
        switch (currentState)
        {
            case PredatorState.Idle:    TickIdle();    break;
            case PredatorState.Roaming: TickRoaming(); break;
            case PredatorState.Alert:   TickAlert();   break;
            case PredatorState.Fleeing: TickFleeing(); break;
            case PredatorState.Chasing: TickChasing(); break;
        }
    }

    // ------------------------------------------------------------------ //
    //  Idle
    // ------------------------------------------------------------------ //

    private void EnterIdle()
    {
        ReleaseCombatSlot();
        SetThreat(null, null);
        currentState = PredatorState.Idle;
        agent.ResetPath();
        baseSpeed =personality.roamSpeed;
        idleTimer = Random.Range(personality.idleTimeMin, personality.idleTimeMax);
    }

    private void TickIdle()
    {
        AnimalStateTransition transition = AnimalPassiveStateExecutor.TickIdle(detectedTarget, ref idleTimer);
        if (transition == AnimalStateTransition.EnterAlert)
        {
            EnterAlert();
            return;
        }

        if (transition != AnimalStateTransition.EnterRoaming)
            return;

        if (TryGetRoamPoint(out Vector3 roamPoint))
        {
            baseSpeed =personality.roamSpeed;
            agent.SetDestination(roamPoint);
            currentState = PredatorState.Roaming;
            return;
        }

        // Could not find valid NavMesh point – try again next idle cycle.
        EnterIdle();
    }

    // ------------------------------------------------------------------ //
    //  Roaming
    // ------------------------------------------------------------------ //

    private void TickRoaming()
    {
        AnimalStateTransition transition = AnimalPassiveStateExecutor.TickRoaming(detectedTarget, agent);
        if (transition == AnimalStateTransition.EnterAlert)
        {
            EnterAlert();
            return;
        }

        if (transition == AnimalStateTransition.EnterIdle)
            EnterIdle();
    }

    // ------------------------------------------------------------------ //
    //  Alert  (brief pause before deciding)
    // ------------------------------------------------------------------ //

    private void EnterAlert()
    {
        currentState = PredatorState.Alert;
        agent.ResetPath();
        alertTimer = AlertDuration;
    }

    private void TickAlert()
    {
        AnimalStateTransition transition = AnimalPassiveStateExecutor.TickAlert(ref alertTimer, ShouldChaseOnThreat);
        if (transition == AnimalStateTransition.EnterChasing)
            EnterChasing();
        else if (transition == AnimalStateTransition.EnterFleeing)
            EnterFleeing();
    }

    // ------------------------------------------------------------------ //
    //  Fleeing
    // ------------------------------------------------------------------ //

    private void EnterFleeing()
    {
        ReleaseCombatSlot();
        currentState = PredatorState.Fleeing;
        baseSpeed =FleeSpeed;
        agent.acceleration = personality.acceleration;
    }

    private void TickFleeing()
    {
        AnimalStateTransition transition = AnimalPassiveStateExecutor.TickFleeing(transform, threat, agent, personality);
        if (transition == AnimalStateTransition.EnterIdle)
        {
            ApplyBaseAgentSettings();
            EnterIdle();
        }
    }

    // ------------------------------------------------------------------ //
    //  Chasing  (no combat – stops at stopping distance)
    // ------------------------------------------------------------------ //

    private void EnterChasing()
    {
        currentState = PredatorState.Chasing;
        baseSpeed =personality.reactionSpeed;
        agent.acceleration = personality.acceleration;
        nextAttackTime = Time.time;
    }

    private void TickChasing()
    {
        AnimalChaseStepResult result = AnimalChaseCombatExecutor.Execute(
            this,
            gameObject,
            agent,
            personality,
            combatFormulaConfig,
            reservedTargetSlot,
            threat,
            threatCharacter,
            ref nextAttackTime);

        if (result == AnimalChaseStepResult.ReturnToIdle)
        {
            ApplyBaseAgentSettings();
            EnterIdle();
        }
    }

    // ------------------------------------------------------------------ //
    //  Group
    // ------------------------------------------------------------------ //

    /// <summary>Call right after spawning, before Start runs.</summary>
    public void JoinGroup(AnimalGroup newGroup)
    {
        group = newGroup;
        group.Add(this);

        if (group.Personality != null)
            personality = group.Personality;
    }

    /// <summary>A group mate spotted this character, so react as if it had been seen.</summary>
    public void ReceiveGroupAlert(SelectableCharacter character)
    {
        if (character == null || character.Stats == null || !character.Stats.IsAlive)
            return;

        if (personality == null || (stats != null && !stats.IsAlive))
            return;

        // An animal already dealing with a threat keeps it.
        if (currentState != PredatorState.Idle && currentState != PredatorState.Roaming)
            return;

        // Past this distance the animal would drop the reaction right away.
        float reactionRange = ShouldChaseOnThreat ? personality.chaseGiveUpDistance : personality.fleeSafeDistance;
        if (Vector3.Distance(transform.position, character.transform.position) >= reactionRange)
            return;

        // DetectPlayer picks this up next frame and moves the animal to Alert.
        aggroMemory.Set(character, Time.time, retaliationMemorySeconds);
    }

    private void LeaveGroup()
    {
        group?.Remove(this);
        group = null;
    }

    private bool TryGetRoamPoint(out Vector3 roamPoint)
    {
        // With no group mates left there is nobody to stay close to.
        if (group != null && group.MemberCount > 1)
            return group.TryGetRoamPoint(personality.roamRadius, out roamPoint);

        return AnimalNavigationUtility.TryGetRoamPoint(spawnOrigin, personality.roamRadius, out roamPoint);
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private void ApplyBaseAgentSettings()
    {
        baseSpeed =personality.roamSpeed;
        agent.acceleration = personality.acceleration;
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnDamaged -= HandleDamaged;
            stats.OnDied -= HandleDied;
        }

        ReleaseCombatSlot();
        LeaveGroup();
    }

    private void HandleDied(GameObject killer)
    {
        // Update() stops ticking once dead, so free the target's attack slot and stop moving here.
        ReleaseCombatSlot();
        LeaveGroup();
        aggroMemory.Clear();
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    private void HandleDamaged(float damage, GameObject source)
    {
        if (!retaliateWhenAttacked || source == null || (stats != null && !stats.IsAlive))
            return;

        SelectableCharacter attacker = source.GetComponent<SelectableCharacter>();
        if (attacker == null)
            attacker = source.GetComponentInParent<SelectableCharacter>();
        if (attacker == null)
            attacker = source.GetComponentInChildren<SelectableCharacter>();

        if (attacker == null || attacker.Stats == null || !attacker.Stats.IsAlive)
            return;

        detectedCharacter = attacker;
        detectedTarget = attacker.transform;
        SetThreat(attacker.transform, attacker);
        aggroMemory.Set(attacker, Time.time, retaliationMemorySeconds);
        group?.Alert(this, attacker);

        // Getting hit should interrupt non-combat states and enforce short retaliatory aggro.
        if (behaviorProfile == null || behaviorProfile.interruptToChaseOnRetaliation)
            EnterChasing();
    }

    private void ReleaseCombatSlot()
    {
        reservedTargetSlot.Release(gameObject);
    }

    // ------------------------------------------------------------------ //
    //  Gizmos
    // ------------------------------------------------------------------ //

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmos || personality == null) return;

        // Detection sphere
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, personality.detectionRange);

        // FOV cone lines
        Gizmos.color = Color.yellow;
        Quaternion leftRay  = Quaternion.AngleAxis(-personality.fieldOfViewAngle, Vector3.up);
        Quaternion rightRay = Quaternion.AngleAxis( personality.fieldOfViewAngle, Vector3.up);
        Vector3 fwd = transform.forward * personality.detectionRange;
        Gizmos.DrawRay(transform.position, leftRay  * fwd);
        Gizmos.DrawRay(transform.position, rightRay * fwd);

        // Roam radius around spawn (editor-time approximation)
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Vector3 origin = Application.isPlaying ? spawnOrigin : transform.position;
        Gizmos.DrawSphere(origin, personality.roamRadius);

        // State label
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.2f,
            Application.isPlaying ? currentState.ToString() : "AnimalPredatorAI"
        );
    }
#endif
}
