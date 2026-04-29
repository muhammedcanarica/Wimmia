using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerDash))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerKnockback))]
[RequireComponent(typeof(PlayerHitStop))]
[RequireComponent(typeof(PlayerDashTrailFeedback))]
public class PlayerController : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerDash dash;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerKnockback knockback;

    [Header("Optional Feedback")]
    [SerializeField] private PlayerHitStop hitStop;
    [SerializeField] private PlayerDashTrailFeedback dashTrailFeedback;

    public PlayerMode currentMode => stateMachine != null ? stateMachine.CurrentMode : PlayerMode.Land;
    public bool IsDashing => stateMachine != null && stateMachine.IsInState(PlayerMotionState.Dash);
    public Vector2 FacingDirection => movement != null ? movement.FacingDirection : Vector2.right;

    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }

    private void Awake()
    {
        CacheDependencies();

        if (inputReader == null ||
            movement == null ||
            dash == null ||
            stateMachine == null ||
            knockback == null)
        {
            Debug.LogError($"[{nameof(PlayerController)}] Missing required player components on '{name}'.", this);
            enabled = false;
            return;
        }

        movement.Initialize(stateMachine);
        dash.Initialize(stateMachine, movement);
        knockback.Initialize(stateMachine);

        stateMachine.ChangeState(PlayerMotionState.Idle, true);
    }

    private void Update()
    {
        dash.TickTimers(Time.deltaTime);
        UpdateLocomotionState();
    }

    private void FixedUpdate()
    {
        bool movementBlocked = dash.IsDashing || knockback.IsActive;
        movement.FixedPrepare(movementBlocked);

        if (knockback.IsActive)
        {
            knockback.FixedStep();
            stateMachine.ChangeState(PlayerMotionState.Knockback);
            return;
        }

        HandleActionRequests();

        if (dash.IsDashing)
        {
            dash.FixedStep();
            stateMachine.ChangeState(PlayerMotionState.Dash);
            return;
        }

        movement.Move(inputReader.MoveInput);
        UpdateLocomotionState();
    }

    public void ApplyModeProperties(PlayerMode mode, bool forceInitialize = false)
    {
        bool exitingWater = currentMode == PlayerMode.Water && mode == PlayerMode.Land;
        dash.CancelActiveDash(preserveMomentum: exitingWater);
        movement.SetMode(mode, forceInitialize);
        UpdateLocomotionState();
    }

    public void ApplyBounce(Vector2 direction, float force, Vector2 hitPosition)
    {
        dash.CancelActiveDash();
        knockback.ApplyBounce(direction, force);
    }

    public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        dash.CancelActiveDash();
        knockback.ApplyKnockback(direction, force, duration);
    }

    public void TriggerHitStop(float duration)
    {
        hitStop?.Trigger(duration);
    }

    private void HandleActionRequests()
    {
        bool jumpRequested = inputReader.ConsumeJumpPressed();
        bool dashRequested = inputReader.ConsumeDashPressed();

        if (currentMode == PlayerMode.Land)
        {
            if (jumpRequested && movement.CanJump)
            {
                movement.Jump();
            }

            if (dashRequested)
            {
                dash.TryStartDash(inputReader.MoveInput);
            }

            return;
        }

        if (jumpRequested || dashRequested)
        {
            dash.TryStartDash(inputReader.MoveInput);
        }
    }

    private void UpdateLocomotionState()
    {
        if (stateMachine == null)
        {
            return;
        }

        if (knockback != null && knockback.IsActive)
        {
            stateMachine.ChangeState(PlayerMotionState.Knockback);
            return;
        }

        if (dash != null && dash.IsDashing)
        {
            stateMachine.ChangeState(PlayerMotionState.Dash);
            return;
        }

        PlayerMotionState locomotionState = movement != null && movement.ShouldUseMoveState(inputReader != null ? inputReader.MoveInput : Vector2.zero)
            ? PlayerMotionState.Move
            : PlayerMotionState.Idle;

        stateMachine.ChangeState(locomotionState);
    }

    private void CacheDependencies()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (dash == null)
        {
            dash = GetComponent<PlayerDash>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (knockback == null)
        {
            knockback = GetComponent<PlayerKnockback>();
        }

        if (hitStop == null)
        {
            hitStop = GetComponent<PlayerHitStop>();
        }

        if (dashTrailFeedback == null)
        {
            dashTrailFeedback = GetComponent<PlayerDashTrailFeedback>();
        }
    }
}
