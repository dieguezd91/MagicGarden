using UnityEngine;
using System.Collections.Generic;

public class RewardsSystem : MonoBehaviour
{
    [Header("REWARDS (gold)")]
    [SerializeField] private int rewardNoPlantsDestroyed = 30;
    [SerializeField] private int rewardHomeUntouched = 50;
    [SerializeField] private int rewardPlayerUnharmed = 20;

    [Header("FEEDBACK (shown at the end of the night)")]
    [Tooltip("No plant was destroyed during the night")]
    [SerializeField] private string defensesIntactMessage = "Defenses Intact";
    [SerializeField] private Color defensesIntactColor = new Color(0.3f, 0.9f, 0.4f);
    [Tooltip("The home took no damage during the night")]
    [SerializeField] private string homeProtectedMessage = "Home Protected";
    [SerializeField] private Color homeProtectedColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("The player took no damage during the night")]
    [SerializeField] private string witchUnharmedMessage = "Witch Unharmed";
    [SerializeField] private Color witchUnharmedColor = new Color(0.7f, 0.4f, 1f);

    private bool anyPlantDestroyed = false;
    private bool playerTookDamage = false;
    private float homeHealthAtNightStart;

    public static RewardsSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void StartNightEvaluation()
    {
        anyPlantDestroyed = false;
        playerTookDamage = false;

        if (LevelManager.Instance != null && GameObject.FindGameObjectWithTag("Home") != null)
        {
            var homeLife = GameObject.FindGameObjectWithTag("Home").GetComponent<LifeController>();
            if (homeLife != null)
                homeHealthAtNightStart = homeLife.currentHealth;
        }
    }

    public void NotifyPlantDestroyed()
    {
        anyPlantDestroyed = true;
    }

    public void NotifyPlayerDamaged()
    {
        playerTookDamage = true;
    }

    public void EvaluateAndGrantReward()
    {
        List<FloatingTextController.GoldReward> rewards = new List<FloatingTextController.GoldReward>();
        int totalGold = 0;

        if (!anyPlantDestroyed)
        {
            rewards.Add(new FloatingTextController.GoldReward
            {
                goldAmount = rewardNoPlantsDestroyed,
                message = defensesIntactMessage,
                textColor = defensesIntactColor
            });
            totalGold += rewardNoPlantsDestroyed;
        }

        float currentHomeHealth = GameObject.FindGameObjectWithTag("Home").GetComponent<LifeController>()?.currentHealth ?? 0f;
        if (Mathf.Approximately(currentHomeHealth, homeHealthAtNightStart))
        {
            rewards.Add(new FloatingTextController.GoldReward
            {
                goldAmount = rewardHomeUntouched,
                message = homeProtectedMessage,
                textColor = homeProtectedColor
            });
            totalGold += rewardHomeUntouched;
        }

        if (!playerTookDamage)
        {
            rewards.Add(new FloatingTextController.GoldReward
            {
                goldAmount = rewardPlayerUnharmed,
                message = witchUnharmedMessage,
                textColor = witchUnharmedColor
            });
            totalGold += rewardPlayerUnharmed;
        }

        if (totalGold > 0)
        {
            InventoryManager.Instance.AddGold(totalGold);
        }

        if (FloatingTextController.Instance != null)
        {
            FloatingTextController.Instance.ShowGoldFeedbackSequence(rewards, totalGold);
        }
    }
}
