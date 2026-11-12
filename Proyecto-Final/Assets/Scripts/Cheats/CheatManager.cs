#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
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

        // Enemies / nights
        Add("Kill alive enemies", KeyCode.K, KillAliveEnemies);
        Add("Spawn & kill whole horde (loot)", KeyCode.N, SpawnAndKillAll);
        Add("Start night now", KeyCode.V, StartNightNow);
        Add("End night (no loot)", KeyCode.None, () => LevelManager.Instance?.ForceEndNight());
        Add("Start boss night", KeyCode.B, () => FindObjectOfType<BossNightManager>()?.ForceBossNight());
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
        Add("Skip tutorial step", KeyCode.T, SkipTutorialStep);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        FindObjectOfType<EnemiesSpawner>()?.KillAllAlive();
    }

    private static void SpawnAndKillAll()
    {
        foreach (var altar in FindObjectsOfType<RitualAltar>())
            altar.ForceStopRitual();

        FindObjectOfType<EnemiesSpawner>()?.SpawnRemainingAndKillAll();
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
    #endregion
}
#endif
