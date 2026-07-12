using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerAttackController : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    public PlayerInputReader inputReader;

    [Header("Targeting")]
    public LayerMask enemyLayer = Physics2D.AllLayers;
    public LayerMask electricNodeLayer = Physics2D.AllLayers;

    [Header("Fin Melee Attack")]
    public int finAttackDamage = 1;
    public float finAttackCooldown = 0.35f;
    public float finAttackRadius = 0.55f;
    public Vector2 finAttackOffset = new Vector2(0.7f, 0f);

    [Header("Tail Electric Attack")]
    public int tailElectricDamage = 1;
    public float tailElectricStunDuration = 1f;
    public float tailElectricCooldown = 2f;
    public float tailElectricHitStopDuration = 0.04f;
    public Vector2 tailElectricSize = new Vector2(2.75f, 0.45f);
    public Vector2 tailElectricOffset = new Vector2(-1.45f, 0f);
    public bool tailAttackUsesBackDirection = true;

    private readonly HashSet<EnemyHealth> processedEnemies = new HashSet<EnemyHealth>();
    private readonly HashSet<BossWeakPoint> processedBossWeakPoints = new HashSet<BossWeakPoint>();
    private readonly HashSet<OctopusBossController> processedBosses = new HashSet<OctopusBossController>();
    private readonly HashSet<ElectricSwitch> processedSwitches = new HashSet<ElectricSwitch>();
    private float finAttackCooldownTimer;
    private float tailElectricCooldownTimer;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        if (playerController == null || inputReader == null)
            return;

        if (finAttackCooldownTimer > 0f)
            finAttackCooldownTimer -= Time.deltaTime;

        if (tailElectricCooldownTimer > 0f)
            tailElectricCooldownTimer -= Time.deltaTime;

        if (inputReader.ConsumePrimaryAttackPressed() && finAttackCooldownTimer <= 0f)
        {
            PerformFinAttack();
        }

        if (inputReader.ConsumeSecondaryAttackPressed() && tailElectricCooldownTimer <= 0f)
        {
            PerformTailElectricAttack();
        }
    }

    private void PerformFinAttack()
    {
        Vector2 attackCenter = GetFinAttackCenter();
        Collider2D[] hitResults = Physics2D.OverlapCircleAll(attackCenter, finAttackRadius, enemyLayer);
        DamageEnemies(hitResults, finAttackDamage, false, 0f);
        finAttackCooldownTimer = finAttackCooldown;
    }

    private void PerformTailElectricAttack()
    {
        Vector2 attackCenter = GetTailAttackCenter();
        Collider2D[] enemyHits = Physics2D.OverlapBoxAll(attackCenter, tailElectricSize, 0f, enemyLayer);
        Collider2D[] nodeHits = Physics2D.OverlapBoxAll(attackCenter, tailElectricSize, 0f, electricNodeLayer);
        bool hitEnemy = DamageEnemies(enemyHits, tailElectricDamage, true, tailElectricStunDuration);
        bool hitNode = ActivateElectricNodes(nodeHits);

        if (hitEnemy || hitNode)
        {
            playerController.TriggerHitStop(tailElectricHitStopDuration);
        }

        tailElectricCooldownTimer = tailElectricCooldown;
    }

    private bool DamageEnemies(Collider2D[] hitResults, int damage, bool applyStun, float stunDuration)
    {
        processedEnemies.Clear();
        processedBossWeakPoints.Clear();
        processedBosses.Clear();
        bool hitEnemy = false;

        for (int i = 0; i < hitResults.Length; i++)
        {
            Collider2D hit = hitResults[i];
            if (hit == null)
                continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = hit.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
            {
                BossWeakPoint bossWeakPoint = hit.GetComponent<BossWeakPoint>();
                if (bossWeakPoint == null)
                    bossWeakPoint = hit.GetComponentInParent<BossWeakPoint>();

                if (bossWeakPoint == null || !processedBossWeakPoints.Add(bossWeakPoint))
                    continue;

                if (!bossWeakPoint.IsVulnerable)
                    continue;

                OctopusBossController boss = bossWeakPoint.Boss;
                if (boss != null && !processedBosses.Add(boss))
                    continue;

                if (bossWeakPoint.TryTakeDamage(damage, transform.position))
                    hitEnemy = true;

                continue;
            }

            if (!processedEnemies.Add(enemyHealth))
                continue;

            hitEnemy = true;
            enemyHealth.TakeDamage(damage, transform.position);

            if (applyStun)
                enemyHealth.ApplyStun(stunDuration);
        }

        return hitEnemy;
    }

    private bool ActivateElectricNodes(Collider2D[] hitResults)
    {
        bool hitNode = false;
        processedSwitches.Clear();

        for (int i = 0; i < hitResults.Length; i++)
        {
            Collider2D hit = hitResults[i];
            if (hit == null)
                continue;

            ElectricNode node = hit.GetComponent<ElectricNode>();
            if (node == null)
                node = hit.GetComponentInParent<ElectricNode>();

            if (node != null)
            {
                hitNode = true;
                node.ReactToElectricHit();
            }

            ElectricSwitch electricSwitch = hit.GetComponent<ElectricSwitch>();
            if (electricSwitch == null)
                electricSwitch = hit.GetComponentInParent<ElectricSwitch>();

            if (electricSwitch == null || !processedSwitches.Add(electricSwitch))
                continue;

            hitNode = true;
            electricSwitch.ActivateSwitch();
        }

        return hitNode;
    }

    private Vector2 GetFinAttackCenter()
    {
        Vector2 horizontalDirection = playerController.FacingDirection;
        return (Vector2)transform.position + Vector2.Scale(finAttackOffset, new Vector2(horizontalDirection.x, 1f));
    }

    private Vector2 GetTailAttackCenter()
    {
        Vector2 horizontalDirection = playerController.FacingDirection;
        float directionSign = tailAttackUsesBackDirection ? -horizontalDirection.x : horizontalDirection.x;
        return (Vector2)transform.position + Vector2.Scale(tailElectricOffset, new Vector2(directionSign, 1f));
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 facingDirection = Vector2.right;
        if (playerController != null)
            facingDirection = playerController.FacingDirection;

        Gizmos.color = Color.green;
        Vector2 finCenter = (Vector2)transform.position + Vector2.Scale(finAttackOffset, new Vector2(facingDirection.x, 1f));
        Gizmos.DrawWireSphere(finCenter, finAttackRadius);

        Gizmos.color = Color.cyan;
        float directionSign = tailAttackUsesBackDirection ? -facingDirection.x : facingDirection.x;
        Vector2 tailCenter = (Vector2)transform.position + Vector2.Scale(tailElectricOffset, new Vector2(directionSign, 1f));
        Gizmos.DrawWireCube(tailCenter, tailElectricSize);
    }
}
