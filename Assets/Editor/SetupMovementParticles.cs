using UnityEngine;
using UnityEditor;

/// <summary>
/// Hareket Particle System'ını optimize su altı ayarlarıyla yapılandırır.
/// Kullanım: Unity menüsünden -> Tools / Setup Movement Particles
/// Çalıştırdıktan sonra bu script silinebilir.
/// </summary>
public class SetupMovementParticles : MonoBehaviour
{
    [MenuItem("Tools/Setup Movement Particles")]
    static void Setup()
    {
        // Sahnedeki "Particle System" objesini bul
        GameObject psObj = GameObject.Find("player/Particle System");
        if (psObj == null)
        {
            // Alternatif isimle dene
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var ps = player.GetComponentInChildren<ParticleSystem>();
                if (ps != null) psObj = ps.gameObject;
            }
        }

        if (psObj == null)
        {
            Debug.LogError("Particle System bulunamadı! Player'ın child'ında 'Particle System' isimli obje aranıyor.");
            return;
        }

        ParticleSystem particleSystem = psObj.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            Debug.LogError("ParticleSystem component bulunamadı!");
            return;
        }

        // ==================== TRANSFORM DÜZELT ====================
        // 2D kullanıyoruz, rotation sıfırlanmalı (3D preset'in 270 derece X sorunu)
        psObj.transform.localRotation = Quaternion.identity;
        psObj.transform.localPosition = Vector3.zero;

        // ==================== MAIN MODULE ====================
        var main = particleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.startDelay = 0f;

        // Lifetime: Daha uzun süre görünür kalsınlar (0.8 - 1.5 sn)
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);

        // Start Speed: Hafif yukarı doğru hız (su baloncuğu hissi)
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);

        // Start Size: Çok daha belirgin, büyük baloncuklar
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);

        // Start Color: Deniz temalı turkuaz / derin mavi gradient
        Color seaCyan = new Color(0.2f, 0.8f, 0.9f, 0.9f); // Canlı turkuaz
        Color deepBlue = new Color(0.1f, 0.4f, 1f, 0.8f); // Koyu deniz mavisi
        main.startColor = new ParticleSystem.MinMaxGradient(seaCyan, deepBlue);

        main.startRotation = 0f;
        main.gravityModifier = -0.08f; // Negatif = yukarı doğru (su altı baloncuk etkisi)
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = true;
        main.maxParticles = 30; // Düşük limit = temiz görüntü

        // ==================== EMISSION ====================
        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(8f, 15f); // Düşük emission
        emission.rateOverDistance = 0f;

        // ==================== SHAPE ====================
        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f; // Küçük alan (karakterin etrafında)
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.rotation = Vector3.zero;
        shape.position = Vector3.zero;
        shape.scale = new Vector3(1f, 1f, 1f);

        // ==================== VELOCITY OVER LIFETIME ====================
        var vel = particleSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        // Hafif yukarı + rastgele yatay hareket
        ParticleSystem.MinMaxCurve curveX = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        ParticleSystem.MinMaxCurve curveY = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        ParticleSystem.MinMaxCurve curveZ = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

        // Unity'nin min==max olan durumlarda modu Constant'a düşürmesini ve uyuşmazlık hatası vermesini engelle
        curveX.mode = ParticleSystemCurveMode.TwoConstants;
        curveY.mode = ParticleSystemCurveMode.TwoConstants;
        curveZ.mode = ParticleSystemCurveMode.TwoConstants;

        vel.x = curveX;
        vel.y = curveY;
        vel.z = curveZ;

        // ==================== SIZE OVER LIFETIME ====================
        // Küçük → Biraz büyü → Küçül (bell curve / diamond shape)
        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;

        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 2f));     // Başta küçük
        sizeCurve.AddKey(new Keyframe(0.35f, 1.0f, 0f, 0f));   // Ortada en büyük
        sizeCurve.AddKey(new Keyframe(1f, 0.1f, -2f, 0f));     // Sonda çok küçük
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ==================== COLOR OVER LIFETIME ====================
        // Alpha: başta %80 → ortada %60 → sonda %0 (yumuşak fade out)
        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 1f, 0.9f), 0f),     // Canlı Turkuaz
                new GradientColorKey(new Color(0.1f, 0.6f, 1f), 0.6f),   // Derin Mavi
                new GradientColorKey(new Color(0.8f, 0.9f, 1f), 1f)      // Sonda biraz beyazlaşarak patlama hissi
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.7f, 0f),     // Başta %70 görünür
                new GradientAlphaKey(0.5f, 0.4f),   // Ortada %50
                new GradientAlphaKey(0.15f, 0.75f),  // Azalmaya başla
                new GradientAlphaKey(0f, 1f)          // Sonda tamamen şeffaf
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // ==================== DEVRE DIŞI BIRAKILACAK MODÜLLER ====================
        var noise = particleSystem.noise;
        noise.enabled = false;

        var collision = particleSystem.collision;
        collision.enabled = false;

        var trigger = particleSystem.trigger;
        trigger.enabled = false;

        var textureSheet = particleSystem.textureSheetAnimation;
        textureSheet.enabled = true;
        textureSheet.mode = ParticleSystemAnimationMode.Sprites;
        
        // Unity'nin her projede olan dahili tam yuvarlak (Knob) görselini sprite olarak ekliyoruz
        Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        
        // Daha önce başka bir şey eklenmediyse temiz olarak ekle
        if (circleSprite != null && textureSheet.spriteCount == 0)
        {
            textureSheet.AddSprite(circleSprite);
        }

        var lights = particleSystem.lights;
        lights.enabled = false;

        var trails = particleSystem.trails;
        trails.enabled = false;

        var externalForces = particleSystem.externalForces;
        externalForces.enabled = false;

        var inheritVelocity = particleSystem.inheritVelocity;
        inheritVelocity.enabled = false;

        var lifetimeByEmitter = particleSystem.lifetimeByEmitterSpeed;
        lifetimeByEmitter.enabled = false;

        var forceOverLifetime = particleSystem.forceOverLifetime;
        forceOverLifetime.enabled = false;

        var colorBySpeed = particleSystem.colorBySpeed;
        colorBySpeed.enabled = false;

        var sizeBySpeed = particleSystem.sizeBySpeed;
        sizeBySpeed.enabled = false;

        var rotationOverLifetime = particleSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = false;

        var rotationBySpeed = particleSystem.rotationBySpeed;
        rotationBySpeed.enabled = false;

        var limitVelocity = particleSystem.limitVelocityOverLifetime;
        limitVelocity.enabled = false;

        var customData = particleSystem.customData;
        customData.enabled = false;

        // ==================== RENDERER AYARLARI ====================
        ParticleSystemRenderer renderer = psObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f; // Sınırlamayı artırdık ki büyük baloncuklar çıkabilsin
            renderer.sortingOrder = 5; // Player'ın önünde
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Material: Sprites-Default (URP uyumlu, pixel art'a uygun)
            // Default particle materyali iz bırakır, Sprites-Default temiz çizim yapar
            Material spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (spriteMat != null)
            {
                renderer.material = spriteMat;
            }
            else
            {
                // Alternatif: URP Sprite-Lit-Default (zaten atanmış)
                Debug.LogWarning("Sprites-Default.mat bulunamadı, mevcut materyal korunuyor.");
            }
        }

        // ==================== MOVEMENTPARTICLECONTROLLER EKLE ====================
        if (psObj.GetComponent<MovementParticleController>() == null)
        {
            psObj.AddComponent<MovementParticleController>();
        }

        // Değişiklikleri kaydet
        EditorUtility.SetDirty(psObj);
        EditorUtility.SetDirty(particleSystem);
        if (renderer != null) EditorUtility.SetDirty(renderer);

        Debug.Log("✅ Movement Particle System başarıyla yapılandırıldı!");
        Debug.Log("📋 Aktif Modüller: Main, Emission, Shape, Velocity Over Lifetime, Size Over Lifetime, Color Over Lifetime");
        Debug.Log("🔧 Renderer: Billboard, Sprites-Default Material, Sorting Order: 5");

        // Inspector'da göster
        Selection.activeGameObject = psObj;
    }
}
