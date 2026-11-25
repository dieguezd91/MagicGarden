using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Enemy attack feel: house camera shake (S2, S5), plant punch (S2, S5), impact particles on Turret/Sniper projectiles (S2, S4, S5)
// and Infernum honoring its FireRate (S3). Editor test assembly deliberately enters real Play Mode.
// Reflection preserves the Assembly-CSharp boundary. No lambdas capturing locals: closures are lost on the domain reload.
public class EnemyImpactFeelTests
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string TurretProjectilePath = "Assets/Prefabs/MinionTurret Proyectile 1.prefab";
    private const string SniperProjectilePath = "Assets/Prefabs/SniperBoss Proyectile.prefab";
    private const string TurretImpactPath = "Assets/Prefabs/UI/ImpactParticles.prefab";
    private const string SniperImpactPath = "Assets/Prefabs/UI/FireImpactParticles.prefab";

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) { Time.timeScale = 1f; yield return new ExitPlayMode(); }
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, AnyInstance).SetValue(target, value);
    private static float Float(object value) => Convert.ToSingle(value);
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
    private static Component Add(GameObject go, string type) => go.AddComponent(TypeOf(type));
    private static object Method(object target, string name, params object[] args) => target.GetType().GetMethod(name, AnyInstance).Invoke(target, args);

    // ---------- Fixtures ----------

    private static Component NewShaker()
    {
        TypeOf("CameraShaker").GetField("Instance").SetValue(null, null); // a scene shaker would make the new one destroy itself
        var shaker = Add(new GameObject("TestCameraShaker"), "CameraShaker");
        Set(shaker, "cameraOffsetController", Add(new GameObject("TestCameraOffset"), "CameraOffsetController"));
        return shaker;
    }

    private static Transform OffsetOf(Component shaker) => ((Component)Field(shaker, "cameraOffsetController")).transform;

    private static Component NewHouse(float intensity, float duration, float minInterval)
    {
        var house = Add(new GameObject("TestHouse"), "HouseLifeController");
        Set(house, "cameraShakeIntensity", intensity);
        Set(house, "cameraShakeDuration", duration);
        Set(house, "cameraShakeMinInterval", minInterval);
        return house;
    }

    private static Component NewLife(Vector3 scale, bool plant)
    {
        var go = new GameObject(plant ? "TestPlant" : "TestPlayerLike");
        go.transform.localScale = scale;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        var life = Add(go, "LifeController");
        Set(life, "maxHealth", 1000f);
        Set(life, "punchMinInterval", 0f);
        if (plant) Call(life, "ConfigureAsPlant", false); // same call Plant.cs makes
        return life;
    }

    private static void Hit(Component life, float amount)
    {
        var type = TypeOf("LifeController");
        type.GetMethod("TakeDamage").Invoke(life, new object[] { amount, Enum.ToObject(type.GetNestedType("DamageType"), 0), Enum.ToObject(type.GetNestedType("DamageElement"), 0) });
    }

    private static void AssertScale(Transform t, Vector3 expected, string message)
    {
        Assert.That(t.localScale.x, Is.EqualTo(expected.x).Within(1e-6f), message + " (x)");
        Assert.That(t.localScale.y, Is.EqualTo(expected.y).Within(1e-6f), message + " (y)");
        Assert.That(t.localScale.z, Is.EqualTo(expected.z).Within(1e-6f), message + " (z)");
    }

    // ---------- House camera shake (S2, S5) ----------

    [UnityTest] public IEnumerator HouseDefaultsShakeTheCameraGentlyAndThrottleIt()
    {
        yield return new EnterPlayMode();
        var house = Add(new GameObject("TestHouse"), "HouseLifeController");
        Assert.That(Float(Field(house, "cameraShakeIntensity")), Is.EqualTo(0.15f).Within(1e-6f), "S2: default house shake intensity is 0.15");
        Assert.That(Float(Field(house, "cameraShakeDuration")), Is.EqualTo(0.15f).Within(1e-6f), "S2: default house shake duration is 0.15");
        Assert.That(Float(Field(house, "cameraShakeMinInterval")), Is.GreaterThan(0f), "S2: the shake is throttled by default so swarms do not spam it");
    }

    [UnityTest] public IEnumerator HouseDamageShakesTheCameraAndStillHurtsTheHouse()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var shaker = NewShaker();
        var house = NewHouse(10f, 0.2f, 0f);
        yield return Frames(2);
        float before = Float(Field(house, "currentHealth"));

        Call(house, "TakeDamage", 5f);

        Assert.That(OffsetOf(shaker).localPosition.magnitude, Is.GreaterThan(0.01f), "S2: damaging the house shakes the camera");
        Assert.That(Float(Field(house, "currentHealth")), Is.EqualTo(before - 5f), "S2: the house still takes the damage");
    }

    [UnityTest] public IEnumerator HouseShakeMinIntervalBlocksRapidHitsUntilItPasses()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var shaker = NewShaker();
        var house = NewHouse(10f, 0.1f, 5f);
        yield return Frames(2);

        Call(house, "TakeDamage", 1f);
        Assert.That(OffsetOf(shaker).localPosition.magnitude, Is.GreaterThan(0.01f), "S2: fixture: the first hit shakes");
        yield return WaitRealtime(0.5f);
        Assert.That(OffsetOf(shaker).localPosition, Is.EqualTo(Vector3.zero), "S2: fixture: the first shake finished and reset the camera");

        Call(house, "TakeDamage", 1f);
        yield return Frames(2);
        Assert.That(OffsetOf(shaker).localPosition, Is.EqualTo(Vector3.zero), "S2/S5: a second hit inside the min interval does not shake again");

        Set(house, "cameraShakeMinInterval", 0f);
        Call(house, "TakeDamage", 1f);
        Assert.That(OffsetOf(shaker).localPosition.magnitude, Is.GreaterThan(0.01f), "S5: with the min interval lowered the next hit shakes");
    }

    [UnityTest] public IEnumerator HouseShakeIsDisabledByZeroIntensityOrZeroDuration()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var shaker = NewShaker();
        var noIntensity = NewHouse(0f, 0.2f, 0f);
        var noDuration = NewHouse(10f, 0f, 0f);
        yield return Frames(2);

        Call(noIntensity, "TakeDamage", 1f);
        Call(noDuration, "TakeDamage", 1f);
        yield return Frames(3);

        Assert.That(OffsetOf(shaker).localPosition, Is.EqualTo(Vector3.zero), "S5: intensity 0 or duration 0 turns the house camera shake off");
        Assert.That(Float(Field(noIntensity, "currentHealth")), Is.EqualTo(Float(Field(noIntensity, "maxHealth")) - 1f), "S5: the house still takes damage with the shake off");
    }

    // ---------- Plant punch (S2, S5) ----------

    [UnityTest] public IEnumerator PlantPunchesOnDamageAndReturnsToTheExactOriginalScale()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var original = new Vector3(2f, 3f, 1f);
        var plant = NewLife(original, true);
        yield return Frames(2);

        Hit(plant, 1f);
        float maxDeviation = 0f;
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            maxDeviation = Mathf.Max(maxDeviation, Mathf.Abs(plant.transform.localScale.x - original.x));
            if (i == 3) Hit(plant, 1f); // a second hit mid-punch must not drift the scale
        }
        Assert.That(maxDeviation, Is.GreaterThan(0.01f), "S2: a damaged plant punches its scale");
        yield return WaitRealtime(0.6f);
        AssertScale(plant.transform, original, "S2: the plant returns to its exact original scale");
    }

    [UnityTest] public IEnumerator PlayerLikeLifeControllerDoesNotPunch()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var original = new Vector3(2f, 2f, 1f);
        var playerLike = NewLife(original, false); // neither enemy nor plant: the same flags as the player
        yield return Frames(2);

        Hit(playerLike, 1f);
        for (int i = 0; i < 6; i++)
        {
            yield return null;
            AssertScale(playerLike.transform, original, "S2: only enemies and plants punch, never the player");
        }
    }

    [UnityTest] public IEnumerator PlantPunchCanBeDisabledWithPunchPlantsAndTheMasterToggle()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var original = new Vector3(2f, 2f, 1f);
        var plantsOff = NewLife(original, true);
        Set(plantsOff, "punchPlants", false);
        var masterOff = NewLife(original, true);
        Set(masterOff, "punchOnDamage", false);
        Assert.That((bool)Field(NewLife(original, true), "punchPlants"), Is.True, "S2: plants punch by default");
        yield return Frames(2);

        Hit(plantsOff, 1f);
        Hit(masterOff, 1f);
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            AssertScale(plantsOff.transform, original, "S5: punchPlants = false keeps the plant still");
            AssertScale(masterOff.transform, original, "S5: punchOnDamage = false keeps the plant still");
        }
    }

    // ---------- Projectile impact particles (S2, S4, S5) ----------

    private static GameObject LoadPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, "fixture: prefab exists at " + path);
        return prefab;
    }

    private static UnityEngine.Object ImpactParticlesOf(string projectilePath, string type) =>
        (UnityEngine.Object)Field(LoadPrefab(projectilePath).GetComponent(TypeOf(type)), "impactParticlesPrefab");

    [Test] public void TurretProjectilePrefabReferencesTheExistingImpactParticles()
    {
        var particles = ImpactParticlesOf(TurretProjectilePath, "TurretMinionProjectile");
        Assert.That(particles, Is.Not.Null, "S2: the turret projectile has impact particles assigned");
        Assert.That(AssetDatabase.GetAssetPath(particles), Is.EqualTo(TurretImpactPath), "S4: it reuses an existing particle prefab, no new art");
        Assert.That(((GameObject)particles).GetComponent<ParticleSystem>(), Is.Not.Null, "S2: the referenced prefab is a particle system");
    }

    [Test] public void SniperProjectilePrefabReferencesTheExistingImpactParticles()
    {
        var particles = ImpactParticlesOf(SniperProjectilePath, "SniperBossProjectile");
        Assert.That(particles, Is.Not.Null, "S2: the sniper projectile has impact particles assigned");
        Assert.That(AssetDatabase.GetAssetPath(particles), Is.EqualTo(SniperImpactPath), "S4: it reuses an existing particle prefab, no new art");
        Assert.That(((GameObject)particles).GetComponent<ParticleSystem>(), Is.Not.Null, "S2: the referenced prefab is a particle system");
    }

    private static GameObject NewPlayerTarget()
    {
        var go = new GameObject("TestProjectileTarget");
        go.transform.position = new Vector3(1000f, 1000f, 0f); // far from the projectile: physics must not trigger a hit by itself, the test calls OnTriggerEnter2D
        go.tag = "Player";
        go.AddComponent<BoxCollider2D>();
        return go;
    }

    private static Component NewProjectile(string path, string type)
    {
        var projectile = UnityEngine.Object.Instantiate(LoadPrefab(path)).GetComponent(TypeOf(type));
        Call(projectile, "Initialize", null, Vector2.right, 0f, 1f, 30f);
        return projectile;
    }

    private static GameObject FindClone(string path) => GameObject.Find(AssetDatabase.LoadAssetAtPath<GameObject>(path).name + "(Clone)");

    private static IEnumerator AssertHitSpawnsTheParticles(string projectilePath, string type, string particlesPath)
    {
        var target = NewPlayerTarget();
        var projectile = NewProjectile(projectilePath, type);
        yield return Frames(1);
        Assert.That(FindClone(particlesPath), Is.Null, "fixture: no impact particles exist before the hit");

        Vector3 impactPoint = projectile.transform.position;
        Method(projectile, "OnTriggerEnter2D", target.GetComponent<Collider2D>());

        var spawned = FindClone(particlesPath);
        Assert.That(spawned, Is.Not.Null, "S2: the hit spawns the impact particles");
        Assert.That(Vector3.Distance(spawned.transform.position, impactPoint), Is.LessThan(0.001f), "S2: the particles appear at the impact point");
        var system = spawned.GetComponent<ParticleSystem>();
        Assert.That(system.isPlaying, Is.True, "S2: the spawned particles are playing");
        float lifetime = Mathf.Max(1f, system.main.duration + system.main.startLifetime.constantMax) + 0.2f;

        yield return WaitRealtime(lifetime + 0.5f);
        Assert.That(FindClone(particlesPath), Is.Null, "S2: the particles are destroyed after their lifetime");
    }

    private static IEnumerator AssertNoParticlesWithoutPrefab(string projectilePath, string type, string particlesPath)
    {
        var target = NewPlayerTarget();
        var projectile = NewProjectile(projectilePath, type);
        Set(projectile, "impactParticlesPrefab", null);
        yield return Frames(1);

        Method(projectile, "OnTriggerEnter2D", target.GetComponent<Collider2D>());

        Assert.That(FindClone(particlesPath), Is.Null, "S5: with no impact particles assigned the hit spawns nothing");
    }

    [UnityTest] public IEnumerator TurretProjectileHitSpawnsAndCleansUpImpactParticles()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return AssertHitSpawnsTheParticles(TurretProjectilePath, "TurretMinionProjectile", TurretImpactPath);
    }

    [UnityTest] public IEnumerator SniperProjectileHitSpawnsAndCleansUpImpactParticles()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return AssertHitSpawnsTheParticles(SniperProjectilePath, "SniperBossProjectile", SniperImpactPath);
    }

    [UnityTest] public IEnumerator ProjectilesWithoutImpactParticlesSpawnNothing()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return AssertNoParticlesWithoutPrefab(TurretProjectilePath, "TurretMinionProjectile", TurretImpactPath);
        yield return AssertNoParticlesWithoutPrefab(SniperProjectilePath, "SniperBossProjectile", SniperImpactPath);
    }

    // ---------- Infernum FireRate (S3) ----------

    private static readonly List<float> ShotTimes = new List<float>();

    private static Transform NewBulletPool()
    {
        TypeOf("BulletPool").GetField("Instance").SetValue(null, null);
        var template = new GameObject("TestBulletTemplate");
        template.SetActive(false);
        template.AddComponent<Rigidbody2D>().gravityScale = 0f;
        var bullet = Add(template, "FireBullet");
        Set(bullet, "lifeTime", 60f); // never returns to the pool during the test, so child count = shots fired
        var poolGo = new GameObject("TestBulletPool");
        poolGo.SetActive(false);
        var pool = Add(poolGo, "BulletPool");
        Set(pool, "bulletPrefab", bullet);
        Set(pool, "iceBallPrefab", bullet);
        Set(pool, "initialFireBulletSize", 0);
        Set(pool, "initialIceBallSize", 0);
        poolGo.SetActive(true);
        return poolGo.transform;
    }

    private static Component NewInfernum(float fireRate)
    {
        var data = ScriptableObject.CreateInstance(TypeOf("RangedEnemyDataSO"));
        Set(data, "fireRate", fireRate);
        var go = new GameObject("TestInfernum");
        go.SetActive(false); // Awake (which reads the data) must run after the fields are set
        var infernum = Add(go, "Infernum");
        Set(infernum, "rangedData", data);
        Set(infernum, "firingPoint", go.transform);
        Set(infernum, "currentTarget", new GameObject("TestInfernumTarget").transform);
        ((Behaviour)infernum).enabled = false; // no Start/Update: the test calls PerformAttack itself
        go.SetActive(true);
        return infernum;
    }

    // Calls PerformAttack every frame for `window` seconds and records Time.time of each bullet fired.
    private static IEnumerator FireFor(Component infernum, Transform pool, float window)
    {
        ShotTimes.Clear();
        int fired = pool.childCount;
        float end = Time.time + window;
        while (Time.time < end)
        {
            Call(infernum, "PerformAttack");
            if (pool.childCount > fired) { fired = pool.childCount; ShotTimes.Add(Time.time); }
            yield return null;
        }
    }

    private static void AssertGaps(float rate, string message)
    {
        for (int i = 1; i < ShotTimes.Count; i++)
        {
            float gap = ShotTimes[i] - ShotTimes[i - 1];
            Assert.That(gap, Is.GreaterThanOrEqualTo(rate - 1e-4f), message + ": shots are never closer than FireRate seconds (gap " + gap + ")");
            Assert.That(gap, Is.LessThan(rate + 0.5f), message + ": shots are not spaced farther than FireRate plus a frame (gap " + gap + ")");
        }
    }

    [UnityTest] public IEnumerator InfernumFiresOnceEveryFireRateSecondsNotEveryFixedSecond()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var fastPool = NewBulletPool();
        var fast = NewInfernum(0.2f);
        yield return Frames(1);
        yield return FireFor(fast, fastPool, 1.3f);
        int fastShots = ShotTimes.Count;
        Assert.That(fastShots, Is.GreaterThanOrEqualTo(4), "S3: FireRate is seconds between shots, so 0.2 fires several times in 1.3 s (the old fixed 1 s fired once or twice)");
        AssertGaps(0.2f, "S3: FireRate 0.2");

        var slowPool = NewBulletPool();
        var slow = NewInfernum(0.6f);
        yield return Frames(1);
        yield return FireFor(slow, slowPool, 2f);
        Assert.That(ShotTimes.Count, Is.LessThan(fastShots), "S3: a larger FireRate fires less often than a smaller one");
        Assert.That(ShotTimes.Count, Is.GreaterThanOrEqualTo(3), "S3: FireRate 0.6 fires three or more times in 2 s");
        AssertGaps(0.6f, "S3: FireRate 0.6");
    }

    [UnityTest] public IEnumerator InfernumFirstShotIsImmediateThenWaitsForFireRate()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var pool = NewBulletPool();
        var infernum = NewInfernum(5f);
        yield return Frames(1);
        Assert.That((bool)Property(infernum, "CanShootNow"), Is.True, "S3: the first shot is allowed right away");

        Call(infernum, "PerformAttack");
        Assert.That(pool.childCount, Is.EqualTo(1), "S3: the first PerformAttack fires immediately");
        Assert.That((bool)Property(infernum, "CanShootNow"), Is.False, "S3: right after a shot CanShootNow honors the configured FireRate");

        Call(infernum, "PerformAttack");
        Assert.That(pool.childCount, Is.EqualTo(1), "S3: a second PerformAttack inside FireRate seconds does not fire");
    }
}
