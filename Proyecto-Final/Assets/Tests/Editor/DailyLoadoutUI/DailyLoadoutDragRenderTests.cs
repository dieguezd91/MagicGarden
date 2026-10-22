using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutDragRenderTests
{
    [UnityTest, Explicit("Requires native graphics. Captures synthetic UI states, not manual gameplay acceptance")]
    public IEnumerator RenderCategoryPanelsAndExclusiveFeedbackAtBothRequestedResolutions()
    {
        yield return new EnterPlayMode();
        Assert.That(SystemInfo.graphicsDeviceType.ToString(), Is.Not.EqualTo("Null"));
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            // Reuse real project art in the disposable fixture, without mutating any source asset.
            var plantArt = AssetDatabase.FindAssets("t:PlantDataSO").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>).Select(p => F.Field(p, "plantIcon") as Sprite).First(p => p != null);
            var spellArt = AssetDatabase.FindAssets("t:SpellDataSO").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>).Select(p => F.Field(p, "spellIcon") as Sprite).First(p => p != null);
            F.Set(f.FirstPlant, "plantIcon", plantArt); F.Set(f.SecondPlant, "plantIcon", plantArt);
            F.Set(f.FirstSpell, "spellIcon", spellArt); F.Set(f.SecondSpell, "spellIcon", spellArt);
            f.Open(); f.Assign(0); f.Select(false, 0); f.Assign(0); yield return null;
            var go = new GameObject("Loadout evidence camera"); var camera = go.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 540; camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .12f);
            drag.Canvas.renderMode = RenderMode.ScreenSpaceCamera; drag.Canvas.worldCamera = camera; drag.Canvas.planeDistance = 1;
            drag.Canvas.GetComponent<CanvasScaler>().enabled = false;
            var previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory("Results/LayoutRenders");
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    try
                    {
                        camera.targetTexture = target; drag.Canvas.scaleFactor = Mathf.Sqrt(size.x / 1920f * size.y / 1080f);
                        foreach (bool plants in new[] { true, false })
                        {
                            f.Select(plants, 0); Canvas.ForceUpdateCanvases(); yield return null; yield return null;
                            string prefix = "Results/LayoutRenders/" + (plants ? "plants" : "spells") + "-" + size.x + "x" + size.y;
                            Capture(target, prefix + ".png");
                            drag.Begin(1);
                            foreach (bool valid in new[] { true, false })
                            {
                                drag.Hover(f.Slot(valid ? plants : !plants, 2)); f.Refresh(); yield return null; yield return null;
                                Assert.That(f.Panel.transform.Find("Drag Preview").gameObject.activeSelf, Is.True);
                                Capture(target, prefix + (valid ? "-valid.png" : "-invalid.png"));
                            }
                            drag.End();
                        }
                    }
                    finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        yield return new ExitPlayMode();
    }

    private static void Capture(RenderTexture target, string path)
    {
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            Assert.That(image.GetPixels32().Select(p => p.r + ":" + p.g + ":" + p.b).Distinct().Take(32).Count(),
                Is.GreaterThan(16), "Evidence must contain the native UI, not a flat camera clear");
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(image); }
    }
}
