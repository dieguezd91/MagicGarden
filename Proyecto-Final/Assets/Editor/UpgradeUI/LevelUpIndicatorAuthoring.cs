using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Explicit one-shot authoring (menu or -executeMethod LevelUpIndicatorAuthoring.Run). Idempotent; never runs on import or play.
public static class LevelUpIndicatorAuthoring
{
    public const string SheetPath = "Assets/Sprites/SpriteSheets/UISprites.png";
    public const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
    public const string IconName = "LevelUpIcon";
    private const string LegacyName = "UISprites_33";
    private const string IndicatorName = "LevelUpIndicator";
    // Sort above the player sprite (order 5) and its world canvas (order 6).
    private const int SortingOrder = 30;
    private const float GapAbovePlayer = 0.2f;

    [MenuItem("Tools/Magic Garden/Configure level-up indicator (once)")]
    public static void Run()
    {
        ConfigureIconSprite();
        AuthorPlayerIndicator();
    }

    // Renames the "LvUp" label through the sprite data provider so internalID/spriteID (and every reference) survive.
    public static void ConfigureIconSprite()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        if (importer == null) throw new InvalidOperationException("Missing sprite sheet " + SheetPath);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = provider.GetSpriteRects();
        if (rects.Any(r => r.name == IconName)) { Debug.Log("LevelUpIcon sprite already configured."); return; }
        var icon = rects.SingleOrDefault(r => r.name == LegacyName);
        if (icon == null) throw new InvalidOperationException(LegacyName + " not found; refusing to guess the icon sprite.");
        if (icon.rect != new Rect(880, 50, 31, 13)) throw new InvalidOperationException("Unexpected rect for " + LegacyName + ": " + icon.rect);
        icon.name = IconName;
        provider.SetSpriteRects(rects);
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var pairs = ids.GetNameFileIdPairs().ToList();
        int index = pairs.FindIndex(p => p.name == LegacyName);
        if (index < 0) throw new InvalidOperationException("No fileID entry for " + LegacyName);
        pairs[index].name = IconName; // keeps the pair's id, so every reference to the sprite survives
        ids.SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.SaveAndReimport();
        Debug.Log("LevelUpIcon sprite configured (renamed from " + LegacyName + ").");
    }

    // World-space SpriteRenderer child of Player.prefab, above the head, driven by LevelUpIndicator.
    public static void AuthorPlayerIndicator()
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().SingleOrDefault(s => s.name == IconName);
        if (sprite == null) throw new InvalidOperationException("LevelUpIcon sprite is missing; configure the sprite first.");
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (root.GetComponentInChildren<LevelUpIndicator>(true) != null) { Debug.Log("Player already has a LevelUpIndicator."); return; }
            var body = root.GetComponent<SpriteRenderer>();
            var experience = root.GetComponent<PlayerExperienceSystem>();
            if (body == null || body.sprite == null || experience == null) throw new InvalidOperationException("Player prefab is missing its body sprite or PlayerExperienceSystem.");
            var child = new GameObject(IndicatorName) { layer = root.layer };
            child.transform.SetParent(root.transform, false);
            float top = body.sprite.bounds.max.y * root.transform.localScale.y;
            child.transform.localPosition = new Vector3(0f, top + GapAbovePlayer + sprite.bounds.extents.y, 0f);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = SortingOrder;
            renderer.enabled = false;
            var indicator = child.AddComponent<LevelUpIndicator>();
            var serialized = new SerializedObject(indicator);
            serialized.FindProperty("experience").objectReferenceValue = experience;
            serialized.FindProperty("icon").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Debug.Log("Player.prefab now carries the LevelUpIndicator at local y=" + child.transform.localPosition.y);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
