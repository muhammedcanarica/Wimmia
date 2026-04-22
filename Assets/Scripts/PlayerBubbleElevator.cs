using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerBubbleElevator : MonoBehaviour
{
    [Header("Bubble Movement")]
    [Tooltip("Yukarı doğru sabit yükselme hızı")]
    public float ascendSpeed = 3f;
    [Tooltip("Sağ/sol hareketin maksimum hızı")]
    public float maxHorizontalSpeed = 2f;
    [Tooltip("Sağ/sol hareketteki ivmelenme (yumuşaklık)")]
    public float horizontalAcceleration = 4f;

    [Header("Exit Conditions")]
    [Tooltip("Balonun içinde kalınabilecek maksimum süre (saniye)")]
    public float maxDuration = 3f;
    [Tooltip("Tavanı kontrol etmek için başın üstünden atılacak ışının uzunluğu")]
    public float ceilingCheckDistance = 0.6f;
    [Tooltip("Tavan kabul edilecek layer'lar")]
    public LayerMask ceilingLayer = 1 << 6; // Default to Ground layer

    [Header("Visuals")]
    [Tooltip("İsteğe bağlı: Bubble görseli için kullanılacak Sprite")]
    public Sprite bubbleSprite;
    public Color bubbleColor = new Color(0.7f, 0.9f, 1f, 0.6f);
    public Vector3 bubbleScale = new Vector3(1.2f, 1.2f, 1f);

    public bool IsInBubble { get; private set; }

    private Rigidbody2D rb;
    private PlayerInputReader inputReader;
    private PlayerController playerController;
    private PlayerMovement playerMovement;
    private PlayerDash playerDash;

    private float originalGravity;
    private float originalDrag;
    private float bubbleTimer;

    private GameObject bubbleVisualObj;
    private SpriteRenderer bubbleRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputReader = GetComponent<PlayerInputReader>();
        
        // Disable edilecek ana komponentler
        playerController = GetComponent<PlayerController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerDash = GetComponent<PlayerDash>();

        CreateBubbleVisual();
    }

    private void CreateBubbleVisual()
    {
        bubbleVisualObj = new GameObject("BubbleVisual");
        bubbleVisualObj.transform.SetParent(transform);
        bubbleVisualObj.transform.localPosition = Vector3.zero;
        bubbleVisualObj.transform.localScale = bubbleScale;

        bubbleRenderer = bubbleVisualObj.AddComponent<SpriteRenderer>();
        bubbleRenderer.color = bubbleColor;
        bubbleRenderer.sortingOrder = 15; // Oyuncunun önünde çizilsin
        bubbleVisualObj.SetActive(false);

        // Eğer inspector'dan atanmamışsa geçici bir daire sprite'ı yarat
        if (bubbleSprite != null)
        {
            bubbleRenderer.sprite = bubbleSprite;
        }
        else
        {
            bubbleRenderer.sprite = CreateCircleSprite(128);
        }
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);
        float radius = size / 2f;
        float center = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Yumuşak balon kenarı çizimi
                if (dist < radius - 4f)
                {
                    // Balonun içi saydam
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, 0.1f)); 
                }
                else if (dist < radius)
                {
                    // Kenar çizgisi daha opak ve yumuşak geçişli
                    float alpha = (radius - dist) / 4f; 
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    // Dışarısı tamamen boş
                    texture.SetPixel(x, y, transparent);
                }
            }
        }
        texture.Apply();
        
        // Pivot tam ortada, 1 birim genişlik için pixelsPerUnit = size
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public void EnterBubble()
    {
        if (IsInBubble) return;

        IsInBubble = true;
        bubbleTimer = 0f;

        // 1. Oyuncu kontrollerini dondur
        if (playerController != null) playerController.enabled = false;
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerDash != null) playerDash.enabled = false;

        // 2. Fizik ayarlarını devral
        originalGravity = rb.gravityScale;
        originalDrag = rb.linearDamping;

        rb.gravityScale = 0f;
        rb.linearDamping = 1f; // Hafif bir sürtünme ekleyerek floaty his yaratır

        // Dikey hızı sıfırla ki yumuşak başlasın
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        // 3. Görseli aç
        if (bubbleSprite == null && bubbleRenderer.sprite == null)
        {
            // Eğer sprite atanmamışsa, geçici bir şey göstermemek yerine
            // uyarı verin. UnityEditor kütüphanesini build'de kullanamayız.
            Debug.LogWarning("Bubble Elevator: Bubble Sprite atanmamış! Lütfen Inspector'dan bir sprite atayın.");
        }
        bubbleVisualObj.SetActive(true);
    }

    public void ExitBubble()
    {
        if (!IsInBubble) return;

        IsInBubble = false;

        // 1. Fizik ayarlarını geri ver
        rb.gravityScale = originalGravity;
        rb.linearDamping = originalDrag;

        // Çıkışta ivmeyi koru veya istersen hafifçe zıplat:
        // rb.linearVelocity = new Vector2(rb.linearVelocity.x, ascendSpeed * 0.5f);

        // 2. Oyuncu kontrollerini aç
        if (playerController != null) playerController.enabled = true;
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerDash != null) playerDash.enabled = true;

        // 3. Görseli kapat
        bubbleVisualObj.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (!IsInBubble) return;

        bubbleTimer += Time.fixedDeltaTime;

        // --- Çıkış Koşulları Kontrolü ---
        
        // 1. Süre doldu mu?
        if (bubbleTimer >= maxDuration)
        {
            ExitBubble();
            return;
        }

        // 2. Tavana çarptı mı?
        Vector2 checkOrigin = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(checkOrigin, Vector2.up, ceilingCheckDistance, ceilingLayer);
        if (hit.collider != null)
        {
            ExitBubble();
            return;
        }

        // --- Hareket Mantığı ---

        Vector2 currentVel = rb.linearVelocity;

        // Y ekseni: Sabit hızla yukarı
        float targetVelocityY = ascendSpeed;

        // X ekseni: Sınırlı input kontrolü
        float inputX = inputReader != null ? inputReader.MoveInput.x : 0f;
        float targetVelocityX = inputX * maxHorizontalSpeed;

        // Yumuşak geçiş (Drag hissi)
        currentVel.x = Mathf.Lerp(currentVel.x, targetVelocityX, horizontalAcceleration * Time.fixedDeltaTime);
        
        // Yukarı hızda aniden fırlamamak için ivmelenerek çık
        currentVel.y = Mathf.Lerp(currentVel.y, targetVelocityY, horizontalAcceleration * Time.fixedDeltaTime);

        rb.linearVelocity = currentVel;
    }

    private void Update()
    {
        if (!IsInBubble) return;

        // 3. Jump tuşuna basıldı mı? (Update içinde input okumak daha güvenlidir)
        if (inputReader != null && inputReader.ConsumeJumpPressed())
        {
            ExitBubble();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Tavan kontrol ışınını çiz
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * ceilingCheckDistance);
    }
}
