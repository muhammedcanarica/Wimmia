using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TentacleSlamInstance : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider2D damageHitbox;
    [SerializeField] private BossWeakPoint weakPoint;

    [Header("Motion")]
    [SerializeField] private Vector2 approachOffset = new Vector2(0f, 3f);
    [SerializeField] private float approachDuration = 0.08f;
    [SerializeField] private float retractDuration = 0.18f;
    [SerializeField] private bool destroyAfterRecover = true;

    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
    private bool damageActive;
    private int playerDamage = 1;
    private OctopusBossController boss;
    private bool cleanupStarted;
    private bool debugSlamTransforms;
    private string selectedSlamPointName;

    private void Reset()
    {
        CacheReferences();
        ConfigureCollider();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureCollider();
        SetWeakPointVulnerable(false);
        SetDamageActive(false);
    }

    private void OnValidate()
    {
        approachDuration = Mathf.Max(0f, approachDuration);
        retractDuration = Mathf.Max(0f, retractDuration);
        playerDamage = Mathf.Max(1, playerDamage);
        CacheReferences();
    }

    public IEnumerator PlaySlam(OctopusBossController owner, float impactDamageDuration, float vulnerableDuration, float recoverDuration, int damage)
    {
        boss = owner;
        cleanupStarted = false;
        playerDamage = Mathf.Max(1, damage);
        impactDamageDuration = Mathf.Max(0f, impactDamageDuration);
        vulnerableDuration = Mathf.Max(0f, vulnerableDuration);
        recoverDuration = Mathf.Max(0f, recoverDuration);

        if (weakPoint != null)
            weakPoint.SetBoss(owner);

        SetWeakPointVulnerable(false);
        SetDamageActive(false);

        Vector3 impactPosition = transform.position;
        yield return MoveFromOffset(impactPosition);
        Physics2D.SyncTransforms();

        if (boss == null || boss.IsDead)
            yield break;

        boss.EnterAttackingState();
        damagedPlayers.Clear();
        SetDamageActive(true);
        LogColliderWorldPositions("impact");
        TryDamagePlayersAlreadyInside();

        if (impactDamageDuration > 0f)
            yield return new WaitForSeconds(impactDamageDuration);

        SetDamageActive(false);

        if (boss != null && !boss.IsDead)
        {
            boss.EnterVulnerableWindowState();
            SetWeakPointVulnerable(true);
            Physics2D.SyncTransforms();
            LogColliderWorldPositions("vulnerable");

            if (vulnerableDuration > 0f)
                yield return new WaitForSeconds(vulnerableDuration);
        }

        SetWeakPointVulnerable(false);

        if (boss != null && !boss.IsDead)
            boss.EnterRecoverState();

        if (recoverDuration > 0f)
            yield return new WaitForSeconds(recoverDuration);

        yield return RetractToOffset(impactPosition);

        if (destroyAfterRecover)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    public void ConfigureDebug(bool enabled, string slamPointName)
    {
        debugSlamTransforms = enabled;
        selectedSlamPointName = slamPointName;
    }

    public void CancelAndCleanup()
    {
        if (cleanupStarted)
            return;

        cleanupStarted = true;
        StopAllCoroutines();
        SetDamageActive(false);
        SetWeakPointVulnerable(false);

        if (destroyAfterRecover)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        damageActive = false;

        if (damageHitbox != null)
            damageHitbox.enabled = false;

        SetWeakPointVulnerable(false);
    }

    private IEnumerator MoveFromOffset(Vector3 impactPosition)
    {
        Vector3 startPosition = impactPosition + (Vector3)approachOffset;
        transform.position = startPosition;

        if (approachDuration <= 0f)
        {
            transform.position = impactPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < approachDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / approachDuration);
            transform.position = Vector3.Lerp(startPosition, impactPosition, t);
            yield return null;
        }

        transform.position = impactPosition;
    }

    private IEnumerator RetractToOffset(Vector3 impactPosition)
    {
        Vector3 endPosition = impactPosition + (Vector3)approachOffset;

        if (retractDuration <= 0f)
        {
            transform.position = endPosition;
            yield break;
        }

        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        while (elapsed < retractDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / retractDuration);
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            yield return null;
        }

        transform.position = endPosition;
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
        {
            damageHitbox.enabled = active || IsWeakPointCollider();
        }
    }

    private bool IsWeakPointCollider()
    {
        return weakPoint != null && damageHitbox != null && damageHitbox.GetComponent<BossWeakPoint>() == weakPoint;
    }

    private void SetWeakPointVulnerable(bool vulnerable)
    {
        if (weakPoint != null)
        {
            weakPoint.SetVulnerable(vulnerable);
        }
    }

    private void CacheReferences()
    {
        if (damageHitbox == null)
            damageHitbox = GetComponent<Collider2D>();

        if (weakPoint == null)
            weakPoint = GetComponentInChildren<BossWeakPoint>();
    }

    private void ConfigureCollider()
    {
        if (damageHitbox != null)
        {
            damageHitbox.isTrigger = true;
        }
    }

    private void LogColliderWorldPositions(string stage)
    {
        if (!debugSlamTransforms)
            return;

        Collider2D weakPointCollider = GetWeakPointCollider();
        string damagePosition = damageHitbox != null ? damageHitbox.bounds.center.ToString() : "missing";
        string weakPointPosition = weakPointCollider != null ? weakPointCollider.bounds.center.ToString() : "missing";
        Debug.Log(
            $"Tentacle Slam '{selectedSlamPointName}' {stage}: root={transform.position}, damageCollider={damagePosition}, weakPointCollider={weakPointPosition}.",
            this);
    }

    private Collider2D GetWeakPointCollider()
    {
        return weakPoint != null ? weakPoint.GetComponent<Collider2D>() : null;
    }

    private void OnDrawGizmosSelected()
    {
        CacheReferences();

        if (damageHitbox != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(damageHitbox.bounds.center, damageHitbox.bounds.size);
        }

        Collider2D weakPointCollider = GetWeakPointCollider();
        if (weakPointCollider != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 extents = weakPointCollider.bounds.extents;
            float radius = Mathf.Max(0.1f, Mathf.Max(extents.x, extents.y));
            Gizmos.DrawWireSphere(weakPointCollider.bounds.center, radius);
        }
    }
}
