using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Fire readability. S1: no knockback / impact particles by default and flame particles are not cut.
// S2: flames draw below the player. S3: the visible stream starts at the fire point and follows it.
// S4: the first particle at the hand has the hand's size and the size grows exponentially with the particle's age.
// S5: the new values are serialized and disableable.
// Reflection keeps the Assembly-CSharp boundary; Play Mode tests build isolated objects (no Game scene) and use no
// lambdas that capture locals in iterators (they would not survive the domain reload).
public class FireStreamTests
{
    private const string FirePath = "Assets/Prefabs/Spells/FireSpell.prefab";
    private const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static GameObject Prefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, "S1-S4: existing prefab " + path);
        return prefab;
    }
    private static SerializedProperty Serialized(Component owner, string field)
    {
        var property = new SerializedObject(owner).FindProperty(field);
        Assert.That(property, Is.Not.Null, "S5: " + owner.GetType().Name + " serializes " + field);
        return property;
    }
    private static void SetField(object owner, string name, object value) =>
        owner.GetType().GetField(name, AnyInstance).SetValue(owner, value);

    // ---- Edit Mode: asset wiring ----

    [Test] public void FirePrefabHasNoKnockbackAndNoImpactParticles()
    {
        var fire = Prefab(FirePath).GetComponent(TypeOf("FireSpell"));
        Assert.That(Serialized(fire, "hitKnockback").floatValue, Is.Zero, "S1: Fire does not knock enemies back");
        Assert.That(Serialized(fire, "spawnImpactParticles").boolValue, Is.False, "S1: Fire spawns no impact particles");
    }

    [Test] public void StreamParticlesLiveAsLongAsTheFlameRangeSoTheyAreNotCutShort()
    {
        var prefab = Prefab(FirePath);
        var fire = prefab.GetComponent(TypeOf("FireSpell"));
        float flameLife = Serialized(fire, "projectileLifeTime").floatValue;
        var lifetime = prefab.GetComponent<ParticleSystem>().main.startLifetime;
        Assert.That(lifetime.constantMax, Is.GreaterThan(0f).And.LessThanOrEqualTo(flameLife),
            "S1: a particle never outlives the flame range (it fades by itself instead of being cut or leaving a trailing cloud)");
        Assert.That(lifetime.constantMax, Is.EqualTo(flameLife).Within(0.001f),
            "S4: the particle life matches the flame range so the drawn stream reaches as far as the damage");
        Assert.That(prefab.GetComponent<ParticleSystem>().main.startSpeed.constantMax, Is.EqualTo(Serialized(fire, "projectileSpeed").floatValue).Within(0.001f),
            "S4: particles travel at the projectile speed");
    }

    [Test] public void FlameRendererSortsBelowThePlayerBody()
    {
        var fire = Prefab(FirePath);
        var flameRenderer = fire.GetComponent<ParticleSystemRenderer>();
        var body = Prefab(PlayerPath).GetComponent<SpriteRenderer>();
        Assert.That(body, Is.Not.Null, "S2: the Player root carries the body sprite");

        int flameLayer = SortingLayer.GetLayerValueFromID(flameRenderer.sortingLayerID);
        int bodyLayer = SortingLayer.GetLayerValueFromID(body.sortingLayerID);
        bool below = flameLayer < bodyLayer || (flameLayer == bodyLayer && flameRenderer.sortingOrder < body.sortingOrder);
        Assert.That(below, Is.True, "S2: flame (layer " + flameLayer + ", order " + flameRenderer.sortingOrder +
            ") draws below the player body (layer " + bodyLayer + ", order " + body.sortingOrder + ")");

        var component = fire.GetComponent(TypeOf("FireSpell"));
        Assert.That(Serialized(component, "applyFlameSorting").boolValue, Is.True, "S2/S5: the sorting override is on and disableable");
        Assert.That(Serialized(component, "flameSortingOrder").intValue, Is.EqualTo(flameRenderer.sortingOrder),
            "S2/S5: the serialized flame sorting order matches the renderer");
    }

    [Test] public void StreamLookValuesAreSerializedAndDisableable()
    {
        var fire = Prefab(FirePath).GetComponent(TypeOf("FireSpell"));
        Assert.That(Serialized(fire, "streamStartsAtHand").boolValue, Is.False, "S3/S5: the stream starts at the fire point by default; starting at the hand is opt-in");
        Assert.That(Serialized(fire, "startSizeMatchesHand").boolValue, Is.True, "S4/S5: hand-sized first particle is on and can be disabled");
        Assert.That(Serialized(fire, "handWorldSize").floatValue, Is.GreaterThan(0f), "S4/S5: editable hand world size");
        Assert.That(Serialized(fire, "exponentialGrowth").boolValue, Is.True, "S4/S5: exponential growth is on and can be disabled");
        Assert.That(Serialized(fire, "endSizeMultiplier").floatValue, Is.GreaterThan(1f), "S4/S5: editable end size multiplier (k = ln of it)");
        Assert.That(Serialized(fire, "streamMatchesProjectile").boolValue, Is.True, "S4/S5: stream speed and life match the projectile, disableable");

        Assert.That(Serialized(fire, "growWhileTraveling").boolValue, Is.True, "S5: hit area growth is on and can be disabled");
        float start = Serialized(fire, "flameStartScale").floatValue;
        float end = Serialized(fire, "flameEndScale").floatValue;
        Assert.That(start, Is.GreaterThan(0f).And.LessThan(end), "S5: the hit area starts smaller than it ends");
        Assert.That(Serialized(fire, "flameGrowthCurve").animationCurveValue.length, Is.GreaterThan(1), "S5: editable hit area growth curve");
    }

    [Test] public void ParticleSystemRendersWhatTheStreamSettingsDescribe()
    {
        var prefab = Prefab(FirePath);
        var ps = prefab.GetComponent<ParticleSystem>();
        var psRenderer = prefab.GetComponent<ParticleSystemRenderer>();

        Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World),
            "S4: world space, so every particle keeps the size and place it gets from its own age");
        Assert.That(ps.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Shape),
            "S4: transform scale never resizes particles already emitted (it only scales the emission shape)");
        Assert.That(psRenderer.minParticleSize, Is.Zero,
            "S4: no minimum on-screen particle size: the old 0.05 floor drew every small particle at the same size");
        Assert.That(psRenderer.maxParticleSize, Is.GreaterThanOrEqualTo(0.5f),
            "S4: the old 0.15 cap would clamp the grown particles to one size");
        Assert.That(ps.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.Cone), "S3: emitted from a point at the hand");
        Assert.That(ps.shape.radius, Is.LessThan(0.1f), "S3/S4: the first particle sits at the hand, not on a wide line");
        Assert.That(ps.emission.rateOverTime.constant, Is.GreaterThan(0f), "S4: the stream emits without the old trail animation");
        Assert.That(prefab.GetComponent<Animation>().enabled, Is.False,
            "S1/S4: the old Infernum Trail animation (fades alpha after 1.5 s and drives the emission rate) no longer fights the stream");
        Assert.That(ps.sizeOverLifetime.enabled, Is.True, "S4: size is driven by age");

        var curve = ps.sizeOverLifetime.size.curve;
        float endMultiplier = new SerializedObject(prefab.GetComponent(TypeOf("FireSpell"))).FindProperty("endSizeMultiplier").floatValue;
        Assert.That(curve.Evaluate(0f), Is.EqualTo(1f).Within(0.01f), "S4: the saved Size over Lifetime curve starts at 1 (the hand size)");
        Assert.That(curve.Evaluate(1f), Is.EqualTo(endMultiplier).Within(0.02f), "S4: ... and reaches the end multiplier at t = 1");
        Assert.That(curve.Evaluate(0.5f), Is.EqualTo(Mathf.Sqrt(endMultiplier)).Within(0.05f), "S4: ... and is e^(k t): half way it is the square root of the end multiplier");
    }

    // handWorldSize 0.34 = mean of the idle hand's opaque pixels (0.31 x 0.38 u), measured offline: the runtime sprite
    // bounds are the full 1.5 u cell, so they cannot be used to check it here.

    [Test] public void TheExponentialCurveStartsAtOneReachesTheMultiplierAndAccelerates()
    {
        var method = TypeOf("FireSpell").GetMethod("ExponentialGrowthCurve", BindingFlags.Static | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, "S4/S5: the growth curve builder is public");
        var curve = (AnimationCurve)method.Invoke(null, new object[] { 6f, 8 });

        Assert.That(curve.Evaluate(0f), Is.EqualTo(1f).Within(0.001f), "S4: normalized to start at 1 (the hand size)");
        Assert.That(curve.Evaluate(1f), Is.EqualTo(6f).Within(0.001f), "S4: reaches the end multiplier at t = 1");
        Assert.That(curve.Evaluate(0.5f), Is.EqualTo(Mathf.Sqrt(6f)).Within(0.05f), "S4: e^(k t) with k = ln(6): half way is sqrt(6)");

        float firstHalf = curve.Evaluate(0.5f) - curve.Evaluate(0f);
        float secondHalf = curve.Evaluate(1f) - curve.Evaluate(0.5f);
        Assert.That(secondHalf, Is.GreaterThan(firstHalf * 1.5f), "S4: exponential, not linear: more size is gained in the second half of the life");
        float previous = curve.Evaluate(0f);
        for (int i = 1; i <= 20; i++)
        {
            float value = curve.Evaluate(i / 20f);
            Assert.That(value, Is.GreaterThan(previous), "S4: size only grows with age (step " + i + ")");
            previous = value;
        }
    }

    // ---- Play Mode: isolated objects ----

    // Whatever scene the Editor had open also starts playing: hide it so no real Player/enemy/particle interferes.
    private static void Isolate()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
    }

    private static Component SpawnFire(Vector3 at)
    {
        var instance = Object.Instantiate(Prefab(FirePath), at, Quaternion.identity);
        return instance.GetComponent(TypeOf("FireSpell"));
    }

    // A flame projectile (what the stream spawns), aimed at +X.
    private static Component CastProjectile(Vector3 at)
    {
        var flame = SpawnFire(at);
        TypeOf("FireSpell").GetField("isProjectile").SetValue(flame, true);
        Call(flame, "Cast", Vector2.right, at);
        return flame;
    }

    private static Component[] Flames()
    {
        var type = TypeOf("FireSpell");
        var field = type.GetField("isProjectile");
        return Object.FindObjectsOfType(type).Cast<Component>().Where(f => (bool)field.GetValue(f)).ToArray();
    }

    private static float PrefabScaleX() => Prefab(FirePath).transform.localScale.x;

    // The stream emitter is the one FireSpell that is not a projectile.
    private static Component Emitter()
    {
        var type = TypeOf("FireSpell");
        var field = type.GetField("isProjectile");
        return Object.FindObjectsOfType(type).Cast<Component>().FirstOrDefault(f => !(bool)field.GetValue(f));
    }

    // A real Player.prefab instance without the scripts that need the Game scene (GameFlowController, input, UI, health...).
    // The instance is built under an inactive holder so no Awake runs on a script that will be removed, then activated.
    // Removed: every MonoBehaviour except PlayerSpellController (kept DISABLED so its Start, which needs
    // GameFlowController.Instance, never runs; production code only reads its FirePoint / hand through it).
    // Rigidbody2D simulation is off so the player does not fall; the Animators keep driving the hand sprite.
    private static GameObject SpawnPlayer(Vector3 at)
    {
        var holder = new GameObject("FireStreamPlayerHolder");
        holder.SetActive(false);
        var player = Object.Instantiate(Prefab(PlayerPath), at, Quaternion.identity, holder.transform);

        var spellControllerType = TypeOf("PlayerSpellController");
        for (int pass = 0; pass < 10; pass++)
        {
            var behaviours = player.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null).ToArray();
            var required = new System.Collections.Generic.HashSet<System.Type>();
            foreach (var behaviour in behaviours)
                foreach (RequireComponent attribute in behaviour.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    foreach (var type in new[] { attribute.m_Type0, attribute.m_Type1, attribute.m_Type2 })
                        if (type != null) required.Add(type);

            bool removed = false;
            foreach (var behaviour in behaviours)
            {
                if (spellControllerType.IsInstanceOfType(behaviour)) continue;
                if (required.Any(type => type.IsInstanceOfType(behaviour))) continue; // removed on a later pass, after its dependents
                Object.DestroyImmediate(behaviour);
                removed = true;
            }
            if (!removed) break;
        }
        ((Behaviour)player.GetComponent(spellControllerType)).enabled = false;
        foreach (var body in player.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;

        player.transform.SetParent(null);
        Object.Destroy(holder);
        return player;
    }

    [UnityTest] public IEnumerator EveryFlameSpawnsAtTheFirePointAndFollowsItWhileTheStreamLasts()
    {
        yield return new EnterPlayMode();
        Isolate();
        var camera = new GameObject("FireStreamCamera") { tag = "MainCamera" };
        camera.transform.position = new Vector3(0, 100, -10);
        camera.AddComponent<Camera>().orthographic = true;

        var firePoint = new GameObject("FireStreamFirePoint").transform;
        firePoint.position = new Vector3(3, 100, 0);
        var controller = SpawnFire(new Vector3(0, 100, 0)); // the stream controller sits away from the fire point
        Call(controller, "SetFirePoint", firePoint);
        Call(controller, "Cast", Vector2.right, new Vector3(0, 100, 0));

        var first = Flames();
        Assert.That(first.Length, Is.EqualTo(1), "S3: the stream spawns its first flame as soon as it is cast");
        Assert.That(Vector3.Distance(first[0].transform.position, firePoint.position), Is.LessThan(0.01f),
            "S3: the first flame spawns at the fire point, not at the controller / player pivot");
        Assert.That(Vector3.Distance(controller.transform.position, firePoint.position), Is.LessThan(0.01f),
            "S3: the visible stream emitter is at the fire point too");

        var flameRenderer = first[0].GetComponent<ParticleSystemRenderer>();
        var body = Prefab(PlayerPath).GetComponent<SpriteRenderer>();
        Assert.That(SortingLayer.GetLayerValueFromID(flameRenderer.sortingLayerID), Is.LessThanOrEqualTo(SortingLayer.GetLayerValueFromID(body.sortingLayerID)), "S2: runtime flame is not on a higher layer than the player");
        Assert.That(flameRenderer.sortingOrder, Is.LessThan(body.sortingOrder), "S2: runtime flame draws below the player body on the same layer");
        Assert.That(controller.GetComponent<ParticleSystemRenderer>().sortingOrder, Is.LessThan(body.sortingOrder), "S2: the stream emitter draws below the player body too");

        // The hand moves: the next flames and the emitter must start from the new fire point.
        firePoint.position = new Vector3(-4, 100, 0);
        float t0 = Time.time;
        Component next = null;
        while (next == null && Time.time - t0 < 1.5f)
        {
            yield return null;
            foreach (var flame in Flames())
                if (flame != first[0] && Vector3.Distance(flame.transform.position, new Vector3(3, 100, 0)) > 3f && next == null) next = flame;
        }
        Assert.That(next, Is.Not.Null, "S3: the stream keeps spawning flames");
        Assert.That(Vector3.Distance(next.transform.position, firePoint.position), Is.LessThan(2f),
            "S3: later flames spawn at the fire point's current position (it follows the hand)");
        Assert.That(Vector3.Distance(controller.transform.position, firePoint.position), Is.LessThan(0.01f),
            "S3: the emitter follows the fire point every frame");
    }

    [UnityTest] public IEnumerator TheStreamStartsAtTheFirePointOfARealPlayerAndFollowsIt()
    {
        yield return new EnterPlayMode();
        Isolate();
        var player = SpawnPlayer(new Vector3(0, 100, 0));
        yield return null;

        var spellController = player.GetComponent(TypeOf("PlayerSpellController"));
        var firePoint = (Transform)Field(spellController, "firePoint");
        Assert.That(firePoint, Is.Not.Null, "S3: the player has a fire point");

        // Production path: the stream finds the PlayerSpellController itself (no SetFirePoint), default settings.
        var fire = SpawnFire(new Vector3(50, 100, 0));
        Call(fire, "SetAimDirection", Vector2.right);
        Call(fire, "Cast", Vector2.right, fire.transform.position);
        float t0 = Time.time;
        while (Time.time - t0 < 0.3f) yield return null;

        var emitter = Emitter();
        Assert.That(emitter, Is.Not.Null, "S3: the stream emitter exists");
        Assert.That(Vector3.Distance(emitter.transform.position, firePoint.position), Is.LessThan(0.05f),
            "S3: the emitter sits at the fire point (emitter " + emitter.transform.position + ", fire point " + firePoint.position + ")");
        foreach (var flame in Flames())
            Assert.That(Vector3.Distance(flame.transform.position, firePoint.position), Is.LessThan(8f), "S3: damage flames start at the fire point and travel from it");

        // Particles are born at the fire point: the youngest one is where speed * age puts it, in front of the fire point.
        var ps = emitter.GetComponent<ParticleSystem>();
        var particles = new ParticleSystem.Particle[ps.main.maxParticles];
        int count = ps.GetParticles(particles);
        Assert.That(count, Is.GreaterThan(2), "S3: the stream emits particles");
        float speed = (float)Field(emitter, "projectileSpeed");
        int youngest = 0;
        for (int i = 1; i < count; i++) if (particles[i].remainingLifetime > particles[youngest].remainingLifetime) youngest = i;
        float age = particles[youngest].startLifetime - particles[youngest].remainingLifetime;
        Assert.That(Vector3.Distance(particles[youngest].position, firePoint.position), Is.LessThan(speed * age + 0.35f),
            "S3: the youngest particle (age " + age.ToString("0.###") + " s) is next to the fire point, not at the pivot");

        // The fire point moves with the player: the emitter keeps starting at it.
        Vector3 before = firePoint.position;
        player.transform.position += new Vector3(3, 0, 0);
        yield return null; yield return null;
        Assert.That(firePoint.position.x - before.x, Is.GreaterThan(2.5f), "S3: the fire point moved with the player");
        Assert.That(Vector3.Distance(emitter.transform.position, firePoint.position), Is.LessThan(0.05f), "S3: the stream follows the fire point while it lasts");
    }


    [UnityTest] public IEnumerator TheFirstParticleHasTheHandSizeAndOlderOnesGrowExponentially()
    {
        yield return new EnterPlayMode();
        Isolate();
        var origin = new Vector3(0, 100, 0);
        var firePoint = new GameObject("FireStreamFirePoint").transform;
        firePoint.position = origin;
        var controller = SpawnFire(origin);
        Call(controller, "SetFirePoint", firePoint);
        Call(controller, "SetAimDirection", Vector2.right);
        Call(controller, "Cast", Vector2.right, origin);

        float handSize = (float)Field(controller, "handWorldSize");
        float endMultiplier = (float)Field(controller, "endSizeMultiplier");
        float speed = (float)Field(controller, "projectileSpeed");
        float t0 = Time.time;
        while (Time.time - t0 < 0.4f) yield return null;

        var ps = controller.GetComponent<ParticleSystem>();
        Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), "S4: world space: each particle keeps its own size and place");
        Assert.That(ps.sizeOverLifetime.enabled, Is.True, "S4: size follows the particle's age");

        var particles = new ParticleSystem.Particle[ps.main.maxParticles];
        int count = ps.GetParticles(particles);
        Assert.That(count, Is.GreaterThanOrEqualTo(4), "S4: after 0.4 s the stream has several particles of different ages");

        // Order the particles by age (youngest first) without lambdas.
        var ages = new float[count];
        var sizes = new float[count];
        var order = new int[count];
        for (int i = 0; i < count; i++)
        {
            ages[i] = particles[i].startLifetime - particles[i].remainingLifetime;
            sizes[i] = particles[i].GetCurrentSize(ps);
            order[i] = i;
        }
        System.Array.Sort(ages.Clone() as float[], order);
        int youngest = order[0], oldest = order[count - 1];

        Assert.That(sizes[youngest], Is.EqualTo(handSize).Within(handSize * 0.25f),
            "S4: the first particle (age " + ages[youngest].ToString("0.###") + " s) has the hand's world size " + handSize + ", is " + sizes[youngest]);
        float expected = handSize * Mathf.Pow(endMultiplier, ages[youngest] / particles[youngest].startLifetime);
        Assert.That(sizes[youngest], Is.EqualTo(expected).Within(expected * 0.1f), "S4: ... and its size is hand size * multiplier^(age / life)");
        Assert.That(Vector3.Distance(particles[youngest].position, origin), Is.LessThan(speed * ages[youngest] + 0.35f),
            "S4: ... and it sits at the origin (the hand), moved only by its age");
        Assert.That(sizes[oldest], Is.GreaterThan(sizes[youngest] * 1.5f),
            "S4: older particles (age " + ages[oldest].ToString("0.###") + " s) are visibly larger: " + sizes[oldest] + " vs " + sizes[youngest]);

        for (int a = 0; a < count; a++)
            for (int b = 0; b < count; b++)
                if (ages[a] + 0.01f < ages[b])
                    Assert.That(sizes[b], Is.GreaterThanOrEqualTo(sizes[a] - 0.001f), "S4: a particle is never smaller than a younger one");

        // Exponential, not linear: the rendered curve gains more size in the second half of the life than in the first.
        var curve = ps.sizeOverLifetime.size.curve;
        float firstHalf = curve.Evaluate(0.5f) - curve.Evaluate(0f);
        float secondHalf = curve.Evaluate(1f) - curve.Evaluate(0.5f);
        Assert.That(curve.Evaluate(0f), Is.EqualTo(1f).Within(0.01f), "S4: the curve starts at 1");
        Assert.That(curve.Evaluate(1f), Is.EqualTo(endMultiplier).Within(0.02f), "S4: the curve reaches the end multiplier");
        Assert.That(secondHalf, Is.GreaterThan(firstHalf * 1.5f), "S4: growth accelerates: " + secondHalf + " gained in the second half vs " + firstHalf + " in the first");
    }

    [UnityTest] public IEnumerator TurningTheHandSizeAndGrowthOffKeepsTheParticleSystemValues()
    {
        yield return new EnterPlayMode();
        Isolate();
        var origin = new Vector3(0, 100, 0);
        var controller = SpawnFire(origin);
        SetField(controller, "startSizeMatchesHand", false);
        SetField(controller, "exponentialGrowth", false);
        SetField(controller, "handWorldSize", 3f);
        var ps = controller.GetComponent<ParticleSystem>();
        float savedSize = ps.main.startSize.constantMax;
        float savedEnd = ps.sizeOverLifetime.size.curve.Evaluate(1f);
        Call(controller, "SetAimDirection", Vector2.right);
        Call(controller, "Cast", Vector2.right, origin);
        yield return null;

        Assert.That(ps.main.startSize.constantMax, Is.EqualTo(savedSize), "S5: with 'start size matches hand' off the saved start size is kept");
        Assert.That(ps.sizeOverLifetime.size.curve.Evaluate(1f), Is.EqualTo(savedEnd), "S5: with exponential growth off the saved curve is kept");
    }

    [UnityTest] public IEnumerator AFlameHitAreaStartsSmallAndGrowsAsItTravels()
    {
        yield return new EnterPlayMode();
        Isolate();
        var flame = CastProjectile(new Vector3(0, 100, 0));
        float start = (float)Field(flame, "flameStartScale");
        float end = (float)Field(flame, "flameEndScale");
        float prefabScale = PrefabScaleX();

        float atSpawn = flame.transform.localScale.x / prefabScale;
        Assert.That(atSpawn, Is.EqualTo(start).Within(0.01f), "S4/S5: the damage projectile starts at flameStartScale");

        float t0 = Time.time;
        while (Time.time - t0 < 0.3f) yield return null;
        float mid = flame.transform.localScale.x / prefabScale;
        while (Time.time - t0 < 0.6f) yield return null;
        float late = flame.transform.localScale.x / prefabScale;

        Assert.That(mid, Is.GreaterThan(atSpawn), "S4/S5: the hit area grows while it travels");
        Assert.That(late, Is.GreaterThan(mid), "S4/S5: the hit area keeps growing towards the end of its travel");
        Assert.That(late, Is.GreaterThan(atSpawn * 2f).And.LessThanOrEqualTo(end + 0.01f), "S4/S5: near the end the hit area is much larger than at spawn and never above flameEndScale");
        Assert.That(flame.GetComponent<ParticleSystem>().particleCount, Is.Zero, "S4: the damage projectile draws nothing: the visible flame is the stream emitter's particles");
    }

    [UnityTest] public IEnumerator TurningGrowthOffKeepsThePrefabScale()
    {
        yield return new EnterPlayMode();
        Isolate();
        var flame = SpawnFire(new Vector3(0, 100, 0));
        TypeOf("FireSpell").GetField("isProjectile").SetValue(flame, true);
        SetField(flame, "growWhileTraveling", false);
        Call(flame, "Cast", Vector2.right, new Vector3(0, 100, 0));
        float t0 = Time.time;
        while (Time.time - t0 < 0.3f) yield return null;

        Assert.That(flame.transform.localScale.x, Is.EqualTo(PrefabScaleX()).Within(0.0001f), "S5: with growth off the flame keeps the prefab scale");
    }

    [UnityTest] public IEnumerator DamageEndsWithTheFlameLifetime()
    {
        yield return new EnterPlayMode();
        Isolate();
        var type = TypeOf("FireSpell");
        type.GetField("lastHitSoundTime", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, float.MinValue);

        var enemy = new GameObject("FireStreamEnemy");
        enemy.tag = "Enemy";
        enemy.transform.position = new Vector3(5, 100, 0);
        enemy.AddComponent<BoxCollider2D>();
        enemy.AddComponent<Rigidbody2D>().gravityScale = 0;
        enemy.AddComponent(TypeOf("LifeController"));
        var life = enemy.GetComponent(TypeOf("LifeController"));

        var flame = SpawnFire(new Vector3(0, 100, 0));
        type.GetField("isProjectile").SetValue(flame, true);
        SetField(flame, "projectileLifeTime", 0.3f);
        Call(flame, "Cast", Vector2.right, new Vector3(0, 100, 0));
        yield return null; yield return null; // LifeController.Start runs

        var collider = enemy.GetComponent<Collider2D>();
        float full = (float)Field(life, "currentHealth");
        type.GetMethod("OnTriggerEnter2D", AnyInstance).Invoke(flame, new object[] { collider });
        float afterFirstHit = (float)Field(life, "currentHealth");
        Assert.That(afterFirstHit, Is.LessThan(full), "S1: a living flame still damages");

        float t0 = Time.time;
        while (flame != null && flame.GetComponent<Collider2D>().enabled && Time.time - t0 < 1f) yield return null;
        Assert.That(flame == null || !flame.GetComponent<Collider2D>().enabled, Is.True, "S1: the damage collider ends with the flame lifetime");
        if (flame != null)
        {
            float afterEnd = (float)Field(life, "currentHealth");
            type.GetMethod("OnTriggerEnter2D", AnyInstance).Invoke(flame, new object[] { collider });
            Assert.That((float)Field(life, "currentHealth"), Is.EqualTo(afterEnd), "S1: no more damage after the flame lifetime");
        }

        float t1 = Time.time;
        while (flame != null && Time.time - t1 < 2f) yield return null;
        Assert.That(flame == null, Is.True, "S1: the damage projectile is destroyed after its lifetime (it draws nothing, so no visible particle is cut)");
    }

    [UnityTest] public IEnumerator TheStreamParticlesAreNotCutWhenTheStreamEnds()
    {
        yield return new EnterPlayMode();
        Isolate();
        var origin = new Vector3(0, 100, 0);
        var controller = SpawnFire(origin);
        SetField(controller, "duration", 0.3f);
        SetField(controller, "fireRate", 0.1f);
        Call(controller, "SetAimDirection", Vector2.right);
        Call(controller, "Cast", Vector2.right, origin);

        float t0 = Time.time;
        while (Time.time - t0 < 0.55f) yield return null; // the 0.3 s stream is over; its particles live 0.85 s

        Assert.That(controller != null, Is.True, "S1: the emitter survives the end of the stream so its particles are not cut");
        var ps = controller.GetComponent<ParticleSystem>();
        Assert.That(ps.IsAlive(true), Is.True, "S1: the flame particles are still alive after the stream ended");
        Assert.That(ps.isEmitting, Is.False, "S1: the stream stopped emitting new particles");
        Assert.That(ps.particleCount, Is.GreaterThan(0), "S1: particles are still being drawn");

        float t1 = Time.time;
        while (controller != null && Time.time - t1 < 3f) yield return null;
        Assert.That(controller == null, Is.True, "S1: the emitter is destroyed once its particles finish");
    }

    // ---- Play Mode: capture of the rendered stream (visual evidence for the parent / the human) ----

    private static void CaptureWorld(Camera camera, string name, int width, int height)
    {
        string output = UpgradeUICompactLayoutTests.Output;
        System.IO.Directory.CreateDirectory(output);
        var rt = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            System.IO.File.WriteAllBytes(output + name + "-" + width + "x" + height + ".png", texture.EncodeToPNG());
            Debug.Log("FIRE_STREAM_CAPTURE " + name + "-" + width + "x" + height);
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = previous; rt.Release();
            Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
        }
    }

    private static IEnumerator CaptureDirection(Camera camera, string label, Vector2 aim)
    {
        var player = SpawnPlayer(new Vector3(0, 100, 0));
        yield return null; yield return null;

        var fire = SpawnFire(new Vector3(50, 100, 0));
        Call(fire, "SetAimDirection", aim);
        Call(fire, "Cast", aim, fire.transform.position);

        float t0 = Time.time;
        float[] marks = { 0.15f, 0.4f, 0.8f };
        for (int i = 0; i < marks.Length; i++)
        {
            while (Time.time - t0 < marks[i]) yield return null;
            CaptureWorld(camera, "fire-stream-" + label + "-" + marks[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), 960, 540);
        }

        Object.Destroy(player);
        foreach (var spell in Object.FindObjectsOfType(TypeOf("FireSpell"))) Object.Destroy(((Component)spell).gameObject);
        yield return null; yield return null;
    }

    // Player components that need the Game scene (GameFlowController, input, UI, health, abilities...) are removed in SpawnPlayer;
    // PlayerSpellController stays but disabled (its Start needs GameFlowController.Instance). Output: Library/MagicGardenScrollbarValidation/native-*/.
    [UnityTest] public IEnumerator CaptureFireStreamFrames()
    {
        yield return new EnterPlayMode();
        Isolate();
        var cameraObject = new GameObject("FireStreamCaptureCamera") { tag = "MainCamera" };
        cameraObject.transform.position = new Vector3(2.5f, 99.2f, -10f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 3f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.18f, 0.22f, 0.18f);

        yield return CaptureDirection(camera, "right", Vector2.right);
        yield return CaptureDirection(camera, "down-right", new Vector2(1f, -0.7f));

        var files = System.IO.Directory.GetFiles(UpgradeUICompactLayoutTests.Output, "fire-stream-*-960x540.png");
        Assert.That(files.Length, Is.GreaterThanOrEqualTo(6), "S3/S4: 2 directions x 3 moments were captured to " + UpgradeUICompactLayoutTests.Output);
    }
}
