using UnityEngine;

public class SpikePlatform : MonoBehaviour
{
    [Header("Hasar Ayarları")]
    [Tooltip("Spike'a dokunulduğunda verilen hasar")]
    public int damage = 1;

    [Tooltip("Sürekli hasar verme aralığı (saniye). 0 = sadece ilk dokunuşta hasar verir.")]
    [Range(0f, 3f)]
    public float damageInterval = 0.5f;

    [Header("Geri İtme")]
    [Tooltip("Oyuncuyu geri itme kuvveti")]
    public float knockbackForce = 10f;

    [Tooltip("Geri itme sersemlik süresi")]
    public float knockbackStunTime = 0.2f;

    private float nextDamageTime;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider, collision.GetContact(0).point);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (damageInterval <= 0f) return;

        if (Time.time >= nextDamageTime)
        {
            TryDamagePlayer(collision.collider, collision.GetContact(0).point);
        }
    }

    private void TryDamagePlayer(Collider2D playerCollider, Vector2 contactPoint)
    {
        if (!playerCollider.CompareTag("Player"))
            return;

        PlayerHealth health = playerCollider.GetComponent<PlayerHealth>();
        if (health == null)
            return;

        health.TakeDamage(damage, transform);
        nextDamageTime = Time.time + damageInterval;
    }

    private void OnDrawGizmos()
    {
        // Spike alanını kırmızı olarak göster
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        // Tehlike işareti — üçgen
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.6f);
        Vector3 c = bounds.center;
        float s = Mathf.Min(bounds.size.x, bounds.size.y) * 0.2f;
        Vector3 top = c + Vector3.up * s;
        Vector3 bottomLeft = c + new Vector3(-s * 0.7f, -s * 0.5f, 0f);
        Vector3 bottomRight = c + new Vector3(s * 0.7f, -s * 0.5f, 0f);
        Gizmos.DrawLine(top, bottomLeft);
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, top);
    }
}
