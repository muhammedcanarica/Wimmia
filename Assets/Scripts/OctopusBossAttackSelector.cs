using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(OctopusBossController))]
public class OctopusBossAttackSelector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OctopusBossController boss;
    [SerializeField] private OctopusBossAttack[] attacks;

    [Header("Loop")]
    [SerializeField] private bool startLoopOnEnable = true;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float phaseTwoAttackCooldown = 1.1f;

    private Coroutine loopRoutine;
    private bool isAttackRunning;
    private int nextAttackIndex;

    public bool IsAttackRunning => isAttackRunning;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (startLoopOnEnable)
        {
            StartLoop();
        }
    }

    private void OnDisable()
    {
        StopLoop();
    }

    private void OnValidate()
    {
        attackCooldown = Mathf.Max(0f, attackCooldown);
        phaseTwoAttackCooldown = Mathf.Max(0f, phaseTwoAttackCooldown);
        CacheReferences();
    }

    public void StartLoop()
    {
        if (loopRoutine != null || boss == null || boss.IsDead)
            return;

        loopRoutine = StartCoroutine(AttackLoopRoutine());
    }

    public void StopLoop()
    {
        if (loopRoutine == null)
            return;

        StopCoroutine(loopRoutine);
        loopRoutine = null;
        isAttackRunning = false;
    }

    private IEnumerator AttackLoopRoutine()
    {
        yield return WaitForCurrentCooldown();

        while (boss != null && !boss.IsDead)
        {
            OctopusBossAttack selectedAttack = SelectNextAttack();
            if (selectedAttack == null)
            {
                boss.EnterIdleState();
                yield return WaitForCurrentCooldown();
                continue;
            }

            isAttackRunning = true;
            yield return selectedAttack.Execute(boss);
            isAttackRunning = false;

            if (boss != null && !boss.IsDead)
            {
                boss.EnterIdleState();
                yield return WaitForCurrentCooldown();
            }
        }

        loopRoutine = null;
        isAttackRunning = false;
    }

    private IEnumerator WaitForCurrentCooldown()
    {
        float cooldown = GetCurrentCooldown();
        if (cooldown > 0f)
            yield return new WaitForSeconds(cooldown);
    }

    private float GetCurrentCooldown()
    {
        if (boss != null && boss.IsPhaseTwo)
            return Mathf.Min(attackCooldown, phaseTwoAttackCooldown);

        return attackCooldown;
    }

    private OctopusBossAttack SelectNextAttack()
    {
        if (attacks == null || attacks.Length == 0)
            return null;

        for (int i = 0; i < attacks.Length; i++)
        {
            int index = nextAttackIndex % attacks.Length;
            nextAttackIndex++;

            OctopusBossAttack attack = attacks[index];
            if (attack != null && attack.CanUse(boss))
                return attack;
        }

        return null;
    }

    private void CacheReferences()
    {
        if (boss == null)
            boss = GetComponent<OctopusBossController>();
    }
}
