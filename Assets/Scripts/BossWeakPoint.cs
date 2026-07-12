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

    private bool warnedMissingBoss;

    public OctopusBossController Boss => boss;
    public bool IsVulnerable => isVulnerable;

    private void Reset()
    {
        boss = GetComponentInParent<OctopusBossController>();
    }

    private void Awake()
    {
        if (boss == null)
        {
            boss = GetComponentInParent<OctopusBossController>();
        }
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

        if (!isVulnerable || boss.IsDead)
            return false;

        boss.TakeDamage(Mathf.Max(1, damage) * Mathf.Max(1, damageMultiplier), damageSourcePosition);
        return true;
    }

    public void SetVulnerable(bool vulnerable)
    {
        isVulnerable = vulnerable;
    }

    public void SetBoss(OctopusBossController targetBoss)
    {
        boss = targetBoss;
        warnedMissingBoss = false;
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

    private void WarnMissingBoss()
    {
        if (warnedMissingBoss)
            return;

        warnedMissingBoss = true;
        Debug.LogWarning($"BossWeakPoint on '{name}' has no OctopusBossController assigned.", this);
    }
}
