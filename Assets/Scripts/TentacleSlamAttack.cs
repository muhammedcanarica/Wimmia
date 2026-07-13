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
    [SerializeField] private float phase1WarningDuration = 0.7f;
    [SerializeField] private float phase2WarningDuration = 0.5f;
    [SerializeField] private float impactDamageDuration = 0.2f;
    [SerializeField] private float phase1VulnerableDuration = 0.9f;
    [SerializeField] private float phase2VulnerableDuration = 0.7f;
    [SerializeField] private float phase1RecoverDuration = 0.3f;
    [SerializeField] private float phase2RecoverDuration = 0.2f;

    [Header("Phase 2 Double Slam")]
    [SerializeField, Range(0f, 1f)] private float doubleSlamChance = 0.45f;
    [SerializeField] private float doubleSlamDelay = 0.25f;

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
        phase1WarningDuration = Mathf.Max(0.4f, phase1WarningDuration);
        phase2WarningDuration = Mathf.Max(0.4f, phase2WarningDuration);
        impactDamageDuration = Mathf.Max(0f, impactDamageDuration);
        phase1VulnerableDuration = Mathf.Max(0f, phase1VulnerableDuration);
        phase2VulnerableDuration = Mathf.Max(0f, phase2VulnerableDuration);
        phase1RecoverDuration = Mathf.Max(0f, phase1RecoverDuration);
        phase2RecoverDuration = Mathf.Max(0f, phase2RecoverDuration);
        doubleSlamChance = Mathf.Clamp01(doubleSlamChance);
        doubleSlamDelay = Mathf.Max(0f, doubleSlamDelay);
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

    public override void CancelActiveAttack()
    {
        CleanupActiveSlam();
    }

    public override IEnumerator Execute(OctopusBossController boss)
    {
        Transform firstSlamPoint = SelectSlamPoint(boss, null);
        if (boss == null || boss.IsDead || firstSlamPoint == null || tentacleSlamPrefab == null)
            yield break;

        bool useDoubleSlam = boss.IsPhaseTwo && Random.value < doubleSlamChance;
        yield return ExecuteSingleSlam(boss, firstSlamPoint);

        if (!useDoubleSlam || boss == null || boss.IsDead)
            yield break;

        if (doubleSlamDelay > 0f)
            yield return new WaitForSeconds(doubleSlamDelay);

        if (boss.IsDead)
            yield break;

        Transform secondSlamPoint = SelectSlamPoint(boss, firstSlamPoint);
        if (secondSlamPoint != null)
            yield return ExecuteSingleSlam(boss, secondSlamPoint);
    }

    private IEnumerator ExecuteSingleSlam(OctopusBossController boss, Transform slamPoint)
    {
        float warningDuration = boss.IsPhaseTwo ? phase2WarningDuration : phase1WarningDuration;
        float vulnerableDuration = boss.IsPhaseTwo ? phase2VulnerableDuration : phase1VulnerableDuration;
        float recoverDuration = boss.IsPhaseTwo ? phase2RecoverDuration : phase1RecoverDuration;

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

    private Transform SelectSlamPoint(OctopusBossController boss, Transform excludedPoint)
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
            if (point != null && point != excludedPoint)
                validSlamPoints.Add(point);
        }

        if (validSlamPoints.Count == 0 && excludedPoint != null)
            validSlamPoints.Add(excludedPoint);

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
