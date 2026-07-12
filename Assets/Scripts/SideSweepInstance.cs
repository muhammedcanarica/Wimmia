using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SideSweepInstance : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider2D damageHitbox;

    [Header("Lifetime")]
    [SerializeField] private bool destroyAfterSweep = true;

    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
    private BossWeakPoint[] weakPoints;
    private bool damageActive;
    private int playerDamage = 1;
    private bool cleanupStarted;

    private void Reset()
    {
        CacheReferences();
        ConfigureCollider();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureCollider();
        SetDamageActive(false);
        SetWeakPointsVulnerable(false);
    }

    private void OnValidate()
    {
        playerDamage = Mathf.Max(1, playerDamage);
        CacheReferences();
    }

    public IEnumerator PlaySweep(OctopusBossController boss, Vector3 startPosition, Vector3 endPosition, float sweepSpeed, int damage)
    {
        cleanupStarted = false;
        playerDamage = Mathf.Max(1, damage);
        sweepSpeed = Mathf.Max(0.01f, sweepSpeed);
        transform.position = startPosition;
        SetWeakPointsVulnerable(false);

        Vector3 direction = endPosition - startPosition;
        if (direction.sqrMagnitude > 0.001f)
            transform.right = direction.normalized;

        if (boss == null || boss.IsDead)
            yield break;

        boss.EnterAttackingState();
        damagedPlayers.Clear();
        SetDamageActive(true);
        TryDamagePlayersAlreadyInside();

        while (Vector3.Distance(transform.position, endPosition) > 0.01f)
        {
            if (boss == null || boss.IsDead)
                break;

            transform.position = Vector3.MoveTowards(transform.position, endPosition, sweepSpeed * Time.deltaTime);
            TryDamagePlayersAlreadyInside();
            yield return null;
        }

        transform.position = endPosition;
        SetDamageActive(false);

        if (destroyAfterSweep)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    public void CancelAndCleanup()
    {
        if (cleanupStarted)
            return;

        cleanupStarted = true;
        StopAllCoroutines();
        SetDamageActive(false);
        SetWeakPointsVulnerable(false);

        if (destroyAfterSweep)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        damageActive = false;

        if (damageHitbox != null)
            damageHitbox.enabled = false;

        SetWeakPointsVulnerable(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayersAlreadyInside()
    {
        if (damageHitbox == null)
            return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        Collider2D[] results = new Collider2D[8];
        int count = damageHitbox.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            TryDamagePlayer(results[i]);
        }
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
    }

    private void SetDamageActive(bool active)
    {
        damageActive = active;

        if (damageHitbox != null)
            damageHitbox.enabled = active;
    }

    private void CacheReferences()
    {
        if (damageHitbox == null)
            damageHitbox = GetComponent<Collider2D>();

        weakPoints = GetComponentsInChildren<BossWeakPoint>(true);
    }

    private void SetWeakPointsVulnerable(bool vulnerable)
    {
        if (weakPoints == null)
            return;

        for (int i = 0; i < weakPoints.Length; i++)
        {
            if (weakPoints[i] != null)
                weakPoints[i].SetVulnerable(vulnerable);
        }
    }

    private void ConfigureCollider()
    {
        if (damageHitbox != null)
            damageHitbox.isTrigger = true;
    }
}
