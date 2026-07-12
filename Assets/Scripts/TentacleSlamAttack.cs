using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TentacleSlamAttack : OctopusBossAttack
{
    [Header("Prefabs")]
    [SerializeField] private TentacleSlamInstance tentacleSlamPrefab;
    [SerializeField] private GameObject warningIndicatorPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform[] slamSpawnPoints;

    [Header("Targeting")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private int phase1NearestPointPoolSize = 3;
    [SerializeField] private int phase2NearestPointPoolSize = 2;

    [Header("Timing")]
    [SerializeField] private float warningDuration = 0.8f;
    [SerializeField] private float impactDamageDuration = 0.2f;
    [SerializeField] private float vulnerableDuration = 1.2f;
    [SerializeField] private float recoverDuration = 0.5f;

    [Header("Damage")]
    [SerializeField] private int playerDamage = 1;

    [Header("Debug")]
    [SerializeField] private bool debugSlamTransforms;
    [SerializeField] private float slamPointGizmoRadius = 0.25f;

    private readonly List<Transform> validSlamPoints = new List<Transform>();
    private GameObject activeWarning;
    private TentacleSlamInstance activeTentacle;
    private OctopusBossController activeBoss;
    private Vector3 lockedSlamPosition;
    private bool hasLockedSlamPosition;

    private void Awake()
    {
        ResolvePlayerTarget();
    }

    private void OnDisable()
    {
        CleanupActiveSlam();
    }

    private void OnValidate()
    {
        warningDuration = Mathf.Max(0f, warningDuration);
        impactDamageDuration = Mathf.Max(0f, impactDamageDuration);
        vulnerableDuration = Mathf.Max(0f, vulnerableDuration);
        recoverDuration = Mathf.Max(0f, recoverDuration);
        playerDamage = Mathf.Max(1, playerDamage);
        phase1NearestPointPoolSize = Mathf.Max(1, phase1NearestPointPoolSize);
        phase2NearestPointPoolSize = Mathf.Max(1, phase2NearestPointPoolSize);
        slamPointGizmoRadius = Mathf.Max(0.05f, slamPointGizmoRadius);
    }

    public override bool CanUse(OctopusBossController boss)
    {
        return base.CanUse(boss) &&
            tentacleSlamPrefab != null &&
            slamSpawnPoints != null &&
            slamSpawnPoints.Length > 0;
    }

    public override IEnumerator Execute(OctopusBossController boss)
    {
        Transform slamPoint = SelectSlamPoint(boss);
        if (boss == null || boss.IsDead || slamPoint == null || tentacleSlamPrefab == null)
            yield break;

        lockedSlamPosition = slamPoint.position;
        hasLockedSlamPosition = true;

        if (debugSlamTransforms)
        {
            Debug.Log($"Tentacle Slam selected '{slamPoint.name}' at world position {lockedSlamPosition}.", this);
        }

        activeBoss = boss;
        activeBoss.Died += HandleBossDied;
        boss.EnterTelegraphState();

        if (warningIndicatorPrefab != null)
        {
            activeWarning = Instantiate(warningIndicatorPrefab, lockedSlamPosition, Quaternion.identity);
        }

        if (warningDuration > 0f)
        {
            yield return new WaitForSeconds(warningDuration);
        }

        DestroyActiveWarning();

        if (boss.IsDead)
        {
            CleanupActiveSlam();
            yield break;
        }

        Vector3 prefabScale = tentacleSlamPrefab.transform.localScale;
        activeTentacle = Instantiate(tentacleSlamPrefab, lockedSlamPosition, Quaternion.identity);
        Transform tentacleTransform = activeTentacle.transform;
        tentacleTransform.SetParent(null, true);
        tentacleTransform.SetPositionAndRotation(lockedSlamPosition, Quaternion.identity);
        tentacleTransform.localScale = prefabScale;
        activeTentacle.ConfigureDebug(debugSlamTransforms, slamPoint.name);
        yield return activeTentacle.PlaySlam(boss, impactDamageDuration, vulnerableDuration, recoverDuration, playerDamage);
        CleanupActiveSlam();
    }

    private void HandleBossDied()
    {
        CleanupActiveSlam();
    }

    private void CleanupActiveSlam()
    {
        if (activeBoss != null)
            activeBoss.Died -= HandleBossDied;

        activeBoss = null;
        DestroyActiveWarning();

        if (activeTentacle != null)
        {
            activeTentacle.CancelAndCleanup();
            activeTentacle = null;
        }

        hasLockedSlamPosition = false;
    }

    private void DestroyActiveWarning()
    {
        if (activeWarning == null)
            return;

        Destroy(activeWarning);
        activeWarning = null;
    }

    private Transform SelectSlamPoint(OctopusBossController boss)
    {
        if (slamSpawnPoints == null || slamSpawnPoints.Length == 0)
            return null;

        ResolvePlayerTarget();
        float targetX = playerTarget != null
            ? playerTarget.position.x
            : boss != null ? boss.transform.position.x : transform.position.x;

        validSlamPoints.Clear();
        for (int i = 0; i < slamSpawnPoints.Length; i++)
        {
            Transform point = slamSpawnPoints[i];
            if (point != null)
                validSlamPoints.Add(point);
        }

        if (validSlamPoints.Count == 0)
            return null;

        validSlamPoints.Sort((a, b) =>
            Mathf.Abs(a.position.x - targetX).CompareTo(Mathf.Abs(b.position.x - targetX)));

        int configuredPoolSize = boss != null && boss.IsPhaseTwo
            ? phase2NearestPointPoolSize
            : phase1NearestPointPoolSize;
        int availablePoolSize = Mathf.Min(configuredPoolSize, validSlamPoints.Count);
        return validSlamPoints[Random.Range(0, availablePoolSize)];
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            playerTarget = player.transform;
    }

    private void OnDrawGizmosSelected()
    {
        if (slamSpawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            for (int i = 0; i < slamSpawnPoints.Length; i++)
            {
                Transform point = slamSpawnPoints[i];
                if (point != null)
                    Gizmos.DrawWireSphere(point.position, slamPointGizmoRadius);
            }
        }

        if (hasLockedSlamPosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(lockedSlamPosition, Vector3.one * slamPointGizmoRadius * 2f);
        }
    }
}
