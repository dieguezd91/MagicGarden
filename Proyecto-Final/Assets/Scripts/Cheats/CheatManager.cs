#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using MagicGarden.Loadout;
using UnityEngine;

// All game cheats in one place. Installs itself on play (no scene setup needed).
// F1 opens the panel: each cheat has an on/off toggle (persisted) and a button; enabled cheats also fire on their hotkey.
public class CheatManager : MonoBehaviour
{
    private const KeyCode PanelKey = KeyCode.F1;
    private const string PrefsPrefix = "Cheat.";

    private class Cheat
    {
        public string Name;
        public KeyCode Key;
        public Action Run;
        public bool Enabled;
        public Func<bool> IsOn;
    }

    public static CheatManager Instance { get; private set; }
    
    public static bool IsPointerOverPanel =>
        Instance != null && Instance.panelVisible &&
        Instance.panelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

    private readonly List<Cheat> cheats = new List<Cheat>();
    private bool panelVisible;
    private bool hotkeysEnabled;
    private Rect panelRect = new Rect(10, 10, 280, 0);
    private Vector2 scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject(nameof(CheatManager));
        DontDestroyOnLoad(go);
        go.AddComponent<CheatManager>();
    }

    private void Awake()
    {
        Instance = this;
        hotkeysEnabled = PlayerPrefs.GetInt(PrefsPrefix + "Hotkeys", 1) == 1;

        // Quick start
        Add("Quick start / reroll loadout (tests on)", KeyCode.F2, () => StartCoroutine(QuickStart()));
        Add("Back to normal play (tests off)", KeyCode.F3, TestTogglesOff);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += ResetQuickStart;

        // Enemies / nights
        Add("Kill alive enemies (bosses too)", KeyCode.K, KillAliveEnemies);
        Add("Spawn & kill whole horde (loot)", KeyCode.N, SpawnAndKillAll);
        Add("Start night now", KeyCode.V, StartNightNow);
        Add("End night (no loot)", KeyCode.None, () => LevelManager.Instance?.ForceEndNight());
        Add("Start boss night", KeyCode.B, StartBossNight);
        Add("Spawn boss now (cycles bosses)", KeyCode.Z, SpawnBossNow);
        Add("End boss night", KeyCode.J, () => FindObjectOfType<BossNightManager>()?.ForceEndBossNight());
        Add("Next moon phase", KeyCode.M, () => FindObjectOfType<LunarCycleManager>()?.ForceNextPhase());

        // Player / progression
        Add("God mode", KeyCode.Y, () => LifeController.PlayerInvulnerable = !LifeController.PlayerInvulnerable,
            () => LifeController.PlayerInvulnerable);
        Add("Infinite mana", KeyCode.X, ToggleInfiniteMana, () => ManaSystem.InfiniteMana);
        Add($"Fast cooldowns ({SpellInventory.FastCooldownCap}s)", KeyCode.C,
            () => SpellInventory.FastCooldowns = !SpellInventory.FastCooldowns, () => SpellInventory.FastCooldowns);
        Add("Restore life & mana", KeyCode.H, RestoreLifeAndMana);
        Add("Level up", KeyCode.U, LevelUp);
        Add("Unlock all spells & plants", KeyCode.O, UnlockAll);
        Add("+100 gold", KeyCode.L, () => InventoryManager.Instance?.AddGold(100));
        Add("Grab all loot", KeyCode.R, GrabAllLoot);

        // Garden / tutorial
        Add("Grow all plants", KeyCode.G, GrowAllPlants);
        Add("Remove all planted plants (soil stays tilled)", KeyCode.F4, RemoveAllPlants);
        Add("Skip tutorial step", KeyCode.T, SkipTutorialStep);
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ResetQuickStart;
        if (Instance == this) Instance = null;
    }

    private void ResetQuickStart(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        quickStartDone = false;
    }

    private void Add(string name, KeyCode key, Action run, Func<bool> isOn = null)
    {
        cheats.Add(new Cheat
        {
            Name = name,
            Key = key,
            Run = run,
            IsOn = isOn,
            Enabled = PlayerPrefs.GetInt(PrefsPrefix + name, 1) == 1,
        });
    }

    private void Update()
    {
        if (Input.GetKeyDown(PanelKey)) panelVisible = !panelVisible;

        if (!hotkeysEnabled || UpgradeRuntime.GameplayBlocked) return;

        foreach (var cheat in cheats)
            if (cheat.Enabled && cheat.Key != KeyCode.None && Input.GetKeyDown(cheat.Key))
                Execute(cheat);
    }

    private void Execute(Cheat cheat)
    {
        Debug.Log($"[Cheat] {cheat.Name}");
        try { cheat.Run(); }
        catch (Exception e) { Debug.LogException(e); } // a broken cheat must not kill the panel
    }

    #region UI

    private void OnGUI()
    {
        if (!panelVisible) return;
        panelRect = GUILayout.Window(GetInstanceID(), panelRect, DrawPanel, $"Cheats  ({PanelKey} to close)");
    }

    private void DrawPanel(int id)
    {
        bool hotkeys = GUILayout.Toggle(hotkeysEnabled, " Hotkeys enabled");
        if (hotkeys != hotkeysEnabled)
        {
            hotkeysEnabled = hotkeys;
            PlayerPrefs.SetInt(PrefsPrefix + "Hotkeys", hotkeys ? 1 : 0);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Enable all")) SetAll(true);
        if (GUILayout.Button("Disable all")) SetAll(false);
        GUILayout.EndHorizontal();

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(cheats.Count * 24 + 8, Screen.height - 120)));
        foreach (var cheat in cheats)
        {
            GUILayout.BeginHorizontal();

            bool enabled = GUILayout.Toggle(cheat.Enabled, "", GUILayout.Width(18));
            if (enabled != cheat.Enabled) SetEnabled(cheat, enabled);

            GUI.enabled = cheat.Enabled;
            string key = cheat.Key == KeyCode.None ? "  -  " : $"[{cheat.Key}]";
            string state = cheat.IsOn == null ? "" : cheat.IsOn() ? "  (ON)" : "  (OFF)";
            if (GUILayout.Button($"{key} {cheat.Name}{state}")) Execute(cheat);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUI.DragWindow();
    }

    private void SetAll(bool enabled)
    {
        foreach (var cheat in cheats) SetEnabled(cheat, enabled);
    }

    private void SetEnabled(Cheat cheat, bool enabled)
    {
        // Disabling a toggle cheat switches its effect off too, so god mode can't stay stuck on.
        if (!enabled && cheat.IsOn != null && cheat.IsOn()) cheat.Run();
        cheat.Enabled = enabled;
        PlayerPrefs.SetInt(PrefsPrefix + cheat.Name, enabled ? 1 : 0);
    }
    
    #endregion

    #region CHEATS
    private static void SpawnBossNow()
    {
        var bosses = FindObjectOfType<BossNightManager>();
        if (bosses == null) return;
        StartNightNow(); // no-op if it's already night
        string spawned = bosses.CheatSpawnBossNow();
        Debug.Log(spawned != null ? $"[Cheats] Spawned boss: {spawned}" : "[Cheats] Could not spawn a boss (no prefabs or spawn point).");
    }

    // By day: turns today into a boss night and starts the night, so the whole boss-night flow runs (announcement, no horde).
    // At night it is too late for that flow (the horde is already running), so the boss is simply spawned.
    private static void StartBossNight()
    {
        var bosses = FindObjectOfType<BossNightManager>();
        if (bosses == null) return;
        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentPhase == GamePhase.Night)
        {
            SpawnBossNow();
            return;
        }
        if (!bosses.ForceBossNight()) Debug.Log("[Cheats] A boss night is already queued (or there are no boss prefabs).");
        StartNightNow();
    }

    private static void StartNightNow()
    {
        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentPhase == GamePhase.Night) return;
        var timer = FindObjectOfType<DayTimerController>();
        if (timer != null) timer.EndDayAndStartNight();
        else DayCycleController.Instance?.StartNight();
    }

    /// <summary>
    /// Teleports every pickup onto the player so each one's own trigger collection runs
    /// (inventory, XP, sounds, fly-to-player), instead of duplicating that logic here.
    /// </summary>
    private static void GrabAllLoot()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player == null) return;
        Vector3 at = player.transform.position;
        int moved = 0;
        foreach (var p in FindObjectsOfType<DropEnemyMaterial>()) { p.transform.position = at; moved++; }
        foreach (var p in FindObjectsOfType<ExperiencePickup>()) { p.transform.position = at; moved++; }
        foreach (var p in FindObjectsOfType<ManaPickup>()) { p.transform.position = at; moved++; }
        foreach (var p in FindObjectsOfType<HouseHealingPotionPickup>()) { p.transform.position = at; moved++; }
        Debug.Log($"[Cheats] Pulled {moved} pickups to the player.");
    }

    private static void KillAliveEnemies()
    {
        FindObjectOfType<EnemiesSpawner>()?.KillAllAlive(includeBosses: true);
    }

    private static void SpawnAndKillAll()
    {
        foreach (var altar in FindObjectsOfType<RitualAltar>())
            altar.ForceStopRitual();

        FindObjectOfType<EnemiesSpawner>()?.SpawnRemainingAndKillAll();
    }

    private static void RemoveAllPlants()
    {
        var tiles = TilePlantingSystem.Instance;
        if (tiles == null) return;
        int removed = 0;
        foreach (var cell in new List<Vector3Int>(tiles.GetAllPlantedTiles()))
        {
            Plant plant = tiles.GetPlantAt(cell);
            tiles.UnregisterPlantAt(cell);
            if (plant != null)
            {
                Destroy(plant.gameObject);
                removed++;
            }
        }
        Debug.Log($"[Cheats] Removed {removed} plants.");
    }

    private static void GrowAllPlants()
    {
        foreach (var plant in FindObjectsOfType<Plant>())
            plant.CompleteGrowth();
    }

    private static void LevelUp()
    {
        var xp = FindObjectOfType<PlayerExperienceSystem>();
        if (xp == null || xp.ExperienceRequired <= 0) return; // 0 = already max level
        xp.AddExperience(xp.ExperienceRequired - xp.CurrentExperience);
    }

    private static void RestoreLifeAndMana()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        var life = player != null ? player.GetComponent<LifeController>() : null;
        if (life != null && life.IsAlive())
        {
            life.currentHealth = life.maxHealth;
            life.onHealthChanged?.Invoke(life.currentHealth, life.maxHealth);
        }

        var mana = FindObjectOfType<ManaSystem>();
        if (mana != null)
            mana.SetMana(mana.GetMaxMana());
    }

    private static void ToggleInfiniteMana()
    {
        ManaSystem.InfiniteMana = !ManaSystem.InfiniteMana;
        if (!ManaSystem.InfiniteMana) return;

        var mana = FindObjectOfType<ManaSystem>();
        if (mana != null) mana.SetMana(mana.GetMaxMana()); // start full so "enough mana?" checks pass right away
    }

    private static void UnlockAll()
    {
        var xp = FindObjectOfType<PlayerExperienceSystem>();
        var unlocks = FindObjectOfType<PlayerContentUnlockSystem>();
        if (xp == null || xp.ProgressionData == null || unlocks == null) return;
        unlocks.RebuildUnlocksUpToLevel(xp.ProgressionData.LevelRequirements.Count);
    }

    private static void SkipTutorialStep()
    {
        var tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.IsTutorialActive())
            tutorial.CompleteCurrentStep();
    }

    #region Quick start

    private const int QuickStartSeeds = 10;
    private const int QuickStartGardenSize = 4;

    private bool quickStartDone;

    // First press in a run: no tutorial, everything unlocked, tilled garden, test toggles on, random loadout.
    // Later presses: test toggles back on and a new random loadout (garden and plants are left alone).
    private IEnumerator QuickStart()
    {
        bool first = !quickStartDone;
        quickStartDone = true;

        if (first)
        {
            var tutorial = TutorialManager.Instance;
            if (tutorial != null && tutorial.IsTutorialActive())
                tutorial.SkipTutorial();
            UnlockAll();
        }

        TestTogglesOn();

        yield return null; // let the tutorial completion and the unlock rebuild settle

        EquipRandomLoadout();
        if (first && !DigGarden(QuickStartGardenSize)) DigGarden(3);
        RestoreLifeAndMana();
    }

    private static void TestTogglesOn()
    {
        LifeController.PlayerInvulnerable = true;
        SpellInventory.FastCooldowns = true;
        if (!ManaSystem.InfiniteMana) ToggleInfiniteMana();
    }

    // Only the test toggles: loadout, seeds, garden, plants and unlocks stay as they are.
    private static void TestTogglesOff()
    {
        LifeController.PlayerInvulnerable = false;
        SpellInventory.FastCooldowns = false;
        ManaSystem.InfiniteMana = false;
    }

    private static void EquipRandomLoadout()
    {
        var unlocks = FindObjectOfType<PlayerContentUnlockSystem>();
        var xp = FindObjectOfType<PlayerExperienceSystem>();
        var session = unlocks != null ? unlocks.Loadout : null;
        if (session == null || xp == null || xp.ProgressionData == null) return;
        session.ReopenTodayForCheat();
        if (!session.CanConfigure)
        {
            Debug.Log("[Cheats] Loadout not changed: the loadout can only be changed by day.");
            return;
        }

        var catalog = new MagicGarden.LoadoutUI.DailyLoadoutCatalog();
        catalog.Refresh(xp.ProgressionData, unlocks);
        var plants = Shuffled(catalog.Plants);
        var spells = Shuffled(catalog.Spells);

        int slots = DailyLoadoutState<SeedsEnum, SpellDataSO>.SlotsPerCategory;
        for (int i = 0; i < slots; i++)
        {
            session.TrySetDraftPlant(i, SeedsEnum.None);
            session.TrySetDraftSpell(i, null);
        }
        for (int i = 0; i < slots; i++)
        {
            session.TrySetDraftPlant(i, i < plants.Count ? plants[i].seedType : SeedsEnum.None);
            session.TrySetDraftSpell(i, i < spells.Count ? spells[i] : null);
        }

        if (!session.TryConfirm())
        {
            Debug.Log("[Cheats] Random loadout was rejected.");
            return;
        }

        for (int i = 0; i < slots && i < plants.Count; i++)
            StockSeeds(plants[i]);

        var presenter = FindObjectOfType<MagicGarden.LoadoutUI.DailyLoadoutPresenter>();
        if (presenter != null && presenter.IsOpen) presenter.Close();
    }

    private static void StockSeeds(PlantDataSO plant)
    {
        var seeds = SeedInventory.Instance;
        if (seeds == null || plant == null) return;
        seeds.TryGrantUnlockedPlant(plant, QuickStartSeeds);
        for (int i = 0; i < seeds.PlantSlotsCount; i++)
        {
            var slot = seeds.GetPlantSlot(i);
            if (slot == null || slot.seedType != plant.seedType) continue;
            if (slot.seedCount < QuickStartSeeds) seeds.AddSeedsToSlot(i, QuickStartSeeds - slot.seedCount);
            return;
        }
    }

    private static List<T> Shuffled<T>(IReadOnlyList<T> source)
    {
        var list = new List<T>(source);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // Tills a size x size block of diggable ground, the closest one to the player that fits completely.
    private static bool DigGarden(int size)
    {
        var abilities = FindObjectOfType<PlayerAbilitySystem>();
        var tiles = TilePlantingSystem.Instance;
        if (abilities == null || tiles == null || abilities.tilledSoilTile == null) return false;

        var map = tiles.PlantingTilemap;
        Vector3Int playerCell = map.WorldToCell(abilities.transform.position);
        int houseLayer = LayerMask.NameToLayer("House");

        bool Diggable(Vector3Int cell)
        {
            if (cell == playerCell || tiles.GetPlantAt(cell) != null) return false;
            RaycastHit2D hit = Physics2D.Raycast(map.GetCellCenterWorld(cell), Vector2.zero, Mathf.Infinity, abilities.diggableLayer);
            return hit.collider != null && hit.collider.gameObject.layer != houseLayer;
        }

        const int searchRadius = 30;
        for (int ring = 1; ring <= searchRadius; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            for (int dy = -ring; dy <= ring; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                var origin = playerCell + new Vector3Int(dx, dy, 0) - new Vector3Int(size / 2, size / 2, 0);

                bool fits = true;
                for (int x = 0; x < size && fits; x++)
                for (int y = 0; y < size && fits; y++)
                    fits = Diggable(origin + new Vector3Int(x, y, 0));
                if (!fits) continue;

                for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    map.SetTile(origin + new Vector3Int(x, y, 0), abilities.tilledSoilTile);
                Debug.Log($"[Cheats] Tilled a {size}x{size} garden at {origin}.");
                return true;
            }
        }

        Debug.Log($"[Cheats] No free {size}x{size} diggable area found near the player.");
        return false;
    }

    #endregion
    #endregion
}
#endif
