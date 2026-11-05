using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/Step")]
public class TutorialStep : ScriptableObject
{
    public string stepID;
    public int stepOrder;

    [TextArea(3, 10)]
    public string instructionText;

    [Header("VISUALS")]
    public Sprite stepImage;

    public TutorialObjectiveType objectiveType;
    public int requiredCount = 1;

    public bool isGatedStep = false;

    [Tooltip("Allows the ritual to start night at this step when the sequence enables explicit night permission.")]
    public bool allowNightTransition;

    public float waitDuration = 3f;

    [Header("CONFIGURED LINEAR COMPLETION (opt-in)")]
    public bool useConfiguredCompletion;
    public TutorialConditionCombination conditionCombination;
    public List<TutorialCondition> conditions = new List<TutorialCondition>();
    [Tooltip("Conditions complete first, then the wait. Continue uses the existing panel button; use objective Wait.")]
    public TutorialWaitMode waitMode;
    [Header("OPTIONAL PRESENTATION")]
    public ContextualHint hint;
    [Tooltip("Small supported presentation actions; no scene references or gameplay scripting.")]
    public List<TutorialStepAction> enterActions = new List<TutorialStepAction>();

    public string DisplayText => instructionText + (hint == null ? "" : "\n\n" + hint.title + "\n" + hint.description);
}

public enum TutorialConditionCombination { All, Any }
public enum TutorialWaitMode { None, Continue, Duration }
public enum TutorialStepAction { HidePanel, ShowPanel }

[System.Serializable]
public class TutorialCondition
{
    public TutorialObjectiveType objective = TutorialObjectiveType.Dig;
    [Min(1)] public int requiredCount = 1;
}

public enum TutorialObjectiveType
{
    None,
    Move,
    Dig,
    Plant,
    Harvest,
    CastSpell,
    EnterHouse,
    UseRitualAltar,
    StartNight,
    DefeatEnemy,
    SurviveNight,
    OpenInventory,
    OpenCrafting,
    OpenRestoration,
    ExploreHouse,
    Wait,
    PlantProduction,
    PlantDefensive,
    PlantHybrid,
    CraftingProximity,
    RestorationProximity,
    RitualAltarProximity,
    FirstPlantReady,
    SeedTooltipDisplayed,
    Welcome,
    TeleportSpell,
    FifthNightWarning,
    AbilityChanged,
    // Append only: earlier values are serialized by existing tutorial assets.
    OpenDailyLoadout = 28,
    DailyLoadoutProximity = 29,
    ConfirmDailyLoadout = 30,
    ExitHouse = 31
}