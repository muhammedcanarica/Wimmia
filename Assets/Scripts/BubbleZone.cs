using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BubbleZone : MonoBehaviour
{
    [Header("Kaldırma Ayarları")]
    [Tooltip("Yukarı kaldırma kuvveti")]
    [Range(1f, 10000f)]
    public float liftForce = 5f;

    [Tooltip("Maksimum yukarı hız")]
    [Range(2f, 10512f)]
    public float maxUpSpeed = 6f;

    [Header("Fizik Değişiklikleri (Zone İçinde)")]
    [Tooltip("Zone içindeyken yerçekimi çarpanı (0 = sıfır yerçekimi, 1 = normal)")]
    [Range(0f, 1f)]
    [SerializeField] private float gravityScaleInZone = 0.3f;

    [Tooltip("Zone içindeyken sürüklenme (drag) değeri")]
    [Range(0f, 5f)]
    [SerializeField] private float dragInZone = 1.5f;

    [Header("Baloncuk Partikülleri")]
    [SerializeField] private bool autoCreateParticles = true;
    [SerializeField] private Color bubbleColor = new Color(0.7f, 0.9f, 1f, 0.7f);
    [SerializeField] private int bubbleCount = 20;
    [SerializeField] private float bubbleSpeed = 2f;
    [SerializeField] private float bubbleSizeMin = 0.15f;
    [SerializeField] private float bubbleSizeMax = 0.4f;

    private BoxCollider2D zoneCollider;
    private ParticleSystem bubbleParticles;

    // Oyuncunun orijinal fizik değerlerini sakla
    private float originalGravityScale;
    private float originalDrag;
    private bool playerInside;
    private Rigidbody2D trackedRb;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;

        if (autoCreateParticles)
            SetupBubbleParticles();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb == null)
            return;

        // Orijinal değerleri kaydet
        originalGravityScale = rb.gravityScale;
        originalDrag = rb.linearDamping;

        // Zone fizik değerlerini uygula
        rb.gravityScale = originalGravityScale * gravityScaleInZone;
        rb.linearDamping = dragInZone;

        trackedRb = rb;
        playerInside = true;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb == null)
            return;

        // Yukarı doğru yumuşak kuvvet uygula
        Vector2 vel = rb.linearVelocity;

        if (vel.y < maxUpSpeed)
        {
            float lift = liftForce * Time.deltaTime;

            // Hıza yaklaştıkça kuvveti azalt (yumuşak geçiş)
            float speedRatio = Mathf.Clamp01(vel.y / maxUpSpeed);
            lift *= (1f - speedRatio * 0.7f);

            vel.y += lift;
            vel.y = Mathf.Min(vel.y, maxUpSpeed);
            rb.linearVelocity = vel;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb == null)
            return;

        // Orijinal fizik değerlerini geri yükle
        rb.gravityScale = originalGravityScale;
        rb.linearDamping = originalDrag;

        trackedRb = null;
        playerInside = false;
    }

    private void OnDisable()
    {
        // Script devre dışı kalırsa orijinal değerleri geri yükle
        if (playerInside && trackedRb != null)
        {
            trackedRb.gravityScale = originalGravityScale;
            trackedRb.linearDamping = originalDrag;
            trackedRb = null;
            playerInside = false;
        }
    }

    private void SetupBubbleParticles()
    {
        GameObject particleObj = new GameObject("BubbleParticles");
        particleObj.transform.SetParent(transform);
        particleObj.transform.localPosition = Vector3.zero;

        bubbleParticles = particleObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = particleObj.GetComponent<ParticleSystemRenderer>();
        bubbleParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Ana ayarlar
        var main = bubbleParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(bubbleSpeed * 0.5f, bubbleSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(bubbleSizeMin, bubbleSizeMax);
        main.startColor = bubbleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = bubbleCount * 5;
        main.gravityModifier = -0.15f; // Negatif = yukarı doğru hafif çekim

        // Emission
        var emission = bubbleParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = bubbleCount;

        // Shape — collider alanının altından çık
        var shape = bubbleParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(zoneCollider.size.x, zoneCollider.size.y * 0.3f, 1f);
        shape.position = new Vector3(
            zoneCollider.offset.x,
            zoneCollider.offset.y - zoneCollider.size.y * 0.35f,
            0f
        );

        // Velocity — yukarı doğru yükselen baloncuklar + hafif yatay salınım
        var velocity = bubbleParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        // Yatay salınım için sinüs eğrisi
        AnimationCurve wobbleCurve = new AnimationCurve();
        wobbleCurve.AddKey(0f, -1f);
        wobbleCurve.AddKey(0.25f, 1f);
        wobbleCurve.AddKey(0.5f, -1f);
        wobbleCurve.AddKey(0.75f, 1f);
        wobbleCurve.AddKey(1f, -1f);
        velocity.x = new ParticleSystem.MinMaxCurve(0.3f, wobbleCurve);
        velocity.y = new ParticleSystem.MinMaxCurve(bubbleSpeed * 0.6f, bubbleSpeed);

        // Boyut değişimi — yükselirken hafifçe büyüyüp kaybolma
        var sizeOverLifetime = bubbleParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.5f, 1f);
        sizeCurve.AddKey(0.85f, 1.1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Saydamlık
        var colorOverLifetime = bubbleParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(bubbleColor, 0f),
                new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0.5f),
                new GradientColorKey(bubbleColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.15f),
                new GradientAlphaKey(0.7f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // Renderer
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 9;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                particleRenderer.material = new Material(shader);
        }

        bubbleParticles.Play();
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        // Bölge sınırı
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.2f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(center, size);

        // Yukarı oklar
        Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.8f);
        int arrowCount = 4;
        float spacing = size.x / (arrowCount + 1);
        for (int i = 0; i < arrowCount; i++)
        {
            float x = center.x - size.x * 0.5f + spacing * (i + 1);
            Vector3 start = new Vector3(x, center.y - size.y * 0.3f, 0f);
            Vector3 end = new Vector3(x, center.y + size.y * 0.3f, 0f);
            Gizmos.DrawLine(start, end);

            // Ok ucu
            Gizmos.DrawLine(end, end + new Vector3(-0.15f, -0.25f, 0f));
            Gizmos.DrawLine(end, end + new Vector3(0.15f, -0.25f, 0f));
        }
    }
}
