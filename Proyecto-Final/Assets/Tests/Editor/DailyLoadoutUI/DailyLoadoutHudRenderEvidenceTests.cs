using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

/// <summary>Opt-in native Game rendering. Automated setup is not physical-input acceptance.</summary>
public sealed class DailyLoadoutHudRenderEvidenceTests
{
    private static Component Find(string type) => (Component)UnityEngine.Object.FindObjectOfType(F.Production(type));

    [UnityTest, Explicit("Requires a native graphics device; writes Game screenshots under the isolated project's Results directory")]
    public IEnumerator CaptureGameEquipmentStatesAtBothRequestedResolutions()
    {
        yield return new EnterPlayMode();
        try
        {
            Assert.That(SystemInfo.graphicsDeviceType.ToString(), Is.Not.EqualTo("Null"));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 20; i++) yield return null;
            var tutorial = Find("TutorialManager");
            if (tutorial != null)
            {
                F.Call(tutorial, "SkipTutorial");
                F.Call(tutorial, "StopAllCoroutines"); F.Call(tutorial, "CancelInvoke");
                ((Behaviour)tutorial).enabled = false;
            } // Evidence setup only, no saved scene or tutorial behavior change.
            var tutorialView = Find("TutorialUI");
            if (tutorialView != null) F.Call(tutorialView, "HideStepImmediate");
            var days = Find("DayCycleController"); var ui = Find("UIManager");
            var world = Find("WorldTransitionAnimator");
            var plants = (Component)F.Property(ui, "SeedSlots"); var spells = (Component)F.Property(ui, "SpellSlots");
            var canvas = ui.GetComponentInParent<Canvas>();
            F.Call(days, "StartDay"); F.Call(ui, "ShowExteriorHUD");
            Time.timeScale = 0;
            yield return Capture(canvas, "empty-day");
            var unlocks = Find("PlayerContentUnlockSystem"); var session = F.Property(unlocks, "Loadout");
            object plant = ((IEnumerable)F.Property(unlocks, "UnlockedPlants")).Cast<object>().First(x => x.ToString() != "None");
            object spell = ((IEnumerable)F.Property(unlocks, "UnlockedSpells")).Cast<object>().First();
            Assert.That(F.Call(session, "TrySetDraftPlant", 0, plant), Is.True);
            Assert.That(F.Call(session, "TrySetDraftSpell", 0, spell), Is.True);
            Assert.That(F.Call(session, "TryConfirm"), Is.True);
            var data = AssetDatabase.FindAssets("t:PlantDataSO").Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => AssetDatabase.LoadAssetAtPath<ScriptableObject>(p)).First(p => Equals(F.Field(p, "seedType"), plant));
            var seeds = Find("SeedInventory"); var inventory = Find("SpellInventory");
            F.Call(seeds, "TryGrantUnlockedPlant", data, 3);
            F.Call(plants, "SelectSlot", 0); // Same public selection route as number-key handling.
            yield return Capture(canvas, "equipped-day");
            F.Call(days, "StartNight"); F.Call(world, "TransitionToNight");
            Time.timeScale = 1; yield return new WaitForSecondsRealtime(.8f); Time.timeScale = 0;
            F.HudSlot(spells, false, 0).GetComponent<Button>().onClick.Invoke();
            int index = (int)F.Call(inventory, "GetSelectedSlotIndex");
            Assert.That(index, Is.GreaterThanOrEqualTo(0)); F.Call(inventory, "StartCooldown", index);
            F.Call(spells, "HandleUpdate");
            yield return Capture(canvas, "equipped-night-cooldown");
            F.Call(ui, "ShowInteriorHUD");
            Assert.That(plants.gameObject.activeSelf || spells.gameObject.activeSelf, Is.False);
            F.Call(days, "StartDay"); F.Call(world, "TransitionToDay");
            F.Call(ui, "ShowExteriorHUD");
            Time.timeScale = 1; yield return new WaitForSecondsRealtime(.8f); Time.timeScale = 0;
            yield return Capture(canvas, "returned-day");
        }
        finally { Time.timeScale = 1; }
        yield return new ExitPlayMode();
    }

    private static IEnumerator Capture(Canvas canvas, string state)
    {
        var worldCamera = Camera.main; Assert.That(worldCamera, Is.Not.Null);
        var cameraObject = new GameObject("HUD evidence camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.orthographic = true; camera.orthographicSize = 540;
        camera.cullingMask = 1 << 5; camera.depth = worldCamera.depth + 100;
        camera.transform.position = new Vector3(0, 0, -10);
        // Separate native targets reproduce Overlay canvas compositing without changing the scene's URP camera stack.
        var compositeMaterial = new Material(Shader.Find("UI/Default"));
        var scaler = canvas.GetComponent<CanvasScaler>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; var oldTarget = worldCamera.targetTexture;
        float scale = canvas.scaleFactor, distance = canvas.planeDistance;
        bool scalerEnabled = scaler.enabled; var previous = RenderTexture.active;
        string folder = Path.GetFullPath("Results/HudRenders"); Directory.CreateDirectory(folder);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            scaler.enabled = false;
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                var rt = new RenderTexture(size.x, size.y, 24);
                var uiTarget = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                var composite = new RenderTexture(size.x, size.y, 0);
                var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try
                {
                    worldCamera.targetTexture = rt; camera.targetTexture = uiTarget;
                    canvas.scaleFactor = Mathf.Sqrt(size.x / 1920f * size.y / 1080f);
                    Canvas.ForceUpdateCanvases(); yield return null; yield return null;
                    var hud = (Component)F.Property(canvas.GetComponentInChildren(F.Production("UIManager"), true), "SeedSlots");
                    Assert.That(hud.gameObject.activeInHierarchy, Is.True, "Evidence must include the active HUD, not terrain alone");
                    Graphics.Blit(rt, composite); RenderTexture.active = composite;
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadPixelMatrix(0, size.x, size.y, 0);
                        Graphics.DrawTexture(new Rect(0, 0, size.x, size.y), uiTarget, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, compositeMaterial);
                    }
                    finally { GL.PopMatrix(); }
                    texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                    Assert.That(texture.GetPixels32().Select(p => p.r + ":" + p.g + ":" + p.b).Distinct().Take(32).Count(), Is.GreaterThan(16));
                    File.WriteAllBytes(Path.Combine(folder, state + "-" + size.x + "x" + size.y + ".png"), texture.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = null; worldCamera.targetTexture = oldTarget; RenderTexture.active = previous;
                    rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
                    uiTarget.Release(); composite.Release();
                    UnityEngine.Object.DestroyImmediate(uiTarget); UnityEngine.Object.DestroyImmediate(composite);
                }
            }
        }
        finally
        {
            scaler.enabled = scalerEnabled; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
            canvas.scaleFactor = scale; canvas.planeDistance = distance;
            UnityEngine.Object.DestroyImmediate(compositeMaterial);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
