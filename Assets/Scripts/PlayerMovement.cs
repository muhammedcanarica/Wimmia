using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerMovement : MonoBehaviour
{
    private static PhysicsMaterial2D frictionlessMaterial;

    [Header("Mode")]
    [SerializeField] private PlayerMode startingMode = PlayerMode.Land;
    [SerializeField] private float modeTransitionDuration = 0.15f;
    [SerializeField] private float gravityBlendSpeed = 12f;
    [SerializeField] private float maxWaterExitVerticalSpeed = 12f;
    [SerializeField] private float maxWaterVerticalSpeed = 12f;
    [SerializeField] private float groundCheckGraceDuration = 0.15f;

    [Header("Water Exit")]
    [Tooltip("Sudan çıkışta yukarı hız sıfıra yakınsa verilecek minimum yukarı itme")]
    [SerializeField] private float waterExitMinBoost = 3f;
    [Tooltip("Sudan çıkış sonrası yerçekimi yumuşak geçiş hızı (düşük = daha yavaş yerçekimi dönüşü)")]
    [SerializeField] private float waterExitGravityBlendSpeed = 4f;

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
    [SerializeField] private float jumpCooldown = 0.2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer = (1 << 0) | (1 << 6);

    [Header("Fall Speed Limit")]
    [SerializeField] private float maxFallSpeed = 10f;

    private Rigidbody2D rb;
    private Collider2D[] colliders;
    private PlayerStateMachine stateMachine;
    private Vector2 waterVelocitySmoothing;
    private float targetGravityScale;
    private float targetDrag;
    private float modeTransitionTimer;
    private float groundCheckGraceTimer;
    private float jumpCooldownTimer;
    private bool isGrounded;
    private bool facingRight = true;

    /// <summary>True while gravity is slowly blending back after a water exit.</summary>
    private bool isWaterExitTransition;
    private float waterExitGravityTimer;

    public PlayerMode StartingMode => startingMode;
    public Rigidbody2D Body => rb;
    public bool IsGrounded => isGrounded;
    public bool CanJump => stateMachine != null &&
                           stateMachine.CurrentMode == PlayerMode.Land &&
                           isGrounded &&
                           jumpCooldownTimer <= 0f &&
                           modeTransitionTimer <= 0f;
    public Vector2 FacingDirection => facingRight ? Vector2.right : Vector2.left;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        stateMachine = GetComponent<PlayerStateMachine>();
        EnsureGroundLayerMask();
        ConfigureBodyForSmoothMotion();
        ConfigureContactMaterials();

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
        colliders = colliders != null && colliders.Length > 0 ? colliders : GetComponents<Collider2D>();
        EnsureGroundLayerMask();
        ConfigureBodyForSmoothMotion();
        ConfigureContactMaterials();

        // Always begin in land mode. Water zones can promote the player to water
        // immediately if the spawn point is inside water.
        SetMode(PlayerMode.Land, true);
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

        // --- Water → Land geçişi ---
        bool isWaterToLand = previousMode == PlayerMode.Water && mode == PlayerMode.Land && !forceInitialize;

        float preservedVerticalVelocity = rb.linearVelocity.y;
        if (isWaterToLand)
        {
            // Aşağı hızı sıfırla — sudan çıkınca düşmesin
            if (preservedVerticalVelocity < 0f)
                preservedVerticalVelocity = 0f;

            // Yukarı hız çok düşükse minimum boost ver
            if (preservedVerticalVelocity < waterExitMinBoost)
                preservedVerticalVelocity = waterExitMinBoost;

            // Çapraz çıkışlarda dikey hız (0.707 * waterMoveSpeed) düz çıkışa göre yarı yarıya daha az zıplama sağlar.
            // Bunu engellemek ve havada "düşme" hissini kırmak için dikey hızı garanti altına alıyoruz.
            if (preservedVerticalVelocity > 0.1f)
            {
                float guaranteedBoost = waterMoveSpeed * 0.85f;
                if (preservedVerticalVelocity < guaranteedBoost)
                {
                    preservedVerticalVelocity = guaranteedBoost;
                }
            }

            preservedVerticalVelocity = Mathf.Min(preservedVerticalVelocity, maxWaterExitVerticalSpeed);
            groundCheckGraceTimer = groundCheckGraceDuration;

            // Yavaş yerçekimi geçişi başlat
            isWaterExitTransition = true;
            waterExitGravityTimer = 0f;
        }
        else if (mode == PlayerMode.Water)
        {
            groundCheckGraceTimer = 0f;
            isWaterExitTransition = false;
        }

        targetGravityScale = mode == PlayerMode.Water ? 0f : landGravityScale;
        targetDrag = mode == PlayerMode.Water ? waterLinearDamping : 0f;
        ResetLandMovementState();

        if (forceInitialize && !isWaterToLand)
        {
            rb.gravityScale = targetGravityScale;
            rb.linearDamping = targetDrag;
        }
        else if (isWaterToLand)
        {
            // Sudan çıkışta yerçekimini sıfırdan başlat — FixedPrepare'da yavaşça artacak
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
        }
        // Smooth transition: gravity ve drag FixedPrepare'da lerp edilecek

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

        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer -= Time.fixedDeltaTime;
        }

        // --- D) Smooth transition: gravity ve drag yumuşak geçiş ---
        float currentGravityBlendSpeed = gravityBlendSpeed;

        if (isWaterExitTransition)
        {
            waterExitGravityTimer += Time.fixedDeltaTime;
            // Çıkışta daha yavaş bir yerçekimi dönüşümü kullan ki hemen düşmesin
            currentGravityBlendSpeed = waterExitGravityBlendSpeed;

            if (Mathf.Abs(rb.gravityScale - targetGravityScale) < 0.1f || waterExitGravityTimer > 1f)
            {
                isWaterExitTransition = false;
            }
        }

        rb.gravityScale = Mathf.Lerp(rb.gravityScale, targetGravityScale, currentGravityBlendSpeed * Time.fixedDeltaTime);
        rb.linearDamping = Mathf.Lerp(rb.linearDamping, targetDrag, currentGravityBlendSpeed * Time.fixedDeltaTime);

        // --- E) Düşme hızını sınırla — ani düşmeyi önle ---
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }

        UpdateGroundedState(suppressGroundCheck);
    }

    public void Move(Vector2 moveInput)
    {
        SetFacingFromDirection(moveInput.x);

        if (stateMachine.CurrentMode == PlayerMode.Water)
        {
            if (modeTransitionTimer > 0f)
            {
                return;
            }

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
        jumpCooldownTimer = jumpCooldown;
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
        float accelerationTime = Mathf.Abs(horizontalInput) > 0.0001f
            ? landAccelerationTime
            : landDecelerationTime;
        float acceleration = accelerationTime > 0.0001f
            ? landMoveSpeed / accelerationTime
            : float.PositiveInfinity;
        float nextVelocityX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetVelocityX,
            acceleration * Time.fixedDeltaTime);

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

    private void OnValidate()
    {
        EnsureGroundLayerMask();
    }

    private void EnsureGroundLayerMask()
    {
        const int defaultLayer = 1 << 0;
        const int groundPhysicsLayer = 1 << 6;

        if ((groundLayer.value & groundPhysicsLayer) != 0)
        {
            groundLayer |= defaultLayer;
        }
    }

    private void ConfigureBodyForSmoothMotion()
    {
        if (rb == null)
        {
            return;
        }

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    private void ResetLandMovementState()
    {
        waterVelocitySmoothing = Vector2.zero;
    }

    private void ConfigureContactMaterials()
    {
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        if (frictionlessMaterial == null)
        {
            frictionlessMaterial = new PhysicsMaterial2D("PlayerFrictionless")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D currentCollider = colliders[i];
            if (currentCollider == null || currentCollider.isTrigger)
            {
                continue;
            }

            currentCollider.sharedMaterial = frictionlessMaterial;
        }
    }
}
