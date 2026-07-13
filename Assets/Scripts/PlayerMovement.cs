using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerMovement : MonoBehaviour
{
    private const float GroundedVerticalVelocityThreshold = 1f;
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
    [FormerlySerializedAs("waterLinearDamping")]
    [SerializeField] private float waterDrag = 0.2f;
    [SerializeField] private float waterGravity = 0f;
    [SerializeField] private float waterEntryVerticalVelocityMultiplier = 1f;
    [SerializeField] private float waterEntryMomentumMultiplier = 1f;
    [SerializeField] private float waterEntryDrag = 18f;
    [SerializeField] private float maxWaterDiveSpeed = 14f;
    [SerializeField] private float waterBuoyancy = 0.2f;
    [SerializeField] private float waterIdleVerticalDrift = -0.15f;

    [Header("Land Movement")]
    [SerializeField] private float landMoveSpeed = 5f;
    [SerializeField] private float landAccelerationTime = 0.08f;
    [SerializeField] private float landDecelerationTime = 0.05f;
    [FormerlySerializedAs("landGravityScale")]
    [SerializeField] private float normalGravity = 3f;
    [SerializeField] private float normalDrag = 0f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float jumpCooldown = 0.2f;
    [SerializeField] private float landGroundAcceleration = 62f;
    [SerializeField] private float landGroundDeceleration = 78f;
    [SerializeField] private float landAirAcceleration = 42f;
    [SerializeField] private float landAirDeceleration = 48f;
    [FormerlySerializedAs("landCoyoteTime")]
    [SerializeField] private float coyoteTime = 0.1f;
    [FormerlySerializedAs("landJumpBufferTime")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    [FormerlySerializedAs("landFallGravityMultiplier")]
    [SerializeField] private float fallMultiplier = 1.9f;
    [FormerlySerializedAs("landLowJumpGravityMultiplier")]
    [SerializeField] private float lowJumpMultiplier = 2.6f;

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
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isGrounded;
    private bool isJumpHeld;
    private bool facingRight = true;
    private float waterEntryMomentum;
    private bool hasWaterEntryMomentum;

    // External forces (CurrentZone, etc.) — consumed each FixedUpdate
    private Vector2 externalVelocityAccum;

    // Source-based modifiers let temporary effects remove only their own entry
    // without mutating or restoring serialized movement values.
    private readonly Dictionary<Object, float> externalMovementMultipliers = new Dictionary<Object, float>();
    private readonly List<Object> staleMovementModifierSources = new List<Object>();
    private float combinedExternalMovementMultiplier = 1f;

    /// <summary>True while gravity is slowly blending back after a water exit.</summary>
    private bool isWaterExitTransition;
    private float waterExitGravityTimer;

    public PlayerMode StartingMode => startingMode;
    public Rigidbody2D Body => rb;
    public bool IsGrounded => isGrounded;
    public bool CanJump => CanUseLandJump();
    public Vector2 FacingDirection => facingRight ? Vector2.right : Vector2.left;
    public float CurrentExternalMovementMultiplier => combinedExternalMovementMultiplier;
    private bool isInWater => stateMachine != null && stateMachine.CurrentMode == PlayerMode.Water;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        stateMachine = GetComponent<PlayerStateMachine>();
        EnsureGroundLayerMask();
        EnsureLandAccelerationDefaults();
        ConfigureBodyForSmoothMotion();
        ConfigureContactMaterials();

        if (groundCheck == null)
        {
            Transform detectedGroundCheck = FindGroundCheckTransform();
            if (detectedGroundCheck != null)
            {
                groundCheck = detectedGroundCheck;
            }
        }
    }

    private void OnDisable()
    {
        externalMovementMultipliers.Clear();
        staleMovementModifierSources.Clear();
        combinedExternalMovementMultiplier = 1f;
    }

    public void Initialize(PlayerStateMachine machine)
    {
        stateMachine = machine;
        rb = rb != null ? rb : GetComponent<Rigidbody2D>();
        colliders = colliders != null && colliders.Length > 0 ? colliders : GetComponents<Collider2D>();
        EnsureGroundLayerMask();
        EnsureLandAccelerationDefaults();
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
            BeginWaterEntryMomentum(preservedVerticalVelocity);
        }
        else
        {
            ClearWaterEntryMomentum();
        }

        targetGravityScale = mode == PlayerMode.Water ? waterGravity : normalGravity;
        targetDrag = mode == PlayerMode.Water ? waterDrag : normalDrag;
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
        if (stateMachine != null &&
            stateMachine.CurrentMode != PlayerMode.Water &&
            rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }

        UpdateGroundedState(suppressGroundCheck);

        if (isInWater)
        {
            coyoteTimer = 0f;
            return;
        }

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            return;
        }

        if (coyoteTimer > 0f)
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }
    }

    /// <summary>Queue an external velocity push (e.g. CurrentZone). Accumulated and consumed once per FixedUpdate.</summary>
    public void AddExternalVelocity(Vector2 velocity)
    {
        externalVelocityAccum += velocity;
    }

    public void SetExternalMovementMultiplier(Object source, float multiplier)
    {
        if (source == null)
            return;

        externalMovementMultipliers[source] = Mathf.Clamp(multiplier, 0.05f, 3f);
        RecalculateExternalMovementMultiplier();
    }

    public void RemoveExternalMovementMultiplier(Object source)
    {
        if (source == null)
            return;

        if (externalMovementMultipliers.Remove(source))
            RecalculateExternalMovementMultiplier();
    }

    public void Move(Vector2 moveInput)
    {
        SetFacingFromDirection(moveInput.x);

        Vector2 ext = externalVelocityAccum;
        externalVelocityAccum = Vector2.zero;

        if (stateMachine.CurrentMode == PlayerMode.Water)
        {
            if (modeTransitionTimer > 0f)
            {
                return;
            }

            ApplyWaterMovement(moveInput, ext);
            return;
        }

        HandleLandMovement(moveInput.x, ext);
        ApplyLandGravity();
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
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isGrounded = false;
    }

    public void SetJumpHeld(bool jumpHeld)
    {
        isJumpHeld = jumpHeld;
    }

    public void HandleLandJump(bool jumpRequested)
    {
        if (isInWater) return;

        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.fixedDeltaTime;
        }

        if (jumpRequested)
        {
            jumpBufferTimer = jumpBufferTime;
        }

        if (!CanUseLandJump() || jumpBufferTimer <= 0f)
        {
            return;
        }

        Jump();
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

    private void ApplyWaterMovement(Vector2 moveInput, Vector2 externalVelocity)
    {
        float movementMultiplier = combinedExternalMovementMultiplier;
        float currentWaterMoveSpeed = waterMoveSpeed * movementMultiplier;
        float targetVerticalVelocity = moveInput.y * currentWaterMoveSpeed
            + externalVelocity.y
            + waterIdleVerticalDrift
            + waterBuoyancy;

        if (Mathf.Abs(moveInput.y) < 0.0001f && hasWaterEntryMomentum)
        {
            targetVerticalVelocity += waterEntryMomentum;
        }

        // External push is added to the target so smoothing works with it, not against it
        Vector2 targetVelocity = new Vector2(
            moveInput.x * currentWaterMoveSpeed + externalVelocity.x,
            targetVerticalVelocity);
        float baseSmoothTime = moveInput.sqrMagnitude > 0.0001f
            ? waterAccelerationTime
            : waterDecelerationTime;
        float smoothTime = baseSmoothTime / Mathf.Max(0.05f, movementMultiplier);

        Vector2 nextVelocity = Vector2.SmoothDamp(
            rb.linearVelocity,
            targetVelocity,
            ref waterVelocitySmoothing,
            smoothTime,
            Mathf.Infinity,
            Time.fixedDeltaTime);

        nextVelocity.y = Mathf.Clamp(nextVelocity.y, -maxWaterDiveSpeed, maxWaterVerticalSpeed);
        rb.linearVelocity = nextVelocity;
        DecayWaterEntryMomentum(moveInput.y);
    }

    private void HandleLandMovement(float horizontalInput, Vector2 externalVelocity)
    {
        if (isInWater) return;

        float movementMultiplier = combinedExternalMovementMultiplier;
        float targetVelocityX = horizontalInput * landMoveSpeed * movementMultiplier + externalVelocity.x;
        bool hasInput = Mathf.Abs(horizontalInput) > 0.0001f;
        bool groundedForMovement = isGrounded && !isWaterExitTransition;
        float acceleration = hasInput
            ? (groundedForMovement ? landGroundAcceleration : landAirAcceleration)
            : (groundedForMovement ? landGroundDeceleration : landAirDeceleration);
        acceleration *= movementMultiplier;
        float nextVelocityX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetVelocityX,
            acceleration * Time.fixedDeltaTime);

        // Vertical external push (e.g. upward current) applied directly
        float nextVelocityY = rb.linearVelocity.y + externalVelocity.y * Time.fixedDeltaTime;
        rb.linearVelocity = new Vector2(nextVelocityX, nextVelocityY);
    }

    private void RecalculateExternalMovementMultiplier()
    {
        float combinedMultiplier = 1f;
        staleMovementModifierSources.Clear();

        foreach (KeyValuePair<Object, float> entry in externalMovementMultipliers)
        {
            if (entry.Key == null)
            {
                staleMovementModifierSources.Add(entry.Key);
                continue;
            }

            combinedMultiplier *= entry.Value;
        }

        for (int i = 0; i < staleMovementModifierSources.Count; i++)
            externalMovementMultipliers.Remove(staleMovementModifierSources[i]);

        staleMovementModifierSources.Clear();
        combinedExternalMovementMultiplier = Mathf.Clamp(combinedMultiplier, 0.05f, 3f);
    }

    private void ApplyLandGravity()
    {
        if (isInWater) return;
        if (isWaterExitTransition || isGrounded)
        {
            return;
        }

        float gravityMultiplier = 1f;

        if (rb.linearVelocity.y < -Mathf.Epsilon)
        {
            gravityMultiplier = fallMultiplier;
        }
        else if (!isJumpHeld && rb.linearVelocity.y > Mathf.Epsilon)
        {
            gravityMultiplier = lowJumpMultiplier;
        }

        if (gravityMultiplier <= 1f)
        {
            return;
        }

        float extraGravity = (gravityMultiplier - 1f) * normalGravity * Mathf.Abs(Physics2D.gravity.y);
        float nextVelocityY = rb.linearVelocity.y - extraGravity * Time.fixedDeltaTime;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(nextVelocityY, -maxFallSpeed));
    }

    private void UpdateGroundedState(bool suppressGroundCheck)
    {
        if (stateMachine == null || stateMachine.CurrentMode == PlayerMode.Water)
        {
            isGrounded = false;
            return;
        }

        if (suppressGroundCheck || groundCheckGraceTimer > 0f || rb.linearVelocity.y > GroundedVerticalVelocityThreshold || groundCheck == null)
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
        EnsureLandAccelerationDefaults();

        if (groundCheck == null)
        {
            groundCheck = FindGroundCheckTransform();
        }
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

    private void EnsureLandAccelerationDefaults()
    {
        if (landGroundAcceleration <= 0f)
        {
            landGroundAcceleration = landAccelerationTime > 0.0001f
                ? landMoveSpeed / landAccelerationTime
                : landMoveSpeed;
        }

        if (landGroundDeceleration <= 0f)
        {
            landGroundDeceleration = landDecelerationTime > 0.0001f
                ? landMoveSpeed / landDecelerationTime
                : landMoveSpeed;
        }

        if (landAirAcceleration <= 0f)
        {
            landAirAcceleration = landGroundAcceleration;
        }

        if (landAirDeceleration <= 0f)
        {
            landAirDeceleration = landGroundDeceleration;
        }
    }

    private Transform FindGroundCheckTransform()
    {
        Transform directChild = transform.Find("GroundCheck");
        if (directChild != null)
        {
            return directChild;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child != null && child != transform && child.name == "GroundCheck")
            {
                return child;
            }
        }

        return null;
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
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        if (stateMachine == null || stateMachine.CurrentMode != PlayerMode.Water)
        {
            ClearWaterEntryMomentum();
        }
    }

    private bool CanUseLandJump()
    {
        return stateMachine != null &&
               !isInWater &&
               (isGrounded || coyoteTimer > 0f) &&
               jumpCooldownTimer <= 0f;
    }

    private void BeginWaterEntryMomentum(float currentVerticalVelocity)
    {
        if (currentVerticalVelocity >= 0f)
        {
            ClearWaterEntryMomentum();
            return;
        }

        float entryVelocity = currentVerticalVelocity * waterEntryVerticalVelocityMultiplier;
        entryVelocity *= waterEntryMomentumMultiplier;
        waterEntryMomentum = Mathf.Clamp(entryVelocity, -maxWaterDiveSpeed, 0f);
        hasWaterEntryMomentum = Mathf.Abs(waterEntryMomentum) > 0.01f;
    }

    private void DecayWaterEntryMomentum(float verticalInput)
    {
        if (!hasWaterEntryMomentum)
        {
            return;
        }

        float drag = waterEntryDrag;
        if (Mathf.Abs(verticalInput) > 0.0001f)
        {
            drag *= 2f;
        }

        waterEntryMomentum = Mathf.MoveTowards(waterEntryMomentum, 0f, drag * Time.fixedDeltaTime);
        if (Mathf.Abs(waterEntryMomentum) <= 0.01f)
        {
            ClearWaterEntryMomentum();
        }
    }

    private void ClearWaterEntryMomentum()
    {
        waterEntryMomentum = 0f;
        hasWaterEntryMomentum = false;
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
