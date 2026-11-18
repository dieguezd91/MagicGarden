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
    [Tooltip("Optional. Two or more images are shown side by side, in list order; one entry is shown like Step Image. When empty, Step Image is used.")]
    public List<Sprite> stepImages = new List<Sprite>();

    public TutorialObjectiveType objectiveType;
    public int requiredCount = 1;

    public bool isGatedStep = false;

    [Tooltip("Allows the ritual to start night at this step when the sequence enables explicit night permission.")]
    public bool allowNightTransition;

    public float waitDuration = 3f;

    [Header("PROGRESSIVE ACTION LOCK")]
    [Tooltip("Player actions this step unlocks. Used only when the sequence enables the action lock; allowed actions are the union of this and every earlier step.")]
    public TutorialAction unlockedActions;

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
    [Tooltip("Configured completion only. Once every objective listed here has been observed during this step, the dialogue panel hides for the rest of the step (for example so it stops covering the HUD). Completion and unlocked actions are unchanged. Empty = never hide.")]
    public List<TutorialObjectiveType> hideDialogueAfter = new List<TutorialObjectiveType>();

    public string DisplayText => instructionText + (hint == null ? "" : "\n\n" + hint.title + "\n" + hint.description);

    /// <summary>Images to present: the non-empty Step Images list, otherwise Step Image alone, otherwise none.</summary>
    public List<Sprite> GetDisplayImages()
    {
        var images = new List<Sprite>();
        if (stepImages != null)
            foreach (var image in stepImages)
                if (image != null) images.Add(image);
        if (images.Count == 0 && stepImage != null) images.Add(stepImage);
        return images;
    }

    /// <summary>True once every <see cref="hideDialogueAfter"/> objective is in <paramref name="observed"/> (configured steps only).</summary>
    public bool ShouldHideDialogue(ICollection<TutorialObjectiveType> observed)
    {
        if (!useConfiguredCompletion || observed == null || hideDialogueAfter == null || hideDialogueAfter.Count == 0) return false;
        foreach (var objective in hideDialogueAfter)
            if (!observed.Contains(objective)) return false;
        return true;
    }
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
    ExitHouse = 31,
    // Fired after the player removes a plant with the remove tool.
    RemovePlant = 32,
    // Fired after the cauldron's upgrade shop opens (OpenCrafting keeps completing on close).
    OpenCauldronShop = 33
}