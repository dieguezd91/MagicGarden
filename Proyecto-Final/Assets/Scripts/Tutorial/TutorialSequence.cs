using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialSequence", menuName = "Tutorial/Sequence")]
public class TutorialSequence : ScriptableObject
{
    [Tooltip("Playback order is exactly this list, not stepOrder. Reuse existing TutorialStep assets.")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    [Tooltip("Only while this tutorial is active, require at least one plant and one spell before daily confirmation.")]
    public bool requireUsableLoadout;

    [Tooltip("Use each step's allowNightTransition instead of the altar's legacy numeric step threshold.")]
    public bool useExplicitNightPermission;
}
