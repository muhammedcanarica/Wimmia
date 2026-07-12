using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class OctopusDropProjectile : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D damageHitbox;

    [Header("Impact")]
    [SerializeField] private LayerMask groundLayerMask = 1 << 6;
    [SerializeField] private bool destroyOnImpact = true;

    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
    private Coroutine dropRoutine;
    private OctopusBossController boss;
    private int playerDamage = 1;
    private bool damageActive;
    private bool impactRequested;
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
        DisableWeakPoints();
        SetDamageActive(false);
    }

    private void OnValidate()
    {
        playerDamage = Mathf.Max(1, playerDamage);
        CacheReferences();
        ConfigurePhysics();
    }

    public void BeginDrop(
        OctopusBossController owner,
        Vector3 targetPosition,
        float fallSpeed,
        float lifetime,
        int damage)
    {
        CancelRoutineOnly();
        boss = owner;
        playerDamage = Mathf.Max(1, damage);
        fallSpeed = Mathf.Max(0.01f, fallSpeed);
        lifetime = Mathf.Max(0.1f, lifetime);
        impactRequested = false;
        cleanupStarted = false;
        isFinished = false;
        damagedPlayers.Clear();
        DisableWeakPoints();
        dropRoutine = StartCoroutine(DropRoutine(targetPosition, fallSpeed, lifetime));
    }

    public void CancelAndCleanup()
    {
        if (cleanupStarted)
            return;

        cleanupStarted = true;
        CancelRoutineOnly();
        FinishProjectile();
    }

    private IEnumerator DropRoutine(Vector3 targetPosition, float fallSpeed, float lifetime)
    {
        SetDamageActive(true);
        float elapsed = 0f;
        WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        while (elapsed < lifetime && !impactRequested && boss != null && !boss.IsDead)
        {
            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
            Vector2 nextPosition = currentPosition + Vector2.down * fallSpeed * Time.fixedDeltaTime;

            if (nextPosition.y <= targetPosition.y)
            {
                nextPosition.y = targetPosition.y;
                impactRequested = true;
            }

            if (body != null)
                body.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            elapsed += Time.fixedDeltaTime;
            yield return waitForFixedUpdate;
        }

        FinishProjectile();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void HandleTrigger(Collider2D other)
    {
        if (other == null)
            return;

        if (TryDamagePlayer(other))
            return;

        int otherLayerMask = 1 << other.gameObject.layer;
        if ((groundLayerMask.value & otherLayerMask) != 0)
            impactRequested = true;
    }

    private bool TryDamagePlayer(Collider2D other)
    {
        if (!damageActive || !other.CompareTag("Player"))
            return false;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null && other.attachedRigidbody != null)
            playerHealth = other.attachedRigidbody.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return false;

        if (damagedPlayers.Add(playerHealth))
            playerHealth.TakeDamage(playerDamage, transform);

        return true;
    }

    private void FinishProjectile()
    {
        SetDamageActive(false);
        DisableWeakPoints();
        isFinished = true;
        dropRoutine = null;

        if (destroyOnImpact)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void CancelRoutineOnly()
    {
        if (dropRoutine == null)
            return;

        StopCoroutine(dropRoutine);
        dropRoutine = null;
    }

    private void OnDisable()
    {
        damageActive = false;
        isFinished = true;

        if (damageHitbox != null)
            damageHitbox.enabled = false;

        DisableWeakPoints();
    }

    private void SetDamageActive(bool active)
    {
        damageActive = active;
        if (damageHitbox != null)
            damageHitbox.enabled = active;
    }

    private void DisableWeakPoints()
    {
        BossWeakPoint[] weakPoints = GetComponentsInChildren<BossWeakPoint>(true);
        for (int i = 0; i < weakPoints.Length; i++)
        {
            if (weakPoints[i] != null)
                weakPoints[i].SetVulnerable(false);
        }
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
        }

        if (damageHitbox != null)
            damageHitbox.isTrigger = true;
    }
}
