using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BossWeakPoint : MonoBehaviour
{
    [Header("Boss Link")]
    [SerializeField] private OctopusBossController boss;
    [SerializeField] private int damageMultiplier = 1;

    [Header("Damage Window")]
    [SerializeField] private bool isVulnerable = true;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer weakPointVisual;
    [SerializeField] private Color inactiveColor = new Color(0.45f, 0.25f, 0.55f, 0.2f);
    [SerializeField] private Color vulnerableColor = new Color(1f, 0.55f, 0.95f, 1f);
    [SerializeField, Min(1f)] private float pulseScaleMultiplier = 1.12f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
    [SerializeField] private ParticleSystem vulnerableLoopParticle;
    [SerializeField] private ParticleSystem hitParticlePrefab;
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.14f;
    [SerializeField] private bool hideVisualWhenInactive = true;

    private bool warnedMissingBoss;
    private bool damageConsumedThisWindow;
    private bool bossDeathSubscribed;
    private Transform cachedVisualTransform;
    private Vector3 baseVisualScale = Vector3.one;
    private Coroutine hitFeedbackRoutine;
    private ParticleSystem activeHitParticle;

    public OctopusBossController Boss => boss;
    public bool IsVulnerable => isVulnerable;

    private void Reset()
    {
        boss = GetComponentInParent<OctopusBossController>();
        weakPointVisual = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        CacheReferences();
        CaptureBaseVisualScale();
        damageConsumedThisWindow = false;
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureBaseVisualScale();
        SubscribeToBossDeath();

        if (boss != null && boss.IsDead)
        {
            isVulnerable = false;
            damageConsumedThisWindow = true;
        }

        ApplyCurrentVisualState();
    }

    private void Update()
    {
        if (boss != null && boss.IsDead)
        {
            HandleBossDied();
            return;
        }

        if (!isVulnerable || hitFeedbackRoutine != null || cachedVisualTransform == null)
            return;

        float pulseAmount = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float scaleMultiplier = Mathf.Lerp(1f, pulseScaleMultiplier, pulseAmount);
        cachedVisualTransform.localScale = new Vector3(
            baseVisualScale.x * scaleMultiplier,
            baseVisualScale.y * scaleMultiplier,
            baseVisualScale.z);
    }

    private void OnDisable()
    {
        isVulnerable = false;
        damageConsumedThisWindow = true;
        UnsubscribeFromBossDeath();
        StopAllFeedback();
    }

    private void OnDestroy()
    {
        UnsubscribeFromBossDeath();
    }

    private void OnValidate()
    {
        damageMultiplier = Mathf.Max(1, damageMultiplier);
        pulseScaleMultiplier = Mathf.Max(1f, pulseScaleMultiplier);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);

        if (!Application.isPlaying)
            CacheReferences();
    }

    public void TakeDamage(int damage, Vector2 damageSourcePosition)
    {
        TryTakeDamage(damage, damageSourcePosition);
    }

    public bool TryTakeDamage(int damage, Vector2 damageSourcePosition)
    {
        if (boss == null)
        {
            WarnMissingBoss();
            return false;
        }

        if (!isVulnerable || damageConsumedThisWindow || boss.IsDead)
            return false;

        // Close the window before forwarding damage so overlapping hitboxes cannot
        // damage the boss more than once in the same vulnerable window.
        isVulnerable = false;
        damageConsumedThisWindow = true;
        StopVulnerableLoopParticle();
        ResetVisualScale();

        boss.TakeDamage(Mathf.Max(1, damage) * damageMultiplier, damageSourcePosition);

        if (boss == null || boss.IsDead)
        {
            StopAllFeedback();
            return true;
        }

        PlayHitFeedback();
        return true;
    }

    public void SetVulnerable(bool vulnerable)
    {
        if (vulnerable)
        {
            if (boss != null && boss.IsDead)
            {
                isVulnerable = false;
                damageConsumedThisWindow = true;
                StopAllFeedback();
                return;
            }

            // Repeated true calls during one window must not reset the one-hit lock.
            if (isVulnerable)
                return;

            StopHitFeedbackRoutine();
            DestroyActiveHitParticle();
            isVulnerable = true;
            damageConsumedThisWindow = false;
            ApplyVulnerableVisual();
            return;
        }

        isVulnerable = false;
        StopHitFeedbackRoutine();
        DestroyActiveHitParticle();
        ApplyInactiveVisual();
    }

    public void SetBoss(OctopusBossController targetBoss)
    {
        if (boss == targetBoss)
        {
            SubscribeToBossDeath();
            return;
        }

        UnsubscribeFromBossDeath();
        boss = targetBoss;
        warnedMissingBoss = false;
        SubscribeToBossDeath();

        if (boss != null && boss.IsDead)
            HandleBossDied();
    }

    [ContextMenu("Debug/Set Vulnerable")]
    private void DebugSetVulnerable()
    {
        SetVulnerable(true);
    }

    [ContextMenu("Debug/Set Invulnerable")]
    private void DebugSetInvulnerable()
    {
        SetVulnerable(false);
    }

    private void CacheReferences()
    {
        if (boss == null)
            boss = GetComponentInParent<OctopusBossController>();

        if (weakPointVisual == null)
            weakPointVisual = GetComponentInChildren<SpriteRenderer>();
    }

    private void CaptureBaseVisualScale()
    {
        if (weakPointVisual == null)
            return;

        Transform visualTransform = weakPointVisual.transform;
        if (cachedVisualTransform == visualTransform)
            return;

        cachedVisualTransform = visualTransform;
        baseVisualScale = visualTransform.localScale;
    }

    private void ApplyCurrentVisualState()
    {
        if (isVulnerable && !damageConsumedThisWindow)
            ApplyVulnerableVisual();
        else
            ApplyInactiveVisual();
    }

    private void ApplyVulnerableVisual()
    {
        CaptureBaseVisualScale();

        if (weakPointVisual != null)
        {
            weakPointVisual.enabled = true;
            weakPointVisual.color = vulnerableColor;
        }

        ResetVisualScale();

        if (vulnerableLoopParticle != null && !vulnerableLoopParticle.isPlaying)
            vulnerableLoopParticle.Play(true);
    }

    private void ApplyInactiveVisual()
    {
        StopVulnerableLoopParticle();
        ResetVisualScale();

        if (weakPointVisual == null)
            return;

        weakPointVisual.color = inactiveColor;
        weakPointVisual.enabled = !hideVisualWhenInactive;
    }

    private void PlayHitFeedback()
    {
        StopHitFeedbackRoutine();
        SpawnHitParticle();

        if (weakPointVisual == null || !isActiveAndEnabled)
        {
            ApplyInactiveVisual();
            return;
        }

        hitFeedbackRoutine = StartCoroutine(HitFeedbackRoutine());
    }

    private IEnumerator HitFeedbackRoutine()
    {
        weakPointVisual.enabled = true;
        weakPointVisual.color = Color.white;

        if (hitFlashDuration <= 0f)
        {
            ApplyInactiveVisual();
            hitFeedbackRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < hitFlashDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / hitFlashDuration);
            float punch = 1f + (pulseScaleMultiplier - 1f) * Mathf.Sin(progress * Mathf.PI);

            if (cachedVisualTransform != null)
            {
                cachedVisualTransform.localScale = new Vector3(
                    baseVisualScale.x * punch,
                    baseVisualScale.y * punch,
                    baseVisualScale.z);
            }

            weakPointVisual.color = Color.Lerp(Color.white, vulnerableColor, progress);
            yield return null;
        }

        hitFeedbackRoutine = null;
        ApplyInactiveVisual();
    }

    private void SpawnHitParticle()
    {
        DestroyActiveHitParticle();

        if (hitParticlePrefab == null)
            return;

        Vector3 spawnPosition = weakPointVisual != null
            ? weakPointVisual.transform.position
            : transform.position;
        activeHitParticle = Instantiate(hitParticlePrefab, spawnPosition, Quaternion.identity, transform);
        activeHitParticle.Play(true);

        ParticleSystem.MainModule main = activeHitParticle.main;
        float cleanupDelay = Mathf.Max(0.1f, main.duration + main.startLifetime.constantMax);
        Destroy(activeHitParticle.gameObject, cleanupDelay);
    }

    private void StopVulnerableLoopParticle()
    {
        if (vulnerableLoopParticle == null)
            return;

        vulnerableLoopParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StopHitFeedbackRoutine()
    {
        if (hitFeedbackRoutine == null)
            return;

        StopCoroutine(hitFeedbackRoutine);
        hitFeedbackRoutine = null;
    }

    private void DestroyActiveHitParticle()
    {
        if (activeHitParticle == null)
            return;

        Destroy(activeHitParticle.gameObject);
        activeHitParticle = null;
    }

    private void StopAllFeedback()
    {
        StopHitFeedbackRoutine();
        StopVulnerableLoopParticle();
        DestroyActiveHitParticle();
        ApplyInactiveVisual();
    }

    private void ResetVisualScale()
    {
        if (cachedVisualTransform != null)
            cachedVisualTransform.localScale = baseVisualScale;
    }

    private void SubscribeToBossDeath()
    {
        if (!isActiveAndEnabled || boss == null || bossDeathSubscribed)
            return;

        boss.Died += HandleBossDied;
        bossDeathSubscribed = true;
    }

    private void UnsubscribeFromBossDeath()
    {
        if (!bossDeathSubscribed)
            return;

        if (boss != null)
            boss.Died -= HandleBossDied;

        bossDeathSubscribed = false;
    }

    private void HandleBossDied()
    {
        isVulnerable = false;
        damageConsumedThisWindow = true;
        StopAllFeedback();
    }

    private void WarnMissingBoss()
    {
        if (warnedMissingBoss)
            return;

        warnedMissingBoss = true;
        Debug.LogWarning($"BossWeakPoint on '{name}' has no OctopusBossController assigned.", this);
    }
}
