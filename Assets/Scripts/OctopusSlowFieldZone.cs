using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class OctopusSlowFieldZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider2D fieldCollider;
    [SerializeField] private SpriteRenderer fieldVisual;

    private readonly Dictionary<Collider2D, PlayerMovement> trackedColliders = new Dictionary<Collider2D, PlayerMovement>();
    private readonly Dictionary<PlayerMovement, int> playerOverlapCounts = new Dictionary<PlayerMovement, int>();
    private OctopusBossController boss;
    private float movementMultiplier = 1f;
    private bool isActive;
    private bool cleanupStarted;
    private bool bossDeathSubscribed;

    private void Reset()
    {
        CacheReferences();
        ConfigureCollider();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        CacheReferences();
        ConfigureCollider();
    }

    private void OnDisable()
    {
        isActive = false;
        RemoveAllPlayerModifiers();
        UnsubscribeFromBossDeath();

        if (fieldCollider != null)
            fieldCollider.enabled = false;
    }

    private void OnDestroy()
    {
        RemoveAllPlayerModifiers();
        UnsubscribeFromBossDeath();
    }

    public void Begin(OctopusBossController owner, Vector2 size, float multiplier)
    {
        RemoveAllPlayerModifiers();
        UnsubscribeFromBossDeath();

        boss = owner;
        movementMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        cleanupStarted = false;
        isActive = boss != null && !boss.IsDead;

        ConfigureSize(size);

        if (fieldCollider != null)
            fieldCollider.enabled = isActive;

        SubscribeToBossDeath();
    }

    public void CancelAndCleanup()
    {
        if (cleanupStarted)
            return;

        cleanupStarted = true;
        isActive = false;
        RemoveAllPlayerModifiers();
        UnsubscribeFromBossDeath();

        if (fieldCollider != null)
            fieldCollider.enabled = false;

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrackPlayerCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TrackPlayerCollider(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        UntrackPlayerCollider(other);
    }

    private void TrackPlayerCollider(Collider2D other)
    {
        if (!isActive || other == null || trackedColliders.ContainsKey(other))
            return;

        PlayerMovement movement = ResolvePlayerMovement(other);
        if (movement == null)
            return;

        trackedColliders.Add(other, movement);
        playerOverlapCounts.TryGetValue(movement, out int overlapCount);
        playerOverlapCounts[movement] = overlapCount + 1;

        if (overlapCount == 0)
            movement.SetExternalMovementMultiplier(this, movementMultiplier);
    }

    private void UntrackPlayerCollider(Collider2D other)
    {
        if (other == null || !trackedColliders.TryGetValue(other, out PlayerMovement movement))
            return;

        trackedColliders.Remove(other);
        if (movement == null || !playerOverlapCounts.TryGetValue(movement, out int overlapCount))
            return;

        overlapCount--;
        if (overlapCount > 0)
        {
            playerOverlapCounts[movement] = overlapCount;
            return;
        }

        playerOverlapCounts.Remove(movement);
        movement.RemoveExternalMovementMultiplier(this);
    }

    private PlayerMovement ResolvePlayerMovement(Collider2D other)
    {
        if (!other.CompareTag("Player") &&
            (other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player")))
        {
            return null;
        }

        PlayerMovement movement = other.GetComponent<PlayerMovement>();
        if (movement == null && other.attachedRigidbody != null)
            movement = other.attachedRigidbody.GetComponent<PlayerMovement>();
        if (movement == null)
            movement = other.GetComponentInParent<PlayerMovement>();

        return movement;
    }

    private void RemoveAllPlayerModifiers()
    {
        foreach (PlayerMovement movement in playerOverlapCounts.Keys)
        {
            if (movement != null)
                movement.RemoveExternalMovementMultiplier(this);
        }

        trackedColliders.Clear();
        playerOverlapCounts.Clear();
    }

    private void ConfigureSize(Vector2 size)
    {
        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);

        if (fieldCollider is BoxCollider2D boxCollider)
            boxCollider.size = size;

        if (fieldVisual == null || fieldVisual.sprite == null)
            return;

        Vector2 spriteSize = fieldVisual.sprite.bounds.size;
        fieldVisual.transform.localScale = new Vector3(
            size.x / Mathf.Max(0.01f, spriteSize.x),
            size.y / Mathf.Max(0.01f, spriteSize.y),
            fieldVisual.transform.localScale.z);
    }

    private void CacheReferences()
    {
        if (fieldCollider == null)
            fieldCollider = GetComponent<Collider2D>();

        if (fieldVisual == null)
            fieldVisual = GetComponentInChildren<SpriteRenderer>();
    }

    private void ConfigureCollider()
    {
        if (fieldCollider != null)
            fieldCollider.isTrigger = true;
    }

    private void SubscribeToBossDeath()
    {
        if (!isActive || boss == null || bossDeathSubscribed)
            return;

        boss.Died += HandleBossDied;
        bossDeathSubscribed = true;
    }

    private void UnsubscribeFromBossDeath()
    {
        if (!bossDeathSubscribed)
            return;

        if (boss != null)
            boss.Died -= HandleBossDied;

        bossDeathSubscribed = false;
    }

    private void HandleBossDied()
    {
        CancelAndCleanup();
    }
}
