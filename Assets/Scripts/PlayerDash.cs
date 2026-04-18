using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerDash : MonoBehaviour
{
    [Header("Water Dash")]
    [SerializeField] private float waterDashSpeed = 15f;
    [SerializeField] private float waterDashDuration = 0.2f;
    [SerializeField] private float waterDashCooldown = 1f;

    [Header("Land Dash")]
    [SerializeField] private float landDashSpeed = 20f;
    [SerializeField] private float landDashDuration = 0.15f;
    [SerializeField] private float landDashCooldown = 1f;
    [SerializeField] private float landDashExitVelocityMultiplier = 0.5f;

    private Rigidbody2D rb;
    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Vector2 dashDirection = Vector2.right;
    private PlayerMode activeDashMode;
    private float activeDashTimer;
    private float waterDashCooldownTimer;
    private float landDashCooldownTimer;

    public bool IsDashing => activeDashTimer > 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
    }

    public void Initialize(PlayerStateMachine machine, PlayerMovement playerMovement)
    {
        rb = rb != null ? rb : GetComponent<Rigidbody2D>();
        stateMachine = machine;
        movement = playerMovement;
    }

    public void TickTimers(float deltaTime)
    {
        if (waterDashCooldownTimer > 0f)
        {
            waterDashCooldownTimer -= deltaTime;
        }

        if (landDashCooldownTimer > 0f)
        {
            landDashCooldownTimer -= deltaTime;
        }
    }

    public bool TryStartDash(Vector2 moveInput)
    {
        if (stateMachine == null || IsDashing)
        {
            return false;
        }

        PlayerMode currentMode = stateMachine.CurrentMode;
        if (!CanStartDash(currentMode))
        {
            return false;
        }

        activeDashMode = currentMode;
        dashDirection = ResolveDashDirection(currentMode, moveInput);
        activeDashTimer = currentMode == PlayerMode.Water ? waterDashDuration : landDashDuration;

        if (currentMode == PlayerMode.Water)
        {
            waterDashCooldownTimer = waterDashCooldown;
        }
        else
        {
            landDashCooldownTimer = landDashCooldown;
        }

        movement?.SetFacingFromDirection(dashDirection.x);
        stateMachine.ChangeState(PlayerMotionState.Dash);
        return true;
    }

    public void FixedStep()
    {
        if (!IsDashing)
        {
            return;
        }

        if (activeDashMode == PlayerMode.Water)
        {
            rb.linearVelocity = dashDirection * waterDashSpeed;
        }
        else
        {
            rb.linearVelocity = new Vector2(dashDirection.x * landDashSpeed, rb.linearVelocity.y);
        }

        activeDashTimer -= Time.fixedDeltaTime;
        if (activeDashTimer <= 0f)
        {
            StopDash();
        }
    }

    public void CancelActiveDash()
    {
        if (!IsDashing)
        {
            return;
        }

        StopDash();
    }

    private bool CanStartDash(PlayerMode mode)
    {
        return mode == PlayerMode.Water
            ? waterDashCooldownTimer <= 0f
            : landDashCooldownTimer <= 0f;
    }

    private Vector2 ResolveDashDirection(PlayerMode mode, Vector2 moveInput)
    {
        if (mode == PlayerMode.Water)
        {
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                return moveInput.normalized;
            }

            return movement != null ? movement.FacingDirection : Vector2.right;
        }

        float horizontalDirection = Mathf.Abs(moveInput.x) > 0.0001f
            ? Mathf.Sign(moveInput.x)
            : (movement != null ? movement.FacingDirection.x : 1f);

        return new Vector2(horizontalDirection, 0f);
    }

    private void StopDash()
    {
        activeDashTimer = 0f;

        if (activeDashMode == PlayerMode.Water)
        {
            rb.linearVelocity *= 0.5f;
        }
        else
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * landDashExitVelocityMultiplier, rb.linearVelocity.y);
        }
    }
}
