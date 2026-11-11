using System.Collections;
using UnityEngine;

public class ManaSystem : MonoBehaviour
{
    // Infinite mana cheat (CheatManager). Always false in release builds.
    public static bool InfiniteMana;

    [Header("SETTINGS")]
    [SerializeField] private float baseMaxMana = 100f;
    [SerializeField] private float currentMana;
    [SerializeField] private float baseDayRegenerationRate = 2f;
    [SerializeField] private float baseNightRegenerationRate = 0.5f;
    [SerializeField] public float modifiedMaxMana;

    [Header("LUNAR INFLUENCE")]
    [SerializeField] private bool useLunarInfluence = true;

    private GameFlowController gameFlowController;
    private LunarCycleManager lunarCycleManager;
    private LunarInfluenceManager lunarInfluenceManager;
    private bool isNight = false;

    public delegate void ManaChangedHandler(float currentMana, float maxMana);
    public event ManaChangedHandler OnManaChanged;

    private void Awake()
    {
        modifiedMaxMana = baseMaxMana;
        currentMana = modifiedMaxMana;
    }

    private void Start()
    {
        if (lunarCycleManager == null)
        {
            lunarCycleManager = LunarCycleManager.Instance;
        }

        if (lunarInfluenceManager == null)
        {
            lunarInfluenceManager = LunarInfluenceManager.Instance;
        }

        gameFlowController = GameFlowController.Instance;
        if (gameFlowController != null)
        {
            isNight = gameFlowController.CurrentPhase == GamePhase.Night;
            gameFlowController.OnPhaseChanged += HandlePhaseChanged;
        }

        if (isNight)
        {
            if (useLunarInfluence && lunarInfluenceManager != null)
            {
                UpdateMaxMana();
            }
        }
        else
        {
            modifiedMaxMana = baseMaxMana;
            currentMana = Mathf.Min(currentMana, modifiedMaxMana);
        }

        StartCoroutine(RegenerateMana());
    }

    private void OnDestroy()
    {
        if (gameFlowController != null)
        {
            gameFlowController.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(GamePhase newPhase)
    {
        bool newIsNight = newPhase == GamePhase.Night;

        if (newIsNight == isNight)
            return;

        isNight = newIsNight;

        if (isNight)
        {
            if (useLunarInfluence && lunarInfluenceManager != null)
            {
                UpdateMaxMana();
            }
        }
        else
        {
            modifiedMaxMana = baseMaxMana;
            currentMana = Mathf.Min(currentMana, modifiedMaxMana);

            OnManaChanged?.Invoke(currentMana, modifiedMaxMana);
        }
    }

    public void SetMana(float amount)
    {
        currentMana = Mathf.Clamp(amount, 0f, modifiedMaxMana);
        OnManaChanged?.Invoke(currentMana, modifiedMaxMana);
    }

    private IEnumerator RegenerateMana()
    {
        while (true)
        {
            if (currentMana < modifiedMaxMana)
            {
                float regenRate = GetCurrentRegenerationRate();
                float manaToAdd = regenRate * Time.deltaTime;

                if (manaToAdd > 0)
                {
                    currentMana = Mathf.Min(currentMana + manaToAdd, modifiedMaxMana);
                    OnManaChanged?.Invoke(currentMana, modifiedMaxMana);
                }
            }
            yield return null;
        }
    }

    private float GetCurrentRegenerationRate()
    {
        float baseRate = isNight ? baseNightRegenerationRate : baseDayRegenerationRate;
        float lunarModifier = 1.0f;

        if (isNight && useLunarInfluence && lunarInfluenceManager != null && lunarCycleManager != null)
        {
            lunarModifier = lunarInfluenceManager.GetManaRegenerationModifier();

            MoonPhase currentPhase = lunarCycleManager.GetCurrentMoonPhase();
            if (currentPhase == MoonPhase.NewMoon)
            {
                return 1.0f;
            }
            else if (currentPhase == MoonPhase.FullMoon)
            {
                return 0.25f;
            }
        }

        return baseRate * lunarModifier;
    }

    public bool UseMana(float amount)
    {
        // Infinite mana cheat: spending is free and the bar stays full, so callers' "enough mana?" checks pass too.
        if (InfiniteMana)
        {
            SetMana(modifiedMaxMana);
            return true;
        }

        if (isNight && useLunarInfluence && lunarInfluenceManager != null)
        {
            float costModifier = lunarInfluenceManager.GetManaCostModifier();
            amount *= costModifier;
        }

        if (isNight && useLunarInfluence && lunarCycleManager != null &&
            lunarCycleManager.GetCurrentMoonPhase() == MoonPhase.GibbousMoon)
        {
            amount *= 0.9f;
        }

        if (currentMana >= amount)
        {
            currentMana -= amount;
            OnManaChanged?.Invoke(currentMana, modifiedMaxMana);
            return true;
        }

        return false;
    }

    public void AddMana(float amount)
    {
        if (isNight && useLunarInfluence && lunarInfluenceManager != null && lunarCycleManager != null &&
            lunarCycleManager.GetCurrentMoonPhase() == MoonPhase.CrescentMoon)
        {
            amount *= 1.15f;
        }

        currentMana = Mathf.Min(currentMana + amount, modifiedMaxMana);
        OnManaChanged?.Invoke(currentMana, modifiedMaxMana);
    }

    private void UpdateMaxMana()
    {
        if (useLunarInfluence && lunarInfluenceManager != null)
        {
            float maxManaModifier = lunarInfluenceManager.GetMaxManaModifier();

            if (isNight)
            {
                modifiedMaxMana = baseMaxMana * maxManaModifier;
            }
            else
            {
                modifiedMaxMana = baseMaxMana;
            }

            currentMana = Mathf.Min(currentMana, modifiedMaxMana);

            OnManaChanged?.Invoke(currentMana, modifiedMaxMana);
        }
    }

    public void OnMoonPhaseChanged(MoonPhase newPhase)
    {
        if (isNight)
        {
            if (lunarInfluenceManager != null)
            {
                UpdateMaxMana();
            }
            else
            {
                lunarInfluenceManager = LunarInfluenceManager.Instance;

                if (lunarInfluenceManager != null)
                {
                    UpdateMaxMana();
                }
            }
        }
    }

    public float GetCurrentMana() => currentMana;
    public float GetMaxMana() => modifiedMaxMana;
    public float GetBaseMaxMana() => baseMaxMana;
}

