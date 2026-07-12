using System.Collections;
using UnityEngine;

public class SideSweepAttack : OctopusBossAttack
{
    [Header("Prefabs")]
    [SerializeField] private SideSweepInstance sideSweepPrefab;
    [SerializeField] private GameObject warningIndicatorPrefab;

    [Header("Sweep Edges")]
    [SerializeField] private Transform leftStartPoint;
    [SerializeField] private Transform rightStartPoint;

    [Header("Height Options")]
    [SerializeField] private Transform[] sweepSpawnPoints;

    [Header("Timing")]
    [SerializeField] private float phase1WarningDuration = 0.7f;
    [SerializeField] private float phase2WarningDuration = 0.5f;
    [SerializeField] private float recoverDuration = 0.15f;

    [Header("Motion")]
    [SerializeField] private float sweepSpeed = 15f;
    [SerializeField] private float phase2SpeedMultiplier = 1.3f;
    [SerializeField] private bool randomizeDirection = true;
    [SerializeField] private bool alternateDirection = true;
    [SerializeField] private bool startFromLeftFirst = true;

    [Header("Warning Visual")]
    [SerializeField] private float warningArrowLength = 2.5f;
    [SerializeField] private float warningArrowHeadLength = 0.8f;
    [SerializeField] private float warningThickness = 0.25f;
    [SerializeField] private float warningEdgeInset = 1.2f;

    [Header("Damage")]
    [SerializeField] private int playerDamage = 1;

    private int nextHeightIndex;
    private bool nextStartsFromLeft = true;
    private GameObject activeWarning;
    private SideSweepInstance activeSweep;
    private OctopusBossController activeBoss;

    private void Awake()
    {
        nextStartsFromLeft = startFromLeftFirst;
    }

    private void OnDisable()
    {
        CleanupActiveSweep();
    }

    private void OnValidate()
    {
        phase1WarningDuration = Mathf.Max(0.4f, phase1WarningDuration);
        phase2WarningDuration = Mathf.Max(0.4f, phase2WarningDuration);
        recoverDuration = Mathf.Max(0f, recoverDuration);
        sweepSpeed = Mathf.Max(0.01f, sweepSpeed);
        phase2SpeedMultiplier = Mathf.Max(0.01f, phase2SpeedMultiplier);
        warningArrowLength = Mathf.Max(0.1f, warningArrowLength);
        warningArrowHeadLength = Mathf.Max(0.1f, warningArrowHeadLength);
        warningThickness = Mathf.Max(0.01f, warningThickness);
        warningEdgeInset = Mathf.Max(0f, warningEdgeInset);
        playerDamage = Mathf.Max(1, playerDamage);
    }

    public override bool CanUse(OctopusBossController boss)
    {
        return base.CanUse(boss) &&
            sideSweepPrefab != null &&
            leftStartPoint != null &&
            rightStartPoint != null;
    }

    public override IEnumerator Execute(OctopusBossController boss)
    {
        if (boss == null || boss.IsDead || sideSweepPrefab == null || leftStartPoint == null || rightStartPoint == null)
            yield break;

        bool startsFromLeft = GetNextDirectionStartsFromLeft();
        float sweepY = GetNextSweepHeight(startsFromLeft);
        Vector3 startPosition = GetEdgePosition(startsFromLeft ? leftStartPoint : rightStartPoint, sweepY);
        Vector3 endPosition = GetEdgePosition(startsFromLeft ? rightStartPoint : leftStartPoint, sweepY);

        activeBoss = boss;
        activeBoss.Died += HandleBossDied;
        boss.EnterTelegraphState();

        activeWarning = CreateWarning(startPosition, endPosition);

        float warningDuration = GetCurrentWarningDuration(boss);
        if (warningDuration > 0f)
            yield return new WaitForSeconds(warningDuration);

        DestroyActiveWarning();

        if (boss.IsDead)
        {
            CleanupActiveSweep();
            yield break;
        }

        activeSweep = Instantiate(sideSweepPrefab, startPosition, Quaternion.identity);
        float currentSweepSpeed = boss.IsPhaseTwo ? sweepSpeed * phase2SpeedMultiplier : sweepSpeed;
        yield return activeSweep.PlaySweep(boss, startPosition, endPosition, currentSweepSpeed, playerDamage);

        if (boss != null && !boss.IsDead)
        {
            boss.EnterRecoverState();

            if (recoverDuration > 0f)
                yield return new WaitForSeconds(recoverDuration);
        }

        CleanupActiveSweep();
    }

    public float GetCurrentWarningDuration(OctopusBossController boss)
    {
        return boss != null && boss.IsPhaseTwo
            ? phase2WarningDuration
            : phase1WarningDuration;
    }

    private void HandleBossDied()
    {
        CleanupActiveSweep();
    }

    private void CleanupActiveSweep()
    {
        if (activeBoss != null)
            activeBoss.Died -= HandleBossDied;

        activeBoss = null;
        DestroyActiveWarning();

        if (activeSweep != null)
        {
            activeSweep.CancelAndCleanup();
            activeSweep = null;
        }
    }

    private void DestroyActiveWarning()
    {
        if (activeWarning == null)
            return;

        Destroy(activeWarning);
        activeWarning = null;
    }

    private bool GetNextDirectionStartsFromLeft()
    {
        if (randomizeDirection)
            return Random.value < 0.5f;

        if (!alternateDirection)
            return startFromLeftFirst;

        bool result = nextStartsFromLeft;
        nextStartsFromLeft = !nextStartsFromLeft;
        return result;
    }

    private float GetNextSweepHeight(bool startsFromLeft)
    {
        Transform point = GetNextSweepPoint();
        if (point != null)
            return point.position.y;

        Transform fallbackPoint = startsFromLeft ? leftStartPoint : rightStartPoint;
        return fallbackPoint != null ? fallbackPoint.position.y : transform.position.y;
    }

    private Transform GetNextSweepPoint()
    {
        if (sweepSpawnPoints == null || sweepSpawnPoints.Length == 0)
            return null;

        for (int i = 0; i < sweepSpawnPoints.Length; i++)
        {
            int index = nextHeightIndex % sweepSpawnPoints.Length;
            nextHeightIndex++;

            Transform point = sweepSpawnPoints[index];
            if (point != null)
                return point;
        }

        return null;
    }

    private Vector3 GetEdgePosition(Transform edgePoint, float y)
    {
        Vector3 position = edgePoint.position;
        position.y = y;
        return position;
    }

    private GameObject CreateWarning(Vector3 startPosition, Vector3 endPosition)
    {
        if (warningIndicatorPrefab == null)
            return null;

        Vector3 direction = endPosition - startPosition;
        if (direction.sqrMagnitude <= 0.001f)
            return null;

        direction.Normalize();
        Vector3 arrowStart = startPosition + direction * warningEdgeInset;
        Vector3 arrowTip = arrowStart + direction * warningArrowLength;
        GameObject warningRoot = new GameObject("OctopusSideSweepDirectionWarning");
        warningRoot.transform.SetPositionAndRotation(arrowStart, Quaternion.identity);

        CreateWarningSegment(
            warningRoot.transform,
            Vector3.Lerp(arrowStart, arrowTip, 0.5f),
            direction,
            warningArrowLength);

        Vector3 upperHeadDirection = Quaternion.Euler(0f, 0f, 150f) * direction;
        Vector3 lowerHeadDirection = Quaternion.Euler(0f, 0f, -150f) * direction;
        CreateWarningSegment(
            warningRoot.transform,
            arrowTip + upperHeadDirection * warningArrowHeadLength * 0.5f,
            upperHeadDirection,
            warningArrowHeadLength);
        CreateWarningSegment(
            warningRoot.transform,
            arrowTip + lowerHeadDirection * warningArrowHeadLength * 0.5f,
            lowerHeadDirection,
            warningArrowHeadLength);

        return warningRoot;
    }

    private void CreateWarningSegment(Transform parent, Vector3 position, Vector3 direction, float length)
    {
        GameObject segment = Instantiate(warningIndicatorPrefab, position, Quaternion.identity, parent);
        segment.transform.right = direction;

        Vector3 scale = segment.transform.localScale;
        SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            scale.x = length / Mathf.Max(0.01f, spriteSize.x);
            scale.y = warningThickness / Mathf.Max(0.01f, spriteSize.y);
        }
        else
        {
            scale.x = length;
            scale.y = warningThickness;
        }

        segment.transform.localScale = scale;
    }
}
