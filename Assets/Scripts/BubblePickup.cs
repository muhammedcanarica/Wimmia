using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class BubblePickup : MonoBehaviour
{
    [Tooltip("Balon alındıktan sonra belli bir süre sonra tekrar oluşsun mu?")]
    public bool respawnable = true;
    [Tooltip("Eğer tekrar oluşacaksa bekleme süresi")]
    public float respawnTime = 5f;

    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        
        // Sadece trigger olarak çalışması gerekir
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerBubbleElevator elevator = collision.GetComponent<PlayerBubbleElevator>();
            if (elevator != null && !elevator.IsInBubble)
            {
                elevator.EnterBubble();

                if (respawnable)
                {
                    StartCoroutine(RespawnRoutine());
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // Kapat
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        col.enabled = false;

        // Bekle
        yield return new WaitForSeconds(respawnTime);

        // Tekrar aç
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        col.enabled = true;
    }
}
