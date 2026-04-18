using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private PlayerMode startingMode = PlayerMode.Water;
    [SerializeField] private float modeTransitionDuration = 0.15f;
    [SerializeField] private float gravityBlendSpeed = 12f;
    [SerializeField] private float maxWaterExitVerticalSpeed = 12f;
    [SerializeField] private float maxWaterVerticalSpeed = 12f;
    [SerializeField] private float groundCheckGraceDuration = 0.15f;

    [Header("Water Movement")]
    [SerializeField] private float waterMoveSpeed = 5f;
    [SerializeField] private float waterAccelerationTime = 0.12f;
    [SerializeField] private float waterDecelerationTime = 0.05f;
    [SerializeField] private float waterLinearDamping = 0.2f;

    [Header("Land Movement")]
    [SerializeField] private float landMoveSpeed = 5f;
    [SerializeField] private float landAccelerationTime = 0.08f;
    [SerializeField] private float landDecelerationTime = 0.05f;
    [SerializeField] private float landGravityScale = 3f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer = 1 << 6;

    private Rigidbody2D rb;
    private PlayerStateMachine stateMachine;
    private Vector2 waterVelocitySmoothing;
    private float landVelocitySmoothing;
    private float targetGravityScale;
    private float modeTransitionTimer;
    private float groundCheckGraceTimer;
    private bool isGrounded;
    private bool facingRight = true;

    public PlayerMode StartingMode => startingMode;
    public Rigidbody2D Body => rb;
    public bool IsGrounded => isGrounded;
    public bool CanJump => stateMachine != null &&
                           stateMachine.CurrentMode == PlayerMode.Land &&
                           isGrounded &&
                           modeTransitionTimer <= 0f;
    public Vector2 FacingDirection => facingRight ? Vector2.right : Vector2.left;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stateMachine = GetComponent<PlayerStateMachine>();

        if (groundCheck == null)
        {
            Transform detectedGroundCheck = transform.Find("GroundCheck");
            if (detectedGroundCheck != null)
            {
                groundCheck = detectedGroundCheck;
            }
        }
    }

    public void Initialize(PlayerStateMachine machine)
    {
        stateMachine = machine;
        rb = rb != null ? rb : GetComponent<Rigidbody2D>();

        SetMode(startingMode, true);
    }

    public void SetMode(PlayerMode mode, bool forceInitialize = false)
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        PlayerMode previousMode = stateMachine.CurrentMode;
        if (!forceInitialize && previousMode == mode)
        {
            return;
        }

        stateMachine.SetMode(mode, forceInitialize);

        float preservedVerticalVelocity = rb.linearVelocity.y;
        if (!forceInitialize &&
            previousMode == PlayerMode.Water &&
            mode == PlayerMode.Land &&
            preservedVerticalVelocity > 0f)
        {
            preservedVerticalVelocity = Mathf.Min(preservedVerticalVelocity, maxWaterExitVerticalSpeed);
            groundCheckGraceTimer = groundCheckGraceDuration;
        }
        else if (mode == PlayerMode.Water)
        {
            groundCheckGraceTimer = 0f;
        }

        targetGravityScale = mode == PlayerMode.Water ? 0f : landGravityScale;
        rb.gravityScale = forceInitialize ? targetGravityScale : rb.gravityScale;
        rb.linearDamping = mode == PlayerMode.Water ? waterLinearDamping : 0f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, preservedVerticalVelocity);
        modeTransitionTimer = forceInitialize ? 0f : modeTransitionDuration;
        isGrounded = false;
    }

    public void FixedPrepare(bool suppressGroundCheck)
    {
        if (modeTransitionTimer > 0f)
        {
            modeTransitionTimer -= Time.fixedDeltaTime;
        }

        if (groundCheckGraceTimer > 0f)
        {
            groundCheckGraceTimer -= Time.fixedDeltaTime;
        }

        rb.gravityScale = Mathf.Lerp(rb.gravityScale, targetGravityScale, gravityBlendSpeed * Time.fixedDeltaTime);
        UpdateGroundedState(suppressGroundCheck);
    }

    public void Move(Vector2 moveInput)
    {
        SetFacingFromDirection(moveInput.x);

        if (modeTransitionTimer > 0f)
        {
            return;
        }

        if (stateMachine.CurrentMode == PlayerMode.Water)
        {
            ApplyWaterMovement(moveInput);
            return;
        }

        ApplyLandMovement(moveInput.x);
    }

    public void Jump()
    {
        if (!CanJump)
        {
            return;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        isGrounded = false;
    }

    public void SetFacingFromDirection(float directionX)
    {
        if (directionX > Mathf.Epsilon)
        {
            facingRight = true;
        }
        else if (directionX < -Mathf.Epsilon)
        {
            facingRight = false;
        }
    }

    public bool ShouldUseMoveState(Vector2 moveInput)
    {
        if (stateMachine == null)
        {
            return false;
        }

        if (stateMachine.CurrentMode == PlayerMode.Water)
        {
            return moveInput.sqrMagnitude > 0.0001f || rb.linearVelocity.sqrMagnitude > 0.0025f;
        }

        return Mathf.Abs(moveInput.x) > 0.0001f || Mathf.Abs(rb.linearVelocity.x) > 0.05f;
    }

    private void ApplyWaterMovement(Vector2 moveInput)
    {
        Vector2 targetVelocity = moveInput * waterMoveSpeed;
        float smoothTime = moveInput.sqrMagnitude > 0.0001f
            ? waterAccelerationTime
            : waterDecelerationTime;

        Vector2 nextVelocity = Vector2.SmoothDamp(
            rb.linearVelocity,
            targetVelocity,
            ref waterVelocitySmoothing,
            smoothTime,
            Mathf.Infinity,
            Time.fixedDeltaTime);

        nextVelocity.y = Mathf.Clamp(nextVelocity.y, -maxWaterVerticalSpeed, maxWaterVerticalSpeed);
        rb.linearVelocity = nextVelocity;
    }

    private void ApplyLandMovement(float horizontalInput)
    {
        float targetVelocityX = horizontalInput * landMoveSpeed;
        float smoothTime = Mathf.Abs(horizontalInput) > 0.0001f
            ? landAccelerationTime
            : landDecelerationTime;

        float nextVelocityX = Mathf.SmoothDamp(
            rb.linearVelocity.x,
            targetVelocityX,
            ref landVelocitySmoothing,
            smoothTime,
            Mathf.Infinity,
            Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(nextVelocityX, rb.linearVelocity.y);
    }

    private void UpdateGroundedState(bool suppressGroundCheck)
    {
        if (stateMachine == null || stateMachine.CurrentMode == PlayerMode.Water)
        {
            isGrounded = false;
            return;
        }

        if (suppressGroundCheck || groundCheckGraceTimer > 0f || rb.linearVelocity.y > Mathf.Epsilon || groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
