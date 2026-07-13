using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class OctopusDropSweepComboAttack : OctopusBossAttack
{
    [Header("Combo Attacks")]
    [SerializeField] private OctopusDropAttack dropAttack;
    [SerializeField] private SideSweepAttack sideSweepAttack;

    [Header("Timing")]
    [SerializeField] private float sideSweepDelayAfterFirstDrop = 0.5f;

    private OctopusBossController activeBoss;
    private int runningAttackCount;

    private void OnValidate()
    {
        sideSweepDelayAfterFirstDrop = Mathf.Max(0f, sideSweepDelayAfterFirstDrop);
    }

    private void OnDisable()
    {
        CleanupCombo();
    }

    public override bool CanUse(OctopusBossController boss)
    {
        return base.CanUse(boss) &&
            boss.IsPhaseTwo &&
            boss.CurrentState != OctopusBossState.PhaseTransition &&
            dropAttack != null &&
            sideSweepAttack != null &&
            dropAttack.CanUse(boss) &&
            sideSweepAttack.CanUse(boss);
    }

    public override void CancelActiveAttack()
    {
        dropAttack?.CancelActiveAttack();
        sideSweepAttack?.CancelActiveAttack();
        CleanupCombo();
    }

    public override IEnumerator Execute(OctopusBossController boss)
    {
        if (!CanUse(boss))
            yield break;

        activeBoss = boss;
        activeBoss.Died += HandleBossDied;
        runningAttackCount = 0;

        StartTrackedAttack(dropAttack.Execute(boss));

        float sweepStartDelay = dropAttack.GetCurrentWarningDuration(boss) + sideSweepDelayAfterFirstDrop;
        if (sweepStartDelay > 0f)
            yield return new WaitForSeconds(sweepStartDelay);

        if (boss == null || boss.IsDead)
        {
            CleanupCombo();
            yield break;
        }

        StartTrackedAttack(sideSweepAttack.Execute(boss));

        while (boss != null && !boss.IsDead && runningAttackCount > 0)
            yield return null;

        CleanupCombo();
    }

    private void StartTrackedAttack(IEnumerator attackRoutine)
    {
        if (attackRoutine == null)
            return;

        runningAttackCount++;
        StartCoroutine(RunTrackedAttack(attackRoutine));
    }

    private IEnumerator RunTrackedAttack(IEnumerator attackRoutine)
    {
        yield return attackRoutine;
        runningAttackCount = Mathf.Max(0, runningAttackCount - 1);
    }

    private void HandleBossDied()
    {
        CleanupCombo();
    }

    private void CleanupCombo()
    {
        if (activeBoss != null)
            activeBoss.Died -= HandleBossDied;

        activeBoss = null;
        runningAttackCount = 0;
        StopAllCoroutines();
    }
}
