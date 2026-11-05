#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

public class TutorialDialogueLayoutTests
{
    const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    static readonly string EvidenceRunId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",
        System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    GameObject root;
    ScriptableObject step;
    RenderTexture target;

    static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    static object Get(object owner, string name) => owner.GetType().GetProperty(name)?.GetValue(owner)
        ?? owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static void Property(object owner, string name, object value) => owner.GetType().GetProperty(name).SetValue(owner, value);
    static void Mesh(Component label) => label.GetType().GetMethod("ForceMeshUpdate").Invoke(label, new object[] { true, true });
    static Rect Bounds(RectTransform item, RectTransform relative)
    {
        var corners = new Vector3[4]; item.GetWorldCorners(corners);
        var points = corners.Select(p => relative.InverseTransformPoint(p)).ToArray();
        return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
    }
    static void Contains(Rect outer, Rect inner, string message)
    {
        Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 2), message);
        Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 2), message);
        Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 2), message);
        Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 2), message);
    }

    [TearDown]
    public void Cleanup()
    {
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (step != null) UnityEngine.Object.DestroyImmediate(step);
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }

    [UnityTest] public IEnumerator DialogueFits1920WithoutImage() => Check(1920, 1080, false);
    [UnityTest] public IEnumerator DialogueFits1920WithImage() => Check(1920, 1080, true);
    [UnityTest] public IEnumerator DialogueFits1024WithoutImage() => Check(1024, 768, false);
    [UnityTest] public IEnumerator DialogueFits1024WithImage() => Check(1024, 768, true);

    IEnumerator Check(int width, int height, bool withImage)
    {
        // Instantiate only the resolved nested panel: the game canvas is never activated.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        var source = prefab.GetComponentsInChildren<Transform>(true)
            .Single(t => t.GetComponent(TypeOf("TutorialUI")) != null);
        root = new GameObject("Isolated tutorial render viewport"); root.SetActive(false);
        var cameraObject = new GameObject("Tutorial test camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform);
        var camera = cameraObject.GetComponent<Camera>();
        target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var canvasObject = new GameObject("Tutorial test canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(root.transform);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        // Match the installed ScaleWithScreenSize / match=0.5 without modifying screen settings.
        canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
        var panelObject = UnityEngine.Object.Instantiate(source.gameObject, canvasObject.transform, false);
        var ui = panelObject.GetComponent(TypeOf("TutorialUI"));
        var panel = (RectTransform)panelObject.transform;
        var label = (Component)Get(ui, "instructionText");
        var button = (Component)Get(ui, "continueButton");
        var image = (Component)Get(ui, "stepImageDisplay");
        var originalFontSize = (float)Get(label, "fontSize");
        var originalPanelSprite = Get(panelObject.GetComponent(TypeOf("UnityEngine.UI.Image")), "sprite");
        // Keep real art/geometry, but detach unrelated audio services in this isolated fixture.
        Set(ui, "tutorialSoundBase", null); Set(ui, "writtingSoundBase", null);
        foreach (var sound in panelObject.GetComponents<MonoBehaviour>().Where(c => c != null &&
            (c.GetType().Name == "TutorialSoundBase" || c.GetType().Name == "WrittingSoundBase")))
            UnityEngine.Object.DestroyImmediate(sound);
        Set(ui, "fadeSpeed", 0.01f); Set(ui, "continueCooldown", 0f);
        root.SetActive(true); yield return null;
        step = ScriptableObject.CreateInstance(TypeOf("TutorialStep"));
        Set(step, "objectiveType", Enum.Parse(TypeOf("TutorialObjectiveType"), "Wait"));
        if (withImage) Set(step, "stepImage", originalPanelSprite);
        var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Managers/TutorialManager.prefab");
        var installedSequence = Get(managerPrefab.GetComponent(TypeOf("TutorialManager")), "sequence");
        Assert.That(installedSequence, Is.Not.Null, "Render actual installed playback, not unused legacy assets.");
        var installedCopies = ((IEnumerable)Get(installedSequence, "steps")).Cast<ScriptableObject>()
            .Select(s => (string)Get(s, "DisplayText")).ToArray();
        var longest = installedCopies.OrderByDescending(text => text.Length).First();
        var movement = installedCopies.First(text => text.Contains("<sprite name=\"ArrowKeys\">"));
        var designerText = "<b>FOREST:</b> " + string.Concat(Enumerable.Repeat(
            "Prepare your garden before night. Move with <sprite name=\"WASDKeys\"> and protect the ancestral tree. ", 24)) +
            "\n<sprite name=\"WASDKeys\"> gypqj";
        foreach (var text in new[] { longest, movement, designerText })
        {
            Set(step, "instructionText", text);
            var finished = 0; Action onFinished = () => finished++;
            ui.GetType().GetEvent("TypingFinished").AddEventHandler(ui, onFinished);
            ui.GetType().GetMethod("ShowStep").Invoke(ui, new object[] { step });
            yield return null; yield return null;
            Assert.That((bool)Get(ui, "IsTyping"), Is.True, "ShowStep retains typewriter semantics.");
            ((UnityEvent)Get(button, "onClick")).Invoke();
            yield return null; yield return null;
            Mesh(label);
            Assert.That((bool)Get(ui, "IsTyping"), Is.False);
            Assert.That(finished, Is.EqualTo(1), "Fast-forward emits exactly one completion event.");
            Assert.That((string)Get(label, "text"), Is.EqualTo(text), "Designer content must not be discarded.");
            Assert.That((float)Get(label, "fontSize"), Is.EqualTo(originalFontSize), "Preserve installed readable typography.");
            Assert.That(Get(panelObject.GetComponent(TypeOf("UnityEngine.UI.Image")), "sprite"), Is.SameAs(originalPanelSprite));
            Assert.That(button.gameObject.activeInHierarchy && (bool)Get(button, "interactable"), Is.True);
            var canvasRect = (RectTransform)canvasObject.transform;
            Contains(canvasRect.rect, Bounds(panel, canvasRect), "Panel must be bounded to target viewport.");
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(panel.localScale, Is.EqualTo(Vector3.one), "Containment must use the settled panel scale.");
            Contains(canvasRect.rect, Bounds(panel, canvasRect), "Settled panel must remain inside target viewport.");
            Contains(panel.rect, Bounds((RectTransform)button.transform, panel), "Continue remains within panel.");
            AssertReadable(label, panel, (RectTransform)button.transform);
            Capture(camera, width, height, withImage,
                text == designerText ? "long" : text == movement ? "movement" : "installed");
            if (text == designerText)
            {
                var scroll = label.GetComponentInParent(TypeOf("UnityEngine.UI.ScrollRect"));
                Assert.That(scroll, Is.Not.Null, "Oversized designer text needs reachable scrolling.");
                var viewport = (RectTransform)Get(scroll, "viewport");
                var content = (RectTransform)Get(scroll, "content");
                Assert.That(content.rect.height, Is.GreaterThan(viewport.rect.height));
                var before = content.anchoredPosition.y;
                var pointer = Activator.CreateInstance(TypeOf("UnityEngine.EventSystems.PointerEventData"), new object[] { null });
                Property(pointer, "scrollDelta", new Vector2(0, -10000));
                scroll.GetType().GetMethod("OnScroll").Invoke(scroll, new[] { pointer });
                yield return null;
                Assert.That(content.anchoredPosition.y, Is.GreaterThan(before), "Normal wheel input must move the content.");
                Assert.That((float)Get(scroll, "verticalNormalizedPosition"), Is.LessThanOrEqualTo(0.01f));
                var info = Get(label, "textInfo");
                var characters = (Array)Get(info, "characterInfo");
                var last = Enumerable.Range(0, (int)Get(info, "characterCount"))
                    .Last(i => (bool)Get(characters.GetValue(i), "isVisible"));
                var lastBottom = viewport.InverseTransformPoint(label.transform.TransformPoint(
                    (Vector3)Get(characters.GetValue(last), "bottomLeft")));
                Assert.That(lastBottom.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 2), "Final glyph must be reachable at scroll bottom.");
                Assert.That(lastBottom.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 2));
                Property(pointer, "scrollDelta", new Vector2(0, 10000));
                scroll.GetType().GetMethod("OnScroll").Invoke(scroll, new[] { pointer });
                yield return null;
                Assert.That((float)Get(scroll, "verticalNormalizedPosition"), Is.GreaterThanOrEqualTo(0.99f));
            }
            if (text == designerText)
            {
                var info = Get(label, "textInfo");
                var characters = (Array)Get(info, "characterInfo");
                Assert.That(((FontStyleMask)GetStyle(characters.GetValue(0)) & FontStyleMask.Bold) != 0, Is.True);
                Assert.That(Enumerable.Range(0, (int)Get(info, "characterCount"))
                    .Any(i => Get(characters.GetValue(i), "elementType").ToString() == "Sprite"), Is.True);
            }
            ui.GetType().GetEvent("TypingFinished").RemoveEventHandler(ui, onFinished);
        }
        var longHeight = panel.rect.height;
        Set(step, "instructionText", "<b>FOREST:</b> Welcome home.");
        Set(step, "objectiveType", Enum.Parse(TypeOf("TutorialObjectiveType"), "Dig"));
        ui.GetType().GetMethod("ShowStep").Invoke(ui, new object[] { step });
        yield return null; yield return null;
        ((UnityEvent)Get(button, "onClick")).Invoke();
        yield return null;
        Mesh(label);
        Assert.That(panel.rect.height, Is.LessThan(longHeight), "Short dialogue must shrink after a long step.");
        Assert.That(button.gameObject.activeSelf, Is.False, "Objective steps retain the existing post-typing button behavior.");
        AssertReadable(label, panel, (RectTransform)button.transform);
        ui.GetType().GetMethod("HideStepImmediate").Invoke(ui, null);
        Assert.That(panelObject.activeSelf, Is.False, "Immediate hide used by skip must still work.");
    }

    static void Capture(Camera camera, int width, int height, bool image, string content)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var previous = RenderTexture.active;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            var directory = System.IO.Path.Combine(Application.dataPath, "../Library/TutorialRevisionEvidences", EvidenceRunId);
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory,
                $"dialogue-{width}x{height}-{(image ? "image" : "text")}-{content}.png");
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            Debug.Log("Tutorial render evidence: " + path);
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    [Flags] enum FontStyleMask { Bold = 1 }
    static int GetStyle(object character) => Convert.ToInt32(Get(character, "style"));

    static void AssertReadable(Component label, RectTransform panel, RectTransform button)
    {
        var scroll = label.GetComponentInParent(TypeOf("UnityEngine.UI.ScrollRect"));
        var readable = scroll == null ? panel : (RectTransform)Get(scroll, "content");
        if (scroll != null)
        {
            var viewport = (RectTransform)Get(scroll, "viewport");
            Assert.That(viewport, Is.Not.Null);
            Contains(panel.rect, Bounds(viewport, panel), "Scroll viewport stays in panel.");
            Assert.That(viewport.GetComponent(TypeOf("UnityEngine.UI.RectMask2D")) != null ||
                viewport.GetComponent(TypeOf("UnityEngine.UI.Mask")) != null, Is.True, "Scroll overflow must be clipped.");
            Assert.That((bool)Get(scroll, "vertical"), Is.True, "All long content must be reachable.");
            Assert.That(Bounds(viewport, panel).Overlaps(Bounds(button, panel)), Is.False);
        }
        var info = Get(label, "textInfo");
        var characters = (Array)Get(info, "characterInfo");
        for (var i = 0; i < (int)Get(info, "characterCount"); i++)
        {
            var character = characters.GetValue(i);
            if (!(bool)Get(character, "isVisible")) continue;
            var bottom = readable.InverseTransformPoint(label.transform.TransformPoint((Vector3)Get(character, "bottomLeft")));
            var top = readable.InverseTransformPoint(label.transform.TransformPoint((Vector3)Get(character, "topRight")));
            var glyph = Rect.MinMaxRect(bottom.x, bottom.y, top.x, top.y);
            Contains(readable.rect, glyph, $"Rendered glyph {i} escaped readable region; panel={panel.rect}, text={((RectTransform)label.transform).rect}.");
            if (scroll == null) Assert.That(glyph.Overlaps(Bounds(button, readable)), Is.False, $"Glyph {i} overlaps Continue.");
        }
        Assert.That((bool)Get(label, "isTextTruncated"), Is.False, "Clipping/truncation is not a readability solution.");
        Assert.That((int)Get(label, "maxVisibleCharacters"), Is.GreaterThanOrEqualTo((int)Get(info, "characterCount")));
    }
}
#endif
