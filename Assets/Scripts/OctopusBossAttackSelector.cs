using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OctopusBossWeightedAttack
{
    [SerializeField] private OctopusBossAttack attack;
    [SerializeField] private float phaseOneWeight = 1f;
    [SerializeField] private float phaseTwoWeight = 1f;

    public OctopusBossAttack Attack => attack;

    public float GetWeight(bool isPhaseTwo)
    {
        return Mathf.Max(0f, isPhaseTwo ? phaseTwoWeight : phaseOneWeight);
    }

    public void Validate()
    {
        phaseOneWeight = Mathf.Max(0f, phaseOneWeight);
        phaseTwoWeight = Mathf.Max(0f, phaseTwoWeight);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(OctopusBossController))]
public class OctopusBossAttackSelector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OctopusBossController boss;
    [SerializeField] private OctopusBossWeightedAttack[] weightedAttacks;
    [SerializeField, HideInInspector] private OctopusBossAttack[] attacks;

    [Header("Loop")]
    [SerializeField] private bool startLoopOnEnable = true;
    [SerializeField] private float initialAttackDelay = 1f;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField] private float phaseTwoAttackCooldown = 0.7f;

    [Header("Phase 2 Combo")]
    [SerializeField] private OctopusBossAttack phaseTwoComboAttack;
    [SerializeField, Range(0f, 1f)] private float comboChance = 0.2f;

    [Header("Selection Rules")]
    [SerializeField] private int maxConsecutiveSameAttack = 2;
    [SerializeField] private bool debugAttackSelection;

    private readonly List<AttackCandidate> candidates = new List<AttackCandidate>();
    private readonly HashSet<OctopusBossAttack> processedAttacks = new HashSet<OctopusBossAttack>();
    private Coroutine loopRoutine;
    private bool isAttackRunning;
    private OctopusBossAttack lastSelectedAttack;
    private int consecutiveSameAttackCount;

    private struct AttackCandidate
    {
        public OctopusBossAttack Attack;
        public float Weight;

        public AttackCandidate(OctopusBossAttack attack, float weight)
        {
            Attack = attack;
            Weight = weight;
        }
    }

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

    private void Start()
    {
        if (startLoopOnEnable)
            StartLoop();
    }

    private void OnDisable()
    {
        StopLoop();
    }

    private void OnValidate()
    {
        attackCooldown = Mathf.Max(0f, attackCooldown);
        phaseTwoAttackCooldown = Mathf.Max(0f, phaseTwoAttackCooldown);
        initialAttackDelay = Mathf.Max(0f, initialAttackDelay);
        comboChance = Mathf.Clamp01(comboChance);
        maxConsecutiveSameAttack = Mathf.Max(1, maxConsecutiveSameAttack);

        if (weightedAttacks != null)
        {
            for (int i = 0; i < weightedAttacks.Length; i++)
                weightedAttacks[i]?.Validate();
        }

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
        if (initialAttackDelay > 0f)
            yield return new WaitForSeconds(initialAttackDelay);

        while (boss != null && !boss.IsDead)
        {
            yield return WaitUntilBossCanSelectAttack();
            if (boss == null || boss.IsDead)
                break;

            AttackCandidate selectedCandidate = SelectNextAttack();
            if (selectedCandidate.Attack == null)
            {
                boss.EnterIdleState();
                yield return WaitForCurrentCooldown();
                continue;
            }

            RegisterSelection(selectedCandidate);
            isAttackRunning = true;
            yield return selectedCandidate.Attack.Execute(boss);
            isAttackRunning = false;

            if (boss != null && !boss.IsDead)
            {
                yield return WaitUntilBossCanSelectAttack();
                if (boss == null || boss.IsDead)
                    break;

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

    private IEnumerator WaitUntilBossCanSelectAttack()
    {
        while (boss != null &&
            !boss.IsDead &&
            boss.CurrentState == OctopusBossState.PhaseTransition)
        {
            yield return null;
        }
    }

    private AttackCandidate SelectNextAttack()
    {
        candidates.Clear();
        processedAttacks.Clear();

        if (boss == null || boss.IsDead || boss.CurrentState == OctopusBossState.PhaseTransition)
            return default;

        if (weightedAttacks != null && weightedAttacks.Length > 0)
        {
            for (int i = 0; i < weightedAttacks.Length; i++)
            {
                OctopusBossWeightedAttack entry = weightedAttacks[i];
                if (entry == null)
                    continue;

                AddCandidate(entry.Attack, entry.GetWeight(boss.IsPhaseTwo));
            }
        }
        else if (attacks != null)
        {
            for (int i = 0; i < attacks.Length; i++)
                AddCandidate(attacks[i], 1f);
        }

        AttackCandidate comboCandidate = GetPhaseTwoComboCandidate();
        bool comboAvailable = comboCandidate.Attack != null;
        bool comboBlockedByRepeatLimit = comboAvailable &&
            comboCandidate.Attack == lastSelectedAttack &&
            consecutiveSameAttackCount >= maxConsecutiveSameAttack &&
            candidates.Count > 0;

        if (comboAvailable &&
            !comboBlockedByRepeatLimit &&
            Random.value < comboChance)
        {
            return comboCandidate;
        }

        if (candidates.Count == 0)
            return comboAvailable && !comboBlockedByRepeatLimit ? comboCandidate : default;

        bool hasAlternative = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Attack != lastSelectedAttack)
            {
                hasAlternative = true;
                break;
            }
        }

        bool comboIsAlternative = comboAvailable && comboCandidate.Attack != lastSelectedAttack;
        bool blockLastAttack = (hasAlternative || comboIsAlternative) &&
            lastSelectedAttack != null &&
            consecutiveSameAttackCount >= maxConsecutiveSameAttack;
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (!blockLastAttack || candidates[i].Attack != lastSelectedAttack)
                totalWeight += candidates[i].Weight;
        }

        if (totalWeight <= 0f)
            return comboAvailable && !comboBlockedByRepeatLimit ? comboCandidate : default;

        float roll = Random.Range(0f, totalWeight);
        AttackCandidate fallback = default;

        for (int i = 0; i < candidates.Count; i++)
        {
            AttackCandidate candidate = candidates[i];
            if (blockLastAttack && candidate.Attack == lastSelectedAttack)
                continue;

            fallback = candidate;
            roll -= candidate.Weight;
            if (roll <= 0f)
                return candidate;
        }

        return fallback;
    }

    private AttackCandidate GetPhaseTwoComboCandidate()
    {
        if (boss == null ||
            !boss.IsPhaseTwo ||
            comboChance <= 0f ||
            phaseTwoComboAttack == null ||
            !phaseTwoComboAttack.isActiveAndEnabled ||
            !phaseTwoComboAttack.CanUse(boss))
        {
            return default;
        }

        return new AttackCandidate(phaseTwoComboAttack, comboChance * 100f);
    }

    private void AddCandidate(OctopusBossAttack attack, float weight)
    {
        if (attack == null || weight <= 0f || !processedAttacks.Add(attack))
            return;

        if (!attack.isActiveAndEnabled || !attack.CanUse(boss))
            return;

        candidates.Add(new AttackCandidate(attack, weight));
    }

    private void RegisterSelection(AttackCandidate selection)
    {
        if (selection.Attack == lastSelectedAttack)
        {
            consecutiveSameAttackCount++;
        }
        else
        {
            lastSelectedAttack = selection.Attack;
            consecutiveSameAttackCount = 1;
        }

        if (debugAttackSelection)
        {
            int phase = boss != null && boss.IsPhaseTwo ? 2 : 1;
            Debug.Log(
                $"Octopus Boss selected {selection.Attack.GetType().Name} in Phase {phase} " +
                $"with weight {selection.Weight:0.##}. Consecutive count: {consecutiveSameAttackCount}/{maxConsecutiveSameAttack}.",
                this);
        }
    }

    private void CacheReferences()
    {
        if (boss == null)
            boss = GetComponent<OctopusBossController>();
    }
}
