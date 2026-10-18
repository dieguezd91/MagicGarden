using System.Collections;
using UnityEngine;

namespace MagicGarden.LoadoutUI
{
    /// <summary>Opt-in isolated scene fixture, authored by the Editor menu. Never added to Game.</summary>
    public sealed class DailyLoadoutPlayModeHarness : MonoBehaviour
    {
        [SerializeField] private DailyLoadoutPresenter presenter;
        [SerializeField] private PlayerContentUnlockSystem owner;
        [SerializeField] private PlayerExperienceSystem experience;
        [SerializeField] private GameFlowController flow;
        [SerializeField] private DayCycleController days;
        [SerializeField] private UIFlowController modals;
        private IEnumerator Start()
        {
            yield return null; // PlayerContentUnlockSystem.Start owns session creation.
            days.StartDay();
            presenter.Initialize(owner.Loadout, owner, experience, flow, modals);
            presenter.Open();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, 300, 235), GUI.skin.box);
            GUILayout.Label("Isolated Daily Loadout harness");
            if (GUILayout.Button("Open panel")) presenter.Open();
            if (GUILayout.Button("Begin night")) days.StartNight();
            if (GUILayout.Button("Begin genuine next day")) days.StartDay();
            if (GUILayout.Button("Grant XP for catalog inspection")) experience.AddExperience(100);
            if (GUILayout.Button("Reset isolated run"))
            {
                experience.ResetProgression();
                owner.Loadout?.ResetForNewRun();
                presenter.Refresh();
            }
            GUILayout.EndArea();
        }
    }
}
