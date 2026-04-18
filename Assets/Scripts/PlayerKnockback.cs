using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerKnockback : MonoBehaviour
{
    [SerializeField] private float defaultBounceDuration = 0.2f;

    private Rigidbody2D rb;
    private PlayerStateMachine stateMachine;
    private float knockbackTimer;

    public bool IsActive => knockbackTimer > 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    public void Initialize(PlayerStateMachine machine)
    {
        rb = rb != null ? rb : GetComponent<Rigidbody2D>();
        stateMachine = machine;
    }

    public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        rb.linearVelocity = normalizedDirection * force;
        knockbackTimer = duration;
        stateMachine?.ChangeState(PlayerMotionState.Knockback);
    }

    public void ApplyBounce(Vector2 direction, float force)
    {
        ApplyKnockback(direction, force, defaultBounceDuration);
    }

    public void FixedStep()
    {
        if (knockbackTimer <= 0f)
        {
            return;
        }

        knockbackTimer -= Time.fixedDeltaTime;
        if (knockbackTimer < 0f)
        {
            knockbackTimer = 0f;
        }
    }
}
