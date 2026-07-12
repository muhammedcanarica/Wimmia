using System.Collections;
using System;
using UnityEngine;

public enum OctopusBossState
{
    Idle,
    Telegraph,
    Attacking,
    VulnerableWindow,
    Recover,
    Hurt,
    PhaseTransition,
    Dead
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class OctopusBossController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 6;
    [SerializeField] private int phaseTwoHealthThreshold = 3;

    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite hurtSprite;
    [SerializeField] private Sprite angrySprite;
    [SerializeField] private Sprite deadSprite;

    [Header("Hurt Feedback")]
    [SerializeField] private float initialHurtDisplayDuration = 0.22f;
    [SerializeField] private int hurtFlickerCount = 2;
    [SerializeField] private float hurtFlickerInterval = 0.09f;
    [SerializeField] private float totalHurtFeedbackDuration = 0.6f;

    [Header("State Timing")]
    [SerializeField] private float phaseTransitionDuration = 0.5f;

    [Header("Runtime Debug")]
    [SerializeField] private int currentHealth;
    [SerializeField] private OctopusBossState currentState = OctopusBossState.Idle;
    [SerializeField] private bool isPhaseTwo;

    private SpriteRenderer spriteRenderer;
    private OctopusBossAttackSelector attackSelector;
    private Coroutine temporaryStateRoutine;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsPhaseTwo => isPhaseTwo;
    public bool IsDead => currentState == OctopusBossState.Dead;
    public OctopusBossState CurrentState => currentState;
    public event Action Died;

    private void Reset()
    {
        CacheReferences();
        currentHealth = maxHealth;
        ApplySpriteForCurrentState();
    }

    private void Awake()
    {
        CacheReferences();
        ResetHealth();
        ApplySpriteForCurrentState();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        phaseTwoHealthThreshold = Mathf.Clamp(phaseTwoHealthThreshold, 1, maxHealth);
        initialHurtDisplayDuration = Mathf.Max(0f, initialHurtDisplayDuration);
        hurtFlickerCount = Mathf.Max(0, hurtFlickerCount);
        hurtFlickerInterval = Mathf.Max(0.01f, hurtFlickerInterval);
        totalHurtFeedbackDuration = Mathf.Max(initialHurtDisplayDuration, totalHurtFeedbackDuration);
        phaseTransitionDuration = Mathf.Max(0f, phaseTransitionDuration);

        if (!Application.isPlaying)
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
            isPhaseTwo = currentHealth > 0 && currentHealth <= phaseTwoHealthThreshold;
            CacheReferences();
            ApplySpriteForCurrentState();
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, transform.position);
    }

    public void TakeDamage(int damage, Vector2 damageSourcePosition)
    {
        if (damage <= 0 || IsDead)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"Octopus Boss took {damage} damage. HP: {currentHealth}/{maxHealth}", this);

        if (currentHealth <= 0)
        {
            EnterDeadState();
            return;
        }

        if (!isPhaseTwo && currentHealth <= phaseTwoHealthThreshold)
        {
            EnterPhaseTransition();
            return;
        }

        EnterTemporaryState(OctopusBossState.Hurt, totalHurtFeedbackDuration);
    }

    public void EnterIdleState()
    {
        if (!IsDead)
            SetState(OctopusBossState.Idle);
    }

    public void EnterTelegraphState()
    {
        if (!IsDead)
            SetState(OctopusBossState.Telegraph);
    }

    public void EnterAttackingState()
    {
        if (!IsDead)
            SetState(OctopusBossState.Attacking);
    }

    public void EnterVulnerableWindowState()
    {
        if (!IsDead)
            SetState(OctopusBossState.VulnerableWindow);
    }

    public void EnterRecoverState()
    {
        if (!IsDead)
            SetState(OctopusBossState.Recover);
    }

    [ContextMenu("Debug/Take 1 Damage")]
    public void DebugTakeOneDamage()
    {
        TakeDamage(1);
    }

    [ContextMenu("Debug/Reset Boss")]
    public void DebugResetBoss()
    {
        ResetHealth();
        EnterIdleState();
        attackSelector?.StartLoop();
    }

    private void ResetHealth()
    {
        currentHealth = maxHealth;
        isPhaseTwo = false;
        currentState = OctopusBossState.Idle;
    }

    private void EnterPhaseTransition()
    {
        isPhaseTwo = true;
        Debug.Log("Octopus Boss entered Phase 2.", this);
        EnterTemporaryState(OctopusBossState.PhaseTransition, phaseTransitionDuration);
    }

    private void EnterDeadState()
    {
        isPhaseTwo = true;
        attackSelector?.StopLoop();
        StopTemporaryStateRoutine();
        SetState(OctopusBossState.Dead);
        Died?.Invoke();
        Debug.Log("Octopus Boss is dead.", this);
    }

    private void EnterTemporaryState(OctopusBossState state, float duration)
    {
        OctopusBossState restoreState = currentState;
        StopTemporaryStateRoutine();
        SetState(state);

        if (duration <= 0f)
        {
            RestoreTemporaryState(state, restoreState);
            return;
        }

        temporaryStateRoutine = state == OctopusBossState.Hurt
            ? StartCoroutine(HurtFeedbackRoutine(duration, restoreState))
            : StartCoroutine(ReturnToStateAfter(duration, state, restoreState));
    }

    private IEnumerator HurtFeedbackRoutine(float duration, OctopusBossState restoreState)
    {
        float elapsed = 0f;
        Sprite hurtFeedbackSprite = hurtSprite != null ? hurtSprite : GetIdleOrPhaseSprite();
        Sprite normalSprite = GetSpriteForState(restoreState);

        SetFeedbackSprite(hurtFeedbackSprite);
        float initialDisplayTime = Mathf.Min(initialHurtDisplayDuration, duration);
        if (initialDisplayTime > 0f)
        {
            elapsed += initialDisplayTime;
            yield return new WaitForSeconds(initialDisplayTime);
        }

        for (int i = 0; i < hurtFlickerCount && elapsed < duration; i++)
        {
            SetFeedbackSprite(normalSprite);
            float normalDisplayTime = Mathf.Min(hurtFlickerInterval, duration - elapsed);
            if (normalDisplayTime > 0f)
            {
                elapsed += normalDisplayTime;
                yield return new WaitForSeconds(normalDisplayTime);
            }

            if (elapsed >= duration)
                break;

            SetFeedbackSprite(hurtFeedbackSprite);
            float hurtDisplayTime = Mathf.Min(hurtFlickerInterval, duration - elapsed);
            if (hurtDisplayTime > 0f)
            {
                elapsed += hurtDisplayTime;
                yield return new WaitForSeconds(hurtDisplayTime);
            }
        }

        if (elapsed < duration)
        {
            SetFeedbackSprite(normalSprite);
            yield return new WaitForSeconds(duration - elapsed);
        }

        temporaryStateRoutine = null;
        RestoreTemporaryState(OctopusBossState.Hurt, restoreState);
    }

    private void SetFeedbackSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private IEnumerator ReturnToStateAfter(float duration, OctopusBossState temporaryState, OctopusBossState restoreState)
    {
        yield return new WaitForSeconds(duration);
        temporaryStateRoutine = null;
        RestoreTemporaryState(temporaryState, restoreState);
    }

    private void RestoreTemporaryState(OctopusBossState temporaryState, OctopusBossState restoreState)
    {
        if (IsDead)
            return;

        if (currentState == temporaryState)
        {
            SetState(restoreState == OctopusBossState.Dead ? OctopusBossState.Idle : restoreState);
        }
        else
        {
            ApplySpriteForCurrentState();
        }
    }

    private void SetState(OctopusBossState nextState)
    {
        currentState = nextState;
        ApplySpriteForCurrentState();
    }

    private void ApplySpriteForCurrentState()
    {
        if (spriteRenderer == null)
            return;

        Sprite targetSprite = GetSpriteForState(currentState);
        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
        }
    }

    private Sprite GetSpriteForState(OctopusBossState state)
    {
        switch (state)
        {
            case OctopusBossState.Dead:
                return deadSprite != null ? deadSprite : GetIdleOrPhaseSprite();
            case OctopusBossState.Hurt:
                return hurtSprite != null ? hurtSprite : GetIdleOrPhaseSprite();
            case OctopusBossState.PhaseTransition:
            case OctopusBossState.Telegraph:
            case OctopusBossState.Attacking:
            case OctopusBossState.VulnerableWindow:
            case OctopusBossState.Recover:
                return GetIdleOrPhaseSprite();
            default:
                return GetIdleOrPhaseSprite();
        }
    }

    private Sprite GetIdleOrPhaseSprite()
    {
        if (isPhaseTwo && angrySprite != null)
            return angrySprite;

        return idleSprite;
    }

    private void StopTemporaryStateRoutine()
    {
        if (temporaryStateRoutine == null)
            return;

        StopCoroutine(temporaryStateRoutine);
        temporaryStateRoutine = null;
    }

    private void CacheReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (attackSelector == null)
            attackSelector = GetComponent<OctopusBossAttackSelector>();
    }
}
