using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctopusSlowFieldAttack : OctopusBossAttack
{
    [Header("Prefabs")]
    [SerializeField] private OctopusSlowFieldZone slowFieldPrefab;
    [SerializeField] private GameObject slowFieldWarningPrefab;
    [SerializeField] private OctopusNoteProjectile noteProjectilePrefab;
    [SerializeField] private BossWeakPoint weakPointPrefab;

    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Transform[] noteSpawnPoints;
    [SerializeField] private LayerMask groundLayerMask = 1 << 6;
    [SerializeField] private float groundProbeHeight = 8f;

    [Header("Slow Field")]
    [SerializeField] private Vector2 fieldSize = new Vector2(6f, 2.4f);
    [SerializeField] private float warningDurationPhase1 = 0.8f;
    [SerializeField] private float warningDurationPhase2 = 0.55f;
    [SerializeField] private float fieldDurationPhase1 = 3.2f;
    [SerializeField] private float fieldDurationPhase2 = 3.8f;
    [SerializeField, Range(0.05f, 1f)] private float movementMultiplierPhase1 = 0.65f;
    [SerializeField, Range(0.05f, 1f)] private float movementMultiplierPhase2 = 0.5f;

    [Header("Note Waves")]
    [SerializeField] private int noteWaveCountPhase1 = 3;
    [SerializeField] private int noteWaveCountPhase2 = 4;
    [SerializeField] private int notesPerWavePhase1 = 2;
    [SerializeField] private int notesPerWavePhase2 = 3;
    [SerializeField] private float waveIntervalPhase1 = 0.65f;
    [SerializeField] private float waveIntervalPhase2 = 0.45f;
    [SerializeField] private float noteSpreadAngle = 14f;

    [Header("Projectile")]
    [SerializeField] private float noteProjectileSpeed = 8.5f;
    [SerializeField] private float phase2ProjectileSpeedMultiplier = 1.2f;
    [SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private int playerDamage = 1;

    [Header("Vulnerable Window")]
    [SerializeField] private float vulnerableDurationPhase1 = 1f;
    [SerializeField] private float vulnerableDurationPhase2 = 0.8f;
    [SerializeField] private float recoverDuration = 0.4f;

    private readonly List<OctopusNoteProjectile> activeProjectiles = new List<OctopusNoteProjectile>();
    private OctopusBossController activeBoss;
    private GameObject activeWarning;
    private OctopusSlowFieldZone activeField;
    private BossWeakPoint activeWeakPoint;
    private Vector3 lockedFieldPosition;
    private bool cancelRequested;
    private bool warnedMissingConfiguration;

    public override bool CanRepeatConsecutively => false;

    public override void CancelActiveAttack()
    {
        cancelRequested = true;
        CleanupAttackObjects();
    }

    private void Awake()
    {
        ResolvePlayerTarget();
    }

    private void OnDisable()
    {
        cancelRequested = true;
        CleanupAttackObjects();
    }

    private void OnValidate()
    {
        fieldSize.x = Mathf.Max(0.1f, fieldSize.x);
        fieldSize.y = Mathf.Max(0.1f, fieldSize.y);
        groundProbeHeight = Mathf.Max(0.1f, groundProbeHeight);
        warningDurationPhase1 = Mathf.Max(0.4f, warningDurationPhase1);
        warningDurationPhase2 = Mathf.Max(0.4f, warningDurationPhase2);
        fieldDurationPhase1 = Mathf.Max(0.1f, fieldDurationPhase1);
        fieldDurationPhase2 = Mathf.Max(0.1f, fieldDurationPhase2);
        movementMultiplierPhase1 = Mathf.Clamp(movementMultiplierPhase1, 0.05f, 1f);
        movementMultiplierPhase2 = Mathf.Clamp(movementMultiplierPhase2, 0.05f, 1f);
        noteWaveCountPhase1 = Mathf.Max(1, noteWaveCountPhase1);
        noteWaveCountPhase2 = Mathf.Max(1, noteWaveCountPhase2);
        notesPerWavePhase1 = Mathf.Max(1, notesPerWavePhase1);
        notesPerWavePhase2 = Mathf.Max(1, notesPerWavePhase2);
        waveIntervalPhase1 = Mathf.Max(0f, waveIntervalPhase1);
        waveIntervalPhase2 = Mathf.Max(0f, waveIntervalPhase2);
        noteSpreadAngle = Mathf.Max(0f, noteSpreadAngle);
        noteProjectileSpeed = Mathf.Max(0.01f, noteProjectileSpeed);
        phase2ProjectileSpeedMultiplier = Mathf.Max(0.01f, phase2ProjectileSpeedMultiplier);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
        playerDamage = Mathf.Max(1, playerDamage);
        vulnerableDurationPhase1 = Mathf.Max(0f, vulnerableDurationPhase1);
        vulnerableDurationPhase2 = Mathf.Max(0f, vulnerableDurationPhase2);
        recoverDuration = Mathf.Max(0f, recoverDuration);
    }

    public override bool CanUse(OctopusBossController boss)
    {
        ResolvePlayerTarget();

        if (!base.CanUse(boss) || boss.CurrentState == OctopusBossState.PhaseTransition)
            return false;

        bool configured = slowFieldPrefab != null &&
            slowFieldWarningPrefab != null &&
            noteProjectilePrefab != null &&
            weakPointPrefab != null &&
            playerTarget != null &&
            HasValidNoteSpawnPoint();

        if (!configured && !warnedMissingConfiguration)
        {
            warnedMissingConfiguration = true;
            Debug.LogWarning(
                "Octopus Slow Field Attack is missing its field, warning, note projectile, weak point, player, or note spawn references. The attack will be skipped.",
                this);
        }

        if (configured)
            warnedMissingConfiguration = false;

        return configured;
    }

    public override IEnumerator Execute(OctopusBossController boss)
    {
        if (!CanUse(boss))
            yield break;

        CleanupAttackObjects();
        cancelRequested = false;
        activeBoss = boss;
        activeBoss.Died += HandleBossDied;

        lockedFieldPosition = CalculateLockedFieldPosition();
        boss.EnterTelegraphState();
        activeWarning = Instantiate(slowFieldWarningPrefab, lockedFieldPosition, Quaternion.identity);
        ScaleVisualToFieldSize(activeWarning);

        float warningDuration = boss.IsPhaseTwo ? warningDurationPhase2 : warningDurationPhase1;
        yield return WaitForAttackDuration(warningDuration);

        DestroyActiveWarning();
        if (ShouldCancel(boss))
        {
            CleanupAttackObjects();
            yield break;
        }

        boss.EnterAttackingState();
        float movementMultiplier = boss.IsPhaseTwo ? movementMultiplierPhase2 : movementMultiplierPhase1;
        activeField = Instantiate(slowFieldPrefab, lockedFieldPosition, Quaternion.identity);
        activeField.Begin(boss, fieldSize, movementMultiplier);

        float fieldDuration = boss.IsPhaseTwo ? fieldDurationPhase2 : fieldDurationPhase1;
        int waveCount = boss.IsPhaseTwo ? noteWaveCountPhase2 : noteWaveCountPhase1;
        int notesPerWave = boss.IsPhaseTwo ? notesPerWavePhase2 : notesPerWavePhase1;
        float waveInterval = boss.IsPhaseTwo ? waveIntervalPhase2 : waveIntervalPhase1;
        float projectileSpeed = boss.IsPhaseTwo
            ? noteProjectileSpeed * phase2ProjectileSpeedMultiplier
            : noteProjectileSpeed;

        float elapsed = 0f;
        float nextWaveTime = 0f;
        int waveIndex = 0;

        while (elapsed < fieldDuration && !ShouldCancel(boss))
        {
            while (waveIndex < waveCount && elapsed >= nextWaveTime)
            {
                SpawnNoteWave(boss, waveIndex, notesPerWave, projectileSpeed);
                waveIndex++;
                nextWaveTime += Mathf.Max(0.01f, waveInterval);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyActiveField();
        DestroyActiveProjectiles();

        if (ShouldCancel(boss))
        {
            CleanupAttackObjects();
            yield break;
        }

        boss.EnterVulnerableWindowState();
        activeWeakPoint = Instantiate(weakPointPrefab, lockedFieldPosition, Quaternion.identity);
        activeWeakPoint.SetBoss(boss);
        activeWeakPoint.SetVulnerable(true);

        float vulnerableDuration = boss.IsPhaseTwo ? vulnerableDurationPhase2 : vulnerableDurationPhase1;
        yield return WaitForAttackDuration(vulnerableDuration);

        DestroyActiveWeakPoint();
        if (ShouldCancel(boss))
        {
            CleanupAttackObjects();
            yield break;
        }

        if (boss.CurrentState != OctopusBossState.PhaseTransition)
            boss.EnterRecoverState();

        yield return WaitForAttackDuration(recoverDuration);
        CleanupAttackObjects();
    }

    private IEnumerator WaitForAttackDuration(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !cancelRequested && activeBoss != null && !activeBoss.IsDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void SpawnNoteWave(
        OctopusBossController boss,
        int waveIndex,
        int notesPerWave,
        float projectileSpeed)
    {
        if (noteProjectilePrefab == null || playerTarget == null || noteSpawnPoints == null)
            return;

        Vector3 lockedTargetPosition = playerTarget.position;
        int spawnedNotes = 0;

        for (int i = 0; i < notesPerWave; i++)
        {
            Transform spawnPoint = GetNextValidSpawnPoint(waveIndex * notesPerWave + i);
            if (spawnPoint == null)
                continue;

            Vector2 baseDirection = lockedTargetPosition - spawnPoint.position;
            if (baseDirection.sqrMagnitude <= 0.0001f)
                baseDirection = Vector2.left;

            float spreadT = notesPerWave <= 1 ? 0.5f : (float)i / (notesPerWave - 1);
            float spreadOffset = Mathf.Lerp(-noteSpreadAngle * 0.5f, noteSpreadAngle * 0.5f, spreadT);
            Vector2 shotDirection = Quaternion.Euler(0f, 0f, spreadOffset) * baseDirection.normalized;

            OctopusNoteProjectile projectile = Instantiate(noteProjectilePrefab, spawnPoint.position, Quaternion.identity);
            if (projectile == null)
                continue;

            projectile.Launch(boss, shotDirection, projectileSpeed, projectileLifetime, playerDamage);
            activeProjectiles.Add(projectile);
            spawnedNotes++;
        }

        if (spawnedNotes == 0 && !warnedMissingConfiguration)
        {
            warnedMissingConfiguration = true;
            Debug.LogWarning("Octopus Slow Field Attack could not find a valid note spawn point.", this);
        }
    }

    private Vector3 CalculateLockedFieldPosition()
    {
        Vector3 targetPosition = playerTarget != null ? playerTarget.position : transform.position;
        Vector2 rayOrigin = (Vector2)targetPosition + Vector2.up * groundProbeHeight;
        float rayDistance = groundProbeHeight * 2f + fieldSize.y;
        RaycastHit2D groundHit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, groundLayerMask);

        if (groundHit.collider != null)
            targetPosition.y = groundHit.point.y + fieldSize.y * 0.5f;

        targetPosition.z = 0f;
        return targetPosition;
    }

    private void ScaleVisualToFieldSize(GameObject target)
    {
        if (target == null)
            return;

        SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
        {
            target.transform.localScale = new Vector3(fieldSize.x, fieldSize.y, 1f);
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            fieldSize.x / Mathf.Max(0.01f, spriteSize.x),
            fieldSize.y / Mathf.Max(0.01f, spriteSize.y),
            renderer.transform.localScale.z);
    }

    private bool HasValidNoteSpawnPoint()
    {
        if (noteSpawnPoints == null)
            return false;

        for (int i = 0; i < noteSpawnPoints.Length; i++)
        {
            if (noteSpawnPoints[i] != null)
                return true;
        }

        return false;
    }

    private Transform GetNextValidSpawnPoint(int startIndex)
    {
        if (noteSpawnPoints == null || noteSpawnPoints.Length == 0)
            return null;

        for (int i = 0; i < noteSpawnPoints.Length; i++)
        {
            Transform spawnPoint = noteSpawnPoints[(startIndex + i) % noteSpawnPoints.Length];
            if (spawnPoint != null)
                return spawnPoint;
        }

        return null;
    }

    private bool ShouldCancel(OctopusBossController boss)
    {
        return cancelRequested || boss == null || boss.IsDead;
    }

    private void HandleBossDied()
    {
        cancelRequested = true;
        CleanupAttackObjects();
    }

    private void CleanupAttackObjects()
    {
        if (activeBoss != null)
            activeBoss.Died -= HandleBossDied;

        activeBoss = null;
        DestroyActiveWarning();
        DestroyActiveField();
        DestroyActiveProjectiles();
        DestroyActiveWeakPoint();
    }

    private void DestroyActiveWarning()
    {
        if (activeWarning == null)
            return;

        Destroy(activeWarning);
        activeWarning = null;
    }

    private void DestroyActiveField()
    {
        if (activeField == null)
            return;

        activeField.CancelAndCleanup();
        activeField = null;
    }

    private void DestroyActiveProjectiles()
    {
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            if (activeProjectiles[i] != null)
                activeProjectiles[i].CancelAndCleanup();
        }

        activeProjectiles.Clear();
    }

    private void DestroyActiveWeakPoint()
    {
        if (activeWeakPoint == null)
            return;

        activeWeakPoint.SetVulnerable(false);
        Destroy(activeWeakPoint.gameObject);
        activeWeakPoint = null;
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
