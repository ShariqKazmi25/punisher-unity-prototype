using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PunisherMovementBeforeStopSync : MonoBehaviour
{
    public enum MovementState { Idle, Walking, Running }

    [Header("References")]
    public CharacterController controller;
    public Animator animator;
    public Transform cameraTransform;

    [Header("Walking Only")]
    [Tooltip("Ignore sprint input while tuning Idle -> Start Walking -> Walk -> Stop.")]
    public bool walkingOnly = true;
    [Tooltip("Use the measured in-place walking speed for the new Mixamo clip instead of the old walkSpeed value.")]
    public bool useCalibratedWalkingSpeed = true;
    [Tooltip("Initial planted-foot speed estimate for the current Rifle Idle Avatar at its current scale. Retune if model scale or animation playback speed changes.")]
    [Min(0f)] public float calibratedWalkingSpeed = 0.6222f;

    [Header("Movement")]
    [Min(0f)] public float walkSpeed = 1.5f;
    [Min(0f)] public float runSpeed = 4.5f;
    [Min(0f)] public float rotationSpeed = 10f;
    [Tooltip("Fallback walk-start duration when Animator timing is unavailable.")]
    [Min(0f)] public float accelerationTime = 0.25f;
    [Tooltip("Fallback walking-stop duration, capped by Walk Braking Time.")]
    [Min(0f)] public float stoppingTime = 2.5843664f;
    [Tooltip("Fallback run-start duration when Animator timing is unavailable.")]
    [Min(0f)] public float runAccelerationTime = 0.6006818f;
    [Tooltip("Fallback full-stop duration when Animator timing is unavailable.")]
    [Min(0f)] public float runStoppingTime = 1.7833334f;

    [Header("Walk / Run Blends")]
    [Tooltip("Seconds to reach running speed when Left Shift is pressed during walking.")]
    [Min(0f)] public float walkToRunTime = 0.5784592f;
    [Tooltip("Seconds to reach walking speed when Left Shift is released while W stays held.")]
    [Min(0f)] public float runToWalkTime = 0.9270649f;

    [Header("Walking Animation Timing")]
    [Tooltip("Use the active Animator timing for walking starts and restarts. Stop blends can shorten braking, but cannot extend Walk Braking Time.")]
    public bool matchWalkingAnimationTiming = true;
    [Tooltip("Start Walking -> Rifle Walk exit time. Match this to the Animator; durations come from the imported clip, not the Mixamo trim counter.")]
    [Range(0.01f, 1f)] public float walkStartEndNormalizedTime = 0.9f;
    [Tooltip("Maximum seconds of forward braking after W is released while walking. The stop pose can continue after movement ends. Set to 0 for an immediate stop.")]
    [Min(0f)] public float walkBrakingTime = 0.08f;

    [Header("Run Animation Timing")]
    [Tooltip("Use the active Animator blend duration for running speed changes. The time fields above are fallbacks.")]
    public bool matchRunAnimationTiming = true;
    [Tooltip("Running To Stop -> Idle exit time. Stop moving by this point in the braking clip.")]
    [Range(0f, 1f)] public float runStopEndNormalizedTime = 0.8770492f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    [Header("Animation Diagnostics")]
    [Tooltip("Log idle/walk/run input changes and both Animator booleans.")]
    public bool logAnimationInput;

    private static readonly int IsWalking = Animator.StringToHash("iswalking");
    private static readonly int IsRunning = Animator.StringToHash("isrunning");
    private static readonly int RunState = Animator.StringToHash("Rifle Run");
    private static readonly int WalkState = Animator.StringToHash("Rifle Walk");
    private static readonly int StartWalkState = Animator.StringToHash("Start Walking");
    private static readonly int WalkStopState = Animator.StringToHash("Rifle Walk To Stop");
    private static readonly int RunStopState = Animator.StringToHash("Running To Stop");
    private float currentSpeed;
    private float targetSpeed;
    private float rampStartSpeed;
    private float rampDuration;
    private float rampElapsed;
    private bool easeSpeedRamp;
    private int synchronizedSource;
    private int synchronizedDestination;
    private int synchronizedTransition;
    private float lastTransitionProgress = -1f;
    private float verticalVelocity;
    private Vector3 moveDirection;
    private bool hasWalkingParameter;
    private bool hasRunningParameter;
    private Animator validatedAnimator;
    private RuntimeAnimatorController validatedController;
    private MovementState speedState;
    private float importedWalkStartDuration;

    // This is the requested input state, not the current animation state.
    // A stopping animation may continue after input returns to Idle.
    public MovementState RequestedState { get; private set; }
    public float EffectiveWalkSpeed => Mathf.Max(0f,
        walkingOnly && useCalibratedWalkingSpeed ? calibratedWalkingSpeed : walkSpeed);

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (controller == null)
        {
            Debug.LogError("PunisherMovement needs a CharacterController.", this);
            enabled = false;
            return;
        }

        // Do not discard small movement steps while accelerating.
        controller.minMoveDistance = 0f;
        moveDirection = Vector3.ProjectOnPlane(controller.transform.forward, Vector3.up).normalized;
    }

    private void OnEnable()
    {
        RequestedState = MovementState.Idle;
        speedState = MovementState.Idle;
        ConfigureAnimator();
        SetAnimationParameters(RequestedState);
    }

    private void ConfigureAnimator()
    {
        hasWalkingParameter = false;
        hasRunningParameter = false;
        validatedAnimator = animator;
        validatedController = animator != null ? animator.runtimeAnimatorController : null;
        importedWalkStartDuration = 0f;
        ClearTimingSynchronization();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("Assign an Animator with the Punisher controller. Movement will still work.", this);
            return;
        }

        // CharacterController owns translation; Animator owns the pose.
        animator.applyRootMotion = false;
        animator.enabled = true;
        animator.speed = 1f;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Use exported clip durations even before the first transition becomes
        // observable. The Mixamo trim counter is not a reliable seconds value.
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "Start Walking")
                importedWalkStartDuration = clip.length;
        }

        int startPath = Animator.StringToHash(animator.GetLayerName(0) + ".Start Walking");
        if (walkingOnly && !animator.HasState(0, startPath))
            Debug.LogWarning("Walking-only setup needs Start Walking in the base layer. Connect Idle -> Start Walking -> Rifle Walk and Stop -> Start Walking; the saved Animator currently bypasses this start phase.", this);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool)
                continue;
            if (parameter.nameHash == IsWalking)
                hasWalkingParameter = true;
            if (parameter.nameHash == IsRunning)
                hasRunningParameter = true;
        }

        if (!hasWalkingParameter)
            Debug.LogError("The assigned Animator needs a Bool named 'iswalking'.", this);
        if (!hasRunningParameter && !walkingOnly)
            Debug.LogError("The assigned Animator needs a Bool named 'isrunning'. Add it and save the controller to enable run transitions.", this);
    }

    public static MovementState ResolveMovementState(bool forwardHeld, bool sprintHeld)
    {
        if (!forwardHeld)
            return MovementState.Idle;
        return sprintHeld ? MovementState.Running : MovementState.Walking;
    }

    private void SetAnimationParameters(MovementState state)
    {
        if (animator == null)
            return;

        // iswalking means forward input, so it stays true while running.
        // Both parameters are written in the same frame before Animator evaluation.
        if (hasWalkingParameter)
            animator.SetBool(IsWalking, state != MovementState.Idle);
        if (hasRunningParameter)
            animator.SetBool(IsRunning, !walkingOnly && state == MovementState.Running);
    }

    private void Update()
    {
        // Revalidate if the Animator/controller is reassigned during Play Mode.
        if (animator != validatedAnimator ||
            (animator != null && animator.runtimeAnimatorController != validatedController))
            ConfigureAnimator();

        Keyboard keyboard = Keyboard.current;
        bool forwardHeld = Application.isFocused && keyboard != null && keyboard.wKey.isPressed;
        bool sprintHeld = !walkingOnly && keyboard != null && keyboard.leftShiftKey.isPressed;
        MovementState previousState = RequestedState;
        RequestedState = ResolveMovementState(forwardHeld, sprintHeld);

        // Walking-only: Idle -> Start Walking -> Rifle Walk -> Walk To Stop.
        // Start/stop interruptions are owned by the Animator, not forced Plays.
        SetAnimationParameters(RequestedState);

        if (logAnimationInput && RequestedState != previousState)
        {
            string target = animator != null ? animator.name : "missing";
            string walking = hasWalkingParameter ? animator.GetBool(IsWalking).ToString() : "missing";
            string running = hasRunningParameter ? animator.GetBool(IsRunning).ToString() : "missing";
            Debug.Log($"PunisherMovement: {RequestedState}, Animator={target}, iswalking={walking}, isrunning={running}", this);
        }

        if (controller == null || !controller.enabled)
            return;

        if (RequestedState != MovementState.Idle)
        {
            Vector3 forward = cameraTransform != null
                ? cameraTransform.forward : controller.transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);

            // Looking straight up/down must not prevent movement.
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(controller.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f)
                moveDirection = forward.normalized;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                controller.transform.rotation = Quaternion.Slerp(
                    controller.transform.rotation,
                    Quaternion.LookRotation(moveDirection),
                    1f - Mathf.Exp(-Mathf.Max(0f, rotationSpeed) * Time.deltaTime));
            }
        }

        // Speed ramps use the active animation's timing, but advance on elapsed
        // time so a stalled/missing animation cannot permanently freeze movement.
        UpdateMovementSpeed(RequestedState, Time.deltaTime);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedGravity;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * currentSpeed + Vector3.up * verticalVelocity;
        CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void UpdateMovementSpeed(MovementState state, float deltaTime)
    {
        float walkingSpeed = EffectiveWalkSpeed;
        float runningSpeed = Mathf.Max(walkingSpeed, runSpeed);
        float nextTarget = state == MovementState.Running ? runningSpeed
            : state == MovementState.Walking ? walkingSpeed : 0f;

        if (state != speedState || nextTarget != targetSpeed)
        {
            float duration;
            if (state == MovementState.Running)
                duration = speedState == MovementState.Walking ? walkToRunTime : runAccelerationTime;
            else if (state == MovementState.Walking)
                duration = currentSpeed > walkingSpeed ? runToWalkTime : accelerationTime;
            else if (speedState == MovementState.Running || currentSpeed > walkingSpeed)
                duration = runStoppingTime;
            else
                duration = Mathf.Min(stoppingTime, walkBrakingTime);

            // Starting from rest must not use the old short Idle -> Walk ramp
            // while waiting a frame for the Animator to enter Start Walking.
            if (walkingOnly && matchWalkingAnimationTiming)
            {
                if (state == MovementState.Walking && currentSpeed <= walkingSpeed && importedWalkStartDuration > 0f)
                    duration = importedWalkStartDuration * walkStartEndNormalizedTime;
            }

            targetSpeed = nextTarget;
            BeginSpeedRamp(duration, true);
            ClearTimingSynchronization();
            speedState = state;
        }

        SynchronizeAnimationSpeedRamp(state);
        rampElapsed += Mathf.Max(0f, deltaTime);
        float progress = rampDuration > 0f ? Mathf.Clamp01(rampElapsed / rampDuration) : 1f;
        if (easeSpeedRamp)
            progress = Mathf.SmoothStep(0f, 1f, progress);
        currentSpeed = Mathf.Lerp(rampStartSpeed, targetSpeed, progress);
    }

    private void BeginSpeedRamp(float duration, bool eased)
    {
        // Capture current momentum once, including when a stop is interrupted.
        rampStartSpeed = currentSpeed;
        rampDuration = Mathf.Max(0f, duration);
        rampElapsed = 0f;
        easeSpeedRamp = eased;
    }

    private void SynchronizeAnimationSpeedRamp(MovementState requested)
    {
        if ((!matchRunAnimationTiming && !matchWalkingAnimationTiming) || animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null || !animator.IsInTransition(0))
        {
            ClearTimingSynchronization();
            return;
        }

        AnimatorStateInfo source = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo destination = animator.GetNextAnimatorStateInfo(0);
        bool enteringRun = matchRunAnimationTiming && requested == MovementState.Running && destination.shortNameHash == RunState;
        bool fromRunning = source.shortNameHash == RunState || source.shortNameHash == RunStopState;
        bool slowingToWalk = matchRunAnimationTiming && requested == MovementState.Walking && destination.shortNameHash == WalkState && fromRunning;
        bool runBraking = matchRunAnimationTiming && requested == MovementState.Idle && destination.shortNameHash == RunStopState;
        // An interrupted Walk -> Stop blend can still report Walk as its
        // current state when it blends back to Walk. Use the actual destination
        // rather than requiring the Stop state to become current first.
        bool enteringWalk = matchWalkingAnimationTiming && requested == MovementState.Walking &&
            destination.shortNameHash == WalkState && !fromRunning;
        bool walkBraking = matchWalkingAnimationTiming && requested == MovementState.Idle && destination.shortNameHash == WalkStopState;
        bool startingWalk = matchWalkingAnimationTiming && requested == MovementState.Walking &&
            destination.shortNameHash == StartWalkState;
        if (!enteringRun && !slowingToWalk && !runBraking && !enteringWalk && !walkBraking && !startingWalk)
            return;

        AnimatorTransitionInfo transition = animator.GetAnimatorTransitionInfo(0);
        float progress = Mathf.Clamp01(transition.normalizedTime);
        bool sameBlend = synchronizedSource == source.fullPathHash &&
            synchronizedDestination == destination.fullPathHash &&
            synchronizedTransition == transition.fullPathHash && lastTransitionProgress >= 0f &&
            progress + 0.0001f >= lastTransitionProgress;
        lastTransitionProgress = progress;
        if (sameBlend)
            return;

        synchronizedSource = source.fullPathHash;
        synchronizedDestination = destination.fullPathHash;
        synchronizedTransition = transition.fullPathHash;

        float animatorSpeed = Mathf.Max(0.0001f, Mathf.Abs(animator.speed));
        float duration = transition.duration;
        if (transition.durationUnit == DurationUnit.Normalized)
            duration *= source.length;
        float remaining = duration * (1f - progress) / animatorSpeed;

        if (walkBraking)
        {
            // Braking starts on input release, before Animator evaluation exposes
            // the stop blend. Preserve that deadline instead of restarting the
            // ramp for the blend duration or the stop clip's settling poses.
            remaining = Mathf.Min(remaining, Mathf.Max(0f, rampDuration - rampElapsed));
        }
        else if (runBraking || startingWalk)
        {
            // Start and stop motion spans the clip, not just its short entry
            // blend. Read the actual imported duration, including entry offset.
            float endTime = startingWalk ? walkStartEndNormalizedTime : runStopEndNormalizedTime;
            float stopRemaining = Mathf.Max(0f, endTime - destination.normalizedTime) *
                destination.length / animatorSpeed;
            remaining = Mathf.Max(remaining, stopRemaining);
        }

        BeginSpeedRamp(remaining, true);
    }

    private void ClearTimingSynchronization()
    {
        synchronizedSource = 0;
        synchronizedDestination = 0;
        synchronizedTransition = 0;
        lastTransitionProgress = -1f;
    }

    private void OnDisable()
    {
        currentSpeed = 0f;
        targetSpeed = 0f;
        rampStartSpeed = 0f;
        rampDuration = 0f;
        rampElapsed = 0f;
        easeSpeedRamp = false;
        ClearTimingSynchronization();
        speedState = MovementState.Idle;
        RequestedState = MovementState.Idle;
        verticalVelocity = 0f;
        SetAnimationParameters(RequestedState);
    }
}

