using UnityEditor;
using UnityEngine;

/// <summary>
/// 2D su altı pixel art oyunu için optimize edilmiş Hit Particle prefab'ını oluşturur.
/// Menu: Tools > Particle > Rebuild Hit Particle
/// </summary>
public static class SetupHitParticle
{
    [MenuItem("Tools/Particle/Rebuild Hit Particle")]
    private static void RebuildHitParticlePrefab()
    {
        string folderPath = "Assets/Prefabs";
        string prefabPath = folderPath + "/HitImpact_Particle.prefab";

        // Klasör yoksa oluştur
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Eski prefab varsa sil (yeniden oluşturmak için)
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        // ============================================================
        // PARTICLE SYSTEM OBJE OLUŞTURMA
        // ============================================================
        GameObject go = new GameObject("HitImpact_Particle");
        var ps = go.AddComponent<ParticleSystem>();

        // ============================================================
        // 1. MAIN MODULE — Ana Ayarlar
        // ============================================================
        var main = ps.main;
        main.duration            = 0.5f;                                          // Sistemin toplam çalışma süresi
        main.loop                = false;                                         // Tek seferlik patlama
        main.startLifetime       = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);  // Her parçacığın yaşam süresi
        main.startSpeed          = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);    // Yavaş ve yumuşak dağılım
        main.startSize           = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);  // Pixel art'a uygun küçük boyut
        main.startRotation       = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad); // Rastgele döndürme
        main.startColor          = new Color(0.75f, 0.92f, 1f, 1f);              // Açık buz mavisi
        main.gravityModifier     = -0.3f;                                         // Hafif yukarı doğru (su altı hissi)
        main.simulationSpace     = ParticleSystemSimulationSpace.World;           // Dünya koordinatında
        main.stopAction          = ParticleSystemStopAction.Destroy;              // Bitince obje silinsin
        main.maxParticles        = 20;                                            // Çok fazla parçacık olmasın
        main.playOnAwake         = true;

        // ============================================================
        // 2. EMISSION MODULE — Yayım Ayarları
        // ============================================================
        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = 0f;                                               // Sürekli yayım yok
        // Tek seferde burst: 6–10 parçacık (temiz, az, optimize)
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 6, 10)
        });

        // ============================================================
        // 3. SHAPE MODULE — Şekil Ayarları (2D için Circle)
        // ============================================================
        var shape = ps.shape;
        shape.enabled          = true;
        shape.shapeType        = ParticleSystemShapeType.Circle;                  // 2D uyumlu daire
        shape.radius           = 0.15f;                                           // Küçük yayılma alanı
        shape.radiusThickness  = 1f;                                              // Tüm alan dolsun
        shape.arc              = 360f;

        // ============================================================
        // 4. VELOCITY OVER LIFETIME — Yaşam Boyu Hız
        // ============================================================
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        
        ParticleSystem.MinMaxCurve curveX = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        ParticleSystem.MinMaxCurve curveY = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        ParticleSystem.MinMaxCurve curveZ = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

        curveX.mode = ParticleSystemCurveMode.TwoConstants;
        curveY.mode = ParticleSystemCurveMode.TwoConstants;
        curveZ.mode = ParticleSystemCurveMode.TwoConstants;

        vel.x = curveX;
        vel.y = curveY;
        vel.z = curveZ;

        // Hızı zamanla yavaşlat (drag)
        var speedMod = ps.limitVelocityOverLifetime;
        speedMod.enabled = true;
        speedMod.dampen  = 0.15f;

        // ============================================================
        // 5. COLOR OVER LIFETIME — Renk & Alpha Gradient
        // Başta görünür → sonda tamamen şeffaf (fade out)
        // ============================================================
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        // Gradient oluştur
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f),           // Başta: Açık buz beyazı
                new GradientColorKey(new Color(0.6f, 0.85f, 1f),  0.5f),         // Ortada: Açık mavi
                new GradientColorKey(new Color(0.5f, 0.75f, 0.95f), 1f)          // Sonda: Biraz daha koyu mavi
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),                                  // Başta: %90 görünür
                new GradientAlphaKey(0.7f, 0.3f),                                // %30'da hâlâ görünür
                new GradientAlphaKey(0.2f, 0.7f),                                // %70'de solmaya başla
                new GradientAlphaKey(0f,   1f)                                    // Sonda: Tamamen şeffaf
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // ============================================================
        // 6. SIZE OVER LIFETIME — Boyut: küçük → biraz büyü → küçül
        // ============================================================
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;

        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(new Keyframe(0f,   0.4f));    // Başta küçük
        sizeCurve.AddKey(new Keyframe(0.25f, 1.0f));   // Hızlıca büyü
        sizeCurve.AddKey(new Keyframe(0.6f,  0.7f));   // Yavaşça küçülmeye başla
        sizeCurve.AddKey(new Keyframe(1f,    0f));      // Sonda tamamen kaybol

        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ============================================================
        // 7. ROTATION OVER LIFETIME — Hafif döndürme
        // ============================================================
        var rotOverLifetime = ps.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);

        // ============================================================
        // 8. RENDERER — Görüntüleme Ayarları
        // ============================================================
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // Built-in Default Particle material kullan
        renderer.material = GetDefaultParticleMaterial();

        renderer.sortingOrder   = 10;                                              // Karakterin üstünde gözüksün
        renderer.minParticleSize = 0.01f;
        renderer.maxParticleSize = 0.3f;

        // ============================================================
        // KAPALI TUTULACAK MODULLER (optimizasyon)
        // ============================================================
        var noise              = ps.noise;              noise.enabled = false;
        var trails             = ps.trails;             trails.enabled = false;
        var collision          = ps.collision;           collision.enabled = false;
        var subEmitters        = ps.subEmitters;        subEmitters.enabled = false;
        var textureSheetAnim   = ps.textureSheetAnimation; textureSheetAnim.enabled = false;
        var lights             = ps.lights;             lights.enabled = false;
        var trigger            = ps.trigger;            trigger.enabled = false;
        var externalForces     = ps.externalForces;     externalForces.enabled = false;
        var inheritVelocity    = ps.inheritVelocity;    inheritVelocity.enabled = false;
        var customData         = ps.customData;         customData.enabled = false;

        // ============================================================
        // PREFAB OLARAK KAYDET
        // ============================================================
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        Debug.Log("✅ HitImpact_Particle prefab'ı yeniden oluşturuldu → " + prefabPath);
    }

    /// <summary>
    /// Unity'nin dahili Default-Particle materyalini bulur.
    /// Additive blend ile yumuşak parçacık görüntüsü verir.
    /// </summary>
    private static Material GetDefaultParticleMaterial()
    {
        // Dahili "Default-Particle" materyali
        Material mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (mat != null) return mat;

        // Bulamazsa Sprites-Default kullan
        return AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
    }
}
