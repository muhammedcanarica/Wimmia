using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CurrentZone : MonoBehaviour
{
    [Header("Akıntı Ayarları")]
    [Tooltip("Akıntının yönü (otomatik normalize edilir)")]
    public Vector2 direction = Vector2.right;

    [Tooltip("Akıntı kuvveti (2-10 arası önerilir)")]
    [Range(1f, 100f)]
    public float force = 5f;

    [Header("Görsel")]
    [Tooltip("Akıntı yönünü gösteren partikül sistemi (opsiyonel)")]
    [SerializeField] private ParticleSystem flowParticles;

    [SerializeField] private bool autoCreateParticles = true;
    [SerializeField] private Color particleColor = new Color(0.6f, 0.85f, 1f, 0.85f);
    [SerializeField] private int particleCount = 25;
    [SerializeField] private float particleSpeed = 3f;
    [SerializeField] private float particleSize = 0.35f;

    private BoxCollider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;

        if (direction != Vector2.zero)
            direction = direction.normalized;

        if (flowParticles == null && autoCreateParticles)
            SetupFlowParticles();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb == null)
            return;

        // Yumuşak ve sürekli kuvvet uygula — oyuncunun kontrolünü elinden almaz
        Vector2 currentForce = direction.normalized * force;
        rb.AddForce(currentForce, ForceMode2D.Force);
    }

    private void SetupFlowParticles()
    {
        GameObject particleObj = new GameObject("FlowParticles");
        particleObj.transform.SetParent(transform);
        particleObj.transform.localPosition = Vector3.zero;

        flowParticles = particleObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = particleObj.GetComponent<ParticleSystemRenderer>();
        flowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Ana ayarlar
        var main = flowParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 3f;
        main.startSpeed = particleSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.6f, particleSize);
        main.startColor = particleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = particleCount * 5;
        main.gravityModifier = 0f;

        // Emission
        var emission = flowParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = particleCount;

        // Shape — collider alanı kadar yayılım
        var shape = flowParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(zoneCollider.size.x, zoneCollider.size.y, 1f);
        shape.position = zoneCollider.offset;

        // Velocity — akıntı yönünde hareket
        var velocity = flowParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(direction.x * particleSpeed * 0.8f, direction.x * particleSpeed * 1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(direction.y * particleSpeed * 0.8f, direction.y * particleSpeed * 1.2f);

        // Boyut azalması
        var sizeOverLifetime = flowParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(0.3f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Saydamlık azalması
        var colorOverLifetime = flowParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(particleColor, 0f), new GradientColorKey(particleColor, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // Renderer
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 8;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                particleRenderer.material = new Material(shader);
        }

        flowParticles.Play();
    }

    private void OnValidate()
    {
        if (direction != Vector2.zero)
            direction = direction.normalized;
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        // Bölge sınırı
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireCube(center, size);

        // Yön okları
        if (direction == Vector2.zero) return;

        Gizmos.color = Color.cyan;
        Vector3 dir3 = (Vector3)(direction.normalized);
        int arrowCount = 3;
        for (int i = 0; i < arrowCount; i++)
        {
            float t = (i + 1f) / (arrowCount + 1f);
            Vector3 start = center - dir3 * (size.magnitude * 0.3f) + Vector3.up * Mathf.Lerp(-size.y * 0.3f, size.y * 0.3f, t);
            Vector3 end = start + dir3 * (size.magnitude * 0.4f);
            Gizmos.DrawLine(start, end);

            // Ok ucu
            Vector3 arrowRight = Quaternion.Euler(0, 0, 30) * (-dir3) * 0.3f;
            Vector3 arrowLeft = Quaternion.Euler(0, 0, -30) * (-dir3) * 0.3f;
            Gizmos.DrawLine(end, end + arrowRight);
            Gizmos.DrawLine(end, end + arrowLeft);
        }
    }
}
