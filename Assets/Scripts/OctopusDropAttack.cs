using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctopusDropAttack : OctopusBossAttack
{
    [Header("Prefabs")]
    [SerializeField] private OctopusDropProjectile dropProjectilePrefab;
    [SerializeField] private GameObject warningIndicatorPrefab;

    [Header("Targets")]
    [SerializeField] private Transform[] targetPoints;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float dropSpawnY = 24f;
    [SerializeField] private int phase1NearestPointPoolSize = 5;
    [SerializeField] private int phase2NearestPointPoolSize = 7;

    [Header("Pattern")]
    [SerializeField] private int phase1ProjectileCount = 3;
    [SerializeField] private int phase2ProjectileCount = 5;
    [SerializeField] private float phase1WarningDuration = 0.7f;
    [SerializeField] private float phase2WarningDuration = 0.5f;
    [SerializeField] private float phase1ProjectileInterval = 0.15f;
    [SerializeField] private float phase2ProjectileInterval = 0.1f;
    [SerializeField] private float recoverDuration = 0.4f;

    [Header("Projectile")]
    [SerializeField] private float projectileFallSpeed = 11f;
    [SerializeField] private float phase2FallSpeedMultiplier = 1.25f;
    [SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private int playerDamage = 1;

    private readonly List<Transform> validPoints = new List<Transform>();
    private readonly List<Vector3> lockedTargets = new List<Vector3>();
    private readonly List<GameObject> activeWarnings = new List<GameObject>();
    private readonly List<OctopusDropProjectile> activeProjectiles = new List<OctopusDropProjectile>();
    private OctopusBossController activeBoss;
    private bool warnedMissingConfiguration;

    private void Awake()
    {
        ResolvePlayerTarget();
    }

    private void OnDisable()
    {
        CleanupAttackObjects();
    }

    private void OnValidate()
    {
        phase1NearestPointPoolSize = Mathf.Max(1, phase1NearestPointPoolSize);
        phase2NearestPointPoolSize = Mathf.Max(1, phase2NearestPointPoolSize);
        phase1ProjectileCount = Mathf.Max(1, phase1ProjectileCount);
        phase2ProjectileCount = Mathf.Max(1, phase2ProjectileCount);
        phase1WarningDuration = Mathf.Max(0.4f, phase1WarningDuration);
        phase2WarningDuration = Mathf.Max(0.4f, phase2WarningDuration);
        phase1ProjectileInterval = Mathf.Max(0f, phase1ProjectileInterval);
        phase2ProjectileInterval = Mathf.Max(0f, phase2ProjectileInterval);
        projectileFallSpeed = Mathf.Max(0.01f, projectileFallSpeed);
        phase2FallSpeedMultiplier = Mathf.Max(0.01f, phase2FallSpeedMultiplier);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
        recoverDuration = Mathf.Max(0f, recoverDuration);
        playerDamage = Mathf.Max(1, playerDamage);
    }

    public override bool CanUse(OctopusBossController boss)
    {
        if (!base.CanUse(boss))
            return false;

        bool configured = dropProjectilePrefab != null &&
            warningIndicatorPrefab != null &&
            targetPoints != null &&
            targetPoints.Length > 0;

        if (!configured && !warnedMissingConfiguration)
        {
            warnedMissingConfiguration = true;
            Debug.LogWarning(
                "Octopus Drop Attack is missing its projectile prefab, warning prefab, or target points. The attack will be skipped.",
                this);
        }

        if (configured)
            warnedMissingConfiguration = false;

        return configured;
    }

    public override IEnumerator Execute(OctopusBossController boss)
    {
        if (boss == null || boss.IsDead || dropProjectilePrefab == null || warningIndicatorPrefab == null)
            yield break;

        SelectAndLockTargets(boss);
        if (lockedTargets.Count == 0)
        {
            Debug.LogWarning("Octopus Drop Attack found no valid target points and was cancelled safely.", this);
            yield break;
        }

        activeBoss = boss;
        activeBoss.Died += HandleBossDied;
        boss.EnterTelegraphState();

        for (int i = 0; i < lockedTargets.Count; i++)
        {
            GameObject warning = Instantiate(warningIndicatorPrefab, lockedTargets[i], Quaternion.identity);
            if (warning != null)
                activeWarnings.Add(warning);
        }

        float warningDuration = GetCurrentWarningDuration(boss);
        if (warningDuration > 0f)
            yield return new WaitForSeconds(warningDuration);

        DestroyWarnings();
        if (boss.IsDead)
        {
            CleanupAttackObjects();
            yield break;
        }

        boss.EnterAttackingState();
        float fallSpeed = boss.IsPhaseTwo
            ? projectileFallSpeed * phase2FallSpeedMultiplier
            : projectileFallSpeed;
        float currentInterval = GetCurrentProjectileInterval(boss);

        for (int i = 0; i < lockedTargets.Count; i++)
        {
            if (boss.IsDead)
                break;

            Vector3 target = lockedTargets[i];
            Vector3 spawnPosition = new Vector3(target.x, dropSpawnY, target.z);
            OctopusDropProjectile projectile = Instantiate(dropProjectilePrefab, spawnPosition, Quaternion.identity);
            if (projectile != null)
            {
                activeProjectiles.Add(projectile);
                projectile.BeginDrop(boss, target, fallSpeed, projectileLifetime, playerDamage);
            }

            if (i < lockedTargets.Count - 1 && currentInterval > 0f)
                yield return new WaitForSeconds(currentInterval);
        }

        while (!boss.IsDead && HasActiveProjectiles())
            yield return null;

        if (boss.IsDead)
        {
            CleanupAttackObjects();
            yield break;
        }

        boss.EnterRecoverState();
        if (recoverDuration > 0f)
            yield return new WaitForSeconds(recoverDuration);

        CleanupAttackObjects();
    }

    public float GetCurrentWarningDuration(OctopusBossController boss)
    {
        return boss != null && boss.IsPhaseTwo
            ? phase2WarningDuration
            : phase1WarningDuration;
    }

    public float GetCurrentProjectileInterval(OctopusBossController boss)
    {
        return boss != null && boss.IsPhaseTwo
            ? phase2ProjectileInterval
            : phase1ProjectileInterval;
    }

    private void SelectAndLockTargets(OctopusBossController boss)
    {
        ResolvePlayerTarget();
        validPoints.Clear();
        lockedTargets.Clear();

        if (targetPoints == null)
            return;

        for (int i = 0; i < targetPoints.Length; i++)
        {
            Transform point = targetPoints[i];
            if (point != null && !validPoints.Contains(point))
                validPoints.Add(point);
        }

        if (validPoints.Count == 0)
            return;

        float targetX = playerTarget != null
            ? playerTarget.position.x
            : boss.transform.position.x;
        validPoints.Sort((a, b) =>
            Mathf.Abs(a.position.x - targetX).CompareTo(Mathf.Abs(b.position.x - targetX)));

        int configuredPoolSize = boss.IsPhaseTwo
            ? phase2NearestPointPoolSize
            : phase1NearestPointPoolSize;
        int candidateCount = Mathf.Min(configuredPoolSize, validPoints.Count);

        for (int i = candidateCount - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Transform temporary = validPoints[i];
            validPoints[i] = validPoints[randomIndex];
            validPoints[randomIndex] = temporary;
        }

        int requestedCount = boss.IsPhaseTwo ? phase2ProjectileCount : phase1ProjectileCount;
        int safeTargetLimit = validPoints.Count > 1 ? validPoints.Count - 1 : 1;
        int selectedCount = Mathf.Min(requestedCount, candidateCount, safeTargetLimit);

        for (int i = 0; i < selectedCount; i++)
            lockedTargets.Add(validPoints[i].position);
    }

    private bool HasActiveProjectiles()
    {
        bool hasActiveProjectile = false;

        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            OctopusDropProjectile projectile = activeProjectiles[i];
            if (projectile == null || projectile.IsFinished)
            {
                activeProjectiles.RemoveAt(i);
                continue;
            }

            hasActiveProjectile = true;
        }

        return hasActiveProjectile;
    }

    private void HandleBossDied()
    {
        CleanupAttackObjects();
    }

    private void CleanupAttackObjects()
    {
        if (activeBoss != null)
            activeBoss.Died -= HandleBossDied;

        activeBoss = null;
        DestroyWarnings();

        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            if (activeProjectiles[i] != null)
                activeProjectiles[i].CancelAndCleanup();
        }

        activeProjectiles.Clear();
        lockedTargets.Clear();
    }

    private void DestroyWarnings()
    {
        for (int i = 0; i < activeWarnings.Count; i++)
        {
            if (activeWarnings[i] != null)
                Destroy(activeWarnings[i]);
        }

        activeWarnings.Clear();
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            playerTarget = player.transform;
    }
}
