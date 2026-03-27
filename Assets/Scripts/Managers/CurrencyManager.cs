using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Optional: If set, CurrencyManager will proxy EconomyManager")]
    [SerializeField] private EconomyManager economyManager;

    [Header("Fallback (used only when EconomyManager is missing)")]
    [SerializeField] private int startGold = 100;
    [SerializeField] private int currentGold;

    public int CurrentGold
    {
        get
        {
            if (economyManager != null) return economyManager.CurrentGold;
            return currentGold;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (economyManager == null)
        {
            economyManager = EconomyManager.Instance;
        }
    }

    private void Start()
    {
        if (economyManager == null)
        {
            currentGold = startGold;
            GameEvents.OnGoldChanged?.Invoke(currentGold);
        }
        else
        {
            GameEvents.OnGoldChanged?.Invoke(economyManager.CurrentGold);
        }
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        if (economyManager != null)
        {
            economyManager.AddGold(amount);
            return;
        }

        currentGold += amount;
        GameEvents.OnGoldChanged?.Invoke(currentGold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;

        if (economyManager != null)
        {
            return economyManager.SpendGold(amount);
        }

        if (currentGold < amount) return false;
        currentGold -= amount;
        GameEvents.OnGoldChanged?.Invoke(currentGold);
        return true;
    }
}

