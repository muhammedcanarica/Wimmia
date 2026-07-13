using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OctopusBossSceneInstaller
{
    private const string BossPrefabFolder = "Assets/Prefabs/Boss";
    private const string GeneratedFolder = "Assets/Prefabs/Boss/Generated";
    private const string SlamTentaclePrefabPath = BossPrefabFolder + "/OctopusTentacleSlam.prefab";
    private const string SideSweepPrefabPath = BossPrefabFolder + "/OctopusSideSweep.prefab";
    private const string DropProjectilePrefabPath = BossPrefabFolder + "/OctopusDropProjectile.prefab";
    private const string SlowFieldPrefabPath = BossPrefabFolder + "/OctopusSlowField.prefab";
    private const string SlowFieldWarningPrefabPath = BossPrefabFolder + "/OctopusSlowFieldWarning.prefab";
    private const string NoteProjectilePrefabPath = BossPrefabFolder + "/OctopusNoteProjectile.prefab";
    private const string SlowFieldWeakPointPrefabPath = BossPrefabFolder + "/OctopusSlowFieldWeakPoint.prefab";
    private const string SlamWarningPrefabPath = BossPrefabFolder + "/OctopusSlamWarning.prefab";
    private const string SideSweepWarningPrefabPath = BossPrefabFolder + "/OctopusSideSweepWarning.prefab";
    private const string TentacleSpritePath = GeneratedFolder + "/OctopusTentaclePlaceholder.png";
    private const string SlamWarningSpritePath = GeneratedFolder + "/OctopusSlamWarningSprite.png";
    private const string SideSweepWarningSpritePath = GeneratedFolder + "/OctopusSideSweepWarningSprite.png";
    private const string DropProjectileSpritePath = GeneratedFolder + "/OctopusDropProjectileSprite.png";
    private const string SlowFieldSpritePath = GeneratedFolder + "/OctopusSlowFieldSprite.png";
    private const string SlowFieldWarningSpritePath = GeneratedFolder + "/OctopusSlowFieldWarningSprite.png";
    private const string NoteProjectileSpritePath = GeneratedFolder + "/OctopusNoteProjectileSprite.png";
    private const string SlowFieldWeakPointSpritePath = GeneratedFolder + "/OctopusSlowFieldWeakPointSprite.png";

    [MenuItem("Tools/Octopus Boss/Install Boss Setup")]
    public static void InstallBossSetup()
    {
        EnsureFolders();

        Sprite tentacleSprite = EnsureSprite(TentacleSpritePath, new Color32(118, 52, 171, 255), 16, 64, 16f);
        Sprite slamWarningSprite = EnsureSprite(SlamWarningSpritePath, new Color32(255, 36, 16, 230), 48, 10, 16f);
        Sprite sideSweepWarningSprite = EnsureSprite(SideSweepWarningSpritePath, new Color32(255, 54, 54, 135), 32, 6, 16f);
        Sprite dropProjectileSprite = EnsureSprite(DropProjectileSpritePath, new Color32(167, 73, 214, 255), 20, 20, 16f);
        Sprite slowFieldSprite = EnsureSprite(SlowFieldSpritePath, new Color32(95, 80, 220, 115), 16, 16, 16f);
        Sprite slowFieldWarningSprite = EnsureSprite(SlowFieldWarningSpritePath, new Color32(255, 208, 70, 130), 16, 16, 16f);
        Sprite noteProjectileSprite = EnsureSprite(NoteProjectileSpritePath, new Color32(255, 105, 210, 255), 16, 16, 16f);
        Sprite slowFieldWeakPointSprite = EnsureSprite(SlowFieldWeakPointSpritePath, new Color32(255, 190, 245, 255), 16, 16, 16f);

        TentacleSlamInstance slamPrefab = EnsureSlamTentaclePrefab(tentacleSprite);
        SideSweepInstance sideSweepPrefab = EnsureSideSweepPrefab(tentacleSprite);
        OctopusDropProjectile dropProjectilePrefab = EnsureDropProjectilePrefab(dropProjectileSprite);
        OctopusSlowFieldZone slowFieldPrefab = EnsureSlowFieldPrefab(slowFieldSprite);
        OctopusNoteProjectile noteProjectilePrefab = EnsureNoteProjectilePrefab(noteProjectileSprite);
        BossWeakPoint slowFieldWeakPointPrefab = EnsureSlowFieldWeakPointPrefab(slowFieldWeakPointSprite);
        GameObject slamWarningPrefab = EnsureWarningPrefab(SlamWarningPrefabPath, slamWarningSprite, "OctopusSlamWarning", new Vector3(1.8f, 1.4f, 1f), 100, new Color(1f, 0.2f, 0.05f, 0.9f));
        GameObject sideSweepWarningPrefab = EnsureWarningPrefab(SideSweepWarningPrefabPath, sideSweepWarningSprite, "OctopusSideSweepWarning", Vector3.one);
        GameObject slowFieldWarningPrefab = EnsureWarningPrefab(
            SlowFieldWarningPrefabPath,
            slowFieldWarningSprite,
            "OctopusSlowFieldWarning",
            Vector3.one,
            18,
            new Color(1f, 0.82f, 0.25f, 0.5f));

        OctopusBossController boss = FindOrCreateBoss();
        ConfigureBossSprites(boss);
        ConfigureBossRendering(boss);

        Transform setupRoot = EnsureChild(boss.transform, "OctopusBossAttackSetup", Vector3.zero);
        DeleteGeneratedSlamPoints(setupRoot);
        Transform sweepLeft = EnsureChild(setupRoot, "SweepLeftStart", new Vector3(-12f, -1.5f, 0f));
        Transform sweepRight = EnsureChild(setupRoot, "SweepRightStart", new Vector3(12f, -1.5f, 0f));
        Transform sweepLow = EnsureChild(setupRoot, "SweepHeight_Low", new Vector3(0f, -4.2f, 0f));
        Transform sweepMid = EnsureChild(setupRoot, "SweepHeight_Mid", new Vector3(0f, -2.2f, 0f));
        Transform sweepHigh = EnsureChild(setupRoot, "SweepHeight_High", new Vector3(0f, -0.2f, 0f));
        Transform noteSpawnLeft = EnsureChild(setupRoot, "NoteSpawn_Left", new Vector3(-2.2f, 2.2f, 0f));
        Transform noteSpawnCenter = EnsureChild(setupRoot, "NoteSpawn_Center", new Vector3(0f, 2.8f, 0f));
        Transform noteSpawnRight = EnsureChild(setupRoot, "NoteSpawn_Right", new Vector3(2.2f, 2.2f, 0f));

        OctopusBossAttackSelector selector = EnsureComponent<OctopusBossAttackSelector>(boss.gameObject);
        TentacleSlamAttack slamAttack = EnsureComponent<TentacleSlamAttack>(boss.gameObject);
        SideSweepAttack sideSweepAttack = EnsureComponent<SideSweepAttack>(boss.gameObject);
        OctopusDropAttack dropAttack = EnsureComponent<OctopusDropAttack>(boss.gameObject);
        OctopusSlowFieldAttack slowFieldAttack = EnsureComponent<OctopusSlowFieldAttack>(boss.gameObject);
        OctopusDropSweepComboAttack comboAttack = EnsureComponent<OctopusDropSweepComboAttack>(boss.gameObject);
        Transform[] allSlamPoints = FindAllSlamPoints(boss);
        ValidateSlamPoints(allSlamPoints, boss);

        ConfigureSelector(selector, boss, slamAttack, sideSweepAttack, dropAttack, slowFieldAttack, comboAttack);
        ConfigureSlamAttack(slamAttack, slamPrefab, slamWarningPrefab, allSlamPoints);
        ConfigureSideSweepAttack(sideSweepAttack, sideSweepPrefab, sideSweepWarningPrefab, sweepLeft, sweepRight, new[] { sweepLow, sweepMid, sweepHigh });
        ConfigureDropAttack(dropAttack, dropProjectilePrefab, slamWarningPrefab, allSlamPoints);
        ConfigureSlowFieldAttack(
            slowFieldAttack,
            slowFieldPrefab,
            slowFieldWarningPrefab,
            noteProjectilePrefab,
            slowFieldWeakPointPrefab,
            new[] { noteSpawnLeft, noteSpawnCenter, noteSpawnRight });
        ConfigureDropSweepCombo(comboAttack, dropAttack, sideSweepAttack);
        ConfigureRoom5Encounter(boss, selector);
        DisableLegacyLooseWeakPoints(boss);

        EditorUtility.SetDirty(boss.gameObject);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        EditorSceneManager.SaveScene(boss.gameObject.scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Octopus boss setup installed: attacks, Room5 encounter flow, doors, prefabs, warnings, and spawn points are wired.", boss);
    }

    [MenuItem("Tools/Octopus Boss/Validate Slam Points")]
    public static void ValidateInstalledSlamPoints()
    {
        OctopusBossController boss = Object.FindFirstObjectByType<OctopusBossController>();
        if (boss == null)
        {
            Debug.LogWarning("Octopus Boss validation could not find an OctopusBossController in the active scene.");
            return;
        }

        TentacleSlamAttack attack = boss.GetComponent<TentacleSlamAttack>();
        Transform[] configuredPoints = attack != null
            ? GetConfiguredSlamPoints(attack)
            : FindAllSlamPoints(boss);
        ValidateSlamPoints(configuredPoints, boss);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Boss");
        EnsureFolder(BossPrefabFolder, "Generated");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Sprite EnsureSprite(string assetPath, Color32 color, int width, int height, float pixelsPerUnit)
    {
        Sprite existing = LoadSprite(assetPath);
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        texture.SetPixels32(pixels);
        texture.Apply();

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return LoadSprite(assetPath);
    }

    private static TentacleSlamInstance EnsureSlamTentaclePrefab(Sprite sprite)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlamTentaclePrefabPath);
        if (existing != null)
        {
            ConfigureSlamTentaclePrefab(existing.GetComponent<TentacleSlamInstance>());
            return existing.GetComponent<TentacleSlamInstance>();
        }

        GameObject root = new GameObject("OctopusTentacleSlam");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.75f, 0.42f, 1f, 1f);
        renderer.sortingOrder = 8;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.1f, 3.8f);

        BossWeakPoint weakPoint = root.AddComponent<BossWeakPoint>();
        TentacleSlamInstance instance = root.AddComponent<TentacleSlamInstance>();

        SerializedObject weakPointObject = new SerializedObject(weakPoint);
        SetBool(weakPointObject, "isVulnerable", false);
        SetInt(weakPointObject, "damageMultiplier", 1);
        weakPointObject.ApplyModifiedPropertiesWithoutUndo();

        ConfigureSlamTentaclePrefab(instance);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlamTentaclePrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<TentacleSlamInstance>();
    }

    private static void ConfigureSlamTentaclePrefab(TentacleSlamInstance instance)
    {
        if (instance == null)
            return;

        Collider2D collider = instance.GetComponent<Collider2D>();
        BossWeakPoint weakPoint = instance.GetComponent<BossWeakPoint>();
        SerializedObject instanceObject = new SerializedObject(instance);
        SetObject(instanceObject, "damageHitbox", collider);
        SetObject(instanceObject, "weakPoint", weakPoint);
        SetVector2(instanceObject, "approachOffset", new Vector2(0f, 4f));
        SetFloat(instanceObject, "approachDuration", 0.08f);
        SetFloat(instanceObject, "retractDuration", 0.18f);
        SetBool(instanceObject, "destroyAfterRecover", true);
        instanceObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(instance);
    }

    private static SideSweepInstance EnsureSideSweepPrefab(Sprite sprite)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SideSweepPrefabPath);
        if (existing != null)
            return existing.GetComponent<SideSweepInstance>();

        GameObject root = new GameObject("OctopusSideSweep");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.7f, 0.35f, 1f, 1f);
        renderer.sortingOrder = 8;
        root.transform.localScale = new Vector3(2.8f, 0.65f, 1f);

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.1f, 3.8f);

        SideSweepInstance instance = root.AddComponent<SideSweepInstance>();
        SerializedObject instanceObject = new SerializedObject(instance);
        SetObject(instanceObject, "damageHitbox", collider);
        SetBool(instanceObject, "destroyAfterSweep", true);
        instanceObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SideSweepPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<SideSweepInstance>();
    }

    private static OctopusDropProjectile EnsureDropProjectilePrefab(Sprite sprite)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DropProjectilePrefabPath);
        if (existing != null)
        {
            OctopusDropProjectile existingProjectile = existing.GetComponent<OctopusDropProjectile>();
            ConfigureDropProjectilePrefab(existingProjectile);
            return existingProjectile;
        }

        GameObject root = new GameObject("OctopusDropProjectile");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.8f, 0.42f, 1f, 1f);
        renderer.sortingOrder = 9;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.05f, 1.05f);

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        OctopusDropProjectile projectile = root.AddComponent<OctopusDropProjectile>();
        ConfigureDropProjectilePrefab(projectile);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DropProjectilePrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<OctopusDropProjectile>();
    }

    private static void ConfigureDropProjectilePrefab(OctopusDropProjectile projectile)
    {
        if (projectile == null)
            return;

        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        Collider2D collider = projectile.GetComponent<Collider2D>();
        SerializedObject projectileObject = new SerializedObject(projectile);
        SetObject(projectileObject, "body", body);
        SetObject(projectileObject, "damageHitbox", collider);
        SetInt(projectileObject, "groundLayerMask", 1 << 6);
        SetBool(projectileObject, "destroyOnImpact", true);
        projectileObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(projectile);
    }

    private static OctopusSlowFieldZone EnsureSlowFieldPrefab(Sprite sprite)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlowFieldPrefabPath);
        if (existing != null)
            return existing.GetComponent<OctopusSlowFieldZone>();

        GameObject root = new GameObject("OctopusSlowField");
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(6f, 2.4f);

        GameObject visualObject = new GameObject("FieldVisual");
        visualObject.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.45f, 0.35f, 1f, 0.45f);
        renderer.sortingOrder = 7;

        OctopusSlowFieldZone zone = root.AddComponent<OctopusSlowFieldZone>();
        SerializedObject zoneObject = new SerializedObject(zone);
        SetObject(zoneObject, "fieldCollider", collider);
        SetObject(zoneObject, "fieldVisual", renderer);
        zoneObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlowFieldPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<OctopusSlowFieldZone>();
    }

    private static OctopusNoteProjectile EnsureNoteProjectilePrefab(Sprite sprite)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(NoteProjectilePrefabPath);
        if (existing != null)
            return existing.GetComponent<OctopusNoteProjectile>();

        GameObject root = new GameObject("OctopusNoteProjectile");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 0.45f, 0.9f, 1f);
        renderer.sortingOrder = 11;
        root.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.45f;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        OctopusNoteProjectile projectile = root.AddComponent<OctopusNoteProjectile>();
        SerializedObject projectileObject = new SerializedObject(projectile);
        SetObject(projectileObject, "body", body);
        SetObject(projectileObject, "damageHitbox", collider);
        projectileObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NoteProjectilePrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<OctopusNoteProjectile>();
    }

    private static BossWeakPoint EnsureSlowFieldWeakPointPrefab(Sprite sprite)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlowFieldWeakPointPrefabPath);
        if (existing != null)
            return existing.GetComponent<BossWeakPoint>();

        GameObject root = new GameObject("OctopusSlowFieldWeakPoint");
        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.65f;

        GameObject visualObject = new GameObject("WeakPointVisual");
        visualObject.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 0.65f, 0.95f, 1f);
        renderer.sortingOrder = 12;
        visualObject.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        BossWeakPoint weakPoint = root.AddComponent<BossWeakPoint>();
        GameObject hitParticleObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HitImpact_Particle.prefab");
        ParticleSystem hitParticle = hitParticleObject != null
            ? hitParticleObject.GetComponent<ParticleSystem>()
            : null;
        SerializedObject weakPointObject = new SerializedObject(weakPoint);
        SetBool(weakPointObject, "isVulnerable", false);
        SetInt(weakPointObject, "damageMultiplier", 1);
        SetObject(weakPointObject, "weakPointVisual", renderer);
        SetColor(weakPointObject, "inactiveColor", new Color(0.45f, 0.25f, 0.55f, 0.18f));
        SetColor(weakPointObject, "vulnerableColor", new Color(1f, 0.45f, 0.9f, 1f));
        SetFloat(weakPointObject, "pulseScaleMultiplier", 1.12f);
        SetFloat(weakPointObject, "pulseSpeed", 2.5f);
        SetObject(weakPointObject, "hitParticlePrefab", hitParticle);
        SetFloat(weakPointObject, "hitFlashDuration", 0.14f);
        SetBool(weakPointObject, "hideVisualWhenInactive", true);
        weakPointObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlowFieldWeakPointPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<BossWeakPoint>();
    }

    private static GameObject EnsureWarningPrefab(string path, Sprite sprite, string name, Vector3 scale)
    {
        return EnsureWarningPrefab(path, sprite, name, scale, 20, Color.white);
    }

    private static GameObject EnsureWarningPrefab(string path, Sprite sprite, string name, Vector3 scale, int sortingOrder, Color color)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            ConfigureWarningPrefab(existing, sprite, scale, sortingOrder, color);
            return existing;
        }

        GameObject root = new GameObject(name);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        ConfigureWarningRenderer(root.transform, renderer, sprite, scale, sortingOrder, color);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureWarningPrefab(GameObject prefab, Sprite sprite, Vector3 scale, int sortingOrder, Color color)
    {
        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = prefab.AddComponent<SpriteRenderer>();

        ConfigureWarningRenderer(prefab.transform, renderer, sprite, scale, sortingOrder, color);
        EditorUtility.SetDirty(prefab);
        EditorUtility.SetDirty(renderer);
    }

    private static void ConfigureWarningRenderer(Transform transform, SpriteRenderer renderer, Sprite sprite, Vector3 scale, int sortingOrder, Color color)
    {
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        transform.localScale = scale;
    }

    private static OctopusBossController FindOrCreateBoss()
    {
        OctopusBossController boss = Object.FindFirstObjectByType<OctopusBossController>();
        if (boss != null)
            return boss;

        GameObject bossObject = new GameObject("OctopusBoss");
        bossObject.transform.position = GetFallbackBossPosition();
        bossObject.AddComponent<SpriteRenderer>();
        return bossObject.AddComponent<OctopusBossController>();
    }

    private static Vector3 GetFallbackBossPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            return player.transform.position + new Vector3(14f, -4f, -5f);

        return Vector3.zero;
    }

    private static void ConfigureBossSprites(OctopusBossController boss)
    {
        Sprite idle = LoadSprite("Assets/boss/Duranzi.png");
        Sprite hurt = LoadSprite("Assets/boss/hasar alanzi.png");
        Sprite angry = LoadSprite("Assets/boss/sinirliy.png");
        Sprite dead = LoadSprite("Assets/boss/ded.png");

        SerializedObject bossObject = new SerializedObject(boss);
        SetInt(bossObject, "maxHealth", 6);
        SetInt(bossObject, "phaseTwoHealthThreshold", 3);
        SetObject(bossObject, "idleSprite", idle);
        SetObject(bossObject, "hurtSprite", hurt);
        SetObject(bossObject, "angrySprite", angry);
        SetObject(bossObject, "deadSprite", dead);
        SetInt(bossObject, "currentHealth", 6);
        SetBool(bossObject, "isPhaseTwo", false);
        bossObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBossRendering(OctopusBossController boss)
    {
        SpriteRenderer renderer = boss.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = boss.gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = LoadSprite("Assets/boss/Duranzi.png");
        renderer.sortingOrder = -5;
        EditorUtility.SetDirty(renderer);
    }

    private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject childObject = new GameObject(name);
            child = childObject.transform;
            child.SetParent(parent, false);
        }

        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        EditorUtility.SetDirty(child);
        return child;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }

    private static void ConfigureRoom5Encounter(
        OctopusBossController boss,
        OctopusBossAttackSelector selector)
    {
        Room room5 = boss != null ? boss.GetComponentInParent<Room>() : null;
        if (room5 == null)
        {
            Debug.LogWarning("Octopus encounter setup could not find the Room component containing the boss.", boss);
            return;
        }

        Room5EnterTrigger cameraTrigger = room5.GetComponentInChildren<Room5EnterTrigger>(true);
        if (cameraTrigger == null)
        {
            Debug.LogWarning("Octopus encounter setup could not find a Room5EnterTrigger under Room5.", room5);
            return;
        }

        Collider2D encounterTrigger = cameraTrigger.GetComponent<Collider2D>();
        if (encounterTrigger == null)
        {
            BoxCollider2D createdTrigger = cameraTrigger.gameObject.AddComponent<BoxCollider2D>();
            createdTrigger.size = new Vector2(2f, 8f);
            encounterTrigger = createdTrigger;
        }

        encounterTrigger.isTrigger = true;

        BoxCollider2D roomCollider = room5.GetComponent<BoxCollider2D>();
        Bounds roomBounds = roomCollider != null
            ? roomCollider.bounds
            : new Bounds(room5.transform.position, new Vector3(40f, 20f, 0f));
        Bounds triggerBounds = encounterTrigger.bounds;
        float doorHeight = Mathf.Max(2f, triggerBounds.size.y);
        float doorY = triggerBounds.center.y;

        Door entranceDoor = EnsureEncounterDoor(
            room5.transform,
            "BossEntranceDoor",
            new Vector3(roomBounds.min.x + 0.25f, doorY, 0f),
            new Vector2(0.5f, doorHeight),
            true);
        Door exitDoor = EnsureEncounterDoor(
            room5.transform,
            "BossExitDoor",
            new Vector3(roomBounds.max.x - 0.25f, doorY, 0f),
            new Vector2(0.5f, doorHeight),
            false);

        AudioSource musicSource = EnsureComponent<AudioSource>(cameraTrigger.gameObject);
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        int playerLayerMask = player != null ? 1 << player.gameObject.layer : 1;

        Room5BossEncounterController encounter = EnsureComponent<Room5BossEncounterController>(cameraTrigger.gameObject);
        SerializedObject encounterObject = new SerializedObject(encounter);
        SetObject(encounterObject, "bossController", boss);
        SetObject(encounterObject, "attackSelector", selector);
        SetObject(encounterObject, "encounterTrigger", encounterTrigger);
        SetInt(encounterObject, "playerLayer", playerLayerMask);
        SetObject(encounterObject, "entranceDoor", entranceDoor);
        SetObject(encounterObject, "exitDoor", exitDoor);
        SetObject(encounterObject, "room5Camera", cameraTrigger);
        SetFloat(encounterObject, "introDelay", 1.25f);
        SetFloat(encounterObject, "deathDelay", 1.25f);
        SetObject(encounterObject, "bossMusicSource", musicSource);
        SetObject(encounterObject, "bossMusicClip", null);
        SetObject(encounterObject, "victoryMusicClip", null);
        SetObject(encounterObject, "bossHealthBar", null);
        SetBool(encounterObject, "startOnlyOnce", true);
        encounterObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        EditorUtility.SetDirty(cameraTrigger.gameObject);
    }

    private static Door EnsureEncounterDoor(
        Transform roomRoot,
        string doorName,
        Vector3 worldPosition,
        Vector2 colliderSize,
        bool startOpen)
    {
        Transform doorTransform = roomRoot.Find(doorName);
        if (doorTransform == null)
        {
            GameObject doorObject = new GameObject(doorName);
            doorTransform = doorObject.transform;
            doorTransform.SetParent(roomRoot, false);
        }

        doorTransform.position = worldPosition;
        doorTransform.rotation = Quaternion.identity;
        doorTransform.localScale = Vector3.one;

        BoxCollider2D blockingCollider = EnsureComponent<BoxCollider2D>(doorTransform.gameObject);
        blockingCollider.isTrigger = false;
        blockingCollider.size = colliderSize;
        blockingCollider.offset = Vector2.zero;
        blockingCollider.enabled = !startOpen;

        Door door = EnsureComponent<Door>(doorTransform.gameObject);
        SerializedObject doorObjectSerialized = new SerializedObject(door);
        SetObject(doorObjectSerialized, "blockingCollider", blockingCollider);
        SetObject(doorObjectSerialized, "animator", null);
        SetBool(doorObjectSerialized, "startOpen", startOpen);
        doorObjectSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(blockingCollider);
        EditorUtility.SetDirty(door);
        EditorUtility.SetDirty(doorTransform);
        return door;
    }

    private static void ConfigureSelector(
        OctopusBossAttackSelector selector,
        OctopusBossController boss,
        TentacleSlamAttack slamAttack,
        SideSweepAttack sideSweepAttack,
        OctopusDropAttack dropAttack,
        OctopusSlowFieldAttack slowFieldAttack,
        OctopusDropSweepComboAttack comboAttack)
    {
        SerializedObject selectorObject = new SerializedObject(selector);
        SetObject(selectorObject, "boss", boss);
        SetBool(selectorObject, "startLoopOnEnable", false);
        SetFloat(selectorObject, "initialAttackDelay", 0f);
        SetFloat(selectorObject, "attackCooldown", 1.4f);
        SetFloat(selectorObject, "phaseTwoAttackCooldown", 0.7f);
        SetObject(selectorObject, "phaseTwoComboAttack", comboAttack);
        SetFloat(selectorObject, "comboChance", 0.2f);
        SetObjectArray(selectorObject, "attacks", new Object[] { slamAttack, sideSweepAttack, dropAttack, slowFieldAttack });
        SetInt(selectorObject, "maxConsecutiveSameAttack", 2);
        SetBool(selectorObject, "debugAttackSelection", false);
        SerializedProperty weightedAttacks = selectorObject.FindProperty("weightedAttacks");
        weightedAttacks.arraySize = 4;
        ConfigureWeightedAttackEntry(weightedAttacks.GetArrayElementAtIndex(0), slamAttack, 45f, 35f);
        ConfigureWeightedAttackEntry(weightedAttacks.GetArrayElementAtIndex(1), sideSweepAttack, 35f, 30f);
        ConfigureWeightedAttackEntry(weightedAttacks.GetArrayElementAtIndex(2), dropAttack, 20f, 35f);
        ConfigureWeightedAttackEntry(weightedAttacks.GetArrayElementAtIndex(3), slowFieldAttack, 12f, 22f);
        selectorObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(selector);
    }

    private static void ConfigureWeightedAttackEntry(
        SerializedProperty entry,
        OctopusBossAttack attack,
        float phaseOneWeight,
        float phaseTwoWeight)
    {
        entry.FindPropertyRelative("attack").objectReferenceValue = attack;
        entry.FindPropertyRelative("phaseOneWeight").floatValue = phaseOneWeight;
        entry.FindPropertyRelative("phaseTwoWeight").floatValue = phaseTwoWeight;
    }

    private static void ConfigureSlamAttack(TentacleSlamAttack attack, TentacleSlamInstance prefab, GameObject warningPrefab, Transform[] slamPoints)
    {
        SerializedObject attackObject = new SerializedObject(attack);
        SetObject(attackObject, "tentacleSlamPrefab", prefab);
        SetObject(attackObject, "warningIndicatorPrefab", warningPrefab);
        SetObjectArray(attackObject, "slamSpawnPoints", slamPoints);
        SetFloat(attackObject, "phase1WarningDuration", 0.7f);
        SetFloat(attackObject, "phase2WarningDuration", 0.5f);
        SetFloat(attackObject, "impactDamageDuration", 0.2f);
        SetFloat(attackObject, "phase1VulnerableDuration", 0.9f);
        SetFloat(attackObject, "phase2VulnerableDuration", 0.7f);
        SetFloat(attackObject, "phase1RecoverDuration", 0.3f);
        SetFloat(attackObject, "phase2RecoverDuration", 0.2f);
        SetFloat(attackObject, "doubleSlamChance", 0.45f);
        SetFloat(attackObject, "doubleSlamDelay", 0.25f);
        SetInt(attackObject, "playerDamage", 1);
        attackObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
    }

    private static Transform[] FindAllSlamPoints(OctopusBossController boss)
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<Transform> slamPoints = new List<Transform>();

        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate.gameObject.scene != boss.gameObject.scene)
                continue;

            if (!candidate.gameObject.activeInHierarchy)
                continue;

            if (!IsUserSlamPoint(candidate.name))
                continue;

            slamPoints.Add(candidate);
        }

        return slamPoints
            .OrderBy(point => GetSlamPointSortGroup(point))
            .ThenBy(point => point.name, System.StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateSlamPoints(Transform[] slamPoints, Object context)
    {
        if (slamPoints == null || slamPoints.Length == 0)
        {
            Debug.LogWarning("Octopus Boss has no valid SlamPoint references.", context);
            return;
        }

        float referenceY = 0f;
        bool hasReferenceY = false;
        int validPointCount = 0;

        for (int i = 0; i < slamPoints.Length; i++)
        {
            Transform point = slamPoints[i];
            if (point == null)
            {
                Debug.LogWarning($"Octopus Boss slam point entry {i} is null.", context);
                continue;
            }

            validPointCount++;
            if (!Approximately(point.lossyScale, Vector3.one))
            {
                Debug.LogWarning($"{point.name} has non-unit world scale {point.lossyScale}.", point);
            }

            if (Quaternion.Angle(point.rotation, Quaternion.identity) > 0.1f)
            {
                Debug.LogWarning($"{point.name} has non-zero world rotation {point.eulerAngles}.", point);
            }

            if (!hasReferenceY)
            {
                referenceY = point.position.y;
                hasReferenceY = true;
            }
            else if (Mathf.Abs(point.position.y - referenceY) > 0.25f)
            {
                Debug.LogWarning($"{point.name} world Y ({point.position.y:0.###}) differs from the first SlamPoint Y ({referenceY:0.###}).", point);
            }
        }

        Debug.Log($"Octopus Boss SlamPoint validation completed with {validPointCount} valid point(s).", context);
    }

    private static Transform[] GetConfiguredSlamPoints(TentacleSlamAttack attack)
    {
        SerializedObject attackObject = new SerializedObject(attack);
        SerializedProperty pointsProperty = attackObject.FindProperty("slamSpawnPoints");
        if (pointsProperty == null || !pointsProperty.isArray)
            return System.Array.Empty<Transform>();

        Transform[] points = new Transform[pointsProperty.arraySize];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
        }

        return points;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.001f &&
            Mathf.Abs(a.y - b.y) < 0.001f &&
            Mathf.Abs(a.z - b.z) < 0.001f;
    }

    private static bool IsUserSlamPoint(string name)
    {
        const string prefix = "SlamPoint_";
        if (!name.StartsWith(prefix, System.StringComparison.Ordinal))
            return false;

        if (name.Length == prefix.Length)
            return false;

        for (int i = prefix.Length; i < name.Length; i++)
        {
            if (!char.IsDigit(name[i]))
                return false;
        }

        return true;
    }

    private static int GetSlamPointSortGroup(Transform point)
    {
        if (point.name.Length > "SlamPoint_".Length && char.IsDigit(point.name["SlamPoint_".Length]))
            return 0;

        return 1;
    }

    private static void DeleteGeneratedSlamPoints(Transform setupRoot)
    {
        DeleteChildIfExists(setupRoot, "SlamPoint_Left");
        DeleteChildIfExists(setupRoot, "SlamPoint_Mid");
        DeleteChildIfExists(setupRoot, "SlamPoint_Right");
    }

    private static void DeleteChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
            return;

        Object.DestroyImmediate(child.gameObject);
        EditorUtility.SetDirty(parent);
    }

    private static void ConfigureSideSweepAttack(SideSweepAttack attack, SideSweepInstance prefab, GameObject warningPrefab, Transform leftStart, Transform rightStart, Transform[] heightPoints)
    {
        SerializedObject attackObject = new SerializedObject(attack);
        SetObject(attackObject, "sideSweepPrefab", prefab);
        SetObject(attackObject, "warningIndicatorPrefab", warningPrefab);
        SetObject(attackObject, "leftStartPoint", leftStart);
        SetObject(attackObject, "rightStartPoint", rightStart);
        SetObjectArray(attackObject, "sweepSpawnPoints", heightPoints);
        SetFloat(attackObject, "phase1WarningDuration", 0.7f);
        SetFloat(attackObject, "phase2WarningDuration", 0.5f);
        SetFloat(attackObject, "recoverDuration", 0.15f);
        SetFloat(attackObject, "sweepSpeed", 15f);
        SetFloat(attackObject, "phase2SpeedMultiplier", 1.3f);
        SetBool(attackObject, "randomizeDirection", true);
        SetBool(attackObject, "alternateDirection", true);
        SetBool(attackObject, "startFromLeftFirst", true);
        SetFloat(attackObject, "warningArrowLength", 2.5f);
        SetFloat(attackObject, "warningArrowHeadLength", 0.8f);
        SetFloat(attackObject, "warningThickness", 0.25f);
        SetFloat(attackObject, "warningEdgeInset", 1.2f);
        SetInt(attackObject, "playerDamage", 1);
        attackObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
    }

    private static void ConfigureDropAttack(
        OctopusDropAttack attack,
        OctopusDropProjectile projectilePrefab,
        GameObject warningPrefab,
        Transform[] targetPoints)
    {
        SerializedObject attackObject = new SerializedObject(attack);
        SetObject(attackObject, "dropProjectilePrefab", projectilePrefab);
        SetObject(attackObject, "warningIndicatorPrefab", warningPrefab);
        SetObjectArray(attackObject, "targetPoints", targetPoints);
        SetFloat(attackObject, "dropSpawnY", 24f);
        SetFloat(attackObject, "phase1WarningDuration", 0.7f);
        SetFloat(attackObject, "phase2WarningDuration", 0.5f);
        SetFloat(attackObject, "phase1ProjectileInterval", 0.15f);
        SetFloat(attackObject, "phase2ProjectileInterval", 0.1f);
        SetInt(attackObject, "phase1ProjectileCount", 3);
        SetInt(attackObject, "phase2ProjectileCount", 5);
        SetInt(attackObject, "phase1NearestPointPoolSize", 5);
        SetInt(attackObject, "phase2NearestPointPoolSize", 7);
        SetFloat(attackObject, "projectileFallSpeed", 11f);
        SetFloat(attackObject, "phase2FallSpeedMultiplier", 1.25f);
        SetFloat(attackObject, "projectileLifetime", 4f);
        SetInt(attackObject, "playerDamage", 1);
        SetFloat(attackObject, "recoverDuration", 0.4f);
        attackObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
    }

    private static void ConfigureSlowFieldAttack(
        OctopusSlowFieldAttack attack,
        OctopusSlowFieldZone slowFieldPrefab,
        GameObject warningPrefab,
        OctopusNoteProjectile noteProjectilePrefab,
        BossWeakPoint weakPointPrefab,
        Transform[] noteSpawnPoints)
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        SerializedObject attackObject = new SerializedObject(attack);
        SetObject(attackObject, "slowFieldPrefab", slowFieldPrefab);
        SetObject(attackObject, "slowFieldWarningPrefab", warningPrefab);
        SetObject(attackObject, "noteProjectilePrefab", noteProjectilePrefab);
        SetObject(attackObject, "weakPointPrefab", weakPointPrefab);
        SetObject(attackObject, "playerTarget", player != null ? player.transform : null);
        SetObjectArray(attackObject, "noteSpawnPoints", noteSpawnPoints);
        SetInt(attackObject, "groundLayerMask", 1 << 6);
        SetFloat(attackObject, "groundProbeHeight", 8f);
        SetVector2(attackObject, "fieldSize", new Vector2(6f, 2.4f));
        SetFloat(attackObject, "warningDurationPhase1", 0.8f);
        SetFloat(attackObject, "warningDurationPhase2", 0.55f);
        SetFloat(attackObject, "fieldDurationPhase1", 3.2f);
        SetFloat(attackObject, "fieldDurationPhase2", 3.8f);
        SetFloat(attackObject, "movementMultiplierPhase1", 0.65f);
        SetFloat(attackObject, "movementMultiplierPhase2", 0.5f);
        SetInt(attackObject, "noteWaveCountPhase1", 3);
        SetInt(attackObject, "noteWaveCountPhase2", 4);
        SetInt(attackObject, "notesPerWavePhase1", 2);
        SetInt(attackObject, "notesPerWavePhase2", 3);
        SetFloat(attackObject, "waveIntervalPhase1", 0.65f);
        SetFloat(attackObject, "waveIntervalPhase2", 0.45f);
        SetFloat(attackObject, "noteSpreadAngle", 14f);
        SetFloat(attackObject, "noteProjectileSpeed", 8.5f);
        SetFloat(attackObject, "phase2ProjectileSpeedMultiplier", 1.2f);
        SetFloat(attackObject, "projectileLifetime", 4f);
        SetInt(attackObject, "playerDamage", 1);
        SetFloat(attackObject, "vulnerableDurationPhase1", 1f);
        SetFloat(attackObject, "vulnerableDurationPhase2", 0.8f);
        SetFloat(attackObject, "recoverDuration", 0.4f);
        attackObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
    }

    private static void ConfigureDropSweepCombo(
        OctopusDropSweepComboAttack combo,
        OctopusDropAttack dropAttack,
        SideSweepAttack sideSweepAttack)
    {
        SerializedObject comboObject = new SerializedObject(combo);
        SetObject(comboObject, "dropAttack", dropAttack);
        SetObject(comboObject, "sideSweepAttack", sideSweepAttack);
        SetFloat(comboObject, "sideSweepDelayAfterFirstDrop", 0.5f);
        comboObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combo);
    }

    private static void DisableLegacyLooseWeakPoints(OctopusBossController boss)
    {
        BossWeakPoint[] weakPoints = Object.FindObjectsByType<BossWeakPoint>(FindObjectsSortMode.None);
        foreach (BossWeakPoint weakPoint in weakPoints)
        {
            if (weakPoint == null || weakPoint.gameObject.scene != boss.gameObject.scene)
                continue;

            if (weakPoint.GetComponent<TentacleSlamInstance>() != null || weakPoint.GetComponent<SideSweepInstance>() != null)
                continue;

            if (weakPoint.GetComponentInParent<OctopusBossController>() == boss)
                continue;

            GameObject weakPointObject = weakPoint.gameObject;
            if (!weakPointObject.name.StartsWith("DISABLED_Legacy_", System.StringComparison.Ordinal))
                weakPointObject.name = "DISABLED_Legacy_" + weakPointObject.name;

            weakPointObject.SetActive(false);
            EditorUtility.SetDirty(weakPointObject);
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is Sprite foundSprite)
                return foundSprite;
        }

        return null;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetObjectArray(SerializedObject serializedObject, string propertyName, Object[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetColor(SerializedObject serializedObject, string propertyName, Color value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    private static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector2Value = value;
    }
}
