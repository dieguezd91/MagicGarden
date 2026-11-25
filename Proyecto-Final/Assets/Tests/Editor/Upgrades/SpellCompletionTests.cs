using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// S2: Fire completes its hit feedback (damage number, impact particles, knockback, no hitstop/shake per tick) and
// Melee/Hammer spawn a swing VFX from existing particle prefabs. Reflection keeps the Assembly-CSharp boundary.
// Play Mode tests build isolated objects (no Game scene) and never use lambdas that capture locals before the domain reload.
public class SpellCompletionTests
{
    private const string SpellFolder = "Assets/Prefabs/Spells/";
    private const string FirePath = SpellFolder + "FireSpell.prefab";
    private const string DamageTextPath = "Assets/Prefabs/UI/FloatingTextDamage.prefab";
    private const string FireParticlesPath = "Assets/Prefabs/UI/FireImpactParticles.prefab";
    private const string ImpactParticlesPath = "Assets/Prefabs/UI/ImpactParticles.prefab";
    private const string DestructionParticlesPath = "Assets/Prefabs/Scenery/DestructionParticles.prefab";
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static GameObject Prefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, "S2/S5: existing prefab " + path);
        return prefab;
    }
    private static string PathOf(Object asset) => asset == null ? "<none>" : AssetDatabase.GetAssetPath(asset);
    private static Object Reference(Component owner, string field)
    {
        var property = new SerializedObject(owner).FindProperty(field);
        Assert.That(property, Is.Not.Null, "S2: " + owner.GetType().Name + " serializes " + field);
        return property.objectReferenceValue;
    }

    // ---- Edit Mode: asset wiring ----

    [Test] public void FireSpellPrefabReferencesTheDamageNumberAndImpactParticles()
    {
        var fire = Prefab(FirePath).GetComponent(TypeOf("FireSpell"));
        Assert.That(PathOf(Reference(fire, "floatingDamagePrefab")), Is.EqualTo(DamageTextPath), "S2: Fire shows the existing floating damage number");
        var particles = PathOf(Reference(fire, "impactParticlesPrefab"));
        Assert.That(particles, Is.EqualTo(FireParticlesPath).Or.EqualTo(ImpactParticlesPath), "S2/S5: Fire spawns existing impact particles, no new art");
    }

    [TestCase("MeleeSpell", "BasicMeleeSpell", ImpactParticlesPath)]
    [TestCase("HammerSpell", "HammerSpell", DestructionParticlesPath)]
    public void MeleeAndHammerReferenceAnExistingSlashEffect(string prefabName, string script, string expectedEffect)
    {
        var spell = Prefab(SpellFolder + prefabName + ".prefab").GetComponent(TypeOf(script));
        Assert.That(spell, Is.Not.Null, "S2: " + prefabName + " carries " + script);
        var effect = Reference(spell, "slashEffectPrefab");
        Assert.That(effect, Is.Not.Null, "S2: " + prefabName + " has a swing VFX");
        Assert.That(PathOf(effect), Is.EqualTo(expectedEffect), "S2/S5: " + prefabName + " reuses an existing particle prefab");
        Assert.That(System.IO.File.Exists(expectedEffect), Is.True, "S2/S5: the referenced prefab exists on disk");
    }

    [TestCase("AllowsHitStop")]
    [TestCase("AllowsImpactShake")]
    public void FireNeverRequestsHitstopOrCameraShakePerTick(string member)
    {
        var type = TypeOf("FireSpell");
        var fire = Prefab(FirePath).GetComponent(type);
        var property = type.GetProperty(member, AnyInstance | BindingFlags.DeclaredOnly);
        Assert.That(property, Is.Not.Null, "S2: FireSpell overrides " + member + " (it ticks every 0.05 s)");
        Assert.That(property.GetValue(fire), Is.False, "S2: FireSpell." + member + " is false");
    }

    // ---- Play Mode: isolated objects ----

    // Whatever scene the Editor had open also starts playing: hide it so no real Player/enemy/particle interferes.
    private static void Isolate()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
    }

    private static Component Spawn(string prefabPath, string script)
    {
        var instance = Object.Instantiate(Prefab(prefabPath), new Vector3(0, 100, 0), Quaternion.identity);
        return instance.GetComponent(TypeOf(script));
    }

    // A flame projectile aimed at +X, and an enemy 5 units to its right with life and knockback.
    // S1: the Fire prefab ships with impact particles and knockback off, so the "enabled" tests switch them on
    // (the code paths must keep working); feedbackOn=false keeps the prefab defaults.
    private static void BuildFireHitFixture(out Component flame, out GameObject enemy, bool feedbackOn = true)
    {
        var type = TypeOf("FireSpell");
        type.GetField("lastImpactParticlesTime", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, float.MinValue);
        type.GetField("lastHitSoundTime", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, float.MinValue);

        enemy = new GameObject("SpellCompletionEnemy");
        enemy.tag = "Enemy";
        enemy.transform.position = new Vector3(5, 100, 0);
        enemy.AddComponent<BoxCollider2D>();
        enemy.AddComponent<Rigidbody2D>().gravityScale = 0;
        enemy.AddComponent(TypeOf("LifeController"));
        enemy.AddComponent(TypeOf("KnockbackReceiver"));

        flame = Spawn(FirePath, "FireSpell");
        type.GetField("isProjectile").SetValue(flame, true);
        if (feedbackOn)
        {
            type.GetField("spawnImpactParticles", AnyInstance).SetValue(flame, true);
            type.GetField("hitKnockback", AnyInstance).SetValue(flame, 3f);
        }
        Call(flame, "Cast", Vector2.right, new Vector3(0, 100, 0));
    }

    private static void FlameHits(Component flame, GameObject enemy) =>
        flame.GetType().GetMethod("OnTriggerEnter2D", AnyInstance).Invoke(flame, new object[] { enemy.GetComponent<Collider2D>() });

    private static int FireParticleBursts() =>
        Object.FindObjectsOfType<ParticleSystem>().Count(p => p.gameObject.name.StartsWith("FireImpactParticles") || p.gameObject.name.StartsWith("ImpactParticles"));

    [UnityTest] public IEnumerator AFireHitShowsADamageNumberSpawnsParticlesAndKnocksTheEnemyBack()
    {
        yield return new EnterPlayMode();
        Isolate();
        BuildFireHitFixture(out var flame, out var enemy);
        yield return null; yield return null; // LifeController.Start and KnockbackReceiver.Start run
        var life = enemy.GetComponent(TypeOf("LifeController"));
        float before = (float)Field(life, "currentHealth");
        var rb = enemy.GetComponent<Rigidbody2D>();
        Assert.That(rb.velocity, Is.EqualTo(Vector2.zero));
        Assert.That(FireParticleBursts(), Is.Zero, "S2: no impact particles before the hit");

        FlameHits(flame, enemy);

        Assert.That((float)Field(life, "currentHealth"), Is.LessThan(before), "S2: the hit still deals damage");
        Assert.That(enemy.GetComponentInChildren(TypeOf("FloatingDamageText")), Is.Not.Null, "S2: the hit shows a floating damage number on the enemy");
        Assert.That(FireParticleBursts(), Is.EqualTo(1), "S2: the hit spawns impact particles");
        Assert.That(rb.velocity.x, Is.GreaterThan(0f), "S2: the hit pushes the enemy along the flame direction");
        Assert.That(rb.velocity.magnitude, Is.LessThan(8f), "S2: the per-tick knockback is smaller than a regular spell hit");
    }

    [UnityTest] public IEnumerator FireTicksShareOneImpactParticleBurstAndKeepOneDamageNumber()
    {
        yield return new EnterPlayMode();
        Isolate();
        BuildFireHitFixture(out var flame, out var enemy);
        yield return null; yield return null;

        FlameHits(flame, enemy);
        FlameHits(flame, enemy); // the next 0.05 s tick, inside the throttle interval

        Assert.That(FireParticleBursts(), Is.EqualTo(1), "S2/S6: ticks inside the min interval do not stack impact particles");
        Assert.That(enemy.GetComponentsInChildren(TypeOf("FloatingDamageText")).Length, Is.EqualTo(1), "S2: ticks accumulate into a single damage number per enemy");
    }

    [UnityTest] public IEnumerator HammerSlamPlaysItsExistingParticleEffect()
    {
        yield return new EnterPlayMode();
        Isolate();
        var hammer = Spawn(SpellFolder + "HammerSpell.prefab", "HammerSpell");
        Call(hammer, "Cast", Vector2.right, new Vector3(0, 100, 0));
        yield return null;

        var effect = Object.FindObjectsOfType<ParticleSystem>().FirstOrDefault(p => p.gameObject.name.StartsWith("DestructionParticles"));
        Assert.That(effect, Is.Not.Null, "S2: the slam spawns its swing VFX");
        Assert.That(effect.isPlaying, Is.True, "S2: the slam VFX is actually playing (the prefab has Play On Awake off)");
    }

    [UnityTest] public IEnumerator MeleeSwingSpawnsItsExistingParticleEffect()
    {
        yield return new EnterPlayMode();
        Isolate();
        var melee = Spawn(SpellFolder + "MeleeSpell.prefab", "BasicMeleeSpell");
        Call(melee, "Cast", Vector2.right, new Vector3(0, 100, 0));
        yield return null;

        var effect = Object.FindObjectsOfType<ParticleSystem>().FirstOrDefault(p => p.gameObject.name.StartsWith("ImpactParticles"));
        Assert.That(effect, Is.Not.Null, "S2: the swing spawns its slash VFX");
        Assert.That(effect.isPlaying, Is.True, "S2: the slash VFX is playing");
    }
}
