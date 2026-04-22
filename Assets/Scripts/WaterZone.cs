using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider2D))]
public class WaterZone : MonoBehaviour
{
    [Header("Splash Ayarlari")]
    [SerializeField] private bool spawnSplashOnTransition = true;
    [SerializeField] private int splashBurstMin = 10;
    [SerializeField] private int splashBurstMax = 16;
    [SerializeField] private float splashLifetimeMin = 0.2f;
    [SerializeField] private float splashLifetimeMax = 0.45f;
    [SerializeField] private float splashSpeedMin = 1.5f;
    [SerializeField] private float splashSpeedMax = 3.5f;
    [SerializeField] private float splashSizeMin = 0.08f;
    [SerializeField] private float splashSizeMax = 0.16f;
    [SerializeField] private float splashGravity = 0.35f;
    [SerializeField] private Color splashColor = new Color(0.42f, 0.74f, 1f, 0.92f);

    [Header("Visual Settings")]
    [SerializeField] private Color waterColor = new Color(0f, 0.4f, 0.9f, 0.25f);

    [Header("Surface Buffer")]
    [Tooltip("Collider üst kenarına eklenecek ekstra yükseklik (görsel suyun üstüne taşar)")]
    [SerializeField] private float colliderTopMargin = 0.5f;

    [Tooltip("Oyuncu su yüzeyinden bu kadar yukarı çıkmadan Water modundan çıkmaz")]
    [SerializeField] private float surfaceExitBuffer = 0.3f;

    [Header("Safety")]
    [Tooltip("Oyuncu su yüzeyinin bu kadar üzerindeyse zorla çıkış yap")]
    [SerializeField] private float safetyExitMargin = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Optional Elements")]
    [SerializeField] private ParticleSystem bubbleParticles;

    private SpriteRenderer waterSpriteRenderer;
    private BoxCollider2D waterCollider;
    private static Material runtimeSplashMaterial;
    private static Sprite runtimeSplashSprite;

    /// <summary>Visible water surface Y (before margin). Used for exit logic.</summary>
    private float visualSurfaceY;

    // --- A) Sürekli Overlap Takibi ---
    // OnTriggerExit2D yerine Player sınırlarını manuel kontrol edeceğiz
    private HashSet<Rigidbody2D> activePlayers = new HashSet<Rigidbody2D>();

    private void Awake()
    {
        waterCollider = GetComponent<BoxCollider2D>();
        waterCollider.isTrigger = true;

        // --- B) Doğru setup kontrolü ---
        // WaterZone'da Rigidbody2D olmamalı
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Debug.LogWarning($"[WaterZone] '{name}' üzerinde gereksiz Rigidbody2D bulundu ve kaldırıldı.", this);
            Destroy(rb);
        }

        // Fazla BoxCollider2D varsa temizle
        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        if (colliders.Length > 1)
        {
            Debug.LogWarning($"[WaterZone] '{name}' üzerinde {colliders.Length} BoxCollider2D var, fazlaları kaldırılıyor.", this);
            for (int i = 1; i < colliders.Length; i++)
                Destroy(colliders[i]);
        }

        // Tag kontrolü — "Water" tag'i gerekli
        if (!gameObject.CompareTag("Water"))
        {
            Debug.LogWarning($"[WaterZone] '{name}' tag'i 'Water' değil! Trigger düzgün çalışmayabilir.", this);
        }

        // --- A) Collider üstüne margin ekle ---
        // Görsel su yüzeyini kaydet, sonra collider'ı yukarı doğru genişlet
        visualSurfaceY = waterCollider.bounds.max.y;

        if (colliderTopMargin > 0f)
        {
            // size.y artır, offset.y yukarı kaydır (böylece alt kenar aynı kalır)
            Vector2 size = waterCollider.size;
            Vector2 offset = waterCollider.offset;
            float addedHeight = colliderTopMargin;
            size.y += addedHeight;
            offset.y += addedHeight * 0.5f;
            waterCollider.size = size;
            waterCollider.offset = offset;

            if (enableDebugLogs)
                Debug.Log($"[WaterZone] Collider genişletildi: +{addedHeight:F2} yukarı margin. Yeni size.y={size.y:F2}", this);
        }

        SetupWaterVisuals();

        if (bubbleParticles != null)
        {
            var shape = bubbleParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(waterCollider.size.x, waterCollider.size.y, 1f);
            bubbleParticles.transform.localPosition = waterCollider.offset;
        }
    }

    // --- D) Continuous bounds check ---
    private void FixedUpdate()
    {
        if (activePlayers.Count == 0) return;

        Bounds waterBounds = waterCollider.bounds;
        float waterTop = visualSurfaceY;
        float waterLeft = waterBounds.min.x;
        float waterRight = waterBounds.max.x;

        List<Rigidbody2D> toRemove = null;

        foreach (Rigidbody2D playerRb in activePlayers)
        {
            if (playerRb == null)
            {
                if (toRemove == null) toRemove = new List<Rigidbody2D>();
                toRemove.Add(playerRb);
                continue;
            }

            Collider2D[] colliders = new Collider2D[10];
            int colCount = playerRb.GetAttachedColliders(colliders);
            if (colCount == 0) continue;

            bool hasBounds = false;
            Bounds playerBounds = new Bounds();
            for (int i = 0; i < colCount; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    if (!hasBounds)
                    {
                        playerBounds = colliders[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        playerBounds.Encapsulate(colliders[i].bounds);
                    }
                }
            }
            if (!hasBounds) playerBounds = colliders[0].bounds; // Fallback to first if all are triggers

            float playerBottom = playerBounds.min.y;
            float playerLeft = playerBounds.min.x;
            float playerRight = playerBounds.max.x;

            float tolerance = 0.2f;
            
            // Su içinde sayılması için hem alt kısmının su yüzeyinin (toleranslı) altında olması
            // hem de yatayda suyun sınırları içinde olması gerekir
            bool isVerticallyInWater = playerBottom <= waterTop - tolerance;
            bool isHorizontallyInWater = playerRight >= waterLeft && playerLeft <= waterRight;

            if (isVerticallyInWater && isHorizontallyInWater)
            {
                PlayerController pc = playerRb.GetComponent<PlayerController>();
                if (pc != null && pc.currentMode != PlayerMode.Water)
                {
                    if (spawnSplashOnTransition)
                        SpawnSplashEffect(GetSplashPosition(playerBounds), playerRb.linearVelocity);

                    pc.ApplyModeProperties(PlayerMode.Water, true);
                }
            }
            else
            {
                PlayerController player = playerRb.GetComponent<PlayerController>();
                if (player != null && player.currentMode == PlayerMode.Water)
                {
                    if (spawnSplashOnTransition)
                        SpawnSplashEffect(new Vector3(playerRb.transform.position.x, waterTop, 0f), playerRb.linearVelocity);

                    player.ApplyModeProperties(PlayerMode.Land, false);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        Rigidbody2D playerRb = collision.attachedRigidbody;
        if (playerRb == null) return;

        if (!activePlayers.Contains(playerRb))
        {
            activePlayers.Add(playerRb);

            if (bubbleParticles != null && !bubbleParticles.isPlaying)
                bubbleParticles.Play();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        Rigidbody2D playerRb = collision.attachedRigidbody;
        if (playerRb == null) return;

        if (activePlayers.Contains(playerRb))
        {
            activePlayers.Remove(playerRb);

            PlayerController player = playerRb.GetComponent<PlayerController>();
            if (player != null && player.currentMode == PlayerMode.Water)
            {
                if (spawnSplashOnTransition)
                    SpawnSplashEffect(new Vector3(playerRb.transform.position.x, visualSurfaceY, 0f), playerRb.linearVelocity);
                
                player.ApplyModeProperties(PlayerMode.Land, false);
            }

            if (activePlayers.Count == 0 && bubbleParticles != null)
                bubbleParticles.Stop();
        }
    }

    // Oyuncu disable/destroy olursa temizle
    private void OnDisable()
    {
        // Tüm takip edilen oyuncuları Land moduna geri al
        foreach (var rb in activePlayers)
        {
            if (rb == null) continue;
            PlayerController player = rb.GetComponent<PlayerController>();
            if (player != null && player.currentMode == PlayerMode.Water)
                player.ApplyModeProperties(PlayerMode.Land, true);
        }
        activePlayers.Clear();
    }

    private void SetupWaterVisuals()
    {
        waterSpriteRenderer = GetComponent<SpriteRenderer>();
        if (waterSpriteRenderer == null)
            waterSpriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (waterSpriteRenderer.sprite == null)
        {
            Texture2D tex = new Texture2D(16, 16);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            waterSpriteRenderer.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, 16, 16),
                new Vector2(0.5f, 0.5f),
                16, 0,
                SpriteMeshType.FullRect,
                new Vector4(1, 1, 1, 1));
        }

        waterSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
        waterSpriteRenderer.color = waterColor;
        waterSpriteRenderer.size = waterCollider.size;
        waterSpriteRenderer.sortingOrder = 10;
    }

    private void OnValidate()
    {
        if (waterCollider == null) waterCollider = GetComponent<BoxCollider2D>();
        if (waterSpriteRenderer == null) waterSpriteRenderer = GetComponent<SpriteRenderer>();

        if (waterCollider != null && waterSpriteRenderer != null)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                waterSpriteRenderer.size = waterCollider.size;
                waterSpriteRenderer.color = waterColor;
            };
#else
            waterSpriteRenderer.size = waterCollider.size;
            waterSpriteRenderer.color = waterColor;
#endif
        }
    }

    private Vector3 GetSplashPosition(Bounds actorBounds)
    {
        if (waterCollider == null)
            return actorBounds.center;

        Bounds waterBounds = waterCollider.bounds;

        float topDistance = Mathf.Abs(actorBounds.min.y - waterBounds.max.y);
        float bottomDistance = Mathf.Abs(actorBounds.max.y - waterBounds.min.y);
        float leftDistance = Mathf.Abs(actorBounds.max.x - waterBounds.min.x);
        float rightDistance = Mathf.Abs(actorBounds.min.x - waterBounds.max.x);

        float nearestDistance = topDistance;
        Vector3 splashPosition = new Vector3(
            Mathf.Clamp(actorBounds.center.x, waterBounds.min.x, waterBounds.max.x),
            waterBounds.max.y,
            actorBounds.center.z);

        if (bottomDistance < nearestDistance)
        {
            nearestDistance = bottomDistance;
            splashPosition = new Vector3(
                Mathf.Clamp(actorBounds.center.x, waterBounds.min.x, waterBounds.max.x),
                waterBounds.min.y,
                actorBounds.center.z);
        }

        if (leftDistance < nearestDistance)
        {
            nearestDistance = leftDistance;
            splashPosition = new Vector3(
                waterBounds.min.x,
                Mathf.Clamp(actorBounds.center.y, waterBounds.min.y, waterBounds.max.y),
                actorBounds.center.z);
        }

        if (rightDistance < nearestDistance)
        {
            splashPosition = new Vector3(
                waterBounds.max.x,
                Mathf.Clamp(actorBounds.center.y, waterBounds.min.y, waterBounds.max.y),
                actorBounds.center.z);
        }

        return splashPosition;
    }

    private void SpawnSplashEffect(Vector3 position, Vector2 playerVelocity)
    {
        GameObject splashFx = new GameObject("WaterSplashFx");
        splashFx.transform.position = position;

        ParticleSystem particleSystem = splashFx.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = splashFx.GetComponent<ParticleSystemRenderer>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float velocityBoost = Mathf.Clamp(playerVelocity.magnitude * 0.15f, 0f, 1.25f);

        var main = particleSystem.main;
        main.duration = 0.45f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(splashLifetimeMin, splashLifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(splashSpeedMin + velocityBoost, splashSpeedMax + velocityBoost);
        main.startSize = new ParticleSystem.MinMaxCurve(splashSizeMin, splashSizeMax);
        main.startColor = splashColor;
        main.gravityModifier = splashGravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = splashBurstMax;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)splashBurstMin, (short)splashBurstMax)
        });

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.radiusThickness = 1f;

        var textureSheetAnimation = particleSystem.textureSheetAnimation;
        textureSheetAnimation.enabled = true;
        textureSheetAnimation.mode = ParticleSystemAnimationMode.Sprites;

        Sprite splashSprite = GetSplashSprite();
        if (splashSprite != null)
        {
            textureSheetAnimation.AddSprite(splashSprite);
        }

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        ParticleSystem.MinMaxCurve velocityX = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        ParticleSystem.MinMaxCurve velocityY = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        ParticleSystem.MinMaxCurve velocityZ = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
        velocityX.mode = ParticleSystemCurveMode.TwoConstants;
        velocityY.mode = ParticleSystemCurveMode.TwoConstants;
        velocityZ.mode = ParticleSystemCurveMode.TwoConstants;
        velocityOverLifetime.x = velocityX;
        velocityOverLifetime.y = velocityY;
        velocityOverLifetime.z = velocityZ;

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(0.35f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.64f, 0.86f, 1f), 0f),
                new GradientColorKey(new Color(0.28f, 0.56f, 0.92f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.92f, 0f),
                new GradientAlphaKey(0.4f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 6;
            particleRenderer.material = GetSplashMaterial();
        }

        particleSystem.Play();
        Destroy(splashFx, main.duration + splashLifetimeMax + 0.2f);
    }

    private static Material GetSplashMaterial()
    {
        if (runtimeSplashMaterial != null)
            return runtimeSplashMaterial;

        ParticleSystemRenderer sourceRenderer = FindExistingSplashRenderer();
        if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
        {
            runtimeSplashMaterial = sourceRenderer.sharedMaterial;
            return runtimeSplashMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            return null;

        runtimeSplashMaterial = new Material(shader)
        {
            name = "RuntimeWaterSplashMaterial"
        };

        return runtimeSplashMaterial;
    }

    private static Sprite GetSplashSprite()
    {
        if (runtimeSplashSprite != null)
            return runtimeSplashSprite;

        ParticleSystem sourceParticleSystem = Object.FindFirstObjectByType<MovementParticleController>()?.GetComponent<ParticleSystem>();
        if (sourceParticleSystem == null)
            sourceParticleSystem = GameObject.Find("Particle System")?.GetComponent<ParticleSystem>();

        if (sourceParticleSystem == null)
            return null;

        var textureSheetAnimation = sourceParticleSystem.textureSheetAnimation;
        if (!textureSheetAnimation.enabled || textureSheetAnimation.spriteCount == 0)
            return null;

        runtimeSplashSprite = textureSheetAnimation.GetSprite(0);
        return runtimeSplashSprite;
    }

    // Gizmos — collider bounds, visual surface, and buffer zone
    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        // Collider sınırı — kırmızı wire (trigger alanı, margin dahil)
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, size);

        // Görsel su alanı — mavi dolgu (margin olmadan)
        // Runtime'da visualSurfaceY set edilir; editörde collider üst kenarını kullan
        float effectiveSurfaceY = Application.isPlaying ? visualSurfaceY : bounds.max.y;
        float visualHeight = effectiveSurfaceY - bounds.min.y;
        Vector3 visualCenter = new Vector3(center.x, bounds.min.y + visualHeight * 0.5f, 0f);
        Vector3 visualSize = new Vector3(size.x, visualHeight, size.z);

        Gizmos.color = new Color(0f, 0.4f, 0.9f, 0.15f);
        Gizmos.DrawCube(visualCenter, visualSize);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.6f);
        Gizmos.DrawWireCube(visualCenter, visualSize);

        // Buffer zone — sarı wire (yüzey + surfaceExitBuffer)
        float bufferY = effectiveSurfaceY + surfaceExitBuffer;
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.5f);
        float leftX = bounds.min.x;
        float rightX = bounds.max.x;
        Gizmos.DrawLine(
            new Vector3(leftX, bufferY, 0f),
            new Vector3(rightX, bufferY, 0f));

        // Üst yüzey — dalgalı çizgi (görsel su yüzeyi)
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        int segments = 20;
        float step = (rightX - leftX) / segments;

        for (int i = 0; i < segments; i++)
        {
            float x1 = leftX + step * i;
            float x2 = leftX + step * (i + 1);
            float wave1 = Mathf.Sin(x1 * 3f) * 0.15f;
            float wave2 = Mathf.Sin(x2 * 3f) * 0.15f;
            Gizmos.DrawLine(
                new Vector3(x1, effectiveSurfaceY + wave1, 0f),
                new Vector3(x2, effectiveSurfaceY + wave2, 0f));
        }

        // Safety exit line — kırmızı çizgi
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        float safetyY = effectiveSurfaceY + safetyExitMargin;
        Gizmos.DrawLine(
            new Vector3(leftX, safetyY, 0f),
            new Vector3(rightX, safetyY, 0f));

        // Su sembolü
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.5f);
        float labelSize = Mathf.Min(size.x, size.y) * 0.08f;
        Gizmos.DrawWireSphere(center, labelSize);
    }

    private static ParticleSystemRenderer FindExistingSplashRenderer()
    {
        ParticleSystemRenderer sourceRenderer = Object.FindFirstObjectByType<MovementParticleController>()?.GetComponent<ParticleSystemRenderer>();
        if (sourceRenderer != null)
            return sourceRenderer;

        GameObject particleObject = GameObject.Find("Particle System");
        if (particleObject == null)
            return null;

        return particleObject.GetComponent<ParticleSystemRenderer>();
    }
}
