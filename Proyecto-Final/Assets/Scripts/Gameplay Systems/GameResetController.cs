using UnityEngine;
using System.Collections.Generic;

public class GameResetController : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private PauseController pauseController;
    [SerializeField] private GameObject home;
    
    private LifeController playerLife;
    private PlayerMovementController playerMovementController;
    private PlayerExperienceSystem playerExperienceSystem;
    private HouseLifeController homeLife;

    private void Awake()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        
        if (player != null)
        {
            playerLife = player.GetComponent<LifeController>();
            playerMovementController = player.GetComponent<PlayerMovementController>();
            playerExperienceSystem = player.GetComponent<PlayerExperienceSystem>();
        }

        if (home == null)
        {
            home = GameObject.FindGameObjectWithTag("Home");
        }

        if (home != null)
        {
            homeLife = home.GetComponent<HouseLifeController>();
        }

        if (pauseController == null)
        {
            pauseController = FindObjectOfType<PauseController>();
        }
    }

    public void ResetGame()
    {
        if (pauseController != null) pauseController.Resume();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseDailyLoadout();
            UIManager.Instance.CloseInventory();
            if (UIManager.Instance.gameOverPanel != null)
                UIManager.Instance.gameOverPanel.SetActive(false);
        }

        if (SeedInventory.Instance != null)
        {
            for (int i = 0; i < SeedInventory.Instance.PlantSlotsCount; i++)
                SeedInventory.Instance.RemoveSeedFromSlot(i);

            SeedInventory.Instance.SelectSlot(0);
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearAllMaterials();
            InventoryManager.Instance.SetGold(0);
        }
        
        if (DayCycleController.Instance != null)
        {
            DayCycleController.Instance.ResetDayCount();
        }

        if (playerLife != null)
        {
            playerLife.currentHealth = playerLife.maxHealth;
            playerLife.onHealthChanged?.Invoke(playerLife.currentHealth, playerLife.maxHealth);
        }

        if (homeLife != null)
        {
            homeLife.ResetLife();
        }

        if (player != null)
        {
            if (playerMovementController != null)
            {
                playerMovementController.SetMovementEnabled(true);
            }

            if (playerLife != null)
            {
                playerLife.ResetLife();
            }

            if (playerExperienceSystem == null)
            {
                playerExperienceSystem = player.GetComponent<PlayerExperienceSystem>();
            }
            playerExperienceSystem?.ResetProgression();
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null) Destroy(enemy);
        }

        BasicRangeSpell[] activeSpells = FindObjectsOfType<BasicRangeSpell>();
        foreach (BasicRangeSpell spell in activeSpells)
        {
            if (spell != null) Destroy(spell.gameObject);
        }

        ManaSystem manaSystem = FindObjectOfType<ManaSystem>();
        if (manaSystem != null)
        {
            manaSystem.SetMana(manaSystem.GetBaseMaxMana());
        }

        // A completed full-run reset, not an ordinary progression rebuild, starts
        // a new equipment session. Preserve every existing reset operation/order.
        if (player != null)
            player.GetComponent<PlayerContentUnlockSystem>()?.Loadout?.ResetForNewRun();
    }
}
