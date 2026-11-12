using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicGarden;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Editor test assembly deliberately enters real Play Mode. Reflection preserves the Assembly-CSharp boundary.
public class UpgradeUIArtPlayModeChecks
{
    private static readonly string Output = UpgradeUICompactLayoutTests.Output;
    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
    private static Component Find(string type) => (Component)UnityEngine.Object.FindObjectOfType(TypeOf(type));
    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);
    private static void Listen(object target, string name, Delegate callback) => target.GetType().GetEvent(name).AddEventHandler(target, callback);
    private static Component Panel(string name, Transform parent)
    {
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"), parent, false);
        return go.GetComponent(TypeOf(name + "View"));
    }
    private static IList Data(string name, int count)
    {
        var type = TypeOf(name); var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
        for (int i = 0; i < count; i++)
        {
            var data = Activator.CreateInstance(type);
            foreach (var field in type.GetFields())
            {
                if (field.FieldType == typeof(string)) field.SetValue(data, field.Name + " " + i);
                if (field.FieldType == typeof(Sprite)) field.SetValue(data, Sprite("CraftButton"));
                if (field.FieldType == typeof(Color)) field.SetValue(data, new Color(.72f, .5f, 1));
            }
            list.Add(data);
        }
        return list;
    }
    private static void Pointer(Component button, string handler)
    {
        var eventType = TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI");
        var current = eventType.GetProperty("current").GetValue(null);
        var data = Activator.CreateInstance(TypeOf("UnityEngine.EventSystems.PointerEventData", "UnityEngine.UI"), current);
        var execute = TypeOf("UnityEngine.EventSystems.ExecuteEvents", "UnityEngine.UI");
        var callback = execute.GetProperty(handler + "Handler").GetValue(null);
        var contract = TypeOf("UnityEngine.EventSystems.I" + char.ToUpperInvariant(handler[0]) + handler.Substring(1) + "Handler", "UnityEngine.UI");
        execute.GetMethods().Single(m => m.Name == "Execute" && m.IsGenericMethod).MakeGenericMethod(contract)
            .Invoke(null, new[] { (object)button.gameObject, data, callback });
    }
    private static void Click(Component button)
    {
        Pointer(button, "pointerEnter"); Pointer(button, "pointerDown"); Pointer(button, "pointerUp"); Pointer(button, "pointerClick");
    }
    internal static IEnumerator ButtonStates(Component button)
    {
        var eventType = TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI");
        var events = eventType.GetProperty("current").GetValue(null);
        eventType.GetMethod("SetSelectedGameObject", new[] { typeof(GameObject) }).Invoke(events, new object[] { null });
        Pointer(button, "pointerExit");
        string[] states = { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" };
        foreach (string state in states)
        {
            if (state == "Highlighted") Pointer(button, "pointerEnter");
            if (state == "Pressed") Pointer(button, "pointerDown");
            if (state == "Selected") { Pointer(button, "pointerUp"); Call(button, "Select"); }
            if (state == "Disabled") Set(button, "interactable", false);
            yield return new WaitForSecondsRealtime(.15f);
            var current = button.GetType().GetProperty("currentSelectionState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
            Assert.That(current.ToString(), Is.EqualTo(state));
            var expected = (Color)Property(Property(button, "colors"), char.ToLowerInvariant(state[0]) + state.Substring(1) + "Color");
            var renderer = (CanvasRenderer)Property(Property(button, "targetGraphic"), "canvasRenderer");
            var actual = renderer.GetColor();
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01)); Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01)); Assert.That(actual.a, Is.EqualTo(expected.a).Within(.01));
        }
        Set(button, "interactable", true); Pointer(button, "pointerExit");
        Debug.Log("UI_ART_STATES: Normal/Highlighted/Pressed/Selected/Disabled transition states and rendered graphic tints verified.");
    }
    private static Rect Bounds(RectTransform r)
    {
        var corners = new Vector3[4]; r.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
    }
    private static void TextFit(GameObject root, string evidence)
    {
        var lines = new List<string>();
        foreach (var label in Components(root, "TMPro.TextMeshProUGUI"))
        {
            if (!label.gameObject.activeInHierarchy) continue;
            Call(label, "ForceMeshUpdate", true, true);
            bool overflowing = (bool)Property(label, "isTextOverflowing");
            var r = (RectTransform)label.transform;
            lines.Add(label.name + " | " + Property(label, "text") + " | rect=" + r.rect.size + " | font=" + Property(label, "fontSize") + " | overflow=" + overflowing);
            Assert.That(overflowing, Is.False, label.name + ": " + Property(label, "text"));
            Assert.That((float)Property(label, "fontSize"), Is.GreaterThanOrEqualTo(24));
            var margin = (Vector4)Property(label, "margin");
            Assert.That(margin.x, Is.GreaterThanOrEqualTo(4)); Assert.That(margin.z, Is.GreaterThanOrEqualTo(4));
            Assert.That(margin.y, Is.GreaterThanOrEqualTo(2)); Assert.That(margin.w, Is.GreaterThanOrEqualTo(2));
            if (!string.IsNullOrWhiteSpace((string)Property(label, "text")))
            {
                var ink = (UnityEngine.Bounds)Property(label, "textBounds");
                Assert.That(ink.min.x, Is.GreaterThanOrEqualTo(r.rect.xMin - 1), label.name + " ink left");
                Assert.That(ink.max.x, Is.LessThanOrEqualTo(r.rect.xMax + 1), label.name + " ink right");
                Assert.That(ink.min.y, Is.GreaterThanOrEqualTo(r.rect.yMin - 1), label.name + " ink bottom");
                Assert.That(ink.max.y, Is.LessThanOrEqualTo(r.rect.yMax + 1), label.name + " ink top");
                // Measure visible glyph ink against the actual sliced border, not merely the outer frame rect.
                for (var parent = label.transform.parent; parent != null; parent = parent.parent)
                {
                    var image = parent.GetComponent(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI"));
                    if (image == null || Property(image, "sprite") != (object)Sprite("TutorialBox")) continue;
                    var frame = (RectTransform)parent;
                    var sprite = (Sprite)Property(image, "sprite");
                    var canvas = parent.GetComponentInParent<Canvas>();
                    Vector4 border = sprite.border * canvas.referencePixelsPerUnit / sprite.pixelsPerUnit / (float)Property(image, "pixelsPerUnitMultiplier");
                    var a = frame.InverseTransformPoint(label.transform.TransformPoint(ink.min));
                    var b = frame.InverseTransformPoint(label.transform.TransformPoint(ink.max));
                    Assert.That(a.x, Is.GreaterThanOrEqualTo(frame.rect.xMin + border.x + 2 - .1f), label.name + " sliced ink left");
                    Assert.That(a.y, Is.GreaterThanOrEqualTo(frame.rect.yMin + border.y + 2 - .1f), label.name + " sliced ink bottom");
                    Assert.That(b.x, Is.LessThanOrEqualTo(frame.rect.xMax - border.z - 2 + .1f), label.name + " sliced ink right");
                    Assert.That(b.y, Is.LessThanOrEqualTo(frame.rect.yMax - border.w - 2 + .1f), label.name + " sliced ink top");
                    break;
                }
            }
        }
        File.WriteAllLines(Output + evidence + "-text-fit.txt", lines);
        foreach (var card in root.GetComponentsInChildren(TypeOf("LevelUpCardView"), false))
        {
            var icon = (Component)Field(card, "icon");
            var target = (Component)Field(card, "targetLabel");
            Assert.That(Bounds((RectTransform)icon.transform).Overlaps(Bounds((RectTransform)target.transform)), Is.False);
            var button = Field(card, "selectButton"); var graphic = (Component)Property(button, "targetGraphic");
            Assert.That(Bounds((RectTransform)graphic.transform).Overlaps(Bounds((RectTransform)((Component)Field(card, "previewLabel")).transform)), Is.False);
        }
        foreach (var row in root.GetComponentsInChildren(TypeOf("SpecialUpgradeItemView"), false))
        {
            var graphic = (Component)Property(Field(row, "purchaseButton"), "targetGraphic");
            foreach (string field in new[] { "costsLabel", "prerequisitesLabel" })
                Assert.That(Bounds((RectTransform)graphic.transform).Overlaps(Bounds((RectTransform)((Component)Field(row, field)).transform)), Is.False, field);
        }
    }
    internal static IEnumerator Capture(Canvas canvas, GameObject panel, string name, int width, int height, bool measured = true)
    {
        Assert.That(SystemInfo.graphicsDeviceType.ToString(), Is.Not.EqualTo("Null"), "Native graphics device required");
        var go = new GameObject("UI capture camera"); var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .18f, .14f);
        camera.orthographic = true; camera.orthographicSize = 540; camera.cullingMask = 1 << 5; camera.transform.position = new Vector3(0, 0, -10);
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        float oldScale = canvas.scaleFactor, oldDistance = canvas.planeDistance;
        var scaler = canvas.GetComponent(TypeOf("UnityEngine.UI.CanvasScaler", "UnityEngine.UI")) as Behaviour;
        bool scalerEnabled = scaler != null && scaler.enabled;
        var previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            // CanvasScaler uses actual Screen dimensions; disable it ONLY for an exact reference-resolution capture.
            if (scaler != null)
            {
                Assert.That(Property(scaler, "referenceResolution"), Is.EqualTo(new Vector2(1920, 1080)));
                Assert.That(Property(scaler, "matchWidthOrHeight"), Is.EqualTo(.5f));
                scaler.enabled = false;
            }
            Directory.CreateDirectory(Output);
            canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f); // existing Canvas matchWidthOrHeight=.5
            Canvas.ForceUpdateCanvases();
            yield return null; // Let the graphics-enabled player loop build and render Canvas meshes into the camera target.
            yield return null;
            string evidence = name + "-" + width + "x" + height;
            if (measured) { UpgradeUICompactLayoutTests.MeasuredCapture(canvas, panel, evidence, width, height); TextFit(panel, evidence); }
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                File.WriteAllBytes(Output + evidence + ".png", texture.EncodeToPNG());
                Assert.That(pixels.Select(p => p.r + ":" + p.g + ":" + p.b).Distinct().Take(32).Count(), Is.GreaterThan(16), "Capture must contain real rendered UI");
                Debug.Log("UI_ART_CAPTURE " + evidence + " graphics=" + SystemInfo.graphicsDeviceType);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        finally
        {
            if (scaler != null) scaler.enabled = scalerEnabled;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
            canvas.scaleFactor = oldScale; canvas.planeDistance = oldDistance; RenderTexture.active = previous;
            camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }
    }
    // The reward is shown alone: drawn after the level-up panel, with its backdrop up and the panel hidden.
    private static void AssertRewardHierarchy(Component reward, Component level)
    {
        Assert.That(reward.transform.GetSiblingIndex(), Is.GreaterThan(level.transform.GetSiblingIndex()));
        var backdrop = (Component)Field(reward, "backdrop");
        Assert.That(backdrop.gameObject.activeInHierarchy, Is.True);
        Assert.That(((CanvasGroup)backdrop.GetComponent(typeof(CanvasGroup))).blocksRaycasts, Is.True);
        Assert.That(level.gameObject.activeSelf, Is.False, "LevelUpPanel stays hidden while the reward is visible");
    }
    private sealed class Game
    {
        public Component flow, runtime, presenter, experience, unlocks, indicator, reward, level;
        public object modal;
        public Canvas canvas;
    }
    // The scene load itself must be yielded by the test method (nested enumerators can only yield null in Edit Mode tests).
    private static Game SetupGame()
    {
        var g = new Game();
        var tutorial = Find("TutorialManager"); if (tutorial != null) Call(tutorial, "SkipTutorial"); // Test fixture bypass only.
        g.flow = Find("GameFlowController"); Call(g.flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Day"));
        g.modal = Property(Find("UIManager"), "Flow");
        var currentModal = Property(g.modal, "CurrentModal"); if (currentModal.ToString() != "None") Call(g.modal, "Close", currentModal);
        g.runtime = Find("UpgradeRuntime"); g.presenter = Find("UpgradePanel"); g.experience = Find("PlayerExperienceSystem");
        g.unlocks = Find("PlayerContentUnlockSystem"); g.indicator = Find("LevelUpIndicator");
        Assert.That(g.runtime, Is.Not.Null); Assert.That(((Behaviour)g.runtime).enabled, Is.True); Assert.That(Property(g.presenter, "IsReady"), Is.True);
        Assert.That(g.indicator, Is.Not.Null, "Player prefab carries the LevelUpIndicator");
        g.level = (Component)Field(g.presenter, "levelUpView"); g.reward = (Component)Field(g.presenter, "rewardView");
        g.canvas = g.presenter.GetComponent<Canvas>();
        Call(g.experience, "ResetProgression"); Time.timeScale = 1;
        return g;
    }
    // No lambdas: a closure over test locals is created before EnterPlayMode and is gone after the domain reload.
    private static IEnumerator WaitForChoosing(Component flow, float seconds, string what)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!(bool)Property(flow, "IsChoosingUpgrade") && Time.realtimeSinceStartup < end) { SkipTutorialIfActive(); yield return null; }
        Assert.That(Property(flow, "IsChoosingUpgrade"), Is.True, what + " | " + FlowState(flow));
    }
    // Test fixture bypass only: the tutorial can (re)start after the scene fade and would gate the upgrade flow.
    private static void SkipTutorialIfActive()
    {
        var tutorial = Find("TutorialManager");
        if (tutorial != null && (bool)Call(tutorial, "IsTutorialActive")) Call(tutorial, "SkipTutorial");
    }
    private static string FlowState(Component flow)
    {
        var experience = Find("PlayerExperienceSystem"); var ui = Find("UIManager"); var modal = Property(ui, "Flow");
        var tutorial = Find("TutorialManager"); var life = experience.GetComponent(TypeOf("LifeController"));
        return "timeScale=" + Time.timeScale + " worldPhase=" + Property(flow, "WorldPhase") + " modal=" + Property(modal, "CurrentModal") +
            " hasOpenModal=" + Property(modal, "HasOpenModal") + " pending=" + Property(experience, "PendingChoices") +
            " alive=" + Call(life, "IsAlive") + " tutorialActive=" + (tutorial != null ? Call(tutorial, "IsTutorialActive") : "n/a") +
            " paused=" + Property(Find("PauseController"), "IsPaused");
    }
    private static bool Choosing(Game g) => (bool)Property(g.flow, "IsChoosingUpgrade");
    private static bool RewardHasContent(Game g, int level)
    {
        var reward = Call(g.unlocks, "GetLevelReward", level);
        return reward != null && (bool)Property(reward, "HasContent");
    }
    private static void CaptureWorld(Camera camera, string name, int width, int height)
    {
        Directory.CreateDirectory(Output);
        var rt = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + name + "-" + width + "x" + height + ".png", texture.EncodeToPNG());
            Debug.Log("UI_ART_CAPTURE " + name + "-" + width + "x" + height + " world");
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = previous; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    private static void CaptureCloseUp(Camera main, Transform player, Transform icon, string name, int width, int height)
    {
        var go = new GameObject("Indicator close-up camera"); var close = go.AddComponent<Camera>();
        try
        {
            close.CopyFrom(main); close.orthographicSize = 3.4f;
            close.transform.position = new Vector3(player.position.x, (player.position.y + icon.position.y) / 2f, main.transform.position.z); // frames player and icon
            CaptureWorld(close, name, width, height);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [UnityTest] public IEnumerator LevelUpIndicatorShowsAbovePlayerAndHoldsTheModalUntilItFinishes()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Call(g.experience, "AddExperience", 100);
        Assert.That(Property(g.indicator, "IsPlaying"), Is.True, "plays the same frame the level is gained");
        float start = Time.realtimeSinceStartup;
        yield return new WaitForSecondsRealtime(.75f);
        Assert.That(Property(g.indicator, "IsPlaying"), Is.True);
        Assert.That(Choosing(g), Is.False); Assert.That(Property(g.modal, "CurrentModal").ToString(), Is.EqualTo("None"));
        var icon = (SpriteRenderer)Field(g.indicator, "icon"); var body = ((Component)g.experience).GetComponent<SpriteRenderer>();
        Assert.That(icon.enabled, Is.True); Assert.That(icon.sprite.name, Is.EqualTo("LevelUpIcon"));
        Assert.That(icon.bounds.center.y, Is.GreaterThan(body.bounds.max.y), "icon is above the player");
        Assert.That(icon.sortingOrder, Is.GreaterThan(body.sortingOrder));
        Assert.That(Time.timeScale, Is.EqualTo(1f), "gameplay is not frozen during the feedback");
        var main = Camera.main; Assert.That(main, Is.Not.Null);
        CaptureWorld(main, "runtime-levelup-indicator-scene", 1920, 1080);
        CaptureCloseUp(main, body.transform, icon.transform, "runtime-levelup-indicator-closeup", 1280, 720);
        float end = Time.realtimeSinceStartup + 4f;
        while ((bool)Property(g.indicator, "IsPlaying") && Time.realtimeSinceStartup < end)
        {
            Assert.That(Choosing(g), Is.False, "modal must not open while the indicator plays");
            Assert.That(Property(g.modal, "CurrentModal").ToString(), Is.Not.EqualTo("LevelUp"));
            SkipTutorialIfActive();
            yield return null;
        }
        Assert.That(Property(g.indicator, "IsPlaying"), Is.False);
        float played = Time.realtimeSinceStartup - start;
        Assert.That(played, Is.InRange(1.0f, 2.5f));
        yield return WaitForChoosing(g.flow, 3f, "modal opens once the indicator finished");
        Assert.That(Property(g.modal, "CurrentModal").ToString(), Is.EqualTo("LevelUp"));
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator RewardModalShowsAloneThenUpgradeCardsAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Assert.That(RewardHasContent(g, 2), Is.False, "not recorded before the level is gained");
        Call(g.experience, "AddExperience", 100);
        Assert.That(RewardHasContent(g, 2), Is.True);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        int pending = (int)Property(g.experience, "PendingChoices");
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        Assert.That(g.level.gameObject.activeSelf, Is.False, "level-up panel hidden while the reward is visible");
        AssertRewardHierarchy(g.reward, g.level);
        yield return null;
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }) yield return Capture(g.canvas, g.reward.gameObject, "runtime-reward-alone", size.x, size.y, false);
        // Selection is impossible while the reward is visible, both through the presenter and the runtime.
        g.presenter.GetType().GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(g.presenter, new object[] { 0 });
        Call(g.runtime, "Choose", 0); yield return null;
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending));
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        Click((Component)Field(g.reward, "continueButton")); yield return null;
        Assert.That(Property(g.reward, "IsVisible"), Is.False);
        Assert.That(g.level.gameObject.activeSelf, Is.True, "upgrade panel appears after Continue");
        Assert.That(Choosing(g), Is.True);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }) yield return Capture(g.canvas, g.level.gameObject, "runtime-upgrades-after-reward", size.x, size.y);
        var cards = (Array)Field(g.level, "cards");
        Click((Component)Field(cards.GetValue(0), "selectButton")); yield return null;
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending - 1));
        Assert.That(Choosing(g), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator EachLevelRewardShowsOnceAndLevelsWithoutRewardGoStraightToUpgrades()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Call(g.experience, "AddExperience", 750);
        int current = (int)Property(g.experience, "CurrentLevel");
        Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(current - 1));
        var rewardLevels = new List<int>(); var plainLevels = new List<int>();
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        for (int step = 0; step < current - 1; step++)
        {
            int pending = (int)Property(g.experience, "PendingChoices");
            int level = current - pending + 1;
            if (RewardHasContent(g, level))
            {
                rewardLevels.Add(level);
                Assert.That(Property(g.reward, "IsVisible"), Is.True, "reward for level " + level);
                Assert.That(g.level.gameObject.activeSelf, Is.False);
                Assert.That((string)Property(Field(g.reward, "headingLabel"), "text"), Does.Contain(level.ToString()));
                Click((Component)Field(g.reward, "continueButton")); yield return null;
                Assert.That(Property(g.reward, "IsVisible"), Is.False);
                yield return null; Assert.That(Property(g.reward, "IsVisible"), Is.False, "a level's reward is shown once");
            }
            else
            {
                plainLevels.Add(level);
                Assert.That(Property(g.reward, "IsVisible"), Is.False, "no reward for level " + level);
            }
            Assert.That(g.level.gameObject.activeSelf, Is.True, "upgrade choices for level " + level);
            var cards = (Array)Field(g.level, "cards");
            Click((Component)Field(cards.GetValue(0), "selectButton")); yield return null;
            Assert.That(Property(g.experience, "PendingChoices"), Is.EqualTo(pending - 1));
        }
        Assert.That(rewardLevels, Is.Not.Empty); Assert.That(plainLevels, Is.Not.Empty);
        Assert.That(rewardLevels.Distinct().Count(), Is.EqualTo(rewardLevels.Count));
        Assert.That(Choosing(g), Is.False);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator InterruptedChoiceShowsItsRewardAgainAfterProgressionReset()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow starts");
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        Call(g.experience, "ResetProgression"); yield return null;
        Assert.That(Choosing(g), Is.False); Assert.That(Property(g.reward, "IsVisible"), Is.False);
        Assert.That(g.level.gameObject.activeSelf, Is.False);
        Time.timeScale = 1; Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, 5f, "upgrade flow restarts");
        Assert.That(Property(g.reward, "IsVisible"), Is.True, "level 2 reward shows again after the reset");
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator IsolatedPrefabPointerSmokeAndRenderedLayouts()
    {
        yield return new EnterPlayMode();
        yield return IsolatedSmoke();
        yield return new ExitPlayMode();
    }
    private static IEnumerator IsolatedSmoke()
    {
        var root = new GameObject("Isolated upgrade UI smoke", typeof(RectTransform), typeof(Canvas)); root.layer = 5;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent(TypeOf("UnityEngine.UI.GraphicRaycaster", "UnityEngine.UI"));
        var events = new GameObject("Smoke EventSystem"); events.AddComponent(TypeOf("UnityEngine.EventSystems.EventSystem", "UnityEngine.UI"));
        var level = Panel("LevelUpPanel", root.transform); var shop = Panel("SpecialUpgradePanel", root.transform);
        int selected = -99, selections = 0, purchases = 0, closed = 0;
        Listen(level, "Selected", (Action<int>)(i => { selected = i; selections++; }));
        Listen(shop, "Purchased", (Action<int>)(i => purchases++)); Listen(shop, "Closed", (Action)(() => closed++));
        var choices = Data("LevelUpCardData", 3);
        string[] targets = { "Player", "Fireball", "Moonflower (seed)" };
        for (int i = 0; i < 3; i++)
        {
            SetField(choices[i], "Target", targets[i]); SetField(choices[i], "Name", "Blessed " + targets[i]);
            SetField(choices[i], "Stat", "AttackSpeed"); SetField(choices[i], "Bonus", "+12.5% base");
            SetField(choices[i], "Rarity", new[] { "COMMON", "RARE", "LEGENDARY" }[i]); SetField(choices[i], "Preview", "1.25 → 1.406 attacks/s");
        }
        Call(level, "Show", choices, 3); Call(level, "Show", choices, 3); yield return null;
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, level.gameObject, "isolated-levelup", size.x, size.y);
        var cards = (Array)Field(level, "cards");
        yield return ButtonStates((Component)Field(cards.GetValue(2), "selectButton"));
        Click((Component)Field(cards.GetValue(1), "selectButton"));
        Assert.That(selections, Is.EqualTo(1)); Assert.That(selected, Is.EqualTo(1));
        Call(level, "Show", Data("LevelUpCardData", 0), 1); yield return null;
        yield return Capture(canvas, level.gameObject, "isolated-empty", 1920, 1080);
        Click((Component)Field(level, "continueButton")); Assert.That(selected, Is.EqualTo(-1)); Assert.That(selections, Is.EqualTo(2)); Call(level, "Hide");
        var items = Data("SpecialUpgradeItemData", 2);
        for (int i = 0; i < 2; i++)
        {
            SetField(items[i], "Target", "Fireball"); SetField(items[i], "Name", "Piercing embers"); SetField(items[i], "Description", "Projectiles pierce one additional enemy.");
            SetField(items[i], "Costs", "Wood 120/20  Crystal 35/10  Stone 60/15"); SetField(items[i], "Stacks", i + "/3"); SetField(items[i], "Prerequisites", "Requires: burning_embers");
            SetField(items[i], "CanPurchase", i == 0);
        }
        Call(shop, "Show", items); Call(shop, "Show", items); yield return null;
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, shop.gameObject, "isolated-shop", size.x, size.y);
        var rows = (IList)Field(shop, "rows"); Click((Component)Field(rows[1], "purchaseButton")); Assert.That(purchases, Is.Zero);
        Click((Component)Field(rows[0], "purchaseButton")); Assert.That(purchases, Is.EqualTo(1));
        Click((Component)Field(shop, "closeButton")); Assert.That(closed, Is.EqualTo(1));
        Call(shop, "Hide"); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(events); yield return null;
    }
    [UnityTest] public IEnumerator MissingIndicatorNeverBlocksTheUpgradeFlow()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var g = SetupGame();
        SetField(g.runtime, "indicator", null);
        float start = Time.realtimeSinceStartup;
        Call(g.experience, "AddExperience", 100);
        yield return WaitForChoosing(g.flow, .9f, "flow opens without waiting for an indicator");
        Assert.That(Property(g.indicator, "IsPlaying"), Is.True, "the (ignored) indicator is still animating");
        Assert.That(Time.realtimeSinceStartup - start, Is.LessThan(.9f));
        Assert.That(Property(g.reward, "IsVisible"), Is.True);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator GameSceneRuntimeChoiceShopPurchaseAndModalRelease()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var tutorial = Find("TutorialManager"); if (tutorial != null) Call(tutorial, "SkipTutorial"); // Test fixture bypass only.
        var flow = Find("GameFlowController"); Call(flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Day"));
        var ui = Find("UIManager"); var modal = Property(ui, "Flow");
        var currentModal = Property(modal, "CurrentModal"); if (currentModal.ToString() != "None") Call(modal, "Close", currentModal);
        var runtime = Find("UpgradeRuntime"); var presenter = Find("UpgradePanel"); var experience = Find("PlayerExperienceSystem");
        Assert.That(runtime, Is.Not.Null); Assert.That(((Behaviour)runtime).enabled, Is.True); Assert.That(Property(presenter, "IsReady"), Is.True);
        Call(experience, "ResetProgression"); Time.timeScale = 1; Call(experience, "AddExperience", 150);
        yield return WaitForChoosing(flow, 5f, "level-up indicator finishes, then the flow opens");
        Assert.That(Property(flow, "IsChoosingUpgrade"), Is.True); Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("LevelUp"));
        var suspended = ((IEnumerable)Field(runtime, "suspendedInteractions")).Cast<Behaviour>().ToArray();
        Assert.That(suspended.Length, Is.GreaterThan(0));
        foreach (var consumer in suspended) Assert.That(consumer.enabled, Is.False);
        var input = Find("InputReader"); Assert.That(Property(input, "MoveInput"), Is.EqualTo(Vector2.zero));
        Assert.That(Call(modal, "Open", Enum.Parse(TypeOf("UIModal"), "Inventory")), Is.False);
        var level = (Component)Field(presenter, "levelUpView"); var canvas = presenter.GetComponent<Canvas>();
        var cards = (Array)Field(level, "cards"); int pending = (int)Property(experience, "PendingChoices");
        // Level 2 unlocked content: the reward modal is shown alone first; the upgrade panel only after Continue.
        var reward = (Component)Field(presenter, "rewardView");
        Assert.That(Property(reward, "IsVisible"), Is.True);
        AssertRewardHierarchy(reward, level);
        yield return null;
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }) yield return Capture(canvas, reward.gameObject, "runtime-reward-alone", size.x, size.y, false);
        Call(runtime, "Choose", 0); yield return null;
        Assert.That(Property(experience, "PendingChoices"), Is.EqualTo(pending), "choices are blocked while the reward is visible");
        Click((Component)Field(reward, "continueButton")); yield return null;
        Assert.That(Property(reward, "IsVisible"), Is.False);
        Assert.That(level.gameObject.activeSelf, Is.True);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, level.gameObject, "runtime-levelup", size.x, size.y);
        Click((Component)Field(cards.GetValue(0), "selectButton")); yield return null;
        Assert.That(Property(experience, "PendingChoices"), Is.EqualTo(pending - 1));
        Assert.That(Property(flow, "IsChoosingUpgrade"), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        foreach (var consumer in suspended) Assert.That(consumer.enabled, Is.True);
        Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("None"));
        Call(runtime, "ToggleShop"); yield return null;
        Assert.That(Property(presenter, "IsShopOpen"), Is.True); Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("Crafting"));
        var shop = (Component)Field(presenter, "shopView"); var rows = (IList)Field(shop, "rows"); Assert.That(rows.Count, Is.GreaterThan(0));
        var definitions = (IList)Field(presenter, "shopDefinitions"); var targetIds = (IList)Field(presenter, "shopTargets");
        var definition = (SpecialDefinition)definitions[0]; string target = (string)targetIds[0];
        var state = (RunState)Property(runtime, "State"); int stacks = state.SpecialStacks(definition.id, target);
        var inventory = Find("InventoryManager");
        // Ensure the fixture proves disabled pointer clicks before granting exactly the production costs.
        var costs = CostPlanner.Aggregate(definition.costs, out var aggregated) ? aggregated : throw new InvalidOperationException("Invalid costs");
        foreach (var cost in costs) Call(inventory, "UseMaterial", Enum.ToObject(TypeOf("MaterialType"), cost.Key), (int)Call(inventory, "GetMaterialAmount", Enum.ToObject(TypeOf("MaterialType"), cost.Key)));
        Call(presenter, "ShowShop"); yield return null;
        Click((Component)Field(rows[0], "purchaseButton")); Assert.That(state.SpecialStacks(definition.id, target), Is.EqualTo(stacks));
        foreach (var cost in costs) Call(inventory, "AddMaterial", Enum.ToObject(TypeOf("MaterialType"), cost.Key), cost.Value);
        Call(presenter, "ShowShop"); yield return null;
        Assert.That(Property(Field(rows[0], "purchaseButton"), "interactable"), Is.True);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) }) yield return Capture(canvas, shop.gameObject, "runtime-shop", size.x, size.y);
        Click((Component)Field(rows[0], "purchaseButton")); yield return null;
        Assert.That(state.SpecialStacks(definition.id, target), Is.EqualTo(stacks + 1));
        foreach (var cost in costs) Assert.That(Call(inventory, "GetMaterialAmount", Enum.ToObject(TypeOf("MaterialType"), cost.Key)), Is.EqualTo(0));
        Click((Component)Field(shop, "closeButton")); yield return null;
        Assert.That(Property(presenter, "IsShopOpen"), Is.False); Assert.That(shop.gameObject.activeSelf, Is.False);
        Assert.That(Property(modal, "CurrentModal").ToString(), Is.EqualTo("None")); Assert.That(Time.timeScale, Is.EqualTo(1));
        Debug.Log("UI_ART_RUNTIME: Game scene pointer selection, disabled purchase, resource spending, stack increment, close, timeScale/input/modal assertions passed; tutorial skipped only in fixture.");
        yield return new ExitPlayMode();
    }
}
