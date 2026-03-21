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
public class AnimalPredatorAI : MonoBehaviour, ICombatActor
{
    // ------------------------------------------------------------------ //
    //  Inspector
    // ------------------------------------------------------------------ //

    [SerializeField] private AnimalPersonality personality;
    [SerializeField] private AnimalPredatorBehaviorProfile behaviorProfile;
    [SerializeField] private CombatFormulaConfig combatFormulaConfig;
    [SerializeField] private bool retaliateWhenAttacked = true;
    [SerializeField, Min(0f)] private float retaliationMemorySeconds = 8f;

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
    private readonly WolfAggroMemory aggroMemory = new WolfAggroMemory();

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
    public bool CanCounter => personality == null || personality.canCounter;

    /// <summary>Countdown timer used in the Idle state.</summary>
    private float idleTimer;

    /// <summary>Brief pause before entering Fleeing/Chasing after Alert.</summary>
    private float alertTimer;

    private float AlertDuration => behaviorProfile != null ? behaviorProfile.alertDuration : 0.4f;

    private bool ShouldChaseOnThreat
    {
        get
        {
            if (behaviorProfile != null)
                return behaviorProfile.ShouldChase(personality);

            return WolfDecisionUtility.ShouldEnterChasing(personality);
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

        if (stats != null)
            stats.OnDamaged += HandleDamaged;

        ApplyBaseAgentSettings();
        EnterIdle();
    }

    private void Update()
    {
        if (stats != null && !stats.IsAlive)
            return;

        DetectPlayer();
        TickState();
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
        WolfDetectionResult detection = WolfPerceptionUtility.DetectNearestTarget(
            transform,
            personality,
            aggroMemory.Target,
            aggroMemory.Character,
            aggroMemory.Until,
            Time.time);

        aggroMemory.TryGetLiveTarget(Time.time, out _, out _);

        detectedTarget = detection.Target;
        detectedCharacter = detection.Character;
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
        currentState = PredatorState.Idle;
        agent.ResetPath();
        agent.speed = personality.roamSpeed;
        idleTimer = Random.Range(personality.idleTimeMin, personality.idleTimeMax);
    }

    private void TickIdle()
    {
        WolfStateTransition transition = WolfPassiveStateExecutor.TickIdle(detectedTarget, ref idleTimer);
        if (transition == WolfStateTransition.EnterAlert)
        {
            EnterAlert();
            return;
        }

        if (transition != WolfStateTransition.EnterRoaming)
            return;

        if (WolfNavigationUtility.TryGetRoamPoint(spawnOrigin, personality.roamRadius, out Vector3 roamPoint))
        {
            agent.speed = personality.roamSpeed;
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
        WolfStateTransition transition = WolfPassiveStateExecutor.TickRoaming(detectedTarget, agent);
        if (transition == WolfStateTransition.EnterAlert)
        {
            EnterAlert();
            return;
        }

        if (transition == WolfStateTransition.EnterIdle)
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
        WolfStateTransition transition = WolfPassiveStateExecutor.TickAlert(ref alertTimer, ShouldChaseOnThreat);
        if (transition == WolfStateTransition.EnterChasing)
            EnterChasing();
        else if (transition == WolfStateTransition.EnterFleeing)
            EnterFleeing();
    }

    // ------------------------------------------------------------------ //
    //  Fleeing
    // ------------------------------------------------------------------ //

    private void EnterFleeing()
    {
        ReleaseCombatSlot();
        currentState = PredatorState.Fleeing;
        agent.speed = personality.reactionSpeed;
        agent.acceleration = personality.acceleration;
    }

    private void TickFleeing()
    {
        WolfStateTransition transition = WolfPassiveStateExecutor.TickFleeing(transform, detectedTarget, agent, personality);
        if (transition == WolfStateTransition.EnterIdle)
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
        agent.speed = personality.reactionSpeed;
        agent.acceleration = personality.acceleration;
        nextAttackTime = Time.time;
    }

    private void TickChasing()
    {
        WolfChaseStepResult result = WolfChaseCombatExecutor.Execute(
            this,
            gameObject,
            agent,
            personality,
            combatFormulaConfig,
            reservedTargetSlot,
            detectedTarget,
            detectedCharacter,
            ref nextAttackTime);

        if (result == WolfChaseStepResult.ReturnToIdle)
        {
            ApplyBaseAgentSettings();
            EnterIdle();
        }
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private void ApplyBaseAgentSettings()
    {
        agent.speed = personality.roamSpeed;
        agent.acceleration = personality.acceleration;
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.OnDamaged -= HandleDamaged;

        ReleaseCombatSlot();
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
        aggroMemory.Set(attacker, Time.time, retaliationMemorySeconds);

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
