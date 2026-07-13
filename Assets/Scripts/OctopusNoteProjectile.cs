using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class OctopusNoteProjectile : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D damageHitbox;

    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
    private Coroutine flightRoutine;
    private OctopusBossController boss;
    private Vector2 lockedDirection = Vector2.left;
    private float projectileSpeed;
    private float projectileLifetime;
    private int playerDamage = 1;
    private bool damageActive;
    private bool cleanupStarted;
    private bool isFinished = true;

    public bool IsFinished => isFinished;

    private void Reset()
    {
        CacheReferences();
        ConfigurePhysics();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigurePhysics();
        SetDamageActive(false);
    }

    private void OnValidate()
    {
        CacheReferences();
        ConfigurePhysics();
    }

    private void OnDisable()
    {
        CancelFlightRoutine();
        damageActive = false;
        isFinished = true;

        if (damageHitbox != null)
            damageHitbox.enabled = false;
    }

    public void Launch(
        OctopusBossController owner,
        Vector2 direction,
        float speed,
        float lifetime,
        int damage)
    {
        CancelFlightRoutine();
        boss = owner;
        lockedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        projectileSpeed = Mathf.Max(0.01f, speed);
        projectileLifetime = Mathf.Max(0.1f, lifetime);
        playerDamage = Mathf.Max(1, damage);
        cleanupStarted = false;
        isFinished = false;
        damagedPlayers.Clear();
        transform.right = lockedDirection;
        flightRoutine = StartCoroutine(FlightRoutine());
    }

    public void CancelAndCleanup()
    {
        if (cleanupStarted)
            return;

        cleanupStarted = true;
        CancelFlightRoutine();
        FinishProjectile();
    }

    private IEnumerator FlightRoutine()
    {
        SetDamageActive(true);
        float elapsed = 0f;
        WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        while (elapsed < projectileLifetime && boss != null && !boss.IsDead)
        {
            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
            Vector2 nextPosition = currentPosition + lockedDirection * projectileSpeed * Time.fixedDeltaTime;

            if (body != null)
                body.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            elapsed += Time.fixedDeltaTime;
            yield return waitForFixedUpdate;
        }

        flightRoutine = null;
        FinishProjectile();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!damageActive || other == null || !other.CompareTag("Player"))
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null && other.attachedRigidbody != null)
            playerHealth = other.attachedRigidbody.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || !damagedPlayers.Add(playerHealth))
            return;

        playerHealth.TakeDamage(playerDamage, transform);
        FinishProjectile();
    }

    private void FinishProjectile()
    {
        if (isFinished)
            return;

        isFinished = true;
        SetDamageActive(false);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void CancelFlightRoutine()
    {
        if (flightRoutine == null)
            return;

        StopCoroutine(flightRoutine);
        flightRoutine = null;
    }

    private void SetDamageActive(bool active)
    {
        damageActive = active;

        if (damageHitbox != null)
            damageHitbox.enabled = active;
    }

    private void CacheReferences()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (damageHitbox == null)
            damageHitbox = GetComponent<Collider2D>();
    }

    private void ConfigurePhysics()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        if (damageHitbox != null)
            damageHitbox.isTrigger = true;
    }
}
